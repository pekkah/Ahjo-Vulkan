namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceGraphicsPipelineLibraryFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint graphicsPipelineLibrary;
}
