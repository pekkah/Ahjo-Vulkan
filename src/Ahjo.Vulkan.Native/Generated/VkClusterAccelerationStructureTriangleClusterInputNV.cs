namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkClusterAccelerationStructureTriangleClusterInputNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkFormat vertexFormat;

    [NativeTypeName("uint32_t")]
    public uint maxGeometryIndexValue;

    [NativeTypeName("uint32_t")]
    public uint maxClusterUniqueGeometryCount;

    [NativeTypeName("uint32_t")]
    public uint maxClusterTriangleCount;

    [NativeTypeName("uint32_t")]
    public uint maxClusterVertexCount;

    [NativeTypeName("uint32_t")]
    public uint maxTotalTriangleCount;

    [NativeTypeName("uint32_t")]
    public uint maxTotalVertexCount;

    [NativeTypeName("uint32_t")]
    public uint minPositionTruncateBitCount;
}
