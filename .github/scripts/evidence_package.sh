#!/usr/bin/env bash
# Builds the evidence package of a release: one zip with what shows how the
# release was tested, reviewed and approved. It is attached to the GitHub
# Release because Actions logs and artifacts expire (90 days by default),
# while release assets stay as long as the release.
#
# Nothing is computed again: the package keeps the results that the
# reviewers saw on the promotion pull request (its CI run on the tested
# commit) and the build that the release publishes.
#
# Input (environment):
#   REPO, GH_TOKEN, SERVER (https://github.com/<owner>/<repo>)
#   RELEASE_SHA  merge commit of the promotion on main
#   TESTED       its second parent: the tested commit (same files)
#   VERSION, TAG
#   BUILD_RUN    Build run of the published package
#   PACKAGE_DIR  the published package, already checked by the release
#   OUTPUT_DIR   where the zip is written
#   RUN_URL      optional, link to the workflow run that builds the package
# Needs the commit history of main and test (checkout with fetch-depth 0),
# gh, jq, zip and sha256sum (preinstalled on GitHub-hosted Ubuntu runners).
# Output: <OUTPUT_DIR>/StarCollector-<VERSION>-evidence.zip, its path as
# path=<file> in GITHUB_OUTPUT (when set).
set -euo pipefail

script_dir=$(cd "$(dirname "$0")" && pwd)
name="StarCollector-$VERSION-evidence"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
dir="$work/$name"
records="$dir/records"
mkdir -p "$records/issues" "$records/pull-requests" "$dir/ci" "$dir/build"

fail() {
  echo "::error title=$1::$2"
  exit 1
}

# jq helper for table cells: titles must not break the Markdown table.
jq_cell='def cell: tostring | gsub("[\r\n]+"; " ") | gsub("\\|"; "\\|");'

# --- Promotion pull request --------------------------------------------------
# GitHub writes "Merge pull request #N from ..." as the subject of the merge
# commit (merge method required by the rulesets).
pr=$(git log -1 --format=%s "$RELEASE_SHA" \
  | sed -nE 's/^Merge pull request #([0-9]+) from .*/\1/p')
[ -n "$pr" ] || fail "No promotion" "${RELEASE_SHA::7} is not the merge commit of a pull request."

promotion="$records/promotion-pull-request.json"
gh api "repos/$REPO/pulls/$pr" > "$promotion"
if [ "$(jq -r '.base.ref' "$promotion")" != "main" ] \
  || [ "$(jq -r '.merged' "$promotion")" != "true" ] \
  || [ "$(jq -r '.head.sha' "$promotion")" != "$TESTED" ]; then
  fail "Wrong promotion" "Pull request #$pr is not the merged promotion of ${TESTED::7} into main."
fi
gh api "repos/$REPO/pulls/$pr/reviews?per_page=100" > "$records/promotion-reviews.json"

# Issues closed by the promotion: same rule as issue-links towards main.
closing=$(BASE_REF=main PR_BODY=$(jq -r '.body // ""' "$promotion") \
  bash "$script_dir/pr_issue_refs.sh" | awk '$2 == "open" || $2 == "closed" { print $1 }')

# Pull requests merged into test since the previous release, with their
# issues: same rule as the Release check comment. Empty for a hotfix, whose
# change is the promotion pull request itself.
promoted=$(BASE_SHA="$RELEASE_SHA^1" HEAD_SHA="$TESTED" bash "$script_dir/promoted_issues.sh")
promoted_prs=$(awk '{ print $2 }' <<< "$promoted" | sort -un)
issues=$(printf '%s\n%s\n' "$closing" "$(awk '{ print $1 }' <<< "$promoted")" | grep -E '^[0-9]+$' | sort -un || true)

for n in $promoted_prs; do
  gh api "repos/$REPO/pulls/$n" > "$records/pull-requests/$n.json"
done
for n in $issues; do
  gh api "repos/$REPO/issues/$n" > "$records/issues/$n.json"
done

# --- CI run of the promotion -------------------------------------------------
# The latest completed CI run of the promotion pull request on the tested
# commit, started before the merge: the run whose required checks allowed
# the merge. The pull request number of a run is not usable (GitHub empties
# that list once the pull request is closed), so the run is recognised by
# its source branch; "before the merge" leaves out a later pull request from
# the same branch and commit (e.g. the back-merge of a hotfix into test).
gh api "repos/$REPO/actions/workflows/ci.yml/runs?head_sha=$TESTED&event=pull_request&status=completed&per_page=100" \
  > "$work/ci-runs.json"
ci_run=$(jq -r --arg branch "$(jq -r '.head.ref' "$promotion")" --arg merged "$(jq -r '.merged_at' "$promotion")" \
  '[.workflow_runs[] | select(.head_branch == $branch and .created_at <= $merged)]
   | sort_by(.run_number) | last | .id // empty' \
  "$work/ci-runs.json")
[ -n "$ci_run" ] || fail "No CI run" "No completed CI run of pull request #$pr on ${TESTED::7}."
gh api "repos/$REPO/actions/runs/$ci_run" > "$records/ci-run.json"
gh api "repos/$REPO/actions/runs/$ci_run/jobs?per_page=100" > "$records/ci-jobs.json"

for artifact in unity-test-results requirements-matrix third-party-inventory; do
  gh run download "$ci_run" --repo "$REPO" --name "$artifact" --dir "$dir/ci/$artifact" \
    || fail "Missing evidence" "Artifact $artifact of CI run $ci_run is not available (expired or never uploaded)."
done

# --- Build ---------------------------------------------------------------------
cp "$PACKAGE_DIR/build-info.json" "$PACKAGE_DIR/SHA256SUMS.txt" "$dir/build/"
gh api "repos/$REPO/actions/runs/$BUILD_RUN" > "$records/build-run.json"

# --- README.md: the readable page ----------------------------------------------
reviews_table=$(jq -r --arg tested "$TESTED" "$jq_cell"'
  if length == 0 then "_No reviews._" else
    "| Reviewer | State | Date (UTC) | Commit | Tested commit |",
    "|---|---|---|---|---|",
    (.[] | "| \(.user.login | cell) | \(.state) | \(.submitted_at // "") | `\((.commit_id // "")[0:7])` | \(if .commit_id == $tested then "yes" else "no" end) |")
  end' "$records/promotion-reviews.json")

if [ -z "$promoted_prs" ]; then
  prs_table="_None: the promotion pull request is the change (e.g. a hotfix)._"
else
  prs_table="| Pull request | Title | Merged into test (UTC) | Issues |
|---|---|---|---|"
  for n in $promoted_prs; do
    refs=$(awk -v p="$n" '$2 == p { printf "%s#%s", (c++ ? ", " : ""), $1 }' <<< "$promoted")
    prs_table+=$'\n'$(jq -r --arg refs "$refs" "$jq_cell"'
      "| [#\(.number)](\(.html_url)) | \(.title | cell) | \(.merged_at // "") | \($refs) |"' \
      "$records/pull-requests/$n.json")
  done
fi

if [ -z "$issues" ]; then
  issues_table="_No issues._"
else
  issues_table="| Issue | Title | Labels | State when collected | Closed by the promotion |
|---|---|---|---|---|"
  for n in $issues; do
    closes=no
    grep -qx "$n" <<< "$closing" && closes=yes
    issues_table+=$'\n'$(jq -r --arg closes "$closes" "$jq_cell"'
      "| [#\(.number)](\(.html_url)) | \(.title | cell) | \([.labels[].name] | join(", ") | cell) | \(.state) | \($closes) |"' \
      "$records/issues/$n.json")
  done
fi

jobs_table=$(jq -r "$jq_cell"'
  "| Job | Result |", "|---|---|",
  (.jobs[] | "| \(.name | cell) | \(.conclusion // .status) |")' "$records/ci-jobs.json")

promotion_line=$(jq -r "$jq_cell"'
  "[#\(.number)](\(.html_url)) \"\(.title | cell)\", merged by \(.merged_by.login // "unknown") on \(.merged_at) (UTC)"' "$promotion")
ci_line=$(jq -r '"[CI run \(.id), attempt \(.run_attempt)](\(.html_url)): \(.conclusion)"' "$records/ci-run.json")
build_line=$(jq -r '"[Build run \(.id)](\(.html_url)): \(.conclusion)"' "$records/build-run.json")
collected=$(date -u '+%Y-%m-%d %H:%M UTC')

cat > "$dir/README.md" <<EOF
# Star Collector $VERSION – evidence package

Collected on $collected by the release workflow${RUN_URL:+ ([run]($RUN_URL))}.
Actions logs and artifacts expire; this package keeps the evidence with the release.

## Release

| | |
|---|---|
| Version | $VERSION |
| Tag | [$TAG]($SERVER/releases/tag/$TAG) |
| Release commit (main) | \`$RELEASE_SHA\` |
| Tested commit | \`$TESTED\`, same files |

## Promotion to production

Pull request $promotion_line.

$reviews_table

Issues closed by the promotion: $(if [ -n "$closing" ]; then printf '#%s ' $closing; else echo none; fi)

## Changes in this release

### Pull requests promoted to test

$prs_table

### Issues

$issues_table

## Checks

- $ci_line
- $build_line (package \`build/build-info.json\`)

$jobs_table

## Contents

- \`ci/unity-test-results/\`: NUnit XML reports of the CI run
- \`ci/requirements-matrix/\`: requirements matrix and test summary
- \`ci/third-party-inventory/\`: third-party components, versions and licenses
- \`build/\`: build information and SHA-256 of the player files (the player is the release package)
- \`records/\`: the GitHub API answers (JSON) this page was written from
- \`SHA256SUMS.txt\`: SHA-256 of every file of this package (\`sha256sum --check SHA256SUMS.txt\`)
EOF

# --- Hashes and zip --------------------------------------------------------------
(cd "$dir" && find . -type f ! -name SHA256SUMS.txt | sed 's#^\./##' | LC_ALL=C sort \
  | while IFS= read -r file; do sha256sum "$file"; done > SHA256SUMS.txt)
(cd "$dir" && sha256sum --quiet --check SHA256SUMS.txt)

mkdir -p "$OUTPUT_DIR"
zip_file="$OUTPUT_DIR/$name.zip"
(cd "$work" && zip -q -r "$zip_file" "$name")

echo "Evidence package: $name.zip"
if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "path=$zip_file" >> "$GITHUB_OUTPUT"
fi
