namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceShader64BitIndexingFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint shader64BitIndexing;
}
