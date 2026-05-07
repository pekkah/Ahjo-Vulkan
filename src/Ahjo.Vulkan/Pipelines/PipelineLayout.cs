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

    // Push-constant ranges declared at create time, keyed by raw handle.
    // PipelineLayout is a `readonly struct` constrained to `unmanaged` by
    // IVulkanHandle, so it can't carry a managed-reference field — the
    // ranges live in this side-table, populated by Device.CreatePipelineLayout
    // and read by CommandRecorder.PushConstants's debug-only range-fits
    // assertion. Layout creation/disposal is not on a hot path; the
    // dictionary lock and the small heap allocation per layout are
    // acceptable. FromRaw'd layouts have no entry here — the assertion
    // gracefully no-ops in that case.
    private static readonly Dictionary<nint, PushConstantRange[]> s_pushRanges = new();
    private static readonly object s_pushRangesLock = new();

    internal static void RegisterPushRanges(VkPipelineLayout_T* handle, PushConstantRange[] ranges)
    {
        lock (s_pushRangesLock) s_pushRanges[(nint)handle] = ranges;
    }

    internal static PushConstantRange[]? TryGetPushRanges(VkPipelineLayout_T* handle)
    {
        lock (s_pushRangesLock)
            return s_pushRanges.TryGetValue((nint)handle, out var ranges) ? ranges : null;
    }

    private static void UnregisterPushRanges(VkPipelineLayout_T* handle)
    {
        lock (s_pushRangesLock) s_pushRanges.Remove((nint)handle);
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
        UnregisterPushRanges(Handle);
        Vk.vkDestroyPipelineLayout(DeviceHandle, Handle, null);
    }
}
