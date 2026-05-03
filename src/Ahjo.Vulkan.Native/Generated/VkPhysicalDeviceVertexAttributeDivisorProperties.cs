namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVertexAttributeDivisorProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxVertexAttribDivisor;

    [NativeTypeName("VkBool32")]
    public uint supportsNonZeroFirstInstance;
}
