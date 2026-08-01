using System.Text;

using Xunit;

namespace Ahjo.Vulkan.Slang.Native.Tests;

/// <summary>
/// Executes the packaged Slang compiler. This suite is the reason the per-RID
/// build matrix exists: a binding that compiles proves nothing about a binary
/// nobody has ever loaded, and every failure mode that matters here — a
/// missing runtime dependency, a glibc mismatch, a vtable slot that shifted
/// against the pinned header, a staged binary that was never refreshed after a
/// version bump — shows up only when real shader source goes through the real
/// compiler.
/// <para>
/// It acquires no Vulkan device and needs no loader and no ICD, by design:
/// Slang produces bytes. See <c>tests/CLAUDE.md</c>.
/// </para>
/// </summary>
public sealed unsafe class SlangSmokeTests
{
    // Compile-time constants, so invariant #1 applies in its literal form:
    // "…"u8 data lives in the assembly's read-only segment and the C#
    // compiler null-terminates it, which is exactly what a const char*
    // parameter needs. Nothing here is round-tripped through
    // Encoding.UTF8.GetBytes.
    private static ReadOnlySpan<byte> SpirvProfile => "spirv_1_5"u8;
    private static ReadOnlySpan<byte> ModuleName => "smoke"u8;
    private static ReadOnlySpan<byte> ModulePath => "smoke.slang"u8;
    private static ReadOnlySpan<byte> VertexMain => "vertexMain"u8;
    private static ReadOnlySpan<byte> FragmentMain => "fragmentMain"u8;

    /// <summary>Every SPIR-V module starts with this magic number.</summary>
    private const uint SpirvMagic = 0x07230203;

    private static ReadOnlySpan<byte> MinimalVertexShader => """
        struct VOut
        {
            float4 pos : SV_Position;
        };

        [shader("vertex")]
        VOut vertexMain(float3 position)
        {
            VOut o;
            o.pos = float4(position, 1.0);
            return o;
        }
        """u8;

    private static ReadOnlySpan<byte> BrokenShader => """
        [shader("vertex")]
        float4 vertexMain() : SV_Position
        {
            return notAThing;
        }
        """u8;

    // The reflection fixture: one of every descriptor shape the SPIR-V target
    // emits into space 0, plus a push-constant block. Each parameter is
    // actually referenced by an entry point so that nothing can be dropped as
    // unused and quietly change the range indices out from under the asserts.
    private static ReadOnlySpan<byte> ReflectionShader => """
        struct Xform { float4x4 m; };
        struct Push  { float4 tint; };

        ConstantBuffer<Xform> gXform;
        Texture2D             gAlbedo;
        SamplerState          gSampler;
        RWStructuredBuffer<float4> gOut;

        [[vk::push_constant]] ConstantBuffer<Push> gPush;

        struct VOut
        {
            float4 pos : SV_Position;
            float2 uv  : TEXCOORD0;
        };

        [shader("vertex")]
        VOut vertexMain(float3 position, float2 uv)
        {
            VOut o;
            o.pos = mul(gXform.m, float4(position, 1.0));
            o.uv  = uv;
            return o;
        }

        [shader("fragment")]
        float4 fragmentMain(VOut i) : SV_Target
        {
            float4 c = gAlbedo.Sample(gSampler, i.uv) * gPush.tint;
            gOut[0] = c;
            return c;
        }
        """u8;

    [Fact]
    public void GlobalSession_Creates()
    {
        // The narrowest possible call into the library: no compilation, no
        // file system, nothing but loading the binary and reaching its one
        // free entry point. When the native dependency story is broken this
        // is what fails, and it fails with a DllNotFoundException naming the
        // library instead of a compiler error twelve frames deep.
        IGlobalSession* global = null;
        var rc = SlangApi.slang_createGlobalSession(0, &global);

        Assert.True(rc >= 0, $"slang_createGlobalSession returned 0x{rc:X8}");
        Assert.True(global != null);

        global->release();
    }

    [Fact]
    public void BuildTag_MatchesPinnedVersion()
    {
        // This is what catches a staged binary that did not get refreshed
        // after a SlangVersion bump: the bindings would be regenerated from
        // the new headers while an old .so kept answering the calls.
        IGlobalSession* global = CreateGlobalSession();
        try
        {
            var buildTag = Utf8ToString(global->getBuildTagString());

            Assert.Equal(SlangPinnedVersion.WithoutLeadingV, buildTag);
        }
        finally
        {
            global->release();
        }
    }

    [Fact]
    public void Compile_SimpleShader_ProducesSpirv()
    {
        IGlobalSession* global = CreateGlobalSession();
        try
        {
            ISession* session = CreateSpirvSession(global);
            try
            {
                IComponentType* linked = LinkVertexOnly(session, MinimalVertexShader);
                try
                {
                    ISlangBlob* code = null;
                    ISlangBlob* diagnostics = null;
                    var rc = linked->getEntryPointCode(0, 0, &code, &diagnostics);

                    var text = ReadBlob(diagnostics);
                    if (diagnostics != null)
                    {
                        diagnostics->release();
                    }

                    Assert.True(rc >= 0, $"getEntryPointCode returned 0x{rc:X8}: {text}");
                    Assert.True(code != null);

                    try
                    {
                        var size = (int)code->getBufferSize();

                        Assert.True(size > 0, "getEntryPointCode succeeded but produced an empty blob.");
                        Assert.Equal(0, size % 4);

                        var words = new ReadOnlySpan<uint>(code->getBufferPointer(), size / 4);
                        Assert.Equal(SpirvMagic, words[0]);
                    }
                    finally
                    {
                        code->release();
                    }
                }
                finally
                {
                    linked->release();
                }
            }
            finally
            {
                session->release();
            }
        }
        finally
        {
            global->release();
        }
    }

    [Fact]
    public void Compile_BrokenShader_ProducesDiagnosticsAndNullModule()
    {
        IGlobalSession* global = CreateGlobalSession();
        try
        {
            ISession* session = CreateSpirvSession(global);
            try
            {
                IModule* module;
                ISlangBlob* diagnostics = null;

                fixed (byte* name = ModuleName)
                fixed (byte* path = ModulePath)
                fixed (byte* source = BrokenShader)
                {
                    module = session->loadModuleFromSourceString(
                        (sbyte*)name, (sbyte*)path, (sbyte*)source, &diagnostics);
                }

                var text = ReadBlob(diagnostics);
                if (diagnostics != null)
                {
                    diagnostics->release();
                }

                if (module != null)
                {
                    module->release();
                }

                // Slang signals this failure by returning nullptr — there is no
                // SlangResult to inspect on this call at all. A wrapper that
                // only checked a result code would sail past a broken compile
                // and hand back an empty blob, which is precisely the failure
                // mode issue #166 exists to prevent.
                Assert.True(module == null, "A shader referencing an undefined identifier loaded as a module.");
                Assert.NotEqual(string.Empty, text);
                Assert.Contains("undefined identifier", text, StringComparison.Ordinal);
            }
            finally
            {
                session->release();
            }
        }
        finally
        {
            global->release();
        }
    }

    [Fact]
    public void Reflection_WalksGlobalScope()
    {
        IGlobalSession* global = CreateGlobalSession();
        try
        {
            ISession* session = CreateSpirvSession(global);
            try
            {
                IComponentType* linked = LinkVertexAndFragment(session, ReflectionShader);
                try
                {
                    ISlangBlob* diagnostics = null;
                    var layout = (SlangProgramLayout*)linked->getLayout(0, &diagnostics);

                    var text = ReadBlob(diagnostics);
                    if (diagnostics != null)
                    {
                        diagnostics->release();
                    }

                    Assert.True(layout != null, $"getLayout returned null: {text}");

                    // Everything below goes through the flat spReflection_*
                    // exports in slang-deprecated.h. That header is the only
                    // way to reach this surface — Slang's own recommended C++
                    // API is a header-only shim over exactly these symbols —
                    // which is why SlangExportDriftTests exists.
                    var globals = SlangApi.spReflection_getGlobalParamsTypeLayout(layout);
                    Assert.True(globals != null);

                    Assert.Equal(1L, SlangApi.spReflectionTypeLayout_getDescriptorSetCount(globals));
                    Assert.Equal(0L, SlangApi.spReflectionTypeLayout_getDescriptorSetSpaceOffset(globals, 0));

                    var rangeCount = SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeCount(globals, 0);
                    Assert.Equal(5L, rangeCount);

                    // Four ordinary descriptor-table slots at index offsets
                    // 0..3, in declaration order.
                    AssertDescriptorRange(globals, 0, 0, SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER);
                    AssertDescriptorRange(globals, 1, 1, SlangBindingType.SLANG_BINDING_TYPE_TEXTURE);
                    AssertDescriptorRange(globals, 2, 2, SlangBindingType.SLANG_BINDING_TYPE_SAMPLER);
                    AssertDescriptorRange(globals, 3, 3, SlangBindingType.SLANG_BINDING_TYPE_MUTABLE_RAW_BUFFER);

                    // The push-constant block arrives as a descriptor range
                    // too, distinguished only by its CATEGORY. Anything
                    // building a VkDescriptorSetLayout has to filter on that,
                    // not on the binding type.
                    Assert.Equal(
                        SlangParameterCategory.SLANG_PARAMETER_CATEGORY_PUSH_CONSTANT_BUFFER,
                        SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeCategory(globals, 0, 4));
                    Assert.Equal(
                        SlangBindingType.SLANG_BINDING_TYPE_PUSH_CONSTANT,
                        SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeType(globals, 0, 4));

                    // Entry points, and the stage each one reports.
                    Assert.Equal(2ul, SlangApi.spReflection_getEntryPointCount(layout));

                    var vertex = SlangApi.spReflection_getEntryPointByIndex(layout, 0);
                    Assert.Equal("vertexMain", Utf8ToString(SlangApi.spReflectionEntryPoint_getName(vertex)));
                    Assert.Equal(SlangStage.SLANG_STAGE_VERTEX, SlangApi.spReflectionEntryPoint_getStage(vertex));

                    var fragment = SlangApi.spReflection_getEntryPointByIndex(layout, 1);
                    Assert.Equal("fragmentMain", Utf8ToString(SlangApi.spReflectionEntryPoint_getName(fragment)));
                    Assert.Equal(SlangStage.SLANG_STAGE_FRAGMENT, SlangApi.spReflectionEntryPoint_getStage(fragment));
                }
                finally
                {
                    linked->release();
                }
            }
            finally
            {
                session->release();
            }
        }
        finally
        {
            global->release();
        }
    }

    private static void AssertDescriptorRange(
        SlangReflectionTypeLayout* globals,
        long rangeIndex,
        long expectedIndexOffset,
        SlangBindingType expectedType)
    {
        Assert.Equal(
            SlangParameterCategory.SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT,
            SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeCategory(globals, 0, rangeIndex));
        Assert.Equal(
            expectedIndexOffset,
            SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeIndexOffset(globals, 0, rangeIndex));
        Assert.Equal(
            1L,
            SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeDescriptorCount(globals, 0, rangeIndex));
        Assert.Equal(
            expectedType,
            SlangApi.spReflectionTypeLayout_getDescriptorSetDescriptorRangeType(globals, 0, rangeIndex));
    }

    private static IGlobalSession* CreateGlobalSession()
    {
        IGlobalSession* global = null;
        var rc = SlangApi.slang_createGlobalSession(0, &global);

        Assert.True(rc >= 0 && global != null, $"slang_createGlobalSession returned 0x{rc:X8}");

        return global;
    }

    private static ISession* CreateSpirvSession(IGlobalSession* global)
    {
        SlangProfileID profile;
        fixed (byte* p = SpirvProfile)
        {
            profile = global->findProfile((sbyte*)p);
        }

        Assert.NotEqual(SlangProfileID.SLANG_PROFILE_UNKNOWN, profile);

        // structureSize is NOT optional. It carries a C++ default member
        // initialiser upstream, which ClangSharp does not reproduce, and Slang
        // reads the field to decide how much of the struct it may look at.
        var target = default(TargetDesc);
        target.structureSize = (nuint)sizeof(TargetDesc);
        target.format = SlangCompileTarget.SLANG_SPIRV;
        target.profile = profile;
        target.flags = SlangApi.SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY;

        var desc = default(SessionDesc);
        desc.structureSize = (nuint)sizeof(SessionDesc);
        desc.targets = &target;
        desc.targetCount = 1;

        ISession* session = null;
        var rc = global->createSession(&desc, &session);

        Assert.True(rc >= 0 && session != null, $"createSession returned 0x{rc:X8}");

        return session;
    }

    private static IComponentType* LinkVertexOnly(ISession* session, ReadOnlySpan<byte> source)
        => Link(session, source, includeFragment: false);

    private static IComponentType* LinkVertexAndFragment(ISession* session, ReadOnlySpan<byte> source)
        => Link(session, source, includeFragment: true);

    /// <summary>
    /// module → findAndCheckEntryPoint per stage → composite → link. The
    /// linked <c>IComponentType</c> is the only thing that can produce both
    /// SPIR-V and a program layout.
    /// </summary>
    private static IComponentType* Link(ISession* session, ReadOnlySpan<byte> source, bool includeFragment)
    {
        IModule* module;
        ISlangBlob* diagnostics = null;

        fixed (byte* name = ModuleName)
        fixed (byte* path = ModulePath)
        fixed (byte* text = source)
        {
            module = session->loadModuleFromSourceString(
                (sbyte*)name, (sbyte*)path, (sbyte*)text, &diagnostics);
        }

        var loadText = ReadBlob(diagnostics);
        if (diagnostics != null)
        {
            diagnostics->release();
            diagnostics = null;
        }

        Assert.True(module != null, $"loadModuleFromSourceString returned null: {loadText}");

        var componentCount = includeFragment ? 3 : 2;
        var components = stackalloc IComponentType*[componentCount];
        components[0] = (IComponentType*)module;

        components[1] = (IComponentType*)FindEntryPoint(module, VertexMain, SlangStage.SLANG_STAGE_VERTEX);
        if (includeFragment)
        {
            components[2] = (IComponentType*)FindEntryPoint(module, FragmentMain, SlangStage.SLANG_STAGE_FRAGMENT);
        }

        IComponentType* composite = null;
        var rc = session->createCompositeComponentType(components, componentCount, &composite, &diagnostics);

        var compositeText = ReadBlob(diagnostics);
        if (diagnostics != null)
        {
            diagnostics->release();
            diagnostics = null;
        }

        Assert.True(rc >= 0 && composite != null, $"createCompositeComponentType returned 0x{rc:X8}: {compositeText}");

        IComponentType* linked = null;
        rc = composite->link(&linked, &diagnostics);

        var linkText = ReadBlob(diagnostics);
        if (diagnostics != null)
        {
            diagnostics->release();
        }

        composite->release();

        Assert.True(rc >= 0 && linked != null, $"link returned 0x{rc:X8}: {linkText}");

        return linked;
    }

    private static IEntryPoint* FindEntryPoint(IModule* module, ReadOnlySpan<byte> name, SlangStage stage)
    {
        IEntryPoint* entryPoint = null;
        ISlangBlob* diagnostics = null;
        int rc;

        fixed (byte* p = name)
        {
            rc = module->findAndCheckEntryPoint((sbyte*)p, stage, &entryPoint, &diagnostics);
        }

        var text = ReadBlob(diagnostics);
        if (diagnostics != null)
        {
            diagnostics->release();
        }

        Assert.True(rc >= 0 && entryPoint != null, $"findAndCheckEntryPoint returned 0x{rc:X8}: {text}");

        return entryPoint;
    }

    /// <summary>
    /// Reads a diagnostics blob. Decoding native bytes into a managed string
    /// is the opposite direction from invariant #1, which is about the
    /// pointers we hand Slang; a blob is neither null-terminated by contract
    /// nor owned by us, so the length-carrying overload is the correct one.
    /// </summary>
    private static string ReadBlob(ISlangBlob* blob)
    {
        if (blob == null)
        {
            return string.Empty;
        }

        var size = (int)blob->getBufferSize();

        return size == 0 ? string.Empty : Encoding.UTF8.GetString((byte*)blob->getBufferPointer(), size);
    }

    private static string Utf8ToString(sbyte* utf8)
        => utf8 == null ? string.Empty : Encoding.UTF8.GetString(new ReadOnlySpan<byte>(utf8, StringLength(utf8)));

    private static int StringLength(sbyte* utf8)
    {
        var length = 0;
        while (utf8[length] != 0)
        {
            length++;
        }

        return length;
    }
}
