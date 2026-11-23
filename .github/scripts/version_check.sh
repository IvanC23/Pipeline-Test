#!/usr/bin/env bash
# Compares the version written in the project with the release tags.
#
# The version is decided by people (Project Settings > Player > Version,
# bundleVersion in ProjectSettings/ProjectSettings.asset); release tags are
# vMAJOR.MINOR.PATCH and are created only by the release workflow on main.
# This script never computes or changes a version: it only checks that a new
# release would have a new, higher number.
#
# Usage: version_check.sh <mode>
#   error    the version must be higher than the latest release tag, otherwise
#            exit 1 (pull requests to main, release workflow);
#   warning  same comparison, but only a warning (pull requests to test, builds):
#            a version equal to a release is normal right after a release, until
#            the next change raises it;
#   read     no comparison, only the outputs.
# In every mode a version that is not MAJOR.MINOR.PATCH is an error (exit 1).
#
# Input (environment, optional): PROJECT_SETTINGS (default
#   ProjectSettings/ProjectSettings.asset). Tags are read from the remote
#   "origin" with git ls-remote: no history or tag fetch needed.
# Output: version=<x.y.z> and latest-tag=<vx.y.z or empty> in GITHUB_OUTPUT,
#   one line in GITHUB_STEP_SUMMARY, the same on standard output.
set -euo pipefail

mode=${1:?usage: version_check.sh error|warning|read}
settings=${PROJECT_SETTINGS:-ProjectSettings/ProjectSettings.asset}

version=$(sed -n 's/^  bundleVersion: *//p' "$settings" | tr -d '\r')
if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "::error title=Invalid version::bundleVersion '$version' in $settings is not MAJOR.MINOR.PATCH."
  exit 1
fi

# Highest release tag by number (not the most recent by date). Tags that are
# not vMAJOR.MINOR.PATCH are ignored.
latest=$(git ls-remote --tags --refs origin 'refs/tags/v*' \
  | sed -n 's#.*refs/tags/v##p' \
  | { grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' || true; } \
  | sort -V | tail -n 1)
latest_tag=${latest:+v$latest}

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "version=$version" >> "$GITHUB_OUTPUT"
  echo "latest-tag=$latest_tag" >> "$GITHUB_OUTPUT"
fi

report() {
  echo "$1"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    echo "$1" >> "$GITHUB_STEP_SUMMARY"
  fi
}

if [ "$mode" = "read" ]; then
  report "Version $version; latest release ${latest_tag:-none}."
  exit 0
fi

if [ -z "$latest" ]; then
  report "✅ Version $version: no release yet, this would be the first one."
  exit 0
fi

# Higher = different and last after a version sort.
highest=$(printf '%s\n%s\n' "$version" "$latest" | sort -V | tail -n 1)
if [ "$version" != "$latest" ] && [ "$highest" = "$version" ]; then
  report "✅ Version $version is higher than the latest release $latest_tag."
  exit 0
fi

message="Version $version is not higher than the latest release $latest_tag: raise it (Project Settings > Player > Version) in a change before the release."
if [ "$mode" = "error" ]; then
  echo "::error title=Version already released::$message"
  report "❌ $message"
  exit 1
fi
echo "::warning title=Version already released::$message"
report "⚠️ $message"
