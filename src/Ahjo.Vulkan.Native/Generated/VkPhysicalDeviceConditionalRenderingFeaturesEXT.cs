namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceConditionalRenderingFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint conditionalRendering;

    [NativeTypeName("VkBool32")]
    public uint inheritedConditionalRendering;
}
