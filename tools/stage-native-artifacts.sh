#!/bin/sh
# Stage downloaded per-RID native artifacts where the native csprojs' pack
# targets look for them. Called by publish.yml (before the NuGet push) and by
# ci.yml's native-pack job (on every PR), so both stage identically (#169).
#
#   sh tools/stage-native-artifacts.sh <download-root> <vma|ktx|slang|ngx>
#
# <download-root> is an actions/download-artifact@v8 path given a
# `<kind>-native-*` pattern: one subdirectory per artifact, named
# `<kind>-native-<rid>`. The RID is taken from that name, so this script holds
# no RID list. A missing RID is not this script's job to notice: the pack
# target fails with AHJOPACK001 naming the file (tools/msbuild/
# Ahjo.Vulkan.NativePack.targets). Nothing matched at all is an error here,
# because that means the download step got nothing.
#
# Runs from the repository root.
set -eu

if [ "$#" -ne 2 ]; then
  echo "usage: $0 <download-root> <vma|ktx|slang|ngx>" >&2
  exit 2
fi

root=$1
kind=$2

case "$kind" in
  vma)   dest_root=native/vma/build ;;
  ktx)   dest_root=native/ktx/staged ;;
  slang) dest_root=native/slang/staged ;;
  ngx)   dest_root=native/ngx/build ;;
  *)
    echo "error: unknown kind '$kind' (expected vma, ktx, slang or ngx)" >&2
    exit 2
    ;;
esac

prefix="$kind-native-"
matched=0

for dir in "$root"/"$prefix"*; do
  [ -d "$dir" ] || continue
  rid=${dir##*/}
  rid=${rid#"$prefix"}
  dest="$dest_root/$rid"
  mkdir -p "$dest"
  cp -rv "$dir"/. "$dest/"
  matched=$((matched + 1))
done

if [ "$matched" -eq 0 ]; then
  echo "error: no $prefix* artifact directory matched under $root" >&2
  exit 1
fi

echo "Staged $matched $kind RID(s) under $dest_root/"
