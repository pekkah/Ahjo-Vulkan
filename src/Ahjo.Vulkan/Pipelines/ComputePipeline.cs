using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkPipeline</c> created from a single compute shader stage. Holds
/// the layout pointer alongside the pipeline handle so the recorder
/// (when #17 lands) doesn't have to re-thread the layout through each
/// dispatch call. <c>readonly struct</c> + <see cref="IDisposable"/>;
/// pipelines are typically built at startup and held for the program's
/// lifetime.
/// </summary>
public readonly unsafe struct ComputePipeline : IVulkanHandle<ComputePipeline>, IDisposable
{
    public readonly VkPipeline_T*       Handle;
    public readonly VkPipelineLayout_T* Layout;
    internal readonly VkDevice_T*       DeviceHandle;

    internal ComputePipeline(VkPipeline_T* handle, VkPipelineLayout_T* layout, VkDevice_T* device)
    {
        Handle       = handle;
        Layout       = layout;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PIPELINE;
    public static ComputePipeline FromRaw(nint handle) => new((VkPipeline_T*)handle, null, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller already owns the lifetime; calling vkDestroyPipeline with
        // a null device handle would crash on every loader.
        if (!OwnsHandle) return;
        Vk.vkDestroyPipeline(DeviceHandle, Handle, null);
    }
}
