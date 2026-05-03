namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupRenderPassBeginInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint deviceMask;

    [NativeTypeName("uint32_t")]
    public uint deviceRenderAreaCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pDeviceRenderAreas;
}
