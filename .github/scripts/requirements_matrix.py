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
A failed test is shown as "Failed", with its reason, but does not fail this
script: the unity-tests job already fails on it.
Requirement ids that are not in the catalog are reported as warnings.

Only the Python standard library is used.
"""

import argparse
import csv
import json
import os
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

REQUIREMENT_PROPERTY = "Requirement"

PASSED = "Passed"
FAILED = "Failed"
NOT_VERIFIED = "Not verified"
NOT_COVERED = "Not covered"

# Longest failure reason kept in the matrix; the full text is in the XML.
MAX_REASON_LENGTH = 200


def error(message):
    print(f"::error::{message}")


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
    """Label and first line of the failure or skip message, if any."""
    message = test_case.findtext("failure/message") or test_case.findtext("reason/message") or ""
    lines = [line.strip() for line in message.strip().splitlines() if line.strip()]
    reason = lines[0] if lines else ""
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


def write_outputs(output_dir, header, rows, unlinked, unknown):
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

    lines = ["# Requirements matrix", ""]
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
    args = parser.parse_args()

    try:
        requirements = load_requirements(args.requirements)
        reports, tests = read_reports(args.results)
    except (OSError, ValueError, KeyError) as exception:
        error(str(exception))
        return 1

    if not reports:
        error(f"No XML test report found in '{args.results}': the tests did not run (compile error or failed setup).")
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

    header = [
        ("Commit", os.environ.get("HEAD_SHA") or os.environ.get("GITHUB_SHA") or "local"),
        ("Reports", ", ".join(report.relative_to(args.results).as_posix() for report in reports)),
        ("Tests", str(len(tests))),
        ("Requirements", str(len(requirements))),
    ]
    markdown = write_outputs(Path(args.output), header, rows, unlinked, unknown)

    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as file:
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
