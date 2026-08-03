# A global `ConstantBuffer<T>` with `[[vk::binding(n, space>0)]]` is reported in the wrong descriptor set

**Issue:** #180
**Paired plan:** `../plans/2026-08-03-issue-180-constantbuffer-space.md`
**Lands consistently with:** `2026-08-02-issue-175-177-slang-reflection-completeness-design.md`
(this repairs quantities that spec's binding-range join consumes; it does not
revise the join).
**Measured on:** Slang `v2026.14.1` (`Directory.Build.props:46`), win-x64, across
three measurement passes; each evidence section states its own count. Every number
in this document is a measurement, not a header reading.
**Revised:** 2026-08-03 (twice).
**R1** — after `vulkan-validation-reviewer` found a shape the first pass did not
cover: D1 was implemented and reviewed before the revision and is **not**
re-opened, D2 was **dropped by maintainer decision**, D3 is new work in the same
PR. See §Status.
**R2** — after Group D was implemented and its live tests disagreed with §E9/§E10.
The rows were re-measured and corrected; §E11 is new. See §Correction record.

---

## Status of each decision

| | Decision | State |
|---|---|---|
| **D1** | Per-scope descriptor space correction (`CollectSpaceCorrections`) | **Implemented, reviewed, green** (108/108). Not re-opened by this revision. |
| **D2** | Duplicate-slot guard in `Group` | **Dropped by maintainer decision.** Do not reintroduce. See §D2. |
| **D3** | Unwrap the constant-buffer-wrapped global scope | **New work**, this PR. |

---

## Problem

### P1 — the filed defect: the space of an explicitly-bound global constant buffer

`SlangReflection` reports a global-scope `ConstantBuffer<T>` that carries an
explicit `[[vk::binding(n, space)]]` with `space > 0` in descriptor set **0**,
while the SPIR-V Slang emits for the same program decorates it
`DescriptorSet = space`. Nothing throws. A caller that builds descriptor set
layouts from reflection — the whole point of the surface
(`SlangReflection.cs:151-179`) — binds the buffer into a set the shader never
uses, and the mismatch surfaces as a validation error or garbage data at draw
time.

The issue body's module is the whole repro; no unbounded array (#176) and no
interface-typed parameter (#177) are involved.

Two consequences follow from the wrong set number, and both were measured
rather than predicted:

1. **A duplicate `(set, slot)` reaches the caller.** In the issue's own module,
   `SlangReflection` reports *two* bindings at set 0 slot 0 — the texture and
   the constant buffer. `Device.CreateDescriptorSetLayout` checks only that the
   span is non-empty (`src/Ahjo.Vulkan/Lifecycle/Device.cs:192-193`) and hands
   both to `vkCreateDescriptorSetLayout`, which requires binding numbers within
   one set to be unique. Nothing in this repo notices.
2. **Names are silently transplanted.** The binding-range join
   (`SlangReflection.cs:646-689` pre-D1) keys facts into a
   `Dictionary<(uint Set, uint Slot), BindingFacts>`, so the mis-keyed constant
   buffer *overwrites* whatever legitimately owns that key. In the issue's module
   the texture at set 0 slot 0 is reported with the name `gXform`. In the mixed
   shape (row D below) the constant buffer is reported with the name `gSampler`.
   This is exactly the collision the `PUSH_CONSTANT` skip in that pass exists to
   prevent (and `src/Ahjo.Vulkan.Slang/CLAUDE.md` rule 6).

The defect was already recorded in-repo, as a workaround note on the #176
fixture (`ShaderFixtures.cs:340-349`), which used `ParameterBlock<Xform>` instead
of `[[vk::binding(0, 1)]] ConstantBuffer<Xform>` because the latter was
misreported.

**P1 is fixed by D1, which is implemented and green.**

### P2 — the shape the first pass missed: a constant-buffer-wrapped global scope

Found by `vulkan-validation-reviewer` after D1 landed. Adding one loose global
uniform to the module changes the *structure* of what Slang hands back, and D1's
correction stops applying:

```hlsl
struct Xform { float4x4 mvp; };

float4 gTint;                                   // loose global uniform — the trigger

[[vk::binding(0, 0)]] Texture2D<float4>     gAlbedo;
[[vk::binding(0, 1)]] ConstantBuffer<Xform> gXform;
```

SPIR-V decorates `gAlbedo (0,0)`, `globalParams (0,1)`, `gXform (1,0)`.
Reflection reports `DescriptorSetCount = 1`, `SetLayoutSlotCount = 1`, and set 0
holding **three bindings all at `Slot = 0`** (`CONSTANT_BUFFER`, `TEXTURE`,
`CONSTANT_BUFFER`), **all with an empty `Name`**, and no set 1 at all. That span
goes straight to `Device.CreateDescriptorSetLayout`
(`src/Ahjo.Vulkan/Lifecycle/Device.cs:190-214`), so three
`VkDescriptorSetLayoutBinding.binding = 0` entries reach
`vkCreateDescriptorSetLayout` —
`VUID-VkDescriptorSetLayoutCreateInfo-binding-00279`.

**P2 is broader than P1 and is not about `[[vk::binding]]` at all.** But the
blast radius differs by shape, and the four cases were measured individually
(§E12) rather than generalized from one:

| Module shape | what the pre-D3 walk produced |
|---|---|
| loose data **only** | `(0,0) CONSTANT_BUFFER` — slot correct, **name empty** |
| loose data + **auto-bound** resources | slots `(0,0)`, `(0,1)`, `(0,2)` all **correct**; **every name empty** |
| loose data + **explicitly-bound** resources (P2's module) | `(0,0)`, `(0,0)`, `(0,0)` — **three bindings at one slot**, no set 1, every name empty |
| loose data + a **`ParameterBlock`** | `(0,0)`, `(0,1)` — the block's `(1,0)` binding **vanishes entirely**, every name empty |

The one defect common to **all four** is that every binding comes back
**unnamed**: the wrapper exposes a single binding range whose leaf variable is
`null`, so `CollectBindingRangeFacts` produces no facts at all and `Group` has
nothing to stamp. The duplicate slots, the missing set and the vanished block are
shape-dependent on top of that.

**P2 is pre-existing and neither caused nor masked by D1.** D1's
`CollectSpaceCorrections` returns `null` for this scope at its kind guard and
`SpaceOf` falls back to the record's own space offset, which is byte-for-byte the
pre-D1 code path.

---

## Evidence

### Method

Throwaway probes compiled each module through `Ahjo.Vulkan.Slang` and dumped, for
every binding, the raw return of every Slang call the walk and the join consume,
alongside the `OpDecorate DescriptorSet` / `OpDecorate Binding` pairs read out of
the emitted module with the suite's existing `SpirvDecorations` reader. The second
pass additionally ran a **standalone simulation** of the proposed walk (§E10). The
probes lived only in the working tree and are deleted; nothing in `src/` or
`tests/` was changed by them.

### E1 — Where P1 lives: **upstream, in Slang**

`CollectBindingRangeFacts` was **not** mis-computing the set. It resolves a
binding range to `(s, r)` and then reads the *same*
`getDescriptorSetSpaceOffset(scope, s)` that step 1's walk reads. Both read the
same record, and the record itself is wrong: Slang places the constant buffer's
descriptor range inside the descriptor-set record for space **0** while keeping
its binding index correct.

The decisive observation is row D. There, a descriptor-set record for space 1
*already exists* (a texture is declared at `[[vk::binding(0, 1)]]`, and Slang
reports `set[1] spaceOffset = 1` for it) — and the constant buffer at
`[[vk::binding(1, 1)]]` is *still* emitted as a range of `set[0]`, whose
`spaceOffset` is 0. No consumer-side arithmetic can recover a space from a record
that does not carry it.

### E2 — The measured table for P1: raw Slang values vs. SPIR-V decorations

Six defective shapes. `record key` is what the walk and the join both computed
before D1 (`getDescriptorSetSpaceOffset(s)`, `…RangeIndexOffset(s, r)`).
`field (space, offset)` is `spReflectionVariableLayout_GetSpace` / `GetOffset`
under `SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT`, read from the declaring
field's variable layout.

| # | Declaration under test | SPIR-V `(set, binding)` | record key | field (space, offset) | reported before D1 |
|---|---|---|---|---|---|
| A | `[[vk::binding(0,1)]] ConstantBuffer<Xform> gXform` beside a space-0 texture + sampler (**the issue's module**) | `gXform (1,0)`, `gAlbedo (0,0)`, `gSampler (0,1)` | `gXform → (0,0)` | `(1, 0)` | `(0,0) TEXTURE 'gXform'`, `(0,0) CONSTANT_BUFFER 'gXform'`, `(0,1) SAMPLER 'gSampler'`; **no set 1** |
| B | `[[vk::binding(0,1)]] ConstantBuffer<Xform>` alone in the module | `gXform (1,0)` | `(0,0)` | `(1, 0)` | `(0,0) CONSTANT_BUFFER 'gXform'` — wrong set, no collision |
| C | `[[vk::binding(5,1)]] ConstantBuffer<Xform>` beside space-0 texture + sampler | `gXform (1,5)` | `(0,5)` | `(1, 5)` | `(0,5) CONSTANT_BUFFER 'gXform'` — binding index right, set wrong |
| D | `[[vk::binding(0,1)]] Texture2D gOther` **and** `[[vk::binding(1,1)]] ConstantBuffer<Xform> gXform`, plus a space-0 texture + sampler | `gAlbedo (0,0)`, `gSampler (0,1)`, `gOther (1,0)`, `gXform (1,1)` | `gOther → (1,0)` ✔, `gXform → (0,1)` ✘ | `gOther (1,0)`, `gXform (1,1)` | `(0,0) TEXTURE 'gAlbedo'`, `(0,1) CONSTANT_BUFFER 'gSampler'`, `(0,1) SAMPLER 'gSampler'`, `(1,0) TEXTURE 'gOther'` |
| E | `[[vk::binding(0,1)]] ConstantBuffer<A> gA` **and** `[[vk::binding(0,2)]] ConstantBuffer<B> gB` | `gA (1,0)`, `gB (2,0)` | both `→ (0,0)` | `gA (1,0)`, `gB (2,0)` | `(0,0) CONSTANT_BUFFER 'gB'` **twice**; `gA` unrecoverable |
| F | `ConstantBuffer<Xform> cb` inside a plain struct global placed at `[[vk::binding(0,1)]]` | `gAlbedo (0,0)`, `gBundle.tex (1,0)`, `gBundle.cb (1,1)` | `tex → (1,0)` ✔, `cb → (0,1)` ✘ | field `gBundle` reports space `1` | `(0,0) TEXTURE 'gAlbedo'`, `(0,1) CONSTANT_BUFFER 'cb'`, `(1,0) TEXTURE 'tex'` |

Three facts this table settles:

- **The collision the issue warns about is real, silent, and worse than a
  duplicate.** Row A produces two bindings at `(0,0)` *and* renames the texture.
  Row D renames a constant buffer after a sampler. Row E loses a whole buffer.
  No exception is thrown anywhere.
- **`TryGetBufferLayout` moves with the key.** Buffer layouts are keyed from the
  same facts dictionary (`SlangReflection.cs:731-763` pre-D1), so in row A
  `TryGetBufferLayout(0, 0, …)` returned `Xform`'s member layout attached to the
  *texture*'s slot.
- **The binding index is never wrong.** Only the space is lost. That bounded the
  repair to one quantity.

### E3 — The `ParameterBlock<T>` contrast, measured not assumed

Replacing row A's constant buffer with `ParameterBlock<Xform> gXform` gives a
global-scope binding range of type `SLANG_BINDING_TYPE_PARAMETER_BLOCK` with
`getBindingRangeDescriptorSetIndex = -1`; it is skipped by the join and handled
by step 3, which reads `SUB_ELEMENT_REGISTER_SPACE = 1` off the sub-object
range's variable layout (`SlangReflection.cs:583-584`) and recurses at
`absoluteSet + 1`. A `ParameterBlock` never travels through the broken
descriptor-set record at all, which is why it is correct.

### E4 — The correct space *is* available from Slang, on a different accessor

For every field of a struct-shaped scope,
`spReflectionVariableLayout_GetSpace(field, SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT)`
returned the space the SPIR-V decorates, in **all 19** shapes of the first pass.
The thirteen controls, all of which reflection already got right and where field
space equals record space offset: `ReflectionGlobals`, `ReflectionSparseSets`
(`gSamp` reports space **2**, offset **7**), `ReflectionTwoBlocks`,
`ReflectionNestedBlock`, `ReflectionOnlyBlocks`, `ReflectionBlockOrdinaryData`,
`ReflectionBindlessArrays`, `ReflectionTextureArray`,
`ReflectionComputeStorageImage`, `ReflectionMaterialBlock`, plus three
probe-only controls (a space-1 `Texture2D`, a space-1
`StructuredBuffer<float4>`, and a `[[vk::binding(2,0)]]` constant buffer).

The predicate *"the declaring field's `DESCRIPTOR_TABLE_SLOT` space differs from
the space offset of the descriptor-set record the range landed in"* was **true in
exactly the six defective shapes and false in all thirteen correct ones**.

### E5 — How a field maps to its binding ranges

`spReflectionTypeLayout_getFieldBindingRangeOffset(scope, f)` is monotone
non-decreasing, and a field that owns no binding ranges reports the offset of the
*next* one. Measured in `ReflectionMaterialBlock`'s block scope
(`getBindingRangeCount = 2`): `Tint`, `Params`, `Transform` (all `UNIFORM`) and
`BaseColorMap` all report `0`; `Sampler` reports `1`. So the span
`[offset(f), offset(f+1))`, with `getBindingRangeCount` as the bound for the last
field, yields empty spans for the uniform members and exactly one range each for
the resources. Row F confirms the span rule on a field that owns *several*
ranges.

### E6 — Two safety constraints on any field walk

- **`GetFieldCount` must not be called unless `getKind == SLANG_TYPE_KIND_STRUCT`.**
  The element scope of a conformance-linked `ParameterBlock<ISurface>` is
  `SLANG_TYPE_KIND_INTERFACE`, reports `getDescriptorSetCount = 0` and a single
  `SLANG_BINDING_TYPE_EXISTENTIAL_VALUE` binding range. This is the same call
  family that takes the process down with `0xC0000005` on that range
  (`SlangReflection.cs:691-717`, issue **#181**, still open and unfixed), and
  `BuildBufferLayout` already guards this way (`SlangReflection.cs:1046`).
- **Inside a `ParameterBlock` element scope the correction is inert.** In every
  block scope measured, both the record's `spaceOffset` and every field's
  `GetSpace(DESCRIPTOR_TABLE_SLOT)` are `0`; the space a block lives in is
  carried by step 3's accumulation.

---

### E7 (new) — The measured kind of the global scope, and what triggers it

`spReflection_getGlobalParamsTypeLayout(layout)` does **not** always return a
struct. Measured directly, `spReflectionTypeLayout_getKind` on it:

| Module | kind of the global params type layout | `GetElementTypeLayout` |
|---|---|---|
| Any module with **no** loose global uniform data — all 10 existing fixtures plus P1's four | `SLANG_TYPE_KIND_STRUCT` | `null` |
| `float4 gTint;` + explicit-space `ConstantBuffer` (P2's module) | **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** | a `SLANG_TYPE_KIND_STRUCT` scope, `GetSize(UNIFORM) = 16` |
| `float4 gTint;` + texture + sampler, no explicit spaces | **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** | struct, `UNIFORM = 16` |
| `float4 gTint;` + space-1 texture | **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** | struct, `UNIFORM = 16` |
| `float4 gTint;` + `ParameterBlock` | **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** | struct, `UNIFORM = 16` |
| `float4 gTint; float gScale;` and nothing else | **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** | struct, `UNIFORM = 32` |

**The reviewer's inference is confirmed and made precise: the kind is
`SLANG_TYPE_KIND_CONSTANT_BUFFER`, not merely "not `STRUCT`".** Deleting the
loose uniform, or moving it into an explicit `ConstantBuffer<Tint>`, returns the
scope to `SLANG_TYPE_KIND_STRUCT` — which is why P1's fixtures were unaffected.
D1's kind guard therefore short-circuits and nothing is repaired, exactly as
inferred.

`SLANG_TYPE_KIND_INTERFACE` was **never** observed as the global scope's kind,
including for the conformance-linked `ParameterBlock<ISurface>` program: there
the global scope is `STRUCT` and the interface scope is reached only as a *block
element* through step 3. So a decision keyed on `== CONSTANT_BUFFER` cannot admit
an interface scope.

### E8 (new) — The two index spaces, measured: they cannot be bridged

This is the reviewer's stated obstacle. It is real. For P2's module:

| | wrapper scope (`CONSTANT_BUFFER`) | element scope (`STRUCT`) |
|---|---|---|
| `getBindingRangeCount` | **1** — a single `CONSTANT_BUFFER` range whose **leaf variable is `null`** (the implicit buffer itself) | **2** — `br[0]` `TEXTURE 'gAlbedo'`, `br[1]` `CONSTANT_BUFFER 'gXform'` |
| `getDescriptorSetCount` | 1 | 1 |
| `set[0]` ranges | **3**: `CONSTANT_BUFFER idx=0`, `TEXTURE idx=0`, `CONSTANT_BUFFER idx=0` | **2**: `TEXTURE idx=0`, `CONSTANT_BUFFER idx=0` |
| `GetFieldCount` | **illegal** — kind is not `STRUCT` | 3: `gTint` (UNIFORM), `gAlbedo` (DTS, space 0), `gXform` (DTS, **space 1**) |

Three measured conclusions:

1. **There is no constant offset between the two `(s, r)` spaces.** In P2's module
   the wrapper's `set[0]` is the element's ranges plus the implicit buffer
   *prepended*, so the relation for that record is `wrapperRange = elementRange +
   1`. But in the `float4 gTint;` + space-1 texture module the wrapper has **two**
   descriptor-set records, and the relation is `+1` for `set[0]` and **`+0`** for
   `set[1]` — the implicit buffer only shifts the record it actually lands in. A
   single offset does not exist.
2. **`getFieldBindingRangeOffset` does not bridge them.** The fields live on the
   *element* scope and their `brOffset` values index the *element's* binding
   ranges (`gAlbedo → 0`, `gXform → 1`). They are correct in element space and
   meaningless in wrapper space. On the wrapper the call cannot legally be made at
   all (E6).
3. **The wrapper's own descriptor-set record is not merely shifted, it is wrong.**
   It reports the implicit buffer's index offset as `0` in every measured shape,
   while SPIR-V decorates `globalParams` at binding `1` or `2` in these shapes.
   (It reports `0` regardless of the true slot; where the true slot happens to
   *be* 0 — E11 — the wrapper looks right by coincidence.)

So a design that tries to *translate* element-scope keys into wrapper-scope keys
has nothing to translate with. **The resolution is not to bridge the two spaces
but to stop using the wrapper's descriptor-set records at all** — see D3.

### E9 (new) — Slang *does* report the implicit buffer's own `(set, binding)`

The one number a walk of the element scope cannot produce is where the implicit
`globalParams` buffer itself goes. Slang reports it on
`spReflection_getGlobalParamsVarLayout(layout)`, under
`SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT`:

**The module descriptions below are exact.** Two rows of this table were
originally written with the `[[vk::binding]]` attributes omitted from the
description, which made them read as claims about different modules; §Correction
record has the detail. Every row was re-measured for R2.

| Module | `GetSpace` / `GetOffset` on the global params var layout | SPIR-V `globalParams` |
|---|---|---|
| P2's module: `gTint` + `[[vk::binding(0,0)]]` tex + `[[vk::binding(0,1)]]` CB | `space 0`, `off 1` | `(0,1)` ✔ |
| `gTint` + `[[vk::binding(0,0)]]` tex + `[[vk::binding(1,0)]]` sampler | `space 0`, `off 2` | `(0,2)` ✔ |
| `gTint` + `[[vk::binding(0,0)]]` tex + `[[vk::binding(0,1)]]` tex | `space 0`, `off 1` | `(0,1)` ✔ |
| `gTint` + `[[vk::binding(0,0)]]` tex + `ParameterBlock` | `space 0`, `off 1` | `(0,1)` ✔ |
| `gTint` + `gScale`, nothing else | `space 0`, `off 0` | `(0,0)` ✔ |
| `gTint` + `[[vk::binding(0,0)]]` tex + space-2 CB + push constant | `space 0`, `off 1` | `(0,1)` ✔ |
| `gTint` + `[[vk::binding(0,0)]]` tex + `[[vk::binding(1,0)]]` sampler + block + space-3 CB | `space 0`, `off 2` | `(0,2)` ✔ |
| **R2:** `gTint` + tex + sampler, **no `[[vk::binding]]` at all** | `space 0`, `off 0` | `(0,0)` ✔ |
| **R2:** same, `gTint` declared **last** | `space 0`, `off 0` | `(0,0)` ✔ |
| **R2:** `gTint` + `[[vk::binding(1,0)]]` tex (slot 0 free) | `space 0`, `off 0` | `(0,0)` ✔ |
| **R2:** `gTint` + `[[vk::binding(5,0)]]` tex (slot 0 free, gap) | `space 0`, `off 0` | `(0,0)` ✔ |
| **R2:** `gTint` + `[[vk::binding(0,0)]]` tex | `space 0`, `off 1` | `(0,1)` ✔ |
| **R2:** `gTint` + CB explicitly in space 1, nothing in space 0 | `space 0`, `off 0` | `(0,0)` ✔ |
| **R2:** the shipped `ReflectionLooseGlobalsWithParameterBlock` (no explicit bindings) | `space 0`, `off 0` | `(0,0)` ✔ |

**The accessor claim survives re-measurement and strengthens: 14 of 14, up from
7 of 7.** Not one row was misread — the accessor matched `OpDecorate` in every
shape of every pass. What was wrong in R1 was two module *descriptions*, not two
numbers. No derivation, no "one past the highest binding in space 0" guess: Slang
states it, on the same `GetSpace`/`GetOffset` accessor family D1 already uses.

Two accessors that do **not** work, measured rather than assumed:
`spReflection_getGlobalConstantBufferBinding` returned `0` in all seven R1 shapes
(wrong in six), and `getContainerVarLayout` / `GetElementVarLayout` on the wrapper
returned `0` for every category swept.

Also measured, and needed by the design:

- `spReflectionVariableLayout_GetVariable(globalParamsVarLayout)` is **`null`**,
  and `spReflectionType_GetName` on both the wrapper and the element type is
  `null`. **Slang gives this binding no name.** SPIR-V's `OpName` for it is
  `globalParams` in all fourteen shapes.
- `spReflectionEntryPoint_hasDefaultConstantBuffer` is `0` for every entry point
  in every shape measured, so no *second* implicit buffer is in play.
- The element scope's fields carry the loose data and nothing else that matters:
  `BuildBufferLayout` on it yields `gTint` at offset 0 size 16 (and `gScale` at
  16 size 4 in the loose-only module), while `gAlbedo` / `gXform` / `gMat` all
  report `GetSize(UNIFORM) = 0` and are already dropped by the existing zero-size
  rule at `SlangReflection.cs:1101-1105`.

### E10 (new) — The proposed walk, simulated against SPIR-V

A standalone simulation of D3 — unwrap when the kind is `CONSTANT_BUFFER`,
synthesize the implicit buffer at the `(space, offset)` from E9, then run the
existing step 0a + step 1 + step 2 + step 3 over the *element* scope — was run
over ten modules and compared to `OpDecorate`. **Every `(set, binding)` the
emitted SPIR-V decorates was produced, in all ten.** Every module in this table
carries `[[vk::binding(0,0)]]` on its texture unless the row says otherwise; that
detail is load-bearing (§E11) and was omitted from two rows in R1.

| Module | SPIR-V | simulated walk |
|---|---|---|
| `gTint` + `[[vk::binding(0,0)]]` tex + space-1 CB (P2) | `(0,0) gAlbedo`, `(0,1) globalParams`, `(1,0) gXform` | `(0,0) TEXTURE`, `(0,1) implicit`, `(1,0) CONSTANT_BUFFER` |
| `gTint` + `[[vk::binding(0,0)]]` tex + `[[vk::binding(1,0)]]` sampler | `(0,0) gAlbedo`, `(0,1) gSampler`, `(0,2) globalParams` | `(0,0) TEXTURE`, `(0,1) SAMPLER`, `(0,2) implicit` |
| `gTint` + `[[vk::binding(0,0)]]` tex + space-1 tex | `(0,0)`, `(0,1) globalParams`, `(1,0)` | identical |
| `gTint` + `[[vk::binding(0,0)]]` tex + `ParameterBlock` | `(0,0) gAlbedo`, `(0,1) globalParams`, `(1,0) gXform` | `(1,0)` via step 2's block buffer |
| `gTint` + `[[vk::binding(0,0)]]` tex + space-**2** CB + push constant | `(0,0)`, `(0,1) globalParams`, `(2,0) gXform` | identical; push constant classified as a range, not a descriptor |
| `gTint` + `gScale` only | `(0,0) globalParams` | `(0,0) implicit` |
| `gTint` + `[[vk::binding(0,0)]]` tex + `[[vk::binding(1,0)]]` sampler + block + space-**3** CB | `(0,0)`, `(0,1)`, `(0,2) globalParams`, `(1,0) gMat`, `(3,0) gXform` | identical — D1's correction and D3's unwrap compose |
| `ReflectionGlobals` (control, `STRUCT`) | `(0,0..3)` | unchanged, no unwrap taken |
| `ReflectionSparseSets` (control) | `(0,3)`, `(2,7)` | unchanged, no unwrap taken |
| `ReflectionMaterialBlock` (control) | `(0,0..2)` | unchanged, no unwrap taken |

The last three are the guarantee that D3 is inert wherever the global scope is
already a struct: the unwrap is not taken and the walk is byte-for-byte today's.

**None of the shipped fixtures is in this table.** The fixtures written for the
plan's step 11 dropped the `[[vk::binding]]` attributes these modules carry, which
moved the implicit buffer to a different slot — §E11 is the rule that explains
why, and §E12 is the shipped fixtures measured directly. Treat §E10 as evidence
that the *walk* is correct, and §E12 as evidence about the fixtures.

### E11 (R2) — **The placement rule for the implicit buffer, measured**

The R1 rows that read wrong all read wrong for one reason: the implicit buffer's
slot depends on what `[[vk::binding]]` claims, and two module descriptions omitted
their `[[vk::binding]]` attributes. Rather than leave a table of pairings, here is
the rule itself, swept deliberately:

| Explicit `[[vk::binding]]` slots claimed in space 0 | `globalParams` slot | other bindings |
|---|---|---|
| none (`gTint` + auto-bound tex + sampler) | **0** | tex `(0,1)`, sampler `(0,2)` — pushed up |
| none, `gTint` declared **last** in the file | **0** | identical — declaration order is irrelevant |
| none (only a CB explicitly in **space 1**) | **0** | CB `(1,0)` — a claim in another space does not count |
| none (nothing else at all) | **0** | — |
| `{1}` | **0** | tex stays at `(0,1)` |
| `{5}` | **0** | tex stays at `(0,5)` — **not** slot 6 |
| `{0}` | **1** | tex stays at `(0,0)` |
| `{0}` + a CB in space 1 | **1** | CB `(1,0)` |
| `{0, 1}` | **2** | tex `(0,0)`, sampler `(0,1)` |

**The measured rule:**

> The implicit global constant buffer is allocated **first**, at the **lowest
> binding in space 0 that no explicit `[[vk::binding]]` has claimed**. Every
> automatically-assigned resource is then allocated after it.

Both halves are needed and each has a decisive row. *Allocated first* is what the
no-explicit rows show — the texture and sampler are pushed to 1 and 2 rather than
keeping 0 and 1. *Lowest unclaimed* is what the `{5}` row shows — `globalParams`
takes 0, not 6, so the rule is not "one past the highest". The `{0,1}` row rules
out "always 0", and the space-1 row shows the search is per-space.

Two things this rule is **not**:

- **It is not declaration order.** Moving `float4 gTint;` below both resources
  changes nothing. (Independently reproduced; the implementer measured the same.)
- **It is not something the wrapper depends on.** D3 *reads* the slot from
  `getGlobalParamsVarLayout` (§E9); it never predicts it. This rule is recorded
  because it explains why two nearly-identical fixtures report different slots and
  because a reader of the fixtures will otherwise think one of them is wrong — not
  because any code branches on it. **Nothing should start deriving the slot from
  it.**

### E12 (R2) — The shipped fixtures, measured directly

The four fixtures Group D actually shipped, measured against `OpDecorate` after
implementation, with what the pre-D3 walk produced for each (read from the
wrapper's own descriptor-set records, which is exactly what step 1 consumed):

| Fixture | explicit bindings | SPIR-V | pre-D3 walk | shipped reflection |
|---|---|---|---|---|
| `ReflectionLooseGlobalsWithExplicitSpace` | `(0,0)` tex, `(0,1)` CB | `(0,0) gAlbedo`, `(0,1) globalParams`, `(1,0) gXform` | `(0,0) CB`, `(0,0) TEX`, `(0,0) CB` — **three at one slot**, all unnamed | matches SPIR-V ✔ |
| `ReflectionLooseGlobalsOnly` | none | `(0,0) globalParams` | `(0,0) CB`, unnamed | matches ✔ |
| `ReflectionLooseGlobalsNoExplicitBinding` | **none** | `(0,0) globalParams`, `(0,1) gAlbedo`, `(0,2) gSampler` | `(0,0) CB`, `(0,1) TEX`, `(0,2) SAMPLER` — **slots already correct**, all unnamed | matches ✔ |
| `ReflectionLooseGlobalsWithParameterBlock` | none | `(0,0) globalParams`, `(0,1) gAlbedo`, `(1,0) gXform` | `(0,0) CB`, `(0,1) TEX` — **the block's `(1,0)` is absent** | matches ✔ |

Two corrections to R1's account of P2 fall out of this:

- **For the no-explicit-binding fixture the slots were never wrong.** The pre-D3
  walk produced `(0,0)`, `(0,1)`, `(0,2)` — the same triple SPIR-V decorates. What
  was broken there was **only the names**, all empty. R1 asserted a duplicate at
  `(0,0)`; that is what the *explicitly-bound* module does, not this one.
- **The `ParameterBlock` fixture loses a binding entirely.** The wrapper's
  sub-object range list does not contain the block — the block is a sub-object of
  the *element* — so step 3 never recursed and `gXform (1,0)` simply did not
  appear. That is a more severe failure than a duplicate slot, and R1 did not
  identify it.

In all four the pre-D3 walk produced **no names at all**: the wrapper reports
`getBindingRangeCount = 1` with a `null` leaf variable, so the facts pass had
nothing to key.

---

## Correction record

**R2 corrected §E9, §E10 and §P2 after the implementation's live tests disagreed
with them.** The discrepancy, and what it was not:

- **What happened.** §E9 and §E10 were measured against probe modules that carried
  `[[vk::binding(0,0)]]` on their texture. Those rows described the modules as
  "`gTint` + tex + sampler" and "`gTint` + tex + `ParameterBlock`", **dropping the
  attribute from the description**. The plan's step 11 then specified fixtures
  written from those descriptions — without the attributes — and the R1 prose
  carried the old slot numbers onto them. Per §E11 the attribute is exactly what
  decides the implicit buffer's slot, so the fixtures legitimately report a
  different one.
- **The numbers were never misread.** Re-measuring both the original probe modules
  and the shipped fixtures reproduces *both* sets of values exactly: `(0,2)` for
  the explicitly-bound module and `(0,0)` for the fixture. R1's rows are correct
  measurements of the modules named in them, once the names are complete. The
  defect was a **lossy module description**, which is the same class of error as a
  wrong number and is treated as one here.
- **§E9's headline survives and improves.** The claim under test was that
  `getGlobalParamsVarLayout` matches `OpDecorate`. It did in 7 of 7 in R1 and in
  **14 of 14** after R2 added the fixture shapes and the placement sweep. No row
  changed its verdict.
- **D3 was never at risk.** D3 reads the slot from Slang rather than predicting it,
  so a wrong prediction in the spec could not produce a wrong binding — which is
  precisely why the implementation went green while the spec was wrong. That is
  worth stating plainly: *a green suite did not validate this document.*
- **A simulation and a run diverged, and the run won.** §E10 is a simulation of the
  proposed walk; §E12 is the shipped code measured. Where the two disagree the
  measurement of the real thing is authoritative, and §E10 is now explicitly
  labelled as evidence about the *walk* rather than about the *fixtures*.

---

## Decision

### D1 — A per-scope space correction, consumed by both passes (**implemented**)

`Walk` gained step 0a, `CollectSpaceCorrections`, running before
`CollectBindingRangeFacts`, producing a scope-local
`Dictionary<(long Set, long Range), uint>`: for each field of a struct-shaped
scope, the field's `DESCRIPTOR_TABLE_SLOT` space is propagated to every binding
range in the field's span (E5), and an entry is recorded only where that space
differs from the space offset of the record the range landed in (E4). Step 1 and
`CollectBindingRangeFacts` both compute
`absoluteSet + SpaceOf(corrections, s, r, spaceOffset)`.

The dictionary is **scope-local and passed down**, never stored in `WalkState`,
because `(s, r)` indices are scope-relative and a shared map would alias across
scopes.

Why this shape: it repairs exactly one quantity in the two places that read it,
leaves the SPIR-V-verified `(s, r)` walk untouched, keeps the walk and the join
reading the same number by construction, and is a **no-op whenever Slang agrees
with itself** — so it retires itself if upstream fixes the descriptor-set view.

**Implemented, reviewed, 108/108 green. Both predicted mutations behaved as
planned (11 red on the correction mutation, exactly 2 on the span-rule mutation).
This revision does not re-open it.**

### D2 — Duplicate-slot guard in `Group` (**dropped by maintainer decision**)

The first draft proposed throwing `NotSupportedException` from `Group` when two
bindings shared a `(set, slot)`, as a backstop for any residual silent collision.
**The maintainer dropped it** — it was raised as OPEN-1 precisely because it
throws from the constructor, making a colliding program wholly unreflectable,
which is the opposite of #176's "report rather than refuse" posture, and because
it had no fixture.

**Do not reintroduce it.** Anyone revisiting this needs a new decision, not a
re-reading of this spec. Note that D3 removes the shape that motivated it most
strongly: after D3 no measured module produces a duplicate `(set, slot)` at all.

### D3 — Unwrap the constant-buffer-wrapped global scope (**new**)

**When `spReflection_getGlobalParamsTypeLayout` returns a scope whose kind is
`SLANG_TYPE_KIND_CONSTANT_BUFFER`, treat it as a wrapper: discard its
descriptor-set records entirely, walk its element instead, and synthesize the one
binding the wrapper itself represents from
`spReflection_getGlobalParamsVarLayout`.**

Concretely, before the existing top-level `Walk` call
(`SlangReflection.cs:110-116`):

1. If the kind is not `SLANG_TYPE_KIND_CONSTANT_BUFFER`, nothing changes — today's
   path exactly (E10's three controls).
2. Otherwise, read `space` and `slot` from
   `spReflectionVariableLayout_GetSpace/GetOffset(globalParamsVarLayout,
   DESCRIPTOR_TABLE_SLOT)` (E9), emit one `SlangDescriptorBinding` at
   `(space, slot)` with `Type = SLANG_BINDING_TYPE_CONSTANT_BUFFER`,
   `Count = Fixed(1)` and `Name = "globalParams"`, and register its
   `SlangBufferLayout` — built from the *element* scope — at the same key.
3. Then `Walk` the **element** scope at `absoluteSet: 0`, with
   `isParameterBlockElement: false`.

Why this shape, and why it resolves E8 rather than restating it:

- **It never mixes the two index spaces.** The obstacle was that element-scope
  `(s, r)` keys do not line up with wrapper-scope ones and no constant offset
  exists (E8). D3 sidesteps that by making the element scope the *only* scope the
  walk ever sees; the wrapper's records are not translated, they are discarded.
  Every `(s, r)`, every binding range index, every field offset and D1's whole
  correction map are then self-consistent within one scope, which is the invariant
  the rest of `Walk` was written against.
- **It is structurally the shape `Walk` already handles.** A `ParameterBlock` is a
  wrapper whose element carries ordinary data; step 2 already synthesizes the
  implicit buffer for one and step 3 already recurses into the element. D3 is the
  same move at the top level, with one measured difference the code must respect:
  a block's implicit buffer is **always** at slot 0 of its space, with the block's
  listed ranges shifted up by one, whereas the global scope's goes at whatever
  slot E9 reports — `0`, `1` or `2` across the measured shapes, per the placement
  rule in E11 — and the element's own indices are **not** shifted. That is why D3
  synthesizes separately instead of passing `isParameterBlockElement: true`. Note
  that slot 0 is often the *right* answer for the global buffer too (E11: whenever
  no explicit `[[vk::binding]]` claims it), so `isParameterBlockElement: true`
  would be wrong in a way that several fixtures cannot see —
  `ReflectionLooseGlobalsWithExplicitSpace` is the one that can, and mutation 13b
  is what exercises it.
- **The `SLANG_TYPE_KIND_STRUCT` guard in D1 is untouched.** D3 adds a check for a
  *different* kind at a *different* place (the constructor, not
  `CollectSpaceCorrections`). After the unwrap the scope handed to `Walk` is a
  `STRUCT`, so D1's guard admits it and the correction starts working again — which
  is what makes P2's module report `gXform` at set 1. Interface scopes remain
  excluded twice over: `== CONSTANT_BUFFER` does not admit them, and E7 measured
  that an interface scope is never the global scope in the first place.
- **P2's loose data becomes a correctly-reported binding, not a dropped one.**
  `globalParams` is a real descriptor the shader binds; E9 gives its set and
  slot, and E10 confirms the result matches SPIR-V. `TryGetBufferLayout(space,
  slot, …)` then returns the loose globals' member layout (E9's last bullet), which
  is the only way a caller can find out where to write `gTint`.

**On the name.** Slang supplies none (E9). D3 uses the literal `"globalParams"`,
which is Slang's own `OpName` in all fourteen measured shapes. This is a
wrapper-chosen constant, and the distinction matters: a set or slot number is a
claim about what the driver will see, whereas this name is cosmetic. A test
pinning it is a rename-detector, not a correctness assertion, and should say so.
It is an ordinary C# `string` on a description type and never reaches a Vulkan
`const char*`, so invariant 1 (UTF-8 literals) does not apply.

### Why not the alternatives

For D1 (retained from the first pass, all still rejected):

- **Fix only `CollectBindingRangeFacts` (the #175 join).** Measurement (E1) shows
  the join faithfully reproduces the descriptor-set record and step 1 computes the
  same wrong set from the same record; repairing only the join would attach a name
  to a set containing no binding.
- **Rebuild step 1 to iterate binding ranges instead of descriptor sets.** The
  `(s, r)` walk is the verified core of the reflection surface; a binding range may
  span several descriptor ranges, so this is a large rewrite for a defect confined
  to one number.
- **Refuse the construct — detect and throw, with no correction.** The correct
  answer is available from Slang and agrees with the emitted SPIR-V in every
  measured shape, and `[[vk::binding(n, space)]]` on a `ConstantBuffer` is
  something Slang compiles correctly.
- **Join through `spReflection_GetParameterByIndex` and match by name.** That list
  covers the global scope only, row F puts the same defect one level deeper, and
  name matching is fragile where reflection names the leaf (`cb`) and SPIR-V names
  the path (`gBundle.cb`).
- **Renumber sets so they are dense, or drop the space.** The reflected set numbers
  are baked into the emitted SPIR-V (`SlangReflection.cs:176-178`).

For D3 (new):

- **Translate element-scope `(s, r)` keys into wrapper-scope keys and keep walking
  the wrapper.** Rejected on measurement, not on taste: E8 shows there is no
  constant offset (`+1` for one record and `+0` for another in the same module),
  `getFieldBindingRangeOffset` indexes the element's ranges and cannot legally be
  called on the wrapper at all, and the wrapper's own index offset for the implicit
  buffer is wrong (a constant `0`, which is right only by coincidence in the
  shapes where E11's rule puts the buffer at 0). There is nothing to translate
  with.
- **Widen D1's kind guard to "not `INTERFACE`", or drop it, so
  `CollectSpaceCorrections` runs on the wrapper.** Rejected: it would call
  `GetFieldCount` on a non-struct, which is the call family that kills the process
  with `0xC0000005` (E6, issue #181 open and unfixed) — and it would not help
  anyway, because the wrapper has no fields and its records are wrong
  independently of any correction.
- **Derive the implicit buffer's slot as "one past the highest binding used in
  space 0".** Rejected although it reproduces all seven measured shapes: it is an
  inference from data points, and E9 shows Slang states the number outright. This
  arc has twice been burned by reasoning where it could have measured; adopting a
  derived rule when the authoritative one is one call away would be a third time.
- **Use `spReflection_getGlobalConstantBufferBinding`.** Rejected on measurement:
  it returns `0` in all seven shapes and is wrong in six.
- **Skip the implicit buffer — walk the element and report nothing for the
  wrapper.** Rejected: `globalParams` is a descriptor the emitted SPIR-V binds, so
  omitting it produces a `VkDescriptorSetLayout` missing a binding the shader
  uses — the same class of defect as the `ParameterBlock` implicit buffer that
  step 2 exists to prevent.
- **Refuse a module with loose global uniform data.** Rejected: it is ordinary,
  valid Slang that the compiler handles correctly, it is what a shader looks like
  before someone wraps their globals in a `cbuffer`, and E9 + E10 show the correct
  answer is fully derivable.
- **Extend the unwrap into step 3, in case a block element is also wrapped.**
  Rejected as unevidenced: every block element scope measured is `STRUCT` (or
  `INTERFACE` for the existential case), never `CONSTANT_BUFFER`. D3 is applied at
  the top-level global scope only; widening it would be designing against a shape
  nobody has seen.

---

## Upstream

**This exact defect is not filed upstream** (searched `shader-slang/slang`).

The closest relative is upstream **#10959** — *"Reflection: `DescriptorSetInfo::spaceOffset`
never set; mis-binds non-zero descriptor spaces on D3D12/Vulkan/WebGPU via
slang-rhi"* — whose Slang-side fix populated `DescriptorSetInfo::spaceOffset = space`
in `_findOrAddDescriptorSet` and closed 2026-05-25 with a
`unit-test-descriptor-set-space-offset-reflection.cpp` regression test. **That fix
is already in our pin**: `v2026.14.1` is dated 2026-07-30 and is the latest
release, which is precisely why row D's *texture* correctly reports
`spaceOffset = 1`. What P1 and P2 are is a narrower residual of the same code
path.

Two consequences:

- **Bumping the pin cannot help.** There is no newer release, and this repo does
  not build Slang from source (`src/Ahjo.Vulkan.Slang.Native/CLAUDE.md`; the pin
  is checksum-verified in `Directory.Build.props:46` and guarded by
  `SlangExportDriftTests`).
- **This area is actively churning**, which argues for the shape D1 and D3 already
  have. Upstream **#11860** is a July 2026 regression in explicit-`vk::binding` set
  assignment introduced by upstream PR #11712. A correction that is *conditional
  and self-retiring* — D1 fires only where Slang contradicts itself; D3 fires only
  on a kind Slang itself chose — degrades gracefully across such churn, whereas an
  unconditional override would have to be re-derived every time upstream moves.

**Filing either defect upstream remains out of scope for this work** (see
OPEN-2).

---

## Bounded uncertainties

- **Per-entry-point stage attribution across a corrected or synthesized set.**
  `ApplyStages` queries
  `IMetadata::isParameterLocationUsed(DESCRIPTOR_TABLE_SLOT, set, slot)`
  (`SlangReflection.cs:915-919`) with the reported set number. Whether that query
  answers `true` for a D1-corrected set, or for D3's synthesized `globalParams`
  binding, was not measured. The failure mode is bounded in one direction only: a
  failed or `false` query falls back to the program stage union
  (`SlangReflection.cs:943`), which is always a legal `stageFlags`. Worst case is a
  stage mask wider than necessary — never `ShaderStages.None`, never illegal. The
  plan requires the value to be measured and recorded rather than assumed.
- **Whether row F's shape (a `ConstantBuffer` inside a plain struct global) is the
  same upstream bug as rows A–E or a related one** is not established. It responds
  to the same correction and matches SPIR-V afterwards; that is what is claimed,
  and no more.
- **`globalParams` as a name** is Slang's `OpName` today, not a documented
  contract. If a future Slang renames it, the name assertion moves; nothing about
  set, slot, type or buffer layout does.
- **E11's placement rule is descriptive, not contractual.** It reproduces every
  shape measured, but Slang documents no such rule and upstream #11860 shows this
  allocator is being changed. Nothing in the wrapper derives a slot from it — D3
  reads the slot from `getGlobalParamsVarLayout` — so if the rule changes, the
  fixtures' expected slots move and the code does not. That asymmetry is deliberate
  and is the reason the rule is recorded as evidence rather than encoded.

---

## Cross-links

- **Resolves:** #180 (P1 and, by maintainer decision to fix it in the same PR, P2).
- **Removes a workaround note in:** #176's fixture
  (`ShaderFixtures.cs:340-349`), which routed around P1.
- **Must land consistently with:** the #175/#176/#177 arc
  (`specs/2026-08-02-issue-175-177-slang-reflection-completeness-design.md`) —
  this repairs inputs the binding-range join consumes and does not revise rule 6
  of `src/Ahjo.Vulkan.Slang/CLAUDE.md`; the join's three skips stay exactly as they
  are.
- **Constrained by:** #181 (the `getBindingRangeImageFormat` access violation on an
  existential range), open and unfixed. The `SLANG_TYPE_KIND_STRUCT` guard in D1
  is one of the two things keeping that call family away from an interface scope;
  D3 must not weaken it, and does not.
- **Prevents:** a duplicate-binding `VkDescriptorSetLayout` reaching
  `Device.CreateDescriptorSetLayout` (`src/Ahjo.Vulkan/Lifecycle/Device.cs:190-214`),
  which validates only that the span is non-empty.
- **Independent of:** #183 (a reflected descriptor count of zero laundered into 1)
  — different quantity, different file region, no ordering dependency. #182
  (variable-descriptor-count pools) is unaffected.
- **Not covered by a benchmark, on purpose:** `src/Ahjo.Vulkan.Slang/CLAUDE.md`
  forbids benchmarking this project and forbids a `docs/benchmarks.md` row.
  Reflection is setup-time; invariant #3 does not apply.
