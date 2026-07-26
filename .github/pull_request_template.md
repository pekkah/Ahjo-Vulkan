## Summary

<!-- What changed and why. One paragraph is fine. -->

Closes #

## Vulkan coverage

<!--
Which run's results are you quoting, and what did that host actually have?
`AHJO_VULKAN_TIER` is the answer — the contract test prints
`AHJO_VULKAN_TIER declared=… observed=… (…)` on every run. Paste that line.

"N tests passed locally" with no tier is not evidence: it is indistinguishable
from N tests skipping. See docs/ci-coverage.md.
-->

- Declared tier of the run quoted below: `none` / `software` / `hardware` / `validation`
- Contract-test line:

```
AHJO_VULKAN_TIER declared=… observed=… (…)
```

- Results: `Failed: 0, Passed: …, Skipped: …, Total: …`

## Checklist

- [ ] `dotnet build Ahjo.Vulkan.slnx` clean (`TreatWarningsAsErrors`, no new suppressions)
- [ ] `dotnet test` green; every new skip goes through `TestGate.Require*`
- [ ] Hot-path change (`Recording/`, `Sync/`, `Pools/`, `Memory/`)? Benchmark run in Release, `Allocated` still `-`
- [ ] `Generated/` untouched (regen via `/regen-bindings` if bindings changed)
