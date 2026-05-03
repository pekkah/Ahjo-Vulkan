namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoReferenceSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("int32_t")]
    public int slotIndex;

    [NativeTypeName("const VkVideoPictureResourceInfoKHR *")]
    public VkVideoPictureResourceInfoKHR* pPictureResource;
}
