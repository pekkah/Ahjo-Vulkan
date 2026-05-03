namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoEncodeFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* dstBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong dstBufferOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong dstBufferRange;

    public VkVideoPictureResourceInfoKHR srcPictureResource;

    [NativeTypeName("const VkVideoReferenceSlotInfoKHR *")]
    public VkVideoReferenceSlotInfoKHR* pSetupReferenceSlot;

    [NativeTypeName("uint32_t")]
    public uint referenceSlotCount;

    [NativeTypeName("const VkVideoReferenceSlotInfoKHR *")]
    public VkVideoReferenceSlotInfoKHR* pReferenceSlots;

    [NativeTypeName("uint32_t")]
    public uint precedingExternallyEncodedBytes;
}
