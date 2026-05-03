namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccelerationStructureCreateFlagsKHR")]
    public uint createFlags;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    public VkAccelerationStructureTypeKHR type;

    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;
}
