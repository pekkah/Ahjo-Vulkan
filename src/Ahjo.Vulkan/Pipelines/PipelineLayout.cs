using System.Collections.Generic;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkPipelineLayout</c>. <c>readonly struct</c> +
/// <see cref="IDisposable"/>; built once and held for the lifetime of the
/// pipelines that reference it.
/// </summary>
public readonly unsafe struct PipelineLayout : IVulkanHandle<PipelineLayout>, IDisposable
{
    public readonly VkPipelineLayout_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    internal PipelineLayout(VkPipelineLayout_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PIPELINE_LAYOUT;
    public static PipelineLayout FromRaw(nint handle) => new((VkPipelineLayout_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    // Constraints declared at create time, keyed by raw handle.
    // PipelineLayout is a `readonly struct` constrained to `unmanaged` by
    // IVulkanHandle, so it can't carry managed-reference fields — the
    // declared push-constant ranges and descriptor-set-layout handles
    // live in these side-tables, populated by Device.CreatePipelineLayout
    // and read by CommandRecorder's debug-only assertions
    // (PushConstants's range-fits check and BindDescriptorSets's
    // set-layout-matches check). Layout creation/disposal is not on a
    // hot path; the dictionary lock and the small heap allocations per
    // layout are acceptable. FromRaw'd layouts have no entry here — the
    // assertions gracefully no-op in that case.
    private static readonly Dictionary<nint, PushConstantRange[]> s_pushRanges = new();
    private static readonly Dictionary<nint, nint[]>              s_setLayouts = new();
    private static readonly object s_metadataLock = new();

    internal static void RegisterPushRanges(VkPipelineLayout_T* handle, PushConstantRange[] ranges)
    {
        lock (s_metadataLock) s_pushRanges[(nint)handle] = ranges;
    }

    internal static PushConstantRange[]? TryGetPushRanges(VkPipelineLayout_T* handle)
    {
        lock (s_metadataLock)
            return s_pushRanges.TryGetValue((nint)handle, out var ranges) ? ranges : null;
    }

    internal static void RegisterSetLayouts(VkPipelineLayout_T* handle, nint[] setLayoutHandles)
    {
        lock (s_metadataLock) s_setLayouts[(nint)handle] = setLayoutHandles;
    }

    internal static nint[]? TryGetSetLayouts(VkPipelineLayout_T* handle)
    {
        lock (s_metadataLock)
            return s_setLayouts.TryGetValue((nint)handle, out var layouts) ? layouts : null;
    }

    private static void UnregisterMetadata(VkPipelineLayout_T* handle)
    {
        lock (s_metadataLock)
        {
            s_pushRanges.Remove((nint)handle);
            s_setLayouts.Remove((nint)handle);
        }
    }

    /// <summary>
    /// Builds a <see cref="DescriptorTemplate{T}"/> for the per-frame
    /// <c>vkCmdPushDescriptorSetWithTemplate</c> path. The descriptor-set
    /// layout backing <paramref name="set"/> in this pipeline layout must
    /// have been created with
    /// <see cref="DescriptorSetLayoutDescription.PushDescriptor"/>
    /// enabled. Template entries are derived from
    /// <typeparamref name="T"/>'s field offsets matched against
    /// <paramref name="bindings"/> in declaration order.
    /// </summary>
    public DescriptorTemplate<T> CreatePushDescriptorTemplate<T>(
        uint                            set,
        VkPipelineBindPoint             bindPoint,
        ReadOnlySpan<DescriptorBinding> bindings)
        where T : unmanaged
        => DescriptorTemplateBuilder.CreateForPush<T>(DeviceHandle, Handle, bindPoint, set, bindings);

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroyPipelineLayout with a
        // null device handle would crash on every loader. There is no
        // side-table entry for a FromRaw'd layout, so skip unregistration too.
        if (DeviceHandle == null) return;
        UnregisterMetadata(Handle);
        Vk.vkDestroyPipelineLayout(DeviceHandle, Handle, null);
    }
}
