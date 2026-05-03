namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH264CapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public StdVideoH264LevelIdc maxLevelIdc;

    public VkOffset2D fieldOffsetGranularity;
}
