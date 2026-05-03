namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePrimitiveTopologyListRestartFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint primitiveTopologyListRestart;

    [NativeTypeName("VkBool32")]
    public uint primitiveTopologyPatchListRestart;
}
