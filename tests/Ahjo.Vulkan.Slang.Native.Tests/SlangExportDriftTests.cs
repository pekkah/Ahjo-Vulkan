using System.Runtime.InteropServices;

using Xunit;

namespace Ahjo.Vulkan.Slang.Native.Tests;

/// <summary>
/// The drift guard.
/// <para>
/// Every reflection entry point this stack depends on lives in
/// <c>slang-deprecated.h</c>, whose own banner says "New code should not use
/// any of these declarations, and the Slang API will drop these declarations
/// over time". That dependency is deliberate and eyes-open: Slang's
/// recommended C++ reflection API is a header-only shim that calls exactly
/// these symbols, so there is nothing else to bind. The mitigation is not
/// avoidance, it is this test.
/// </para>
/// <para>
/// A <c>SlangVersion</c> bump that drops any of these names fails here, once,
/// with the missing name in the message — instead of somewhere downstream as
/// an <see cref="EntryPointNotFoundException"/> from whichever call site
/// happened to run first.
/// </para>
/// <para>
/// Resolution goes through <see cref="NativeLibrary"/> by name. No
/// reflection, no <c>Assembly.GetTypes()</c>, no attribute scanning — the
/// list is a literal array, so this test is Native AOT clean by construction
/// and stays that way.
/// </para>
/// </summary>
public sealed class SlangExportDriftTests
{
    /// <summary>
    /// The exports the Ahjo.Vulkan.Slang wrapper calls, or will call. Keep
    /// this in step with the wrapper: an entry that nothing calls is noise,
    /// and a call the wrapper makes that is missing here is an unguarded
    /// dependency on a deprecated symbol.
    /// </summary>
    private static readonly string[] RequiredExports =
    [
        // Session + build identity.
        "slang_createGlobalSession",
        "spGetBuildTagString",

        // Program-level reflection.
        "spReflection_GetParameterCount",
        "spReflection_GetParameterByIndex",
        "spReflection_getEntryPointCount",
        "spReflection_getEntryPointByIndex",
        "spReflection_getGlobalParamsTypeLayout",

        // Composition: SlangProgramBuilder.AddTypeConformance resolves both of
        // its type names against the composite's layout before it can call
        // createTypeConformanceComponentType.
        "spReflection_FindTypeByName",

        // Type layouts: sizes, element types, and the descriptor-set walk that
        // produces DescriptorBinding / PushConstantRange.
        "spReflectionTypeLayout_getKind",
        "spReflectionTypeLayout_GetSize",
        "spReflectionTypeLayout_getAlignment",
        "spReflectionTypeLayout_GetElementTypeLayout",
        "spReflectionTypeLayout_GetParameterCategory",
        "spReflectionTypeLayout_getDescriptorSetCount",
        "spReflectionTypeLayout_getDescriptorSetSpaceOffset",
        "spReflectionTypeLayout_getDescriptorSetDescriptorRangeCount",
        "spReflectionTypeLayout_getDescriptorSetDescriptorRangeIndexOffset",
        "spReflectionTypeLayout_getDescriptorSetDescriptorRangeDescriptorCount",
        "spReflectionTypeLayout_getDescriptorSetDescriptorRangeType",
        "spReflectionTypeLayout_getDescriptorSetDescriptorRangeCategory",
        "spReflectionTypeLayout_GetType",

        // The ParameterBlock walk. A block is a descriptor *space*, so the
        // reflection walk recurses into it rather than mapping it, and the set
        // index it recurses with is the enclosing set plus the sub-object
        // range's SUB_ELEMENT_REGISTER_SPACE offset.
        //
        // spReflectionTypeLayout_getSubObjectRangeSpaceOffset is deliberately
        // NOT in this list. It is the wrong function for that offset — it
        // returns 0 for every sub-object range, including blocks the emitted
        // SPIR-V puts in spaces 1 and 2 — so nothing here calls it and nothing
        // here should start.
        "spReflectionTypeLayout_getSubObjectRangeCount",
        "spReflectionTypeLayout_getSubObjectRangeBindingRangeIndex",
        "spReflectionTypeLayout_getSubObjectRangeOffset",
        "spReflectionTypeLayout_getBindingRangeType",
        "spReflectionTypeLayout_getBindingRangeLeafTypeLayout",

        // The binding-range pass (SlangReflection.CollectBindingRangeFacts):
        // the additive join from a descriptor range back to the name, image
        // format, specializability and leaf type layout of what declared it.
        // This is spec E8 route 1, measured: the keys these produce match the
        // SPIR-V-verified descriptor walk exactly.
        //
        // spReflectionTypeLayout_getBindingRangeImageFormat is called only for
        // texture and typed-buffer ranges — it access-violates on an
        // EXISTENTIAL_VALUE range (SlangReflection.ImageFormatOf) — but it is
        // called, so it belongs here.
        "spReflectionTypeLayout_getBindingRangeCount",
        "spReflectionTypeLayout_getBindingRangeDescriptorSetIndex",
        "spReflectionTypeLayout_getBindingRangeFirstDescriptorRangeIndex",
        "spReflectionTypeLayout_getBindingRangeLeafVariable",
        "spReflectionTypeLayout_getBindingRangeImageFormat",
        "spReflectionTypeLayout_isBindingRangeSpecializable",

        // Struct-typed vertex inputs: one level of recursion, locations
        // accumulating parent offset + field offset. The same two calls walk a
        // buffer's members (SlangReflection.AppendMembers).
        "spReflectionTypeLayout_GetFieldCount",
        "spReflectionTypeLayout_GetFieldByIndex",

        // Buffer member layouts: bytes, padding and matrix orientation
        // (SlangReflection.AppendMembers).
        "spReflectionTypeLayout_GetStride",
        "spReflectionTypeLayout_GetElementStride",
        "spReflectionTypeLayout_GetMatrixLayoutMode",
        "spReflectionType_GetName",

        // Types: what a vertex attribute's VkFormat is derived from.
        "spReflectionType_GetKind",
        "spReflectionType_GetElementCount",
        "spReflectionType_GetElementType",
        "spReflectionType_GetScalarType",
        "spReflectionType_GetRowCount",
        "spReflectionType_GetColumnCount",

        // Variable layouts: locations, semantics, names.
        "spReflectionVariableLayout_GetVariable",
        "spReflectionVariableLayout_GetTypeLayout",
        "spReflectionVariableLayout_GetOffset",
        "spReflectionVariableLayout_GetSemanticName",
        "spReflectionVariableLayout_GetSemanticIndex",
        "spReflectionVariable_GetName",

        // Entry points and their varying parameters.
        //
        // spReflectionEntryPoint_getNameOverride is deliberately NOT here: it
        // returned exactly getName's string for every fixture and every stage
        // on v2026.14.1, so SlangEntryPointInfo carries no member for it and
        // nothing calls it. An entry with no caller is noise.
        "spReflectionEntryPoint_getName",
        "spReflectionEntryPoint_getStage",
        "spReflectionEntryPoint_getComputeThreadGroupSize",
        "spReflectionEntryPoint_getParameterCount",
        "spReflectionEntryPoint_getParameterByIndex",

        // SlangReflection.ToJson — the diagnostics escape hatch for what the
        // typed surface does not cover.
        "spReflection_ToJson",
    ];

    [Fact]
    public void EveryRequiredExport_IsPresentInTheShippedBinary()
    {
        // "slang" is the same name the generated DllImports use, resolved
        // against the same assembly, so this loads the very binary the
        // bindings will call — not some other copy on the search path.
        var library = NativeLibrary.Load("slang", typeof(SlangApi).Assembly, null);

        try
        {
            var missing = new List<string>();

            foreach (var name in RequiredExports)
            {
                if (!NativeLibrary.TryGetExport(library, name, out _))
                {
                    missing.Add(name);
                }
            }

            Assert.True(
                missing.Count == 0,
                $"The shipped Slang binary is missing {missing.Count} export(s) the wrapper depends on: "
                + string.Join(", ", missing)
                + ". These live in slang-deprecated.h; upstream has dropped or renamed them. "
                + "Do not delete the call sites — decide what replaces them.");
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }
}
