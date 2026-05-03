namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLegacyVertexAttributesPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint nativeUnalignedPerformance;
}
