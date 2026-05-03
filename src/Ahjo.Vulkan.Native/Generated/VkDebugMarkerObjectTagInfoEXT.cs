namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDebugMarkerObjectTagInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDebugReportObjectTypeEXT objectType;

    [NativeTypeName("uint64_t")]
    public ulong @object;

    [NativeTypeName("uint64_t")]
    public ulong tagName;

    [NativeTypeName("size_t")]
    public nuint tagSize;

    [NativeTypeName("const void *")]
    public void* pTag;
}
