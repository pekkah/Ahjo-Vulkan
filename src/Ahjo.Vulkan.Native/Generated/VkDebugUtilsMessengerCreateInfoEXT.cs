namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugUtilsMessengerCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDebugUtilsMessengerCreateFlagsEXT")]
    public uint flags;

    [NativeTypeName("VkDebugUtilsMessageSeverityFlagsEXT")]
    public uint messageSeverity;

    [NativeTypeName("VkDebugUtilsMessageTypeFlagsEXT")]
    public uint messageType;

    [NativeTypeName("PFN_vkDebugUtilsMessengerCallbackEXT")]
    public delegate* unmanaged[Stdcall]<VkDebugUtilsMessageSeverityFlagBitsEXT, uint, VkDebugUtilsMessengerCallbackDataEXT*, void*, uint> pfnUserCallback;

    public void* pUserData;
}
