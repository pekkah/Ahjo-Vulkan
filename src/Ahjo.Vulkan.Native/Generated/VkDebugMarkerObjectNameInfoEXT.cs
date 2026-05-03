namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugMarkerObjectNameInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDebugReportObjectTypeEXT objectType;

    [NativeTypeName("uint64_t")]
    public ulong @object;

    [NativeTypeName("const char *")]
    public sbyte* pObjectName;
}
