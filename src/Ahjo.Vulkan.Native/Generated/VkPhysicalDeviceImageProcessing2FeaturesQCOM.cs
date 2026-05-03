namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceImageProcessing2FeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint textureBlockMatch2;
}
