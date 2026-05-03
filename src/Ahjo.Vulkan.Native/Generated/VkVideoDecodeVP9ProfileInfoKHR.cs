namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeVP9ProfileInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public StdVideoVP9Profile stdProfile;
}
