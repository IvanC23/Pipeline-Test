"""Builds the requirements matrix from the Unity test reports.

Reads the requirements catalog and every NUnit 3 XML report under the results
folder, then writes the matrix requirement -> tests -> outcome as Markdown
and CSV. Tests declare the requirement they verify with
[Property("Requirement", "ID")]; a property set on a fixture applies to all
of its tests. A test can verify more than one requirement.

Not [Category("ID")]: the NUnit version used by Unity marks a test as not
runnable when its category contains '-', and requirement ids do.

Exit code 1 (traceability problem):
  - no XML report, or reports without tests (e.g. a compile error);
  - a requirement of the catalog with no test ("Not covered");
  - a requirement whose tests did not run ("Not verified": skipped,
    inconclusive).
A failed test is shown as "Failed", with its reason (the failure message on
one line, shortened to MAX_REASON_LENGTH), but does not fail this script: the
unity-tests job already fails on it.
Requirement ids that are not in the catalog are reported as warnings.

The Markdown starts with a test summary: version of the project, commit,
pass/fail counts and the failed tests with their reason. When there is no
XML report, the summary lists the compiler errors found in the editor logs
of the results folder (also as annotations on the files), so the reason is
readable without downloading the logs.

Only the Python standard library is used.
"""

import argparse
import csv
import json
import os
import re
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

# Compiler message in the Unity editor log, e.g.
# Assets/Scripts/Game.cs(12,5): error CS1002: ; expected
COMPILER_ERROR_RE = re.compile(r"^(?P<file>[^\s(][^(]*)\((?P<line>\d+),(?P<column>\d+)\): error (?P<code>\w+): (?P<message>.*)$")
# Most compiler errors listed in the summary; the full list is in the log.
MAX_COMPILER_ERRORS = 50

REQUIREMENT_PROPERTY = "Requirement"

PASSED = "Passed"
FAILED = "Failed"
NOT_VERIFIED = "Not verified"
NOT_COVERED = "Not covered"

# Longest failure reason kept in the matrix; the full text is in the XML.
MAX_REASON_LENGTH = 200


def escape_command(text):
    """Escapes a value for a GitHub workflow command (::error ...)."""
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def error(message):
    print(f"::error::{escape_command(message)}")


def warning(message):
    print(f"::warning::{message}")


def load_requirements(path):
    with open(path, encoding="utf-8") as file:
        requirements = json.load(file)["requirements"]
    ids = [requirement["id"] for requirement in requirements]
    duplicates = sorted({item for item in ids if ids.count(item) > 1})
    if duplicates:
        raise ValueError(f"Duplicate requirement ids in {path}: {', '.join(duplicates)}")
    return requirements


def requirements_of(element):
    properties = element.find("properties")
    if properties is None:
        return set()
    return {
        prop.get("value").strip()
        for prop in properties.findall("property")
        if prop.get("name") == REQUIREMENT_PROPERTY and prop.get("value", "").strip()
    }


def reason_of(test_case):
    """Label and failure or skip message on one line, if any.

    The message lines are joined, so that an assertion keeps both
    "Expected: ..." and "But was: ...".
    """
    message = test_case.findtext("failure/message") or test_case.findtext("reason/message") or ""
    lines = [" ".join(line.split()) for line in message.splitlines() if line.strip()]
    reason = " / ".join(lines)
    label = test_case.get("label")
    if label:
        reason = f"{label}: {reason}" if reason else label
    if len(reason) > MAX_REASON_LENGTH:
        reason = reason[:MAX_REASON_LENGTH - 3] + "..."
    return reason


def collect_tests(element, inherited, tests):
    """Walks the report; each test-case gets the requirements of its suites."""
    requirements = inherited | requirements_of(element)
    if element.tag == "test-case":
        result = element.get("result") or "Unknown"
        tests.append({
            "name": element.get("fullname") or element.get("name") or "?",
            "result": result,
            "reason": "" if result == PASSED else reason_of(element),
            "requirements": requirements,
        })
        return
    for child in element:
        if child.tag in ("test-suite", "test-case"):
            collect_tests(child, requirements, tests)


def read_reports(results_dir):
    reports = sorted(Path(results_dir).rglob("*.xml")) if Path(results_dir).is_dir() else []
    tests = []
    for report in reports:
        try:
            root = ElementTree.parse(report).getroot()
        except ElementTree.ParseError as exception:
            raise ValueError(f"Test report {report} is not valid XML: {exception}") from exception
        collect_tests(root, set(), tests)
    return reports, tests


def read_version(project_settings):
    """Version written in the project (Player > Version), or 'unknown'."""
    try:
        text = Path(project_settings).read_text(encoding="utf-8")
    except OSError:
        return "unknown"
    match = re.search(r"^  bundleVersion: *(\S+)\s*$", text, re.MULTILINE)
    return match.group(1) if match else "unknown"


def read_compiler_errors(results_dir):
    """Distinct compiler errors in the editor logs, in order of appearance.

    Unity prints each error more than once (per assembly and in the final
    list): duplicates are removed.
    """
    logs = sorted(Path(results_dir).rglob("*.log")) if Path(results_dir).is_dir() else []
    errors = []
    seen = set()
    for log in logs:
        for raw_line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            match = COMPILER_ERROR_RE.match(raw_line.strip())
            if not match:
                continue
            key = match.group("file", "line", "column", "code")
            if key in seen:
                continue
            seen.add(key)
            errors.append(match.groupdict())
    return logs, errors


def count_results(tests):
    counts = {PASSED: 0, FAILED: 0}
    other = 0
    for test in tests:
        if test["result"] in counts:
            counts[test["result"]] += 1
        else:
            other += 1
    return counts[PASSED], counts[FAILED], other


def summary_header(version, commit):
    return ["# Test summary", "", f"- Version: {version}", f"- Commit: {commit}"]


def write_no_report_summary(output_dir, version, commit, logs, compiler_errors):
    """Summary when the tests did not run: the compiler errors, if any."""
    lines = summary_header(version, commit)
    lines += ["- Result: ❌ no test report: the tests did not run", ""]
    if compiler_errors:
        lines += [f"## Compiler errors ({len(compiler_errors)})", ""]
        for item in compiler_errors[:MAX_COMPILER_ERRORS]:
            lines.append(
                f"- `{item['file']}({item['line']},{item['column']})` "
                f"{item['code']}: {markdown_cell(item['message'])}"
            )
        if len(compiler_errors) > MAX_COMPILER_ERRORS:
            lines.append(f"- … {len(compiler_errors) - MAX_COMPILER_ERRORS} more in the editor log.")
    elif logs:
        lines.append("No compiler errors found in the editor logs: read the logs in the "
                     "`unity-test-results` artifact (license, Docker or startup problem).")
    else:
        lines.append("No editor log found: the test job stopped before starting Unity.")
    lines.append("")
    markdown = "\n".join(lines)
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "requirements-matrix.md").write_text(markdown, encoding="utf-8")
    return markdown


def requirement_status(results):
    if not results:
        return NOT_COVERED
    if FAILED in results:
        return FAILED
    if all(result == PASSED for result in results):
        return PASSED
    return NOT_VERIFIED


def markdown_cell(text):
    return text.replace("|", "\\|").replace("\n", " ")


def test_line(test):
    line = f"- {test['result']}: `{test['name']}`"
    if test["reason"]:
        line += f" – {markdown_cell(test['reason'])}"
    return line


def write_outputs(output_dir, summary, header, rows, unlinked, unknown):
    output_dir.mkdir(parents=True, exist_ok=True)

    with open(output_dir / "requirements-matrix.csv", "w", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        writer.writerow(["Requirement", "Title", "Status", "Test", "Test result", "Reason"])
        for requirement, status, tests in rows:
            if not tests:
                writer.writerow([requirement["id"], requirement["title"], status, "", "", ""])
            for test in tests:
                writer.writerow([
                    requirement["id"], requirement["title"], status,
                    test["name"], test["result"], test["reason"],
                ])

    lines = summary + ["# Requirements matrix", ""]
    lines += [f"- {label}: {value}" for label, value in header]
    lines += ["", "| Requirement | Status | Tests | Title |", "|---|---|---|---|"]
    for requirement, status, tests in rows:
        lines.append(
            f"| {requirement['id']} | {status} | {len(tests)} | {markdown_cell(requirement['title'])} |"
        )
    lines += ["", "## Tests per requirement", ""]
    for requirement, status, tests in rows:
        lines.append(f"### {requirement['id']} – {status}")
        lines.append("")
        if not tests:
            lines.append("No tests.")
        lines += [test_line(test) for test in tests]
        lines.append("")
    lines += ["## Tests not linked to a requirement", ""]
    lines += [test_line(test) for test in unlinked] or ["None."]
    lines.append("")
    if unknown:
        lines += ["## Requirement ids not in the catalog", ""]
        lines += [f"- `{requirement_id}`" for requirement_id in sorted(unknown)]
        lines.append("")

    markdown = "\n".join(lines)
    (output_dir / "requirements-matrix.md").write_text(markdown, encoding="utf-8")
    return markdown


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--requirements", required=True, help="Requirements catalog (JSON).")
    parser.add_argument("--results", required=True, help="Folder with the NUnit XML reports.")
    parser.add_argument("--output", required=True, help="Folder for the matrix files.")
    parser.add_argument("--project-settings", default="ProjectSettings/ProjectSettings.asset",
                        help="Unity project settings, for the version.")
    args = parser.parse_args()

    version = read_version(args.project_settings)
    commit = os.environ.get("HEAD_SHA") or os.environ.get("GITHUB_SHA") or "local"
    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")

    try:
        requirements = load_requirements(args.requirements)
        reports, tests = read_reports(args.results)
    except (OSError, ValueError, KeyError) as exception:
        error(str(exception))
        return 1

    if not reports:
        logs, compiler_errors = read_compiler_errors(args.results)
        markdown = write_no_report_summary(Path(args.output), version, commit, logs, compiler_errors)
        if step_summary:
            with open(step_summary, "a", encoding="utf-8") as file:
                file.write(markdown + "\n")
        for item in compiler_errors[:MAX_COMPILER_ERRORS]:
            print(
                f"::error file={escape_command(item['file'])},line={item['line']},col={item['column']},"
                f"title={item['code']}::{escape_command(item['message'])}"
            )
        error(f"No XML test report found in '{args.results}': the tests did not run "
              f"({len(compiler_errors)} compiler error(s) in the editor logs).")
        return 1
    if not tests:
        error("The XML test reports contain no tests.")
        return 1

    known_ids = {requirement["id"] for requirement in requirements}
    rows = []
    for requirement in requirements:
        linked = [test for test in tests if requirement["id"] in test["requirements"]]
        rows.append((requirement, requirement_status([test["result"] for test in linked]), linked))

    unlinked = [test for test in tests if not test["requirements"]]
    unknown = {requirement_id for test in tests for requirement_id in test["requirements"]} - known_ids

    passed, failed, other = count_results(tests)
    outcome = f"❌ {failed} test(s) failed" if failed else "✅ all tests passed"
    if not failed and other:
        outcome = f"⚠️ {other} test(s) did not run"
    summary = summary_header(version, commit) + [
        f"- Result: {outcome} – {passed} passed, {failed} failed, {other} skipped or inconclusive "
        f"({len(tests)} tests)",
        "",
    ]
    failed_tests = [test for test in tests if test["result"] == FAILED]
    if failed_tests:
        summary += [f"## Failed tests ({len(failed_tests)})", ""]
        summary += [test_line(test) for test in failed_tests]
        summary.append("")

    # Version and commit are already in the test summary above.
    header = [
        ("Reports", ", ".join(report.relative_to(args.results).as_posix() for report in reports)),
        ("Tests", str(len(tests))),
        ("Requirements", str(len(requirements))),
    ]
    markdown = write_outputs(Path(args.output), summary, header, rows, unlinked, unknown)

    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as file:
            file.write(markdown + "\n")

    for requirement_id in sorted(unknown):
        warning(f"Requirement '{requirement_id}' used by a test is not in the catalog.")

    exit_code = 0
    for requirement, status, _ in rows:
        print(f"{requirement['id']}: {status}")
        if status == NOT_COVERED:
            error(f"Requirement {requirement['id']} has no tests.")
            exit_code = 1
        elif status == NOT_VERIFIED:
            error(f"The tests of requirement {requirement['id']} did not run (skipped or inconclusive).")
            exit_code = 1
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
