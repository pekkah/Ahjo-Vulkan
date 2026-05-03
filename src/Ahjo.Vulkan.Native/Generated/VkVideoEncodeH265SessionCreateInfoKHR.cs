namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265SessionCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useMaxLevelIdc;

    public StdVideoH265LevelIdc maxLevelIdc;
}
