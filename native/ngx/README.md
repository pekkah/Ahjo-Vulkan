# native/ngx — NVIDIA DLSS (NGX) SDK for local development

Tracking issue: #214. Nothing here is built or packed yet; this directory is the
pinned input for the upcoming `Ahjo.Vulkan.Ngx.Native` shim and for running the
DLSS samples/tests on an NVIDIA machine.

## Setup

```pwsh
./tools/setup-ngx.ps1                       # host platform, verify against pins
./tools/setup-ngx.ps1 -Platform all -IncludeDocs
```

The script fetches individual files from <https://github.com/NVIDIA/DLSS> at the
`<NgxVersion>` tag in `Directory.Build.props` (no clone: the repo is >600 MB of
DLLs), verifies every file against `pins.sha256`, and stages:

| Path | Committed? | What |
|---|---|---|
| `include/*.h` | yes | SDK headers, the generator input of record |
| `NGX-LICENSE.txt` | yes | NVIDIA RTX SDKs licence, packed beside the shim later |
| `pins.sha256` | yes | SHA-256 per fetched file at the pinned tag |
| `staged/<rid>/nvsdk_ngx_s*.lib`, `libnvsdk_ngx.a` | no | static NGX client library the shim links |
| `staged/<rid>/rel/` | no | production feature DLL (`nvngx_dlss.dll` / `libnvidia-ngx-dlss.so.*`) |
| `staged/<rid>/dev/` | no | watermarked development DLL with the debug overlay; never ship |
| `doc/` | no | programming guide PDF (`-IncludeDocs`) |
| `downloaded/<tag>/` | no | raw download cache mirroring upstream paths |

## The feature DLL is not part of any package

By decision (#214) the Ahjo packages ship wrapper code and the shim only. An
application that uses DLSS obtains `nvngx_dlss.dll` from NVIDIA itself and ships
it beside its executable (or points `NgxDescription.DlssSearchPaths` at it). The
copies under `staged/` exist so this repo's samples and tests can run on a
developer's NVIDIA GPU; they are git-ignored and must stay that way.

## Bumping the pin

```pwsh
./tools/setup-ngx.ps1 -Version v310.8.0 -Platform all -IncludeDocs -Force -UpdatePins
```

Then update `<NgxVersion>` in `Directory.Build.props`, review the header diff
under `include/`, and commit `pins.sha256`, `include/` and `NGX-LICENSE.txt`
together. Once the shim exists, a bump also requires `/regen-bindings`.
