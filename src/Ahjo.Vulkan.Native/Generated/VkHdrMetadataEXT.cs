namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHdrMetadataEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkXYColorEXT displayPrimaryRed;

    public VkXYColorEXT displayPrimaryGreen;

    public VkXYColorEXT displayPrimaryBlue;

    public VkXYColorEXT whitePoint;

    public float maxLuminance;

    public float minLuminance;

    public float maxContentLightLevel;

    public float maxFrameAverageLightLevel;
}
