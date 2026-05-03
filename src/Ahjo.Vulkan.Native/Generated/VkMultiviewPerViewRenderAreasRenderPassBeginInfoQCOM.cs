namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMultiviewPerViewRenderAreasRenderPassBeginInfoQCOM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint perViewRenderAreaCount;

    [NativeTypeName("const VkRect2D *")]
    public VkRect2D* pPerViewRenderAreas;
}
