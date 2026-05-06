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
        Vk.vkDestroyPipelineLayout(DeviceHandle, Handle, null);
    }
}
