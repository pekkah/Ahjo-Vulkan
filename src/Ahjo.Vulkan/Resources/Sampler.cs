using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkSampler</c> bound to the device that created it. Two-pointer
/// struct; <see cref="Dispose"/> calls <c>vkDestroySampler</c>.
/// </summary>
/// <remarks>
/// Holds a raw <c>VkDevice_T*</c> rather than a <see cref="Device"/>
/// reference — the struct needs only a destroy target, not the managed
/// wrapper. The pointer is valid as
/// long as the parent <see cref="Device"/> hasn't been disposed.
/// <c>default(Sampler)</c> is a legal null handle: <see cref="IsNull"/>
/// returns <see langword="true"/> and <see cref="Dispose"/> is a no-op.
/// </remarks>
public readonly unsafe struct Sampler : IVulkanHandle<Sampler>, IDisposable
{
    public readonly VkSampler_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    internal Sampler(VkSampler_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_SAMPLER;

    public static Sampler FromRaw(nint handle) => new((VkSampler_T*)handle, null);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroySampler with a null
        // device handle would crash on every loader.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroySampler(DeviceHandle, Handle, null);
    }
}
