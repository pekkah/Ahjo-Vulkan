using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkShaderModule</c>. <c>readonly struct</c> + <see cref="IDisposable"/>;
/// shader modules are typically created at startup, fed into pipeline
/// builders, and disposed once the pipelines they back are alive (the
/// pipelines retain whatever they need).
/// </summary>
/// <remarks>
/// <c>default(ShaderModule)</c> is a legal null handle. Holds the owning
/// <c>VkDevice_T*</c> so disposal doesn't require the caller to thread
/// the device through.
/// </remarks>
public readonly unsafe struct ShaderModule : IVulkanHandle<ShaderModule>, IDisposable
{
    public readonly VkShaderModule_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    internal ShaderModule(VkShaderModule_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_SHADER_MODULE;
    public static ShaderModule FromRaw(nint handle) => new((VkShaderModule_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroyShaderModule with a
        // null device handle would crash on every loader.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyShaderModule(DeviceHandle, Handle, null);
    }
}
