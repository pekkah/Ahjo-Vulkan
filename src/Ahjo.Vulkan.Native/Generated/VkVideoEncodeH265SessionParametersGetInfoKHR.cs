namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265SessionParametersGetInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint writeStdVPS;

    [NativeTypeName("VkBool32")]
    public uint writeStdSPS;

    [NativeTypeName("VkBool32")]
    public uint writeStdPPS;

    [NativeTypeName("uint32_t")]
    public uint stdVPSId;

    [NativeTypeName("uint32_t")]
    public uint stdSPSId;

    [NativeTypeName("uint32_t")]
    public uint stdPPSId;
}
