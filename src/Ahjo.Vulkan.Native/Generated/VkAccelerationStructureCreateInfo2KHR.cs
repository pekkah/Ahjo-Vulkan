namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureCreateInfo2KHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureCreateFlagsKHR")]
    public uint createFlags;

    public VkDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;

    public VkAccelerationStructureTypeKHR type;
}
