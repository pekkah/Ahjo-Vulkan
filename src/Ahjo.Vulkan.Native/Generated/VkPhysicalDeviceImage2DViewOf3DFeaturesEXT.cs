namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImage2DViewOf3DFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint image2DViewOf3D;

    [NativeTypeName("VkBool32")]
    public uint sampler2DViewOf3D;
}
