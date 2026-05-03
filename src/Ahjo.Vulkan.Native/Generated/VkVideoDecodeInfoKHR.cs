namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkVideoDecodeFlagsKHR")]
    public uint flags;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* srcBuffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong srcBufferOffset;

    [NativeTypeName("VkDeviceSize")]
    public ulong srcBufferRange;

    public VkVideoPictureResourceInfoKHR dstPictureResource;

    [NativeTypeName("const VkVideoReferenceSlotInfoKHR *")]
    public VkVideoReferenceSlotInfoKHR* pSetupReferenceSlot;

    [NativeTypeName("uint32_t")]
    public uint referenceSlotCount;

    [NativeTypeName("const VkVideoReferenceSlotInfoKHR *")]
    public VkVideoReferenceSlotInfoKHR* pReferenceSlots;
}
