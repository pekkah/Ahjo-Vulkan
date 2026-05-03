namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassSampleLocationsBeginInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint attachmentInitialSampleLocationsCount;

    [NativeTypeName("const VkAttachmentSampleLocationsEXT *")]
    public VkAttachmentSampleLocationsEXT* pAttachmentInitialSampleLocations;

    [NativeTypeName("uint32_t")]
    public uint postSubpassSampleLocationsCount;

    [NativeTypeName("const VkSubpassSampleLocationsEXT *")]
    public VkSubpassSampleLocationsEXT* pPostSubpassSampleLocations;
}
