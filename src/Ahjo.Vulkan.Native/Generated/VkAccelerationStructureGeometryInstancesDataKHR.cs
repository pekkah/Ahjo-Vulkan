namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureGeometryInstancesDataKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint arrayOfPointers;

    public VkDeviceOrHostAddressConstKHR data;
}
