using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-device cache of extension entry points the wrapper itself uses.
/// Empty for issue 08; populated incrementally by later issues
/// (e.g. timeline-semaphore helpers, debug-utils naming).
/// </summary>
internal readonly unsafe struct DeviceFunctionTable
{
    private readonly VkDevice_T* _device;

    public DeviceFunctionTable(VkDevice_T* device) { _device = device; }

    public delegate* unmanaged[Stdcall]<void> Resolve(Utf8Name name) =>
        Vk.vkGetDeviceProcAddr(_device, name.Ptr);
}
