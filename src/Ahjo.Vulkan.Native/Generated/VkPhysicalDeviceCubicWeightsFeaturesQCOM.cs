namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceCubicWeightsFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint selectableCubicWeights;
}
