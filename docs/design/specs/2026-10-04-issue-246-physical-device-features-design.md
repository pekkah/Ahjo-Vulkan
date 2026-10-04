# Physical-device feature query — a generic, gated `TryGetFeatures<T>` on `PhysicalDevice`

- Issue: [#246](https://github.com/pekkah/Ahjo-Vulkan/issues/246) — "PhysicalDevice: query VkPhysicalDeviceFaultFeaturesEXT before device create (deviceFaultVendorBinary)"
- Consumer request: Ahjo Task 557 / item 379 residual (`docs/status/phase-3-completed.md` in the Ahjo repo)
- Plan: [`../plans/2026-10-04-issue-246-physical-device-features.md`](../plans/2026-10-04-issue-246-physical-device-features.md)
- **Mirrors:** the properties-chain query, [`2026-08-22-issue-201-properties-chain-query-design.md`](2026-08-22-issue-201-properties-chain-query-design.md) (`PhysicalDevice.TryGetProperties<T>`). This design is that one, on the other `vkGetPhysicalDevice*2` root.
- **Must land consistently with:** [#242 device fault](2026-10-02-issue-242-device-fault-design.md) (§F and the rejected "query `deviceFaultVendorBinary` via `vkGetPhysicalDeviceFeatures2`" bullet, reconciled below), [#53](https://github.com/pekkah/Ahjo-Vulkan/issues/53) (the duplicate-`sType` rule that shaped `ConfigureFeatures`).
- **Unblocks, does not resolve:** [#244](https://github.com/pekkah/Ahjo-Vulkan/issues/244) (masked KHR polling needs the `deviceFaultReportMasked` gate), [#172](https://github.com/pekkah/Ahjo-Vulkan/issues/172) / [#168](https://github.com/pekkah/Ahjo-Vulkan/issues/168) (`shaderDrawParameters` default needs a `vkGetPhysicalDeviceFeatures2` gate).
- **Test strategy constrained by:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (wrapper suite is Windows-only), [#158](https://github.com/pekkah/Ahjo-Vulkan/issues/158) (`TestGate` / `[gate:*]`).

## Problem

A consumer cannot learn, before `vkCreateDevice`, whether a GPU supports an
optional feature bit — any bit of any `VkPhysicalDeviceFeatures2` `pNext`
struct. The request that surfaced it:

**Ahjo cannot enable `deviceFaultVendorBinary`, so `DeviceFaultReport.VendorBinary`
is always empty in the engine.** Ahjo enables `VK_EXT_device_fault` exactly
when the adapter lists it (`ahjo/src/Ahjo.Rhi/RhiDevice.cs:246-250`) and pushes
`VkPhysicalDeviceFaultFeaturesEXT` with `deviceFault = 1` only
(`ahjo/src/Ahjo.Rhi/RhiDevice.cs:510-514`). Its own doc says why the second
bit is never set: *"`deviceFaultVendorBinary` is never enabled: the wrapper has
no feature query to check it first."* (`ahjo/src/Ahjo.Rhi/RhiAccelerationStructure.cs:29-30`).
Enabling a feature the device does not support fails `vkCreateDevice` with
`VK_ERROR_FEATURE_NOT_PRESENT`, and Ahjo's rule for a missing wrapper door is a
release request, not a raw Vulkan call on its side (issue #246, "Why").

The wrapper itself says the same in its public docs:
`VulkanExtensions.ExtDeviceFault` — *"the wrapper cannot check the feature —
Vulkan exposes no post-`vkCreateDevice` feature query"*
(`src/Ahjo.Vulkan/Rendering/VulkanExtensions.cs:154-156`); the KHR twin
repeats it (`:171-172`).

A consumer has no way round it. `PhysicalDevice.Handle` is `internal`
(`src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs:24`). The only per-adapter
feature read the wrapper performs is the picker's eager chain — base + 1.1–1.4,
nothing else (`src/Ahjo.Vulkan/Lifecycle/Instance.cs:282-292`) — exposed
through `PhysicalDeviceInfo.Features…Features14`
(`src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs:21-25`), a `ref struct` that
cannot escape the picker (`:11`).

**The gap is general, not device-fault-specific.** Two more open items need
the same primitive:

- #244 (deferred): masked KHR polling is gated on `deviceFaultReportMasked`, a
  bit of `VkPhysicalDeviceFaultFeaturesKHR` (issue #244, "Feature gate").
- #172 / #168: the wrapper will not enable `shaderDrawParameters` by default
  because it is optional even in 1.4 and *"that needs a
  `vkGetPhysicalDeviceFeatures2` gate first"* (#172 comment). The wrapper's own
  baseline sidesteps this by calling the 1.0 `vkGetPhysicalDeviceFeatures`
  (`src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs:584-592`), which cannot reach
  any `pNext` struct.

And the `ChainBuilder` doc example already points at an API that does not
exist: *"hand chain.Head to vkGetPhysicalDeviceFeatures2 (or
device.GetFeatures(...))"* (`src/Ahjo.Vulkan/Memory/ChainBuilder.cs:43`).

## Evidence

### 1. The properties side already solved this; the features side has nothing

`PhysicalDevice.TryGetProperties<T>` (#201's properties-chain spec) is the
exact shape needed, one chain root over:

| Member | Location |
|---|---|
| `TryGetProperties<T>(ReadOnlySpan<byte> utf8ExtensionName, out T)` | `Lifecycle/PhysicalDevice.cs:301-312` |
| `TryGetProperties<T>(Utf8Name extension, out T)` | `:324-336` |
| `TryGetProperties<T>(VulkanVersion minimumApiVersion, out T)` | `:362-373` |
| private `QueryChained<T>` — two-node stack chain, one `vkGetPhysicalDeviceProperties2` | `:388-399` |
| `SupportsExtension(ReadOnlySpan<byte>)` / `(Utf8Name)` — the extension gate | `:121-146`, `:154-157` |
| private `ReadApiVersion()` — the version gate | `:754-759` |

There is no features counterpart. A grep of `src/`, `samples/` and `tests/`
outside `Generated/` for `vkGetPhysicalDeviceFeatures2` finds **two** call
sites: the picker's eager chain (`src/Ahjo.Vulkan/Lifecycle/Instance.cs:292`)
and a test-only probe (`tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs:67`, raw
`Vk.*` on its own throwaway instance). Neither is reachable by a consumer for
an extension struct.

### 2. The generated chain metadata already covers every feature struct — no codegen

`tools/Ahjo.Vulkan.StructExtendsGen` emits `IChainable<VkPhysicalDeviceFeatures2>`
on **270** generated files (count of `src/Ahjo.Vulkan.Native/Generated/Chains/*.g.cs`
containing that interface; the properties root has 122). Both device-fault
structs are among them, and each carries its `sType` as a static abstract:

```csharp
// Generated/Chains/VkPhysicalDeviceFaultFeaturesEXT.Chain.g.cs:6,13
public unsafe partial struct VkPhysicalDeviceFaultFeaturesEXT
    : IChainable<VkPhysicalDeviceFeatures2>, IChainable<VkDeviceCreateInfo>
{ public static VkStructureType SType => …PHYSICAL_DEVICE_FAULT_FEATURES_EXT; }
// Generated/Chains/VkPhysicalDeviceFaultFeaturesKHR.Chain.g.cs — same shape, …_FAULT_FEATURES_KHR
```

`VkPhysicalDeviceFeatures2` is `IChainRoot`
(`Generated/Chains/VkPhysicalDeviceFeatures2.Root.g.cs`). So
`ChainBuilder.For<VkPhysicalDeviceFeatures2>(…).Push<T>()` writes `T.SType`
(`src/Ahjo.Vulkan/Memory/ChainBuilder.cs:97`) for any of the 270, and a struct
`vk.xml` does not allow on this root is a compile error. No `tools/*.rsp`
change, no regen, nothing under `Generated/` moves. AOT posture is the one
already recorded for `TryGetProperties<T>` (`docs/aot-notes.md:14`): static
abstract on an `unmanaged`-constrained generic, exact instantiations, no
reflection.

### 3. The read-chain hazard applies to features *more* directly than to properties

The repo's recorded failure — SwiftShader logging `"UNSUPPORTED:
curExtension->sType: 55"` and later SIGSEGV-ing when an unrecognized struct
sat in a read-back chain — happened on a **features** chain
(`src/Ahjo.Vulkan/Lifecycle/Instance.cs:272-281`). The properties spec gated
its query because of that observation; the features query has the stronger
reason to. The gate rule carries over unchanged: no node whose extension (or
core version) the GPU does not advertise ever reaches the driver.

### 4. Device-fault feature facts (pinned headers + this host, measured today)

**Registry** (`native/include/vulkan/vk.xml`, Vulkan-Headers 1.4.363,
`Directory.Build.props:18`):

| | `VkPhysicalDeviceFaultFeaturesEXT` | `VkPhysicalDeviceFaultFeaturesKHR` |
|---|---|---|
| Fields | `deviceFault`, `deviceFaultVendorBinary` (`Generated/VkPhysicalDeviceFaultFeaturesEXT.cs`) | `deviceFault`, `deviceFaultVendorBinary`, `deviceFaultReportMasked`, `deviceFaultDeviceLostOnMasked` (`Generated/VkPhysicalDeviceFaultFeaturesKHR.cs`) |
| `sType` | `1000341000` (`Generated/VkStructureType.cs:750`) | `1000573000` (`:1124`) |
| Extension | `VK_EXT_device_fault` (`vk.xml:27444`) | `VK_KHR_device_fault` (`vk.xml:30952`) |
| Required feature | `deviceFault` only (`vk.xml:27467`) | `deviceFault` only (`vk.xml:30973`) |

So `deviceFault` is guaranteed wherever the extension is advertised (which is
why Ahjo's extension-only check is correct for that bit,
`ahjo/src/Ahjo.Rhi/RhiAdapterInfo.cs` `HasDeviceFaultExtension` doc), and
**`deviceFaultVendorBinary` is optional on both** — the bit #246 is about.

**This host** — `vulkaninfo`, instance 1.4.363, run 2026-10-04:

| Adapter | EXT advertised | EXT `deviceFault` / `VendorBinary` | KHR advertised | KHR `deviceFault` / `VendorBinary` / `ReportMasked` / `DeviceLostOnMasked` |
|---|---|---|---|---|
| NVIDIA GeForce RTX 4070 Ti (api 1.4.351, driver 616.92) | rev 2 | true / **true** | rev 1 | true / true / false / false |
| AMD Radeon(TM) Graphics (api 1.4.315, driver 2.0.353) | rev 2 | true / **false** | no | — |

That matches the issue's 2026-10-02 measurement. The answer differs per
adapter, and the AMD iGPU is the case where blindly setting
`deviceFaultVendorBinary = 1` fails device creation.

**EXT and KHR are separate questions.** The two structs have distinct `sType`s
and each is pushed into `VkDeviceCreateInfo` separately (#242 spec §1, "Not
shared"). Enabling a bit in one struct is legal iff *that* struct reports it.
They describe the same capability (KHR is EXT's promotion,
`vk.xml:27444 promotedto=`) and agree on the 4070 Ti, but nothing in the
specification obliges a driver's two structs to agree, and the wrapper should
not assume it.

### 5. Reconciling with #242

#242's spec rejected *"Query `deviceFaultVendorBinary` support via
`vkGetPhysicalDeviceFeatures2`. That answers 'supported', not 'enabled'"*. That
rejection was about the **wrapper** tracking what was enabled so that
`TryGetDeviceFault` could reason about it; §F settled that the wrapper does not
track it, because the vendor-binary size protocol already reports 0 when the
feature is off. #246 asks the other question — **supported**, before create,
for the **consumer's** enable decision. Nothing in #242's decision moves:
`Device.DeviceFaultApi` / `IsDeviceFaultEnabled` stay extension-only
(`src/Ahjo.Vulkan/Lifecycle/Device.cs:170-195`), and the reader stays
feature-agnostic.

### 6. Consumer audit — the call sites under the new design

**Ahjo (the only consumer).** Today:

- adapter snapshot inside an all-`false` picker walk
  (`ahjo/src/Ahjo.Rhi/RhiDevice.cs:558-601`, `HasDeviceFaultExtension = info.SupportsExtension("VK_EXT_device_fault"u8)` at `:600`);
- the selected adapter is re-picked by index (`:206`), extensions appended
  (`:244-250`), and one of **four static method-group configurers** chosen by a
  `(rayQuery, deviceFault)` switch (`:256-262`); the two device-fault variants
  end in `PushDeviceFault` (`:510-514`).

Under this design Ahjo can answer the question in either place:

```csharp
// in Snapshot(in info), beside HasDeviceFaultExtension:
HasDeviceFaultVendorBinary =
    info.Device.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(VulkanExtensions.ExtDeviceFault, out var f)
    && f.deviceFaultVendorBinary != 0,
```

and then must get the bool into the configurer. **That is the one real cost on
the consumer side, and it is Ahjo's to choose:** a third bool doubles the
static configurer set (4 → 8), or one capturing lambda replaces the switch
(one closure allocation at device create — setup time, allowed by
`src/Ahjo.Vulkan/CLAUDE.md`, "Setup-time allocations … are fine"). The
`DeviceFeatureChainConfigurer` delegate does not receive the `PhysicalDevice`
(`src/Ahjo.Vulkan/Lifecycle/DeviceFeatureChainConfigurer.cs:65-70`), and this
design does not change that (see "Why not").

**In-repo.** Zero existing callers (the capability does not exist). The
nearest is `tests/Ahjo.Vulkan.Tests/DeviceFaultTests.cs:367-379`, whose
helper deliberately never requests `deviceFaultVendorBinary` *"a GPU without it
(the AMD iGPU on the dev host) would turn the whole tier into
`VK_ERROR_FEATURE_NOT_PRESENT` skips"* — the same gap, worked around in a test.

### 7. The picker's eager chain is a zero-allocation benchmarked path

`PickPhysicalDevice` is covered by `tests/Ahjo.Vulkan.Benchmarks/PhysicalDevicePickerBenchmark.cs:6-36`
(issue #7, "zero managed allocations after the ArrayPool is warm"). Its step
order is properties (2a) → features (2b) → memory (2c) → queues (2d) →
extensions (2e) (`src/Ahjo.Vulkan/Lifecycle/Instance.cs:265-328`): the
extension list that would gate an extension struct in 2b is not known until
2e. A lazy query on `PhysicalDevice` touches none of this.

### 8. Hot-path classification

Everything added lives in `Lifecycle/`, which is not on the
zero-per-frame-allocation list (`src/Ahjo.Vulkan/CLAUDE.md`). The precedent
methods are classified "not a hot path" by name in
`.claude/agents/bench-coverage-checker.md` (the `Lifecycle/PhysicalDevice.cs`
row) and in `docs/benchmarks.md` ("No row for the physical-device property
queries, deliberately"). The new query issues a native driver call and is
setup-time by nature.

## Decision

### A. `PhysicalDevice.TryGetFeatures<T>` — the general door, mirroring `TryGetProperties<T>`

```csharp
// src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs
public bool TryGetFeatures<T>(ReadOnlySpan<byte> utf8ExtensionName, out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>;

public bool TryGetFeatures<T>(Utf8Name extension, out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>;

public bool TryGetFeatures<T>(VulkanVersion minimumApiVersion, out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>;
```

Semantics are identical to `TryGetProperties<T>`, deliberately:

- **Gate fails ⇒** `false`, `features = default`, and **no native call with the
  node in the chain**. `false` means "not supported", never "supported but
  zero".
- **Gate passes ⇒** a two-node `ChainBuilder<VkPhysicalDeviceFeatures2>`
  (`Root()`, `Push<T>()`) on a `stackalloc` of
  `sizeof(VkPhysicalDeviceFeatures2) + Unsafe.SizeOf<T>() + 16`, one
  `vkGetPhysicalDeviceFeatures2`, copy the node out by value, `true`.
- Extension gate = `SupportsExtension` (existing). Version gate =
  `ReadApiVersion()` (existing). Same rule for struct-to-overload choice as
  the properties docs: extension-only structs take a name; core-promoted
  structs (`VkPhysicalDeviceVulkan11Features` is Vulkan **1.2**, …`Vulkan14Features`
  is 1.4) take a version.
- The returned node's `pNext` is null (tail node; `ChainBuilder.WriteHeader`,
  `Memory/ChainBuilder.cs:163-169`), so nothing points into the dead frame.

**Name:** `TryGetFeatures<T>`, not the issue's suggested `GetFeatures<T>()`.
`Try` + `out` is the repo idiom and the sibling's name; a non-`Try` form has
no in-band "unsupported" answer.

### B. EXT and KHR: two independent answers, one per struct you will push

No merged device-fault answer. The rule documented on both
`VulkanExtensions` members is **"query the struct you will push"**:

| Adapter exposes | `TryGetFeatures<…FaultFeaturesEXT>(ExtDeviceFault)` | `TryGetFeatures<…FaultFeaturesKHR>(KhrDeviceFault)` |
|---|---|---|
| EXT only (AMD iGPU) | `true`, its bits | `false`, `default`, KHR `sType` never sent |
| KHR only | `false`, `default` | `true`, its bits |
| both (RTX 4070 Ti) | `true`, its bits | `true`, its bits |
| neither | `false` | `false` |

This meets #246's "done when" for Ahjo (EXT only, per Ahjo's own rule at
`RhiDevice.cs:244-245`): enable `deviceFaultVendorBinary` iff the EXT query
returned `true` with the bit set. It also gives #244 its gate
(`…KHR.deviceFaultReportMasked`) without another release.

### C. No typed projection for device fault

#201's properties spec set the rule: *ship a typed projection when the raw
struct's interpretation is ambiguous; use the generic when it is not*. The
EXT struct is two flat bools, the KHR struct four; nothing is ambiguous. The
only thing a `DeviceFaultFeatures` projection could add is merging EXT and
KHR, which is exactly what B says not to do.

### D. Docs move with the code

The sentence "the wrapper cannot check the feature" on
`VulkanExtensions.ExtDeviceFault` / `KhrDeviceFault` becomes half-wrong and is
rewritten: pre-create support is readable through `TryGetFeatures<T>`;
post-create *enabled* state still is not (that half stays true and is still
why `Device.DeviceFaultApi` is extension-only). `ChainBuilder.cs:43`'s
dangling `device.GetFeatures(...)` points at the real method.
`DeviceFeatureChainConfigurer` gains one paragraph: to enable an optional bit
only where supported, query it before `CreateDevice` and capture the result,
because the configurer does not receive the `PhysicalDevice`.

### Breaking changes

None. Additive public surface (three overloads). The private
`QueryChained<T>` is renamed `QueryChainedProperties<T>` to sit beside the new
`QueryChainedFeatures<T>`; neither is public.

### Why not the alternatives

- **Extend the picker's eager features chain (`Instance.cs:282-292`) with
  `VkPhysicalDeviceFaultFeaturesEXT`, as the issue's "Where" suggests** —
  rejected: it is per-feature churn on `PhysicalDeviceInfo` (one new field per
  optional feature, the per-release request the issue wants to stop); it
  needs a per-adapter extension gate that the loop can only apply after
  reordering 2e before 2b; it widens every adapter's read chain on a
  zero-allocation benchmarked path (Evidence §7); and the answer dies with
  the `ref struct` at callback return.
- **A narrow `PhysicalDeviceInfo.DeviceFaultFeatures` / `SupportsDeviceFaultVendorBinary`
  bool pair** — rejected: solves #246 only. #244 and #172 would each need
  another release-gated field, and a single bool hides whether it is the EXT
  or the KHR struct's bit (B).
- **`TryGetFeatures<T>` on `PhysicalDeviceInfo` as well** — rejected for the
  same reason the properties spec rejected it: `info.Device` is the
  `PhysicalDevice` (`PhysicalDeviceInfo.cs:19`), so a picker already writes
  `info.Device.TryGetFeatures<…>(…)`. The duplicate would save two extension
  enumerations at setup time.
- **An ungated `GetFeatures<T>()`** — rejected: silent zeros on an unsupported
  struct, and it lets an unrecognized `sType` reach the driver — the
  `Instance.cs:272-281` failure, on the root where it was observed.
- **A multi-struct read (configure several nodes, one native call)** —
  rejected: needs a callback that receives the filled chain (the read is
  push → query → read back, and the properties spec rejected the configurer
  shape for that reason); every known consumer reads one struct.
- **Pass the `PhysicalDevice` (or the queried features) into
  `DeviceFeatureChainConfigurer`** — rejected: a breaking signature change to
  every configurer, to save the consumer one captured value. Out of scope for
  a query door; can be revisited on its own issue if consumers ask.
- **Have the wrapper auto-enable `deviceFaultVendorBinary` (or
  `shaderDrawParameters`) when supported** — rejected here: that is a defaults
  decision (#168's), not a query. This spec provides the gate those decisions
  were blocked on and changes no default.
- **Cache the features on `PhysicalDevice`** — rejected, consistent with
  `TryGetProperties`, `GetMemoryLimits` and `Device.TimestampPeriod`: a cache
  adds lifetime and thread-safety questions to a setup-time read.
- **Derive the extension name from `T` through generated metadata** —
  rejected, consistent with the properties spec: a new static abstract on 270
  generated files and a regen to remove one argument.

## Invariants honored

1. **UTF-8.** Extension names arrive as `"…"u8` / `Utf8Name`
   (`VulkanExtensions.ExtDeviceFault`); no `string` round-trip.
2. **AOT.** One generic method family, `unmanaged` + static-abstract
   constraint, same as `TryGetProperties<T>` (`docs/aot-notes.md:14`).
3. **Zero per-frame allocations.** Not a per-frame path (Evidence §8). The
   version-gated overload is stack-only; the name-gated overloads rent and
   return one pooled `VkExtensionProperties[]` via `SupportsExtension`.
4. **Generated code untouched.** Consumes existing `IChainable<VkPhysicalDeviceFeatures2>`.
5. **Warnings-as-errors.** No suppression needed.

## Test strategy

New `tests/Ahjo.Vulkan.Tests/PhysicalDeviceFeaturesTests.cs` for the mechanism;
the device-create proof of #246's "done when" goes in `DeviceFaultTests`.

- **`[gate:driver]`, unconditional.** `TryGetFeatures<VkPhysicalDeviceVulkan11Features>(V1_2)`
  (and 12/13, and 14 under its own gate) must byte-match the picker's eager
  read past the 16-byte header — a real oracle that the driver filled the node
  (and the one #172's gate would rely on). Extension-gate relationship on
  **every** adapter: result `==` `SupportsExtension`, all-zero on `false`, for
  both fault structs. Version-gate relationship for `Vulkan14Features`. Null
  and empty names.
- **`[gate:feature]`.** Every adapter advertising EXT reports
  `deviceFault == 1` (`vk.xml:27467`); KHR likewise (`:30973`). Per EXT
  adapter: create a device with `deviceFault = 1` and
  `deviceFaultVendorBinary = <queried>` — must succeed. On this host that runs
  both branches (4070 Ti: 1; AMD: 0).
- **`[gate:validation]`.** The fault-struct queries on every adapter and the
  vendor-binary device create, zero validation errors; KHR cases use the
  existing `MinKhrLayer` floor (1.4.363).
- **Forbidden:** enabling `deviceFaultVendorBinary` on an adapter that reports
  0 to "prove" the gate. It tests the driver, and a failing create pollutes
  validation captures.

CI (hosted Windows runner) has neither device-fault extension, so the
`[gate:feature]` cases skip there as the #242 tier does; the
`[gate:driver]` mechanism tests run. The PR quotes a local
`AHJO_VULKAN_TIER=validation` run on this host with both adapters.

## Uncertainty, stated

- **Whether the validation layer checks feature-query `pNext` structs against
  the physical device's extension list.** If the 1.4.363 layer flags a KHR
  struct queried on the AMD iGPU, the validation test proves the gate matters;
  if it does not, the gate is still justified by `Instance.cs:272-281`. Not
  verified ahead of the test run.
- **EXT/KHR agreement on one adapter** is measured on one driver (NVIDIA
  616.92) only. The design does not depend on it.
- **Ahjo's configurer reshaping** (8 statics vs. one closure) is the
  consumer's decision and is not verified here.
- **Stack sizing** inherits the properties path's arithmetic (`+16` for two
  8-byte pads, `ChainBuilder.cs:138-146`); `VkPhysicalDeviceFeatures2` is a different size from
  the properties root but compile-time-known, so the same formula
  holds.
