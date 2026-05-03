namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVertexAttributeRobustnessFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint vertexAttributeRobustness;
}
