namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCooperativeVectorFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint cooperativeVector;

    [NativeTypeName("VkBool32")]
    public uint cooperativeVectorTraining;
}
