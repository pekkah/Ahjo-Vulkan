namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH264SessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxStdSPSCount;

    [NativeTypeName("uint32_t")]
    public uint maxStdPPSCount;

    [NativeTypeName("const VkVideoDecodeH264SessionParametersAddInfoKHR *")]
    public VkVideoDecodeH264SessionParametersAddInfoKHR* pParametersAddInfo;
}
