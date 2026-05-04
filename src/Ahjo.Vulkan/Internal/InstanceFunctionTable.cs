using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-instance cache of extension entry points resolved through
/// <c>vkGetInstanceProcAddr</c>. The loader does not export extension
/// functions through <c>vulkan-1.dll</c>; the only legal way to call them
/// is via the function pointer the loader hands back at runtime.
/// </summary>
internal readonly unsafe struct InstanceFunctionTable
{
    public readonly delegate* unmanaged[Stdcall]<
        VkInstance_T*,
        VkDebugUtilsMessengerCreateInfoEXT*,
        VkAllocationCallbacks*,
        VkDebugUtilsMessengerEXT_T**,
        VkResult> CreateDebugUtilsMessenger;

    public readonly delegate* unmanaged[Stdcall]<
        VkInstance_T*,
        VkDebugUtilsMessengerEXT_T*,
        VkAllocationCallbacks*,
        void> DestroyDebugUtilsMessenger;

    private readonly VkInstance_T* _instance;

    public InstanceFunctionTable(VkInstance_T* instance)
    {
        _instance = instance;
        CreateDebugUtilsMessenger =
            (delegate* unmanaged[Stdcall]<VkInstance_T*, VkDebugUtilsMessengerCreateInfoEXT*, VkAllocationCallbacks*, VkDebugUtilsMessengerEXT_T**, VkResult>)
            Resolve(Utf8Name.FromLiteral(InstanceExtensionNames.CreateDebugUtilsMessenger));
        DestroyDebugUtilsMessenger =
            (delegate* unmanaged[Stdcall]<VkInstance_T*, VkDebugUtilsMessengerEXT_T*, VkAllocationCallbacks*, void>)
            Resolve(Utf8Name.FromLiteral(InstanceExtensionNames.DestroyDebugUtilsMessenger));
    }

    public delegate* unmanaged[Stdcall]<void> Resolve(Utf8Name name) =>
        Vk.vkGetInstanceProcAddr(_instance, name.Ptr);
}
