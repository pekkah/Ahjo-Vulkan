# Upstream defect reports

Reduced repros and drafted reports for bugs that belong to an upstream project
rather than to this repo. Nothing here is compiled by the solution or run by CI —
these are documents plus the smallest program that reproduces the defect.

A file lands here when all of the following hold:

- the defect is in an upstream dependency (Slang, VMA, libktx, the Vulkan headers)
  rather than in our use of it;
- the repo carries a workaround whose only justification is that defect, so the
  workaround cannot be evaluated without it; and
- the repro is small enough to be worth keeping — the point is to re-run it
  against a new upstream version and find out whether the workaround can go.

The GitHub issue stays the tracking record (see #170 and #181); these files are
the evidence it points at. When a report is filed upstream, put the upstream
issue number in the tracking issue and in whatever `CLAUDE.md` rule documents the
workaround, so the next person to read the guard can find out whether it is still
needed.

| File | Upstream | Status |
|---|---|---|
| `slang-getbindingrangeimageformat-crash.{md,cpp}` | shader-slang/slang | Not filed. Tracked as #181. Guard: `SlangReflection.ImageFormatOf`. Fixed incidentally by open PR [#11344](https://github.com/shader-slang/slang/pull/11344), unmerged — see "Prior art upstream" in the report. Repro last re-run 2026-08-03 against the pinned `v2026.14.1`: still crashes (`0xC0000005`). |

## Re-testing after a version bump

The Slang repro builds against the pinned release archives the build already
downloads — see `native/slang/downloaded/<rid>/` for the headers and libraries,
and `Directory.Build.props` for the pinned version and its checksums. Build and
run instructions are in each report. A repro that stops crashing is the signal
that the corresponding workaround can be removed; delete the guard, keep a test
that exercises the previously fatal call, and bump the pin in the same change.
