# Ahjo.Vulkan.Ngx.Native — the NGX shim, its export contract, and the `wchar_t` boundary

`Generated/` is ClangSharp output from `native/ngx/src/ahjo_ngx.h` plus the pinned NGX headers at `NgxVersion` in `Directory.Build.props`. **Never hand-edit anything under `Generated/`** — it is overwritten wholesale on the next regen.

To change the bindings: edit `tools/generate-ngx.rsp`, bump `NgxVersion` if needed, then:

```bash
./tools/setup-ngx.ps1 -SkipFeatureDll          # only needed once, or after a pin bump
dotnet build src/Ahjo.Vulkan.Ngx.Native -t:Regenerate
```

The regen needs **no network**: the headers it parses are committed under `native/ngx/include/`, because they are the generator input of record and a pin bump should show the API diff before the generated output is re-derived from it.

## The shim exists because NGX has no DLL

NVIDIA ships NGX as a **static client library** — `nvsdk_ngx_s.lib` / `libnvsdk_ngx.a` — which finds and loads the real NGX runtime inside the display driver at call time. There is nothing to P/Invoke until something links that archive into a shared library. `native/ngx/src/ahjo_ngx.cpp` is that something, and `ahjo_ngx.dll` / `libahjo_ngx.so` is what this package ships.

The Windows archive is the **static-CRT release** build (its embedded object names read `_out/wddm_amd64_release_static_release_vs2015/…`), so the shim must be MSVC `/MT`. `MSVC_RUNTIME_LIBRARY "MultiThreaded"` in `native/ngx/CMakeLists.txt` is a hard requirement, not a preference.

## The export list lives in three places, and a test is what keeps them equal

27 names, written out in:

1. `native/ngx/src/ahjo_ngx.def` — the Windows `/DEF:` list. `WINDOWS_EXPORT_ALL_SYMBOLS` (what VMA uses) does **not** work here: CMake computes that list from the target's own object files, and 20 of the 27 come from the linked static library, so it would silently produce a DLL with 7 exports.
2. `native/ngx/src/ahjo_ngx.map` — the Linux version script, paired with `--whole-archive` on the static library and `local: *;` hiding everything else.
3. `NgxExportDriftTests.RequiredExports` in `tests/Ahjo.Vulkan.Ngx.Native.Tests` — a literal `string[]`, no reflection.

`NgxExportDriftTests` checks all three against each other **and** against `NativeLibrary.TryGetExport` on the loaded shim. Add a name in one place and the test fails naming the others. Do not weaken it: hand-maintained lists in three files is the cost of D1's hybrid contract, and this test is what buys it back.

20 of the 27 are re-exported **verbatim** from NVIDIA's archive with no wrapper code — the rsp parses NVIDIA's own headers for them, so a pin bump shows the API diff in `git diff native/ngx/include/` and is absorbed by a regen. `NVSDK_NGX_VULKAN_EvaluateFeature_C` in particular is on the per-frame path and must stay a direct call with no shim frame.

## The `wchar_t` rule

**Nothing wide may ever appear in `Generated/`.** If a regen introduces one, `--exclude` the struct that carries it. Do **not** `--remap wchar_t`.

`wchar_t` is 2 bytes on Windows and 4 on Linux, and every rsp in this repo parses at a fixed `--target=x86_64-unknown-linux-gnu` so generated output is a function of the pin rather than of the host. Under that target a `const wchar_t*` comes out as `int*` — a UTF-32 buffer — which the SDK then reads as UTF-16 on Windows. The failure surfaces as `FAIL_FeatureNotFound` with no clue why, on Windows only, for paths only. A remap would fix Windows and break Linux; it is a per-RID lie in a file whose whole purpose is being host-independent.

Excluding the wchar_t-taking *functions* is not enough either: `NVSDK_NGX_PathListInfo.Path` and `NVSDK_NGX_FeatureDiscoveryInfo.ApplicationDataPath` would still be generated with `int**` / `int*` fields that nothing stops a caller filling in. So the six wide-string **structs** are excluded and `AhjoNgxInitInfo` — declared in our own header, generated from that one definition — is their UTF-8 mirror. The shim widens on the way in and narrows on the way out, with a hand-rolled converter (not `mbstowcs`, which depends on the process locale a library must not set; not `MultiByteToWideChar`, which is Windows-only).

`grep -r wchar_t src/Ahjo.Vulkan.Ngx.Native/Generated` must return nothing. Check it after every regen.

## Two rsp deltas, both forced

`tools/generate-ngx.rsp` is the only rsp here that turns `generate-macro-bindings` **on** and the only one that leaves `strip-enum-member-type-name` **off**. Neither was a style choice.

**`generate-macro-bindings` on.** Without it, **zero** of the 204 `NVSDK_NGX_Parameter_*` string macros are emitted — measured. With it they come out as `public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Width => "Width"u8;`, which is exactly the UTF-8 literal form the repo's invariant 1 requires. Hand-writing them instead is the drift this project exists to avoid.

The cost is that it also emits 74 `NVSDK_NGX_EParameter_*` constants whose values embed raw `0x01`–`0x1f` bytes (ClangSharp escapes `\t`, `\n`, `\r`, `\0` and emits the rest literally). They are deprecated aliases of the names above, nothing in the DLSS path uses them, and committing raw control characters into a `.cs` file under `text=auto eol=lf` normalization is a hazard with no upside — so they are excluded. After a pin bump, regenerate that exclusion list mechanically rather than editing it:

```bash
grep -o '^#define \(NVSDK_NGX_EParameter_[A-Za-z0-9_]*\)' \
  native/ngx/include/nvsdk_ngx_defs.h | sed 's/^#define //'
```

**`strip-enum-member-type-name` off.** With it, the output does not compile. `NVSDK_NGX_Result` is a bitwise-or chain off one member, and the stripper rewrites member *names* but not initializer expressions that reference sibling members — 18 × `CS0103`. Enum members therefore keep their `NVSDK_NGX_`-prefixed names in this one project. Don't "fix" that.

## The shim build is opt-in, and an absent SDK is not an error

The SDK is proprietary and licence-encumbered, so **nothing here ever downloads it**. `./tools/setup-ngx.ps1` is an explicit act. `BuildNgxForHost` is conditioned on the staged static library existing; when it does not, `WarnNgxSdkMissing` prints one high-importance `<Message>` — **not** a `<Warning>`, because `TreatWarningsAsErrors=true` is repo-wide and a warning would break `git clone && dotnet build` for every contributor who will never touch DLSS.

The test suite completes that contract: it skips wholesale without a shim, **unless `AHJO_NGX_REQUIRE_SHIM=1`**, which the `ngx-native` CI lane sets. That is what stops the lane reporting green while executing nothing.

## What is never committed, packed or fetched

The feature DLL — `nvngx_dlss.dll` / `libnvidia-ngx-dlss.so.<version>`. Consumers supply it (#214). `native/ngx/staged/` is git-ignored, `PackNgxRuntimes` packs only the shim we compiled plus `NGX-LICENSE.txt`, and CI fetches with `-SkipFeatureDll` so a CI run is structurally incapable of pulling one rather than merely not asked to. The static client library and archive are not packed either.

## The lane

`ngx-native` (`build-ngx-native.yml`, called by both `ci.yml` and `publish.yml`) builds the shim per RID and runs this suite against it before uploading. It provisions no Vulkan loader and no ICD and leaves `AHJO_VULKAN_TIER` unset: the shim **links no `vulkan-1`** — it only includes the headers, the same reason `VMA_STATIC_VULKAN_FUNCTIONS=0` exists — so if this suite ever needs a loader, something got linked in that the package's contract says is not there.

It **cannot** evaluate DLSS: no hosted runner has an NVIDIA driver. Real `GetFeatureRequirements` / create / evaluate coverage is a local-NVIDIA-hardware item, recorded as such in `docs/ci-coverage.md`. It is a build-artifact check — don't grow it.
