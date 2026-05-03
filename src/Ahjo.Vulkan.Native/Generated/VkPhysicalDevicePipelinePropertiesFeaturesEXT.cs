namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelinePropertiesFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint pipelinePropertiesIdentifier;
}
