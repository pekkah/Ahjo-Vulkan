namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1QuantizationMapCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("int32_t")]
    public int minQIndexDelta;

    [NativeTypeName("int32_t")]
    public int maxQIndexDelta;
}
