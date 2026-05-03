namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1SessionCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useMaxLevel;

    public StdVideoAV1Level maxLevel;
}
