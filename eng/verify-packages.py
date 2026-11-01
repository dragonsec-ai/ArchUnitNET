#!/usr/bin/env python3
"""Inspect the entire package graph and test it from outside the source checkout."""

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile


def require(condition, message):
    if not condition:
        raise SystemExit(message)


def inspect_packages(repository, feed, version, require_license):
    projects = sorted((repository / "src").glob("*/*.csproj"))
    identities = {p.stem: "ArchUnit" if p.stem == "ArchUnitSharp" else p.stem for p in projects}
    require(len(projects) == 11, "Update package inspection when the shipping graph changes.")
    expected = {f"{identity}.{version}.nupkg" for identity in identities.values()}
    actual = {p.name for p in feed.glob("*.nupkg")}
    require(actual == expected, f"Package set mismatch. Missing: {expected - actual}; unexpected: {actual - expected}")
    namespace = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
    for project in projects:
        identity = identities[project.stem]
        with zipfile.ZipFile(feed / f"{identity}.{version}.nupkg") as package:
            nuspecs = [n for n in package.namelist() if n.endswith(".nuspec")]
            require(len(nuspecs) == 1, f"{identity}: expected one manifest")
            document = ET.fromstring(package.read(nuspecs[0]))
            # NuGet's schema namespace varies with the metadata features in use.
            namespace["n"] = document.tag.partition("}")[0].removeprefix("{")
            metadata = document.find("n:metadata", namespace)
            require(metadata is not None, f"{identity}: missing metadata")
            text = lambda name: metadata.findtext(f"n:{name}", namespaces=namespace)
            require(text("id") == identity and text("version") == version, f"{identity}: wrong identity/version")
            require("Robey Beswick" in (text("authors") or ""), f"{identity}: missing original-author attribution")
            require(text("readme") == "README.md" and "README.md" in package.namelist(), f"{identity}: missing README")
            repository_metadata = metadata.find("n:repository", namespace)
            require(repository_metadata is not None and repository_metadata.get("url") == "https://github.com/dragonsec-ai/ArchUnitNET", f"{identity}: incorrect repository")
            require(f"lib/net10.0/{project.stem}.dll" in package.namelist(), f"{identity}: missing net10.0 assembly")
            require(f"lib/net10.0/{project.stem}.xml" in package.namelist(), f"{identity}: missing XML documentation")
            require(not any(".Tests" in name for name in package.namelist()), f"{identity}: test artifacts leaked")
            if require_license:
                license_node = metadata.find("n:license", namespace)
                require(license_node is not None and license_node.get("type") == "file" and license_node.text == "LICENSE", f"{identity}: publication requires the reviewed LICENSE metadata (#53)")
                require("LICENSE" in package.namelist() and package.read("LICENSE").strip(), f"{identity}: missing license text")
            references = ET.parse(project).getroot().findall(".//ProjectReference")
            expected_dependencies = {
                identities[Path(item.attrib["Include"].replace("\\", "/")).stem]
                for item in references
            }
            groups = metadata.findall("n:dependencies/n:group", namespace)
            require(len(groups) == 1 and groups[0].get("targetFramework") == "net10.0", f"{identity}: unexpected framework groups")
            dependencies = {d.get("id"): d.get("version") for d in groups[0].findall("n:dependency", namespace)}
            internal = {name for name in dependencies if name in identities.values()}
            require(internal == expected_dependencies, f"{identity}: incorrect internal dependencies: {internal}")
            for dependency in internal:
                require(dependencies[dependency] in (version, f"[{version}, )", f"[{version},)"), f"{identity}: incorrect dependency version for {dependency}")
    print(f"Inspected all {len(expected)} packages at {version}.", flush=True)
    return set(identities.values())


def test_consumer(repository, feed, version, identities, public):
    with tempfile.TemporaryDirectory(prefix="archunit-package-consumer-") as temporary:
        root = Path(temporary).resolve()
        require(not root.is_relative_to(repository), "Consumer must be outside the repository.")
        consumer = root / "consumer"
        shutil.copytree(repository / "eng/package-consumer", consumer, ignore=shutil.ignore_patterns("bin", "obj"))
        configuration = ET.Element("configuration")
        sources = ET.SubElement(configuration, "packageSources")
        ET.SubElement(sources, "clear")
        if not public:
            ET.SubElement(sources, "add", key="local", value=str(feed))
        ET.SubElement(sources, "add", key="nuget", value="https://api.nuget.org/v3/index.json")
        mapping = ET.SubElement(configuration, "packageSourceMapping")
        if not public:
            local = ET.SubElement(mapping, "packageSource", key="local")
            ET.SubElement(local, "package", pattern="ArchUnit")
            ET.SubElement(local, "package", pattern="ArchUnitSharp.*")
        remote = ET.SubElement(mapping, "packageSource", key="nuget")
        ET.SubElement(remote, "package", pattern="*")
        fallback = ET.SubElement(configuration, "fallbackPackageFolders")
        ET.SubElement(fallback, "clear")
        config_path = root / "NuGet.config"
        ET.ElementTree(configuration).write(config_path, encoding="unicode")
        environment = dict(os.environ, NUGET_PACKAGES=str(root / "packages"))
        common = [f"-p:ArchUnitPackageVersion={version}"]
        dotnet = os.environ.get("DOTNET", "dotnet")
        subprocess.run([dotnet, "restore", "PackageConsumer.csproj", "--configfile", str(config_path), "--no-cache", "-p:RestoreFallbackFolders=", *common], cwd=consumer, env=environment, check=True)
        assets = json.loads((consumer / "obj/project.assets.json").read_text())
        require(all(library["type"] == "package" for library in assets["libraries"].values()), "Consumer restored a project reference.")
        for identity in identities:
            require(f"{identity}/{version}" in assets["libraries"], f"Consumer is missing {identity}/{version}")
        subprocess.run([dotnet, "test", "PackageConsumer.csproj", "-c", "Release", "--no-restore", *common], cwd=consumer, env=environment, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, default=Path("artifacts/packages"))
    parser.add_argument("--version", required=True)
    parser.add_argument("--require-license", action="store_true")
    parser.add_argument("--inspect-only", action="store_true")
    parser.add_argument("--public", action="store_true", help="Restore from public NuGet after publication instead of the local feed.")
    args = parser.parse_args()
    require(re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?", args.version), "Expected a plain SemVer release version.")
    repository = Path(__file__).resolve().parent.parent
    feed = args.packages.resolve()
    identities = inspect_packages(repository, feed, args.version, args.require_license)
    if not args.inspect_only:
        test_consumer(repository, feed, args.version, identities, args.public)


if __name__ == "__main__":
    main()
