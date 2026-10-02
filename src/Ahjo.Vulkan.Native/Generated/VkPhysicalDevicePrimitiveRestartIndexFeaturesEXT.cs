namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePrimitiveRestartIndexFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint primitiveRestartIndex;
}
