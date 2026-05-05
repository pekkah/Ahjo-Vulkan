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
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_SHADER_MODULE;
    public static ShaderModule FromRaw(nint handle) => new((VkShaderModule_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroyShaderModule(DeviceHandle, Handle, null);
    }
}
