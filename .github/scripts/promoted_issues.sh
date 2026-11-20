#!/usr/bin/env bash
# Lists the change issues promoted to test and not yet released: the issues
# cited by the pull requests merged into test since the last merge into main.
# Used on pull requests to main to help prepare and review the release.
#
# Input (environment): REPO, BASE_SHA (main), HEAD_SHA (test), GH_TOKEN.
# Needs the commit history of both branches (checkout with fetch-depth 0).
# Output: one line per issue and pull request, "<issue> <pull request> <state>",
#   state = open | closed. The same issue can come from more than one pull request.
set -euo pipefail

script_dir=$(dirname "$0")

# Pull requests merged into test: GitHub writes "Merge pull request #N from ..."
# as the subject of each merge commit (merge method required by the rulesets).
prs=$(git log --merges --format=%s "$BASE_SHA..$HEAD_SHA" \
  | sed -nE 's/^Merge pull request #([0-9]+) from .*/\1/p' | sort -un)

for pr in $prs; do
  # Only pull requests to test: merges of other pull requests (for example
  # feature branches into development) are not promotions.
  base=$(gh pr view "$pr" --repo "$REPO" --json baseRefName --jq .baseRefName)
  [ "$base" = "test" ] || continue

  # Same rule as issue-links and the issue diary for pull requests to test.
  refs=$(BASE_REF=test \
    PR_TITLE=$(gh pr view "$pr" --repo "$REPO" --json title --jq .title) \
    PR_BODY=$(gh pr view "$pr" --repo "$REPO" --json body --jq .body) \
    bash "$script_dir/pr_issue_refs.sh")

  while read -r n kind; do
    case "$kind" in open|closed) echo "$n $pr $kind" ;; esac
  done <<< "$refs"
done
