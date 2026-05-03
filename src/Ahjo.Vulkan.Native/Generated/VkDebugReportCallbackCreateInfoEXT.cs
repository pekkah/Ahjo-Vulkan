namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugReportCallbackCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDebugReportFlagsEXT")]
    public uint flags;

    [NativeTypeName("PFN_vkDebugReportCallbackEXT")]
    public delegate* unmanaged[Stdcall]<uint, VkDebugReportObjectTypeEXT, ulong, nuint, int, sbyte*, sbyte*, void*, uint> pfnCallback;

    public void* pUserData;
}
