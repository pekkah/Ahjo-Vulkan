namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceNestedCommandBufferFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint nestedCommandBuffer;

    [NativeTypeName("VkBool32")]
    public uint nestedCommandBufferRendering;

    [NativeTypeName("VkBool32")]
    public uint nestedCommandBufferSimultaneousUse;
}
