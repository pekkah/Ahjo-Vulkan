namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265GopRemainingFrameInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useGopRemainingFrames;

    [NativeTypeName("uint32_t")]
    public uint gopRemainingI;

    [NativeTypeName("uint32_t")]
    public uint gopRemainingP;

    [NativeTypeName("uint32_t")]
    public uint gopRemainingB;
}
