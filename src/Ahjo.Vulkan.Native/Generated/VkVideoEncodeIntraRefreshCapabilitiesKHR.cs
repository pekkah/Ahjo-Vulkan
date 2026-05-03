namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeIntraRefreshCapabilitiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeIntraRefreshModeFlagsKHR")]
    public uint intraRefreshModes;

    [NativeTypeName("uint32_t")]
    public uint maxIntraRefreshCycleDuration;

    [NativeTypeName("uint32_t")]
    public uint maxIntraRefreshActiveReferencePictures;

    [NativeTypeName("VkBool32")]
    public uint partitionIndependentIntraRefreshRegions;

    [NativeTypeName("VkBool32")]
    public uint nonRectangularIntraRefreshRegions;
}
