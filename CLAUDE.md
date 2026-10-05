# Ahjo.Vulkan — Claude Project Memory

.NET 10 / C# 14 Vulkan bindings + low-allocation wrapper, aimed at the Ahjo game engine. Eight publishable NuGet packages live in this repo; see `README.md` for the consumer-facing overview.

Work is driven by GitHub issues. `/work-issue <number>` runs the standard flow: triage → architect (spec + plan) → approval → implementer → reviewers → PR.

## Load-bearing invariants (index)

Full details live in scoped CLAUDE.md files that load automatically when you work in the relevant directory. The one-line versions, so none get violated from a distance:

1. **UTF-8 string literals** — Vulkan `const char*` takes `Utf8Name.FromLiteral("…"u8)`; never round-trip through `string` + `Encoding.UTF8.GetBytes`. → `src/Ahjo.Vulkan/CLAUDE.md`
2. **Native AOT stays clean** — no reflection discovery, no dynamic codegen, nothing trim-unsafe reachable from the wrapper; CI publishes `samples/AotSmoke` with `PublishAot=true`. → `src/Ahjo.Vulkan/CLAUDE.md`, `docs/aot-notes.md`
3. **Zero per-frame allocations** on `Recording/`, `Sync/`, `Pools/`, `Memory/` hot paths — and anything else called per frame, e.g. `Ahjo.Vulkan.Ngx`'s `DlssFeature.Evaluate`; setup-time allocation is fine. → `src/Ahjo.Vulkan/CLAUDE.md`, `src/Ahjo.Vulkan.Ngx/CLAUDE.md`
4. **Generated code is generated** — never hand-edit `src/*/Generated/`, `native/downloaded/`, `native/ktx/downloaded/`, `native/slang/downloaded/`; edit `tools/*.rsp` + regenerate (`/regen-bindings`). → per-project CLAUDE.md files
5. **`TreatWarningsAsErrors=true`** repo-wide with `AnalysisLevel=latest`. Fix the diagnostic; don't `#pragma`-suppress to get green.

## Common commands

Benchmarks go through `/run-bench` (always `-c Release`); binding regeneration goes through `/regen-bindings`.

```bash
# AOT smoke locally (Windows; needs MSVC env via vcvars or VS dev shell)
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
```

## CI (summary)

Wrapper tests run on `windows-latest` only — issue #32 closed Linux wrapper coverage (SwiftShader SIGSEGV; software rasterizers aren't honest coverage). Two narrow lanes (`vma-linux`, `ktx-native`) exist solely to prove shipped native binaries execute before they reach NuGet — they are **not** wrapper coverage; don't grow them. Full rationale and rules: `.github/CLAUDE.md`.

Publishing: preview packages on `push:main`, stable on GitHub release; one `v0.x.y` tag ships every package.

## Roles: architect and implementer

Non-trivial work splits into two roles, each a subagent in `.claude/agents/`:

- **architect** — turns a GitHub issue into a paired design spec + implementation plan under `docs/design/`. Explores the code, weighs options, decides. Touches docs only, never `src/`.
- **implementer** — executes an approved plan step by step: edits code, builds, tests. Doesn't redesign; deviations from the plan get reported back, not improvised.

The bar for "non-trivial" is: would a reviewer want the *why* written down? Typo/one-liner fixes skip the spec and go straight to implementation.

Reviewers close the loop before a PR: `vulkan-validation-reviewer` (Vulkan correctness) and `bench-coverage-checker` (allocation coverage) on any diff touching the wrapper surface.

## Spec-driven workflow

- `docs/design/specs/YYYY-MM-DD-issue-NN-<topic>-design.md` — "what and why"
- `docs/design/plans/YYYY-MM-DD-issue-NN-<topic>.md` — "how"

Conventions and the quality bar: `docs/design/CLAUDE.md`.

## Commit + PR style

Commits: `<area>: <imperative>` — e.g. `CI: enable auto-publish`, `Packaging: add LICENSE + Source Link`. PRs reference their issue (`Closes #NN`) and merge to `main`.

## What lives in `~/.claude/projects/.../memory/` (auto-memory)

Long-running project context that's likely to drift (issue numbers, design decisions in flight, user-specific preferences) lives in auto-memory, not in this file. The CLAUDE.md layer (this file + the scoped ones) is **stable** — invariants that hold across sessions and that anyone working in the repo should follow.
