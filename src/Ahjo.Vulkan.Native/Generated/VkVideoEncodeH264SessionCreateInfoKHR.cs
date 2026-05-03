namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264SessionCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useMaxLevelIdc;

    public StdVideoH264LevelIdc maxLevelIdc;
}
