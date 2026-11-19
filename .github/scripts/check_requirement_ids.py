"""Checks the requirement ids of the catalog and of the test sources.

Static check: it reads the C# sources, it does not compile them and does not
need Unity. So it fails in seconds, and it also sees the ids of tests that do
not compile or do not run, which the requirements matrix cannot see.

Catalog (Requirements/requirements.json):
  - "idPrefixes" lists the allowed prefixes (upper case letters, no
    duplicates);
  - every requirement id has the format PREFIX-nn (two digits) with an
    allowed prefix, and appears only once.

Sources (every .cs file under the sources folder): every
[Property("Requirement", ...)] must have a string literal as value, with the
format PREFIX-nn, an allowed prefix, and an id that is in the catalog.
Comments are ignored, so a commented-out attribute is not checked.

Exit code 1 when any problem is found, or when no requirement id is found in
the sources (a wrong folder would otherwise pass without checking anything).
Problems are printed as GitHub annotations, with file and line when known.

Only the Python standard library is used.
"""

import argparse
import json
import re
import sys
from pathlib import Path

REQUIREMENT_PROPERTY = "Requirement"

PREFIX_PATTERN = re.compile(r"^[A-Z]+$")
ID_PATTERN = re.compile(r"^(?P<prefix>[A-Z]+)-[0-9]{2}$")

# [Property("Requirement", <value>)], also written as PropertyAttribute or
# with the namespace (NUnit.Framework.Property). The value is everything up
# to the closing parenthesis; it is checked to be a single string literal.
PROPERTY_PATTERN = re.compile(
    r"\bProperty(?:Attribute)?\s*\(\s*\"" + REQUIREMENT_PROPERTY + r"\"\s*,\s*(?P<value>[^)]*)\)"
)
STRING_LITERAL_PATTERN = re.compile(r"^\"(?P<text>[^\"\\]*)\"$")


class Problems:
    def __init__(self):
        self.count = 0

    def add(self, message, file=None, line=None):
        self.count += 1
        location = ""
        if file is not None:
            location = f" file={file}" + (f",line={line}" if line is not None else "")
        print(f"::error{location}::{message}")


def strip_comments(source):
    """Replaces C# comments with spaces, keeping line breaks and strings.

    Line numbers stay the same, and comment markers inside strings or
    character literals are left alone.
    """
    result = []
    i = 0
    length = len(source)
    while i < length:
        char = source[i]
        pair = source[i:i + 2]
        if pair == "//":
            end = source.find("\n", i)
            end = length if end == -1 else end
            result.append(" " * (end - i))
            i = end
        elif pair == "/*":
            end = source.find("*/", i + 2)
            end = length if end == -1 else end + 2
            result.append("".join(c if c == "\n" else " " for c in source[i:end]))
            i = end
        elif pair == '@"':
            # Verbatim string: "" is an escaped quote, no backslash escapes.
            end = i + 2
            while end < length:
                if source[end] == '"':
                    if source[end + 1:end + 2] == '"':
                        end += 2
                        continue
                    break
                end += 1
            result.append(source[i:end + 1])
            i = end + 1
        elif char in ('"', "'"):
            end = i + 1
            while end < length and source[end] != char and source[end] != "\n":
                end += 2 if source[end] == "\\" else 1
            result.append(source[i:end + 1])
            i = end + 1
        else:
            result.append(char)
            i += 1
    return "".join(result)


def check_catalog(path, problems):
    """Returns the allowed prefixes and the ids of the catalog."""
    with open(path, encoding="utf-8") as file:
        catalog = json.load(file)

    prefixes = catalog.get("idPrefixes")
    if not isinstance(prefixes, list) or not prefixes:
        problems.add(f"{path}: \"idPrefixes\" must be a non-empty list of prefixes.", file=path)
        prefixes = []
    for prefix in prefixes:
        if not isinstance(prefix, str) or not PREFIX_PATTERN.match(prefix):
            problems.add(f"{path}: invalid prefix {prefix!r}: upper case letters only.", file=path)
    duplicate_prefixes = sorted({p for p in prefixes if prefixes.count(p) > 1})
    if duplicate_prefixes:
        problems.add(f"{path}: duplicate prefixes: {', '.join(duplicate_prefixes)}.", file=path)

    ids = [requirement.get("id", "") for requirement in catalog.get("requirements", [])]
    for requirement_id in ids:
        reason = id_problem(requirement_id, prefixes)
        if reason:
            problems.add(f"{path}: catalog id {requirement_id!r} {reason}.", file=path)
    duplicate_ids = sorted({i for i in ids if ids.count(i) > 1})
    if duplicate_ids:
        problems.add(f"{path}: duplicate requirement ids: {', '.join(duplicate_ids)}.", file=path)

    return set(prefixes), set(ids)


def id_problem(requirement_id, prefixes):
    """Why an id is not valid, or None."""
    match = ID_PATTERN.match(requirement_id) if isinstance(requirement_id, str) else None
    if match is None:
        return "does not have the format PREFIX-nn (e.g. FUNC-01)"
    if match.group("prefix") not in prefixes:
        return f"has a prefix that is not allowed (allowed: {', '.join(sorted(prefixes))})"
    return None


def check_sources(sources_dir, prefixes, catalog_ids, problems):
    """Returns how many requirement ids were found in the sources."""
    found = 0
    for source_file in sorted(Path(sources_dir).rglob("*.cs")):
        text = strip_comments(source_file.read_text(encoding="utf-8-sig"))
        file_name = source_file.as_posix()
        for match in PROPERTY_PATTERN.finditer(text):
            found += 1
            line = text.count("\n", 0, match.start()) + 1
            value = match.group("value").strip()
            literal = STRING_LITERAL_PATTERN.match(value)
            if literal is None:
                problems.add(
                    f"Requirement value {value!r} is not a plain string literal: it cannot be checked.",
                    file=file_name, line=line)
                continue
            requirement_id = literal.group("text")
            reason = id_problem(requirement_id, prefixes)
            if reason is None and requirement_id not in catalog_ids:
                reason = "is not in the requirements catalog"
            if reason:
                problems.add(f"Requirement id {requirement_id!r} {reason}.", file=file_name, line=line)
    return found


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--requirements", required=True, help="requirements catalog (JSON)")
    parser.add_argument("--sources", required=True, help="folder with the C# test sources")
    args = parser.parse_args()

    problems = Problems()
    prefixes, catalog_ids = check_catalog(args.requirements, problems)
    found = check_sources(args.sources, prefixes, catalog_ids, problems)
    if found == 0:
        problems.add(f"No [Property(\"{REQUIREMENT_PROPERTY}\", ...)] found under {args.sources}.")

    print(f"Catalog: {len(catalog_ids)} requirement ids, prefixes {', '.join(sorted(prefixes))}.")
    print(f"Sources: {found} requirement references checked, {problems.count} problems.")
    return 1 if problems.count else 0


if __name__ == "__main__":
    sys.exit(main())
