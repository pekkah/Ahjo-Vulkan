namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong compactedSize;

    public VkAccelerationStructureInfoNV info;
}
