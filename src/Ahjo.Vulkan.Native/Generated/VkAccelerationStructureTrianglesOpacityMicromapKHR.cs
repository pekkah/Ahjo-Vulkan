namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAccelerationStructureTrianglesOpacityMicromapKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkIndexType indexType;

    [NativeTypeName("VkDeviceAddress")]
    public ulong indexBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong indexStride;

    [NativeTypeName("uint32_t")]
    public uint baseTriangle;

    [NativeTypeName("VkAccelerationStructureKHR")]
    public VkAccelerationStructureKHR_T* micromap;
}
