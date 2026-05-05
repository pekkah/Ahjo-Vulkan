using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkPipeline</c> created from a graphics pipeline state. Holds the
/// layout pointer alongside the pipeline handle so the recorder doesn't
/// have to re-thread the layout through each draw.
/// </summary>
public readonly unsafe struct GraphicsPipeline : IVulkanHandle<GraphicsPipeline>, IDisposable
{
    public readonly VkPipeline_T*       Handle;
    public readonly VkPipelineLayout_T* Layout;
    internal readonly VkDevice_T*       DeviceHandle;

    internal GraphicsPipeline(VkPipeline_T* handle, VkPipelineLayout_T* layout, VkDevice_T* device)
    {
        Handle       = handle;
        Layout       = layout;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PIPELINE;
    public static GraphicsPipeline FromRaw(nint handle) => new((VkPipeline_T*)handle, null, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroyPipeline(DeviceHandle, Handle, null);
    }
}
