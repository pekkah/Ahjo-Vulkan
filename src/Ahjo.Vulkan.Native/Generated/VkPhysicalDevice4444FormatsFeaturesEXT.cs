namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevice4444FormatsFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint formatA4R4G4B4;

    [NativeTypeName("VkBool32")]
    public uint formatA4B4G4R4;
}
