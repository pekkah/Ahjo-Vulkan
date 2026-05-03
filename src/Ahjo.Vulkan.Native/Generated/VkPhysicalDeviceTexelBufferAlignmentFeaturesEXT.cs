namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTexelBufferAlignmentFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint texelBufferAlignment;
}
