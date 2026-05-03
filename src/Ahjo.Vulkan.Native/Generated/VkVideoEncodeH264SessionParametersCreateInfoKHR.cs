namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264SessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxStdSPSCount;

    [NativeTypeName("uint32_t")]
    public uint maxStdPPSCount;

    [NativeTypeName("const VkVideoEncodeH264SessionParametersAddInfoKHR *")]
    public VkVideoEncodeH264SessionParametersAddInfoKHR* pParametersAddInfo;
}
