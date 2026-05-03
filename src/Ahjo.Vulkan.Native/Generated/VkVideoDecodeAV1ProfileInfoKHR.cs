namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeAV1ProfileInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public StdVideoAV1Profile stdProfile;

    [NativeTypeName("VkBool32")]
    public uint filmGrainSupport;
}
