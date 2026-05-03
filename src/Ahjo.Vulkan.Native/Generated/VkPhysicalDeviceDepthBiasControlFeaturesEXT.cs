namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDepthBiasControlFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint depthBiasControl;

    [NativeTypeName("VkBool32")]
    public uint leastRepresentableValueForceUnormRepresentation;

    [NativeTypeName("VkBool32")]
    public uint floatRepresentation;

    [NativeTypeName("VkBool32")]
    public uint depthBiasExact;
}
