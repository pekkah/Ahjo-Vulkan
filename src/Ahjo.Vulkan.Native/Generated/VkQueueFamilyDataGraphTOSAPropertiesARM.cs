namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyDataGraphTOSAPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint profileCount;

    [NativeTypeName("const VkDataGraphTOSANameQualityARM *")]
    public VkDataGraphTOSANameQualityARM* pProfiles;

    [NativeTypeName("uint32_t")]
    public uint extensionCount;

    [NativeTypeName("const VkDataGraphTOSANameQualityARM *")]
    public VkDataGraphTOSANameQualityARM* pExtensions;

    public VkDataGraphTOSALevelARM level;
}
