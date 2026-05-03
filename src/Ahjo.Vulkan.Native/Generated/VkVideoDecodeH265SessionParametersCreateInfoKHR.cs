namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH265SessionParametersCreateInfoKHR
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

    [NativeTypeName("const VkVideoDecodeH265SessionParametersAddInfoKHR *")]
    public VkVideoDecodeH265SessionParametersAddInfoKHR* pParametersAddInfo;
}
