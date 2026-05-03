namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceClusterCullingShaderVrsFeaturesHUAWEI
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint clusterShadingRate;
}
