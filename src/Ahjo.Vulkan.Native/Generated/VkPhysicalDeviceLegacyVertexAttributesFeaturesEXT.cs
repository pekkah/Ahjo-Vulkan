namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLegacyVertexAttributesFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint legacyVertexAttributes;
}
