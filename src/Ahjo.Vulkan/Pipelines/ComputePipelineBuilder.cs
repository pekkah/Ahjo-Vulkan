using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Fluent builder for <see cref="ComputePipeline"/>. <c>ref struct</c> so
/// the in-progress configuration cannot escape the build scope and the
/// inline UTF-8 entry-point bytes don't need a heap allocation.
/// </summary>
/// <remarks>
/// <para>The <see cref="WithEntryPoint"/> default is <c>"main"</c> — the
/// glslc / DXC default, and the dominant case. The bytes live in the
/// builder as an inline <see cref="EntryPointBuffer"/> so the
/// <c>const char*</c> handed to Vulkan stays valid for the duration of
/// <see cref="Build"/>.</para>
/// <para>Specialization constants are intentionally not on the builder
/// yet — the typed <c>SpecializationInfo&lt;T&gt;</c> wrapper depends on
/// the <see cref="Pipelines"/> design landing alongside the graphics
/// builder (#21) and a dedicated follow-up. Compute pipelines that need
/// specialization can drop down to the raw bindings until then.</para>
/// </remarks>
public unsafe ref struct ComputePipelineBuilder
{
    private readonly Device _device;

    private VkShaderModule_T*   _module;
    private VkPipelineLayout_T* _layout;
    private VkPipelineCache_T*  _cache;
    private EntryPointBuffer    _entry;
    private int                 _entryLen;

    internal ComputePipelineBuilder(Device device)
    {
        _device = device;
        // Default entry point: "main\0"
        _entry[0] = (byte)'m';
        _entry[1] = (byte)'a';
        _entry[2] = (byte)'i';
        _entry[3] = (byte)'n';
        _entry[4] = 0;
        _entryLen = 4;
    }

    public ComputePipelineBuilder WithShader(in ShaderModule module)
    {
        _module = module.Handle;
        return this;
    }

    public ComputePipelineBuilder WithLayout(in PipelineLayout layout)
    {
        _layout = layout.Handle;
        return this;
    }

    /// <summary>
    /// Sets the SPIR-V entry-point name. Default is <c>"main"</c>; pass a
    /// different value when targeting an HLSL shader compiled with a
    /// non-default entry point. Maximum 31 bytes plus the trailing NUL.
    /// </summary>
    public ComputePipelineBuilder WithEntryPoint(ReadOnlySpan<byte> name)
    {
        if (name.Length > 31)
            throw new ArgumentException("Entry-point name exceeds 31 bytes (wrapper ceiling).", nameof(name));
        Span<byte> dest = MemoryMarshal.CreateSpan(ref _entry.e0, 32);
        dest.Clear();
        name.CopyTo(dest);
        // Trailing NUL is implicit because we just cleared the buffer.
        _entryLen = name.Length;
        return this;
    }

    /// <summary>
    /// Provides an optional <c>VkPipelineCache</c> the driver may consult /
    /// populate during <see cref="Build"/>. Pass <c>null</c> (the default)
    /// to skip the cache.
    /// </summary>
    public ComputePipelineBuilder WithCache(VkPipelineCache_T* cache)
    {
        _cache = cache;
        return this;
    }

    /// <summary>
    /// Issues <c>vkCreateComputePipelines</c>. The builder is left in an
    /// indeterminate state after this call; callers should not reuse it.
    /// </summary>
    public ComputePipeline Build()
    {
        if (_module == null) throw new InvalidOperationException("ComputePipelineBuilder requires WithShader.");
        if (_layout == null) throw new InvalidOperationException("ComputePipelineBuilder requires WithLayout.");

        VkPipeline_T* raw = null;
        fixed (byte* pEntry = &_entry.e0)
        {
            var stage = new VkPipelineShaderStageCreateInfo
            {
                sType  = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage  = VkShaderStageFlagBits.VK_SHADER_STAGE_COMPUTE_BIT,
                module = _module,
                pName  = (sbyte*)pEntry,
            };
            var ci = new VkComputePipelineCreateInfo
            {
                sType              = VkStructureType.VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO,
                stage              = stage,
                layout             = _layout,
                basePipelineHandle = null,
                basePipelineIndex  = -1,
            };
            Vk.vkCreateComputePipelines(_device.Handle, _cache, 1, &ci, null, &raw).ThrowIfFailed();
        }
        return new ComputePipeline(raw, _layout, _device.Handle);
    }

    [InlineArray(32)]
    private struct EntryPointBuffer { internal byte e0; }
}
