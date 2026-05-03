namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTextureCompressionASTC3DFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint textureCompressionASTC_3D;
}
