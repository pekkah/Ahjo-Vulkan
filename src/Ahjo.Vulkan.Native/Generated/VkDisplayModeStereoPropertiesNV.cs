namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayModeStereoPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint hdmi3DSupported;
}
