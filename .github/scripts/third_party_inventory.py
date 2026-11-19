"""Builds the inventory of the third-party components of the project.

Names and exact versions are extracted automatically from the files that
decide them, so the inventory cannot drift from what is really used:
  - GitHub Actions and their pinned commits (.github/workflows/*.yml);
  - tool versions pinned in the workflows (env FOO_VERSION, action inputs
    ending in "Version", e.g. cliVersion);
  - Unity Editor version (ProjectSettings/ProjectVersion.txt);
  - Unity packages, direct and indirect (Packages/packages-lock.json);
  - binaries committed under Assets (*.dll): file version and SHA-256;
  - cspell dictionaries imported by URL (cspell.json);
  - environment of the jobs: runner image, Python, git, gh, Docker, and the
    Unity Docker image really pulled by the unity-tests job.

What no file contains (category, license when it is not detected, purpose,
validation) comes from a small metadata file, written once per component
and matched by name pattern. A component without metadata is reported with
a warning, it never fails the job: the inventory is evidence, not a check.

Two commands:
  unity-environment  run inside the unity-tests job, after the tests: records
                     the Docker image used and the licenses of the packages
                     in Library/PackageCache, which exist only there.
  report             builds inventory.json and inventory.md.

Only the Python standard library is used.
"""

import argparse
import datetime
import fnmatch
import hashlib
import json
import os
import platform
import re
import struct
import subprocess
import sys
from pathlib import Path

SHA_RE = re.compile(r"^[0-9a-f]{40}$")
USES_RE = re.compile(r"^\s*(?:-\s+)?uses:\s*['\"]?([^@\s'\"]+)@([^\s'\"#]+)['\"]?\s*(?:#\s*(\S+))?")
ENV_VERSION_RE = re.compile(r"^\s*([A-Z][A-Z0-9_]*)_VERSION:\s*['\"]?([^\s'\"#]+)")
INPUT_VERSION_RE = re.compile(r"^\s*([a-z][A-Za-z0-9]*Version):\s*['\"]?([^\s'\"#]+)")
STEP_START_RE = re.compile(r"^\s*-\s+(?:name|uses|run|id|if|env|with):")
NPM_URL_RE = re.compile(r"/npm/((?:@[^/@]+/)?[^/@]+)@([0-9][^/]*)/")

LICENSE_PATTERNS = [
    ("Unity Companion License", re.compile(r"Unity Companion (Package )?License", re.I)),
    ("Unity Package Distribution License", re.compile(r"Unity Package Distribution License", re.I)),
    ("MIT", re.compile(r"MIT License|Permission is hereby granted, free of charge", re.I)),
    ("Apache-2.0", re.compile(r"Apache License", re.I)),
]

ENV_FILE = "environment.txt"
LICENSES_FILE = "package-licenses.tsv"


def warn(message):
    print(f"::warning title=Third-party inventory::{message}")


def run_command(args):
    """First line of the output of a command, or None when it is missing."""
    try:
        result = subprocess.run(args, capture_output=True, text=True, timeout=30, check=False)
    except (OSError, subprocess.SubprocessError):
        return None
    output = (result.stdout or result.stderr).strip()
    return output.splitlines()[0] if output and result.returncode == 0 else None


# --- unity-environment -----------------------------------------------------

def detect_license(package_dir):
    for candidate in sorted(package_dir.iterdir()):
        if candidate.is_file() and candidate.name.lower().startswith(("license", "licence")):
            text = candidate.read_text(encoding="utf-8", errors="replace")
            for name, pattern in LICENSE_PATTERNS:
                if pattern.search(text):
                    return name
            return f"unknown, see {candidate.name}"
    # Built-in modules have no license file: the metadata file covers them.
    return ""


def command_unity_environment(args):
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)

    lines = [
        f"job={os.environ.get('GITHUB_JOB', '')}",
        f"runner_image_os={os.environ.get('ImageOS', '')}",
        f"runner_image_version={os.environ.get('ImageVersion', '')}",
        f"docker={run_command(['docker', '--version']) or ''}",
    ]
    try:
        images = subprocess.run(
            ["docker", "image", "ls", "--digests", "--format", "{{.Repository}}:{{.Tag}}@{{.Digest}}"],
            capture_output=True, text=True, timeout=30, check=False,
        ).stdout.split()
    except (OSError, subprocess.SubprocessError):
        images = []
    for image in images:
        if args.image_filter in image:
            lines.append(f"container_image={image}")
    (out / ENV_FILE).write_text("\n".join(lines) + "\n", encoding="utf-8")

    rows = []
    cache = Path(args.package_cache)
    if cache.is_dir():
        for package_dir in sorted(p for p in cache.iterdir() if p.is_dir()):
            manifest = package_dir / "package.json"
            if not manifest.is_file():
                continue
            data = json.loads(manifest.read_text(encoding="utf-8"))
            rows.append(f"{data.get('name', package_dir.name)}\t{data.get('version', '')}\t{detect_license(package_dir)}")
    else:
        warn(f"{cache} not found: package licenses not recorded.")
    (out / LICENSES_FILE).write_text("\n".join(rows) + ("\n" if rows else ""), encoding="utf-8")
    print("\n".join(lines))
    print(f"{len(rows)} package licenses recorded.")
    return 0


# --- report: extraction ------------------------------------------------------

def component(kind, name, version, pinned_by, source, location, license_=None):
    return {
        "kind": kind, "name": name, "version": version, "pinnedBy": pinned_by,
        "source": source, "location": location, "detectedLicense": license_,
    }


def scan_workflows(workflows_dir):
    actions = {}
    tools = []
    for workflow in sorted(Path(workflows_dir).glob("*.y*ml")):
        current_action = None
        for number, line in enumerate(workflow.read_text(encoding="utf-8").splitlines(), 1):
            stripped = line.split("#", 1)[0] if "uses:" not in line else line
            if STEP_START_RE.match(line):
                current_action = None
            match = USES_RE.match(line)
            if match:
                name, ref, comment = match.groups()
                if name.startswith(("./", "$/", "docker://")):
                    continue
                current_action = name
                if SHA_RE.match(ref):
                    version, pinned = comment or "unknown (no version comment)", f"commit {ref}"
                else:
                    version, pinned = ref, "tag (mutable)"
                key = (name, ref)
                entry = actions.setdefault(key, component(
                    "github-action", name, version, pinned,
                    f"https://github.com/{'/'.join(name.split('/')[:2])}", []))
                entry["location"].append(f"{workflow.as_posix()}:{number}")
                continue
            match = ENV_VERSION_RE.match(stripped)
            if match:
                tool, version = match.groups()
                tools.append(component("tool", tool.lower(), version, "version in workflow",
                                       "", f"{workflow.as_posix()}:{number}"))
                continue
            match = INPUT_VERSION_RE.match(stripped)
            if match and current_action:
                field, version = match.groups()
                tools.append(component("tool", f"{current_action}:{field}", version, "action input",
                                       "", f"{workflow.as_posix()}:{number}"))
    result = sorted(actions.values(), key=lambda entry: entry["name"])
    for entry in result:
        entry["location"] = ", ".join(entry["location"])
    return result + tools


def scan_checksums(workflows_dir, tools):
    """Attaches FOO_SHA256 values to the tool FOO pinned in the same file."""
    for tool in tools:
        if tool["kind"] != "tool" or ":" in tool["name"]:
            continue
        path = tool["location"].rsplit(":", 1)[0]
        text = Path(path).read_text(encoding="utf-8")
        match = re.search(rf"^\s*{re.escape(tool['name'].upper())}_SHA256:\s*['\"]?([0-9a-f]{{64}})", text, re.M)
        if match:
            tool["pinnedBy"] = f"version + SHA-256 {match.group(1)}"


def scan_editor(project_version):
    path = Path(project_version)
    text = path.read_text(encoding="utf-8")
    match = re.search(r"m_EditorVersionWithRevision:\s*(\S+)\s*\((\w+)\)", text)
    if not match:
        warn(f"No editor version found in {path}.")
        return []
    version, revision = match.groups()
    return [component("unity-editor", "Unity Editor", version, f"{path.as_posix()} (revision {revision})",
                      "https://unity.com/releases/editor/archive", path.as_posix())]


def scan_packages(lock_file, licenses):
    path = Path(lock_file)
    data = json.loads(path.read_text(encoding="utf-8"))
    result = []
    for name, info in sorted(data.get("dependencies", {}).items()):
        depth = info.get("depth", 0)
        source = info.get("source", "")
        origin = info.get("url") or ("Unity Editor (built-in)" if source == "builtin" else source)
        entry = component("unity-package", name, info.get("version", ""),
                          f"{path.as_posix()} ({'direct' if depth == 0 else f'indirect, depth {depth}'})",
                          origin, path.as_posix(), licenses.get(name))
        result.append(entry)
    return result


def pe_file_version(data):
    """FileVersion of a Windows/.NET binary, read from VS_FIXEDFILEINFO."""
    offset = data.find(b"\xbd\x04\xef\xfe")
    if offset < 0 or offset + 24 > len(data):
        return None
    _, _, ms, ls, _, _ = struct.unpack_from("<6I", data, offset)
    return f"{ms >> 16}.{ms & 0xFFFF}.{ls >> 16}.{ls & 0xFFFF}"


def scan_binaries(assets_dir):
    result = []
    for path in sorted(Path(assets_dir).rglob("*.dll")):
        data = path.read_bytes()
        result.append(component("binary", path.stem, pe_file_version(data) or "unknown",
                                f"file in repository, SHA-256 {hashlib.sha256(data).hexdigest()}",
                                "", path.as_posix()))
    return result


def scan_cspell(cspell_file):
    path = Path(cspell_file)
    if not path.is_file():
        return []
    data = json.loads(path.read_text(encoding="utf-8"))
    result = []
    for url in data.get("import", []):
        match = NPM_URL_RE.search(url)
        if match:
            result.append(component("dictionary", match.group(1), match.group(2), "version in URL",
                                    url, path.as_posix()))
    return result


def runner_component(job, image_os, image_version, location):
    if not image_os:
        return []
    return [component("runner-image", f"GitHub runner image {image_os}", image_version or "unknown",
                      f"runs-on label, image of job {job}",
                      f"https://github.com/actions/runner-images/releases/tag/{image_os}/{image_version}",
                      location)]


def scan_environment(unity_env_dir):
    """Environment of this job, plus the one recorded by unity-tests."""
    job = os.environ.get("GITHUB_JOB", "local")
    result = runner_component(job, os.environ.get("ImageOS"), os.environ.get("ImageVersion"), "this job")
    result.append(component("runtime", "Python", platform.python_version(), f"runner image, job {job}",
                            "https://www.python.org", sys.executable))
    for name, args, pattern in (("git", ["git", "--version"], r"([0-9][\w.]*)"),
                                ("GitHub CLI", ["gh", "--version"], r"([0-9][\w.]*)")):
        output = run_command(args)
        if output:
            match = re.search(pattern, output)
            result.append(component("runtime", name, match.group(1) if match else output,
                                    f"runner image, job {job}", "", args[0]))

    licenses = {}
    env_dir = Path(unity_env_dir) if unity_env_dir else None
    if not env_dir or not (env_dir / ENV_FILE).is_file():
        warn("Unity environment not available (unity-tests did not record it): "
             "Docker image and package licenses are missing from this inventory.")
        return result, licenses

    values = {}
    images = []
    for line in (env_dir / ENV_FILE).read_text(encoding="utf-8").splitlines():
        key, _, value = line.partition("=")
        if key == "container_image":
            images.append(value)
        else:
            values[key] = value
    unity_job = values.get("job") or "unity-tests"
    result += runner_component(unity_job, values.get("runner_image_os"), values.get("runner_image_version"),
                               f"recorded by {unity_job}")
    if values.get("docker"):
        match = re.search(r"([0-9][\w.]*)", values["docker"])
        result.append(component("runtime", "Docker", match.group(1) if match else values["docker"],
                                f"runner image, job {unity_job}", "", "docker"))
    if not images:
        warn(f"No Unity Docker image recorded by {unity_job}.")
    for image in images:
        reference, _, digest = image.partition("@")
        repository, _, tag = reference.rpartition(":")
        result.append(component("container-image", repository, tag, f"digest {digest}",
                                f"https://hub.docker.com/r/{repository}", f"pulled by {unity_job}"))

    licenses_file = env_dir / LICENSES_FILE
    if licenses_file.is_file():
        for row in licenses_file.read_text(encoding="utf-8").splitlines():
            parts = row.split("\t")
            if len(parts) == 3 and parts[2]:
                licenses[parts[0]] = parts[2]
    return result, licenses


# --- report: metadata and output ----------------------------------------------

def apply_metadata(components, metadata_file):
    entries = json.loads(Path(metadata_file).read_text(encoding="utf-8")).get("components", [])
    missing = []
    for item in components:
        match = next((m for m in entries
                      if m.get("kind") == item["kind"] and fnmatch.fnmatchcase(item["name"], m.get("match", ""))),
                     None)
        if match is None:
            missing.append(item)
            item.update(category="?", license=item["detectedLicense"] or "?", purpose="?", validation="?",
                        metadataMissing=True)
            continue
        item.update(
            category=match.get("category", "?"),
            license=item["detectedLicense"] or match.get("license", "?"),
            purpose=match.get("purpose", "?"),
            validation=match.get("validation", "?"),
            metadataMissing=False,
        )
    for item in missing:
        warn(f"No metadata for {item['kind']} '{item['name']}': add an entry to {metadata_file}.")
    return missing


KIND_TITLES = [
    ("github-action", "GitHub Actions"),
    ("tool", "Tools pinned in the workflows"),
    ("unity-editor", "Unity Editor"),
    ("container-image", "Docker images"),
    ("unity-package", "Unity packages"),
    ("binary", "Binaries in the repository"),
    ("dictionary", "Spelling dictionaries"),
    ("runner-image", "Runner images"),
    ("runtime", "Runtimes and command-line tools of the runners"),
]


def cell(value):
    return str(value).replace("|", "\\|").replace("\n", " ") if value not in (None, "") else "-"


def write_markdown(path, header, components, missing):
    lines = ["# Third-party component inventory", ""]
    lines += [f"- {key}: {cell(value)}" for key, value in header.items()]
    mutable = [c for c in components if c["pinnedBy"] == "tag (mutable)"]
    lines += [f"- Components: {len(components)}; without metadata: {len(missing)}; "
              f"actions on a mutable tag: {len(mutable)}", ""]
    lines += ["Categories: A = part of the game; B = decides whether a change passes "
              "(process tool); editor = editor only, neither in the game nor a check.", ""]
    for kind, title in KIND_TITLES:
        items = [c for c in components if c["kind"] == kind]
        if not items:
            continue
        lines += [f"## {title}", "",
                  "| Component | Version | Pinned by | Cat. | License | Purpose | Validation | Source |",
                  "|---|---|---|---|---|---|---|---|"]
        for c in items:
            lines.append("| " + " | ".join(cell(v) for v in (
                c["name"], c["version"], c["pinnedBy"], c["category"], c["license"],
                c["purpose"], c["validation"], c["source"])) + " |")
        lines.append("")
    Path(path).write_text("\n".join(lines), encoding="utf-8")


def command_report(args):
    components = scan_workflows(args.workflows)
    scan_checksums(args.workflows, components)
    components += scan_editor(args.project_version)
    environment, licenses = scan_environment(args.unity_environment)
    components += [c for c in environment if c["kind"] == "container-image"]
    components += scan_packages(args.packages_lock, licenses)
    components += scan_binaries(args.assets)
    components += scan_cspell(args.cspell)
    components += [c for c in environment if c["kind"] != "container-image"]

    missing = apply_metadata(components, args.metadata)

    server = os.environ.get("GITHUB_SERVER_URL", "")
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    run_id = os.environ.get("GITHUB_RUN_ID", "")
    header = {
        "Repository": repository or "local",
        "Commit": os.environ.get("HEAD_SHA") or os.environ.get("GITHUB_SHA") or "local",
        "Ref": os.environ.get("GITHUB_HEAD_REF") or os.environ.get("GITHUB_REF_NAME") or "local",
        "Run": f"{server}/{repository}/actions/runs/{run_id}" if run_id else "local",
        "Generated": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }

    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    (out / "inventory.json").write_text(
        json.dumps({"header": header, "components": components}, indent=2) + "\n", encoding="utf-8")
    write_markdown(out / "inventory.md", header, components, missing)
    print(f"{len(components)} components, {len(missing)} without metadata.")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)

    unity = commands.add_parser("unity-environment")
    unity.add_argument("--package-cache", default="Library/PackageCache")
    unity.add_argument("--image-filter", default="unityci/editor")
    unity.add_argument("--output", required=True)

    report = commands.add_parser("report")
    report.add_argument("--workflows", default=".github/workflows")
    report.add_argument("--project-version", default="ProjectSettings/ProjectVersion.txt")
    report.add_argument("--packages-lock", default="Packages/packages-lock.json")
    report.add_argument("--assets", default="Assets")
    report.add_argument("--cspell", default="cspell.json")
    report.add_argument("--metadata", default=".github/third-party-metadata.json")
    report.add_argument("--unity-environment", default=None)
    report.add_argument("--output", required=True)

    args = parser.parse_args()
    if args.command == "unity-environment":
        return command_unity_environment(args)
    return command_report(args)


if __name__ == "__main__":
    sys.exit(main())
