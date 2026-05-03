namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceGraphicsPipelineLibraryPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint graphicsPipelineLibraryFastLinking;

    [NativeTypeName("VkBool32")]
    public uint graphicsPipelineLibraryIndependentInterpolationDecoration;
}
