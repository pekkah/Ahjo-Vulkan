namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAntiLagDataAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkAntiLagModeAMD mode;

    [NativeTypeName("uint32_t")]
    public uint maxFPS;

    [NativeTypeName("const VkAntiLagPresentationInfoAMD *")]
    public VkAntiLagPresentationInfoAMD* pPresentationInfo;
}
