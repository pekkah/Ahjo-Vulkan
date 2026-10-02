namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageProcessing3FeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint imageGatherLinear;

    [NativeTypeName("VkBool32")]
    public uint imageGatherExtendedModes;

    [NativeTypeName("VkBool32")]
    public uint blockMatchExtendedClampToEdge;
}
