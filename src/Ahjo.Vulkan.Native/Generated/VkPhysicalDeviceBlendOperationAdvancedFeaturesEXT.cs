namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceBlendOperationAdvancedFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint advancedBlendCoherentOperations;
}
