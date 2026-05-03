namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTextureCompressionASTCHDRFeatures
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint textureCompressionASTC_HDR;
}
