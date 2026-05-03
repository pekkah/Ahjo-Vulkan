namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoSessionMemoryRequirementsKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint memoryBindIndex;

    public VkMemoryRequirements memoryRequirements;
}
