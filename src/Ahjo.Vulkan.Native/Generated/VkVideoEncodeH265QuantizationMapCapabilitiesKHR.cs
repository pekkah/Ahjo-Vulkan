namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265QuantizationMapCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("int32_t")]
    public int minQpDelta;

    [NativeTypeName("int32_t")]
    public int maxQpDelta;
}
