namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265SessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxStdVPSCount;

    [NativeTypeName("uint32_t")]
    public uint maxStdSPSCount;

    [NativeTypeName("uint32_t")]
    public uint maxStdPPSCount;

    [NativeTypeName("const VkVideoEncodeH265SessionParametersAddInfoKHR *")]
    public VkVideoEncodeH265SessionParametersAddInfoKHR* pParametersAddInfo;
}
