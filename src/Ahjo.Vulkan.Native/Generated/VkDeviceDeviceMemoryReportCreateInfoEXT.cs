namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceDeviceMemoryReportCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceMemoryReportFlagsEXT")]
    public uint flags;

    [NativeTypeName("PFN_vkDeviceMemoryReportCallbackEXT")]
    public delegate* unmanaged[Stdcall]<VkDeviceMemoryReportCallbackDataEXT*, void*, void> pfnUserCallback;

    public void* pUserData;
}
