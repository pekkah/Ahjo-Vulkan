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

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroyPipelineLayout(DeviceHandle, Handle, null);
    }
}
