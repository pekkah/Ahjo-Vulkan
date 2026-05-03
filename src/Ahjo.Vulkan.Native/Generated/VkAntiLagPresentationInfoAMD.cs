namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAntiLagPresentationInfoAMD
{
    public VkStructureType sType;

    public void* pNext;

    public VkAntiLagStageAMD stage;

    [NativeTypeName("uint64_t")]
    public ulong frameIndex;
}
