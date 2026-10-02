namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeMatrixInfo2EXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkScopeKHR scope;

    [NativeTypeName("uint32_t")]
    public uint invocations;

    [NativeTypeName("uint32_t")]
    public uint subgroupSize;

    [NativeTypeName("VkCooperativeMatrixFlagsEXT")]
    public uint flags;
}
