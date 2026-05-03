namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTilePropertiesFeaturesQCOM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint tileProperties;
}
