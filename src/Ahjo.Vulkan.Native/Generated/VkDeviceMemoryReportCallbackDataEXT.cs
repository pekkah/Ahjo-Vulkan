namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceMemoryReportCallbackDataEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceMemoryReportFlagsEXT")]
    public uint flags;

    public VkDeviceMemoryReportEventTypeEXT type;

    [NativeTypeName("uint64_t")]
    public ulong memoryObjectId;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    public VkObjectType objectType;

    [NativeTypeName("uint64_t")]
    public ulong objectHandle;

    [NativeTypeName("uint32_t")]
    public uint heapIndex;
}
