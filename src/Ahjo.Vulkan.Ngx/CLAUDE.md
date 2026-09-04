# Ahjo.Vulkan.Ngx — the managed DLSS/DLAA wrapper

Wraps `Ahjo.Vulkan.Ngx.Native` (the shim + bindings) in the vocabulary the rest
of `Ahjo.Vulkan` speaks. Design of record:
`docs/design/specs/2026-09-03-issue-218-ngx-wrapper-design.md`; Phase 1's
guarantees this layer consumes: `…-issue-216-ngx-native-design.md`, D9 and E11.

**Never hand-edit `src/Ahjo.Vulkan.Ngx.Native/Generated/`.** It is ClangSharp
output, overwritten wholesale on the next `/regen-bindings`. If something is
missing from the binding surface, the fix is in `tools/generate-ngx.rsp`.

## `Evaluate` is a per-frame hot path

`DlssFeature.Evaluate` carries the repo's zero-per-frame-allocation rule even
though it does not live under `Recording/`. Four properties hold it, and each
is load-bearing (spec D9):

1. The up-to-six `NVSDK_NGX_Resource_VK` values are **stack locals of
   `Evaluate` itself**. They must be — the parameter map stores raw pointers to
   them and NGX dereferences those inside `EvaluateFeature_C`, so splitting
   "prepare" from "evaluate" across two public calls would leave the live map
   holding dead stack addresses. That is why there is one method and no
   `Prepare`.
2. Parameter names are `static readonly Utf8Name` fields in
   `Internal/NgxParameterNames.cs`, each `Utf8Name.FromLiteral` over an
   RVA-backed `"…"u8` from `NgxApi`. No `fixed`, no pinning, no per-call
   pointer derivation.
3. The parameter map is allocated once per feature and reused every frame.
4. `ref CommandRecorder`, the established parameter shape — a `ref` *parameter*
   costs the caller no safe-context (#209/#213).

Anything that would allocate in `Evaluate` — a string, a LINQ query, a lambda,
a `new` array, a boxed enum — is a defect, not a style preference. The
benchmark that pins it is `DlssEvaluateBenchmarks`; it is host-gated and must
never be folded into `CommandRecorderBenchmarks`.

## Three invariants, three different answers

Recorded here because the obvious reading of each is wrong:

- **(a) The view / image / subresource-range triple.** Made *unrepresentable*.
  `NgxImage` is the only producer of `NVSDK_NGX_ImageViewInfo_VK`, and both its
  factories derive the range from the same `ImageViewDescription` that
  describes the view. `Wrap`'s doc states the one unenforceable half — nothing
  in Vulkan recovers a `VkImageView`'s range.
- **(b) `ReadWrite`.** Never crosses the public API; the wrapper sets it from
  the slot. Written as C# `true`/`false`, **not** `1`/`0` — Phase 1 measured the
  field as `bool` (#216 E11). What is checkable is the image's usage, and that
  is checked under `AhjoValidation.Enabled`. `ImageUsage.None` is the
  `Image.FromRaw` "unknown" state and is **skipped, not failed** — the obvious
  reading of that check rejects every swapchain image.
  **Do not extend that skip to extent or format.** Usage is unknown-and-harmless
  because NGX never reads it; `Width`, `Height` and `Format` are fields NGX
  *does* read out of `NVSDK_NGX_ImageViewInfo_VK`, so an `Image.FromRaw`
  handle's `0x0` / `VK_FORMAT_UNDEFINED` is a silent wrong answer and those
  **fail**. That asymmetry is deliberate and is the one thing in this file most
  likely to be "simplified" back into a bug.
  **The output image also needs `ImageUsage.TransferDst`**, which is not in
  NVIDIA's headers and not enforced: DLSS clears the output itself with
  `vkCmdClearColorImage` (`VUID-vkCmdClearColorImage-image-00002`, observed on
  driver 610.47). The `RequireUsage` message says so; the check does not fail on
  it, because one driver version cannot establish "DLSS always clears". If a
  second one ever does, this is where enforcement would go.
- **(c) Image layout.** Cannot be enforced, and is documented as such rather
  than papered over. `Ahjo.Vulkan` does not track layout by decision (#17), so
  there is no value to compare and no barrier to emit. Validation layers are
  the oracle; the wrapper's job is to make the resulting `NVSDK_NGX_Result`
  legible.

## Other things that bite

- **NGX is not thread safe** (DLSS Programming Guide §5.2.4) and the capability
  parameter map is mutable shared state that `GetOptimalSettings` *writes*. The
  `AhjoValidation`-gated re-entrancy guard on `NgxContext` is the enforcement.
- **DLSS's VRAM is invisible to VMA.** History and scratch surfaces are
  allocated inside the driver. `Allocator.GetHeapBudgets` only sees them when
  the allocator was created with `AllocatorDescription.EnableMemoryBudget`
  *and* the device enabled `VulkanExtensions.ExtMemoryBudget`.
- **Do not add `NVSDK_NGX_EParameter_*` names.** They were excluded from the
  bindings on purpose (#216 D7/E7): their values embed raw control bytes.
  `DlssStats` ships one field for exactly this reason — see the spec's OPEN-3.
- **No `InternalsVisibleTo` back to `Ahjo.Vulkan`.** `NgxValidation` re-expresses
  `AhjoValidation.Fail` in three lines against the public surface; two
  independently versioned published packages do not get to see each other's
  internals to save that.
- **Setup-time allocation is fine.** `NgxDescription.Validate`, the UTF-8
  block, `NgxExtensionSet` — all cold paths. Only `Evaluate` is hot.
- **Turning NGX logging on forfeits the zero-allocation guarantee, by design.**
  `NgxContext.LogThunk` builds an interpolated string with two boxed enum
  arguments, and NGX may call it from inside `EvaluateFeature_C` — i.e. per
  frame. That is why `NgxLoggingLevel.Off` is the default and why no callback is
  installed at all at that level: with logging off there is no path from NGX
  back into managed code, so `Allocated: -` is never at risk in the shape anyone
  ships. Do not "fix" the thunk by making it allocation-free; a diagnostic that
  costs nothing to leave on is a diagnostic nobody turns off.
