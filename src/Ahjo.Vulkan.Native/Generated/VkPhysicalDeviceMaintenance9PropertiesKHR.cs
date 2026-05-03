namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance9PropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint image2DViewOf3DSparse;

    public VkDefaultVertexAttributeValueKHR defaultVertexAttributeValue;
}
