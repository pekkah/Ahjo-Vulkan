namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceMemoryOverallocationCreateInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkMemoryOverallocationBehaviorAMD overallocationBehavior;
}
