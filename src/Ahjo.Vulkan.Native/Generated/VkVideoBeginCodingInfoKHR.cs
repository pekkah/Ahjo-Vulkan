namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoBeginCodingInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoBeginCodingFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkVideoSessionKHR")]
    public VkVideoSessionKHR_T* videoSession;

    [NativeTypeName("VkVideoSessionParametersKHR")]
    public VkVideoSessionParametersKHR_T* videoSessionParameters;

    [NativeTypeName("uint32_t")]
    public uint referenceSlotCount;

    [NativeTypeName("const VkVideoReferenceSlotInfoKHR *")]
    public VkVideoReferenceSlotInfoKHR* pReferenceSlots;
}
