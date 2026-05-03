namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoSessionCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint queueFamilyIndex;

    [NativeTypeName("VkVideoSessionCreateFlagsKHR")]
    public uint flags;

    [NativeTypeName("const VkVideoProfileInfoKHR *")]
    public VkVideoProfileInfoKHR* pVideoProfile;

    public VkFormat pictureFormat;

    public VkExtent2D maxCodedExtent;

    public VkFormat referencePictureFormat;

    [NativeTypeName("uint32_t")]
    public uint maxDpbSlots;

    [NativeTypeName("uint32_t")]
    public uint maxActiveReferencePictures;

    [NativeTypeName("const VkExtensionProperties *")]
    public VkExtensionProperties* pStdHeaderVersion;
}
