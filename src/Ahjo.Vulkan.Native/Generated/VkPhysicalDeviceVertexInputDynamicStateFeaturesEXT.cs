namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVertexInputDynamicStateFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint vertexInputDynamicState;
}
