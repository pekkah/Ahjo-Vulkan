namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1GopRemainingFrameInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint useGopRemainingFrames;

    [NativeTypeName("uint32_t")]
    public uint gopRemainingIntra;

    [NativeTypeName("uint32_t")]
    public uint gopRemainingPredictive;

    [NativeTypeName("uint32_t")]
    public uint gopRemainingBipredictive;
}
