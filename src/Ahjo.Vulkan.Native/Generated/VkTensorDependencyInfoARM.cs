namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkTensorDependencyInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint tensorMemoryBarrierCount;

    [NativeTypeName("const VkTensorMemoryBarrierARM *")]
    public VkTensorMemoryBarrierARM* pTensorMemoryBarriers;
}
