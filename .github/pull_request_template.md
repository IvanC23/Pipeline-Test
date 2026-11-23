<!--
Guide only: nothing in this template is mandatory.
These notes are hidden once the pull request is created.

development → test
- Cite the change request this work comes from: "Refs #" followed by its number.
  No issue yet? Open one with the "Change request" form first, then cite it here.
- Describe the tests to expect on the test build: what to check by hand and how.

test → main
- Close the change requests completed in this release: "Closes #" followed by each number.
  A change that continues in a later release: use "Refs #" instead, the issue stays open.
- The "Release check" comment on this pull request lists the change issues
  promoted to test since the last release: use it to check the list.
- The player-build check waits for the Build of the commit and turns green on
  its own. "Expected - Waiting for status to be reported" means no Build ran
  for it (e.g. a hotfix branch): run Actions > Build > Run workflow on the
  branch, test the build, then merge.
-->

## Issues


## Tests

