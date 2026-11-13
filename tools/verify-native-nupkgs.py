#!/usr/bin/env python3
"""Open each built native nupkg and compare it with its declared pack list (#169).

    python3 -I tools/verify-native-nupkgs.py <artifacts-dir> <PackageId> [<PackageId>...]

The declared list is <artifacts-dir>/<PackageId>.native-files.txt, written by
the AhjoPackNativeFiles target (tools/msbuild/Ahjo.Vulkan.NativePack.targets)
from the same items the csproj packs. It is the FULL declared list, not
filtered by what existed, so this script never carries an expected-file list
of its own.

For each package, fails when:
  - the manifest is missing (the pack target did not run);
  - there is not exactly one <PackageId>.<version>.nupkg beside it;
  - the manifest declares no runtimes/ entry;
  - a declared entry is not in the nupkg;
  - the nupkg has a runtimes/ entry the manifest does not declare (e.g. the
    NGX feature DLL, which must never ship: #214).

Failures print as GitHub Actions ::error:: lines. Exit 1 on any failure.
Standard library only.
"""

import os
import re
import sys
import zipfile


def verify(artifacts_dir, package_id, errors):
    manifest_path = os.path.join(artifacts_dir, package_id + ".native-files.txt")
    if not os.path.isfile(manifest_path):
        errors.append(
            "{0}: no {0}.native-files.txt in {1} (did its pack target run?)".format(package_id, artifacts_dir)
        )
        return

    # \.\d keeps Ahjo.Vulkan.Slang from matching Ahjo.Vulkan.Slang.Native.*;
    # the $ anchor keeps .snupkg out.
    pattern = re.compile("^" + re.escape(package_id) + r"\.\d.*\.nupkg$")
    candidates = sorted(name for name in os.listdir(artifacts_dir) if pattern.match(name))
    if len(candidates) != 1:
        errors.append(
            "{0}: expected exactly one {0}.<version>.nupkg in {1}, found {2}".format(
                package_id, artifacts_dir, len(candidates)
            )
        )
        return
    nupkg = candidates[0]

    with open(manifest_path, encoding="utf-8") as handle:
        declared = [line.strip() for line in handle if line.strip()]

    failed = False

    if not any(entry.startswith("runtimes/") for entry in declared):
        errors.append("{0}: {0}.native-files.txt declares no runtimes/ entry".format(package_id))
        failed = True

    with zipfile.ZipFile(os.path.join(artifacts_dir, nupkg)) as archive:
        entries = set(archive.namelist())

    declared_set = set(declared)

    for entry in declared:
        if entry not in entries:
            errors.append("{0}: {1} is declared in the pack list but is not in {2}".format(package_id, entry, nupkg))
            failed = True

    for entry in sorted(entries):
        if entry.startswith("runtimes/") and entry not in declared_set:
            errors.append("{0}: {1} is in {2} but not in the pack list".format(package_id, entry, nupkg))
            failed = True

    if not failed:
        print("{0}: {1} declared, checked against {2}".format(package_id, len(declared), nupkg))


def main(argv):
    if len(argv) < 3:
        print("usage: verify-native-nupkgs.py <artifacts-dir> <PackageId> [<PackageId>...]", file=sys.stderr)
        return 2

    artifacts_dir = argv[1]
    errors = []
    for package_id in argv[2:]:
        verify(artifacts_dir, package_id, errors)

    for error in errors:
        print("::error::" + error)

    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
