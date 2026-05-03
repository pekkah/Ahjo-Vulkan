namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayTracingPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupHandleSize;

    [NativeTypeName("uint32_t")]
    public uint maxRecursionDepth;

    [NativeTypeName("uint32_t")]
    public uint maxShaderGroupStride;

    [NativeTypeName("uint32_t")]
    public uint shaderGroupBaseAlignment;

    [NativeTypeName("uint64_t")]
    public ulong maxGeometryCount;

    [NativeTypeName("uint64_t")]
    public ulong maxInstanceCount;

    [NativeTypeName("uint64_t")]
    public ulong maxTriangleCount;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetAccelerationStructures;
}
