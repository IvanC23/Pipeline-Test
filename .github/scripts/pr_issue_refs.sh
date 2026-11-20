#!/usr/bin/env bash
# Lists the issues referenced by a pull request and what each reference is.
# One rule for every workflow that reads them (issue-links, issue-diary,
# promotion comments).
#
# Input (environment): REPO, BASE_REF, PR_TITLE, PR_BODY, GH_TOKEN.
# Output: one line per reference, "<number> <kind>", where kind is
#   open | closed   an issue of this repository
#   pull-request    a pull request (the issues endpoint returns both)
#   missing         nothing with that number in this repository
set -euo pipefail

if [ "${BASE_REF:-}" = "main" ]; then
  # Promotion to production: only closing keywords in the description count,
  # because GitHub ignores them in the pull request title.
  refs=$(printf '%s\n' "${PR_BODY:-}" \
    | grep -oiE '\b(close[sd]?|fix(e[sd])?|resolve[sd]?):?[[:space:]]+#[0-9]+' \
    | grep -oE '[0-9]+$' | sort -un || true)
else
  # Promotion to test: any reference in title or description counts.
  refs=$(printf '%s\n%s\n' "${PR_TITLE:-}" "${PR_BODY:-}" \
    | grep -oE '#[0-9]+' | tr -d '#' | sort -un || true)
fi

for n in $refs; do
  kind=$(gh api "repos/$REPO/issues/$n" \
    --jq 'if .pull_request then "pull-request" else .state end' 2>/dev/null || echo "missing")
  echo "$n $kind"
done
