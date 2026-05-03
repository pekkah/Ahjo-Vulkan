namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCreateFlags2CreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCreateFlags2")]
    public ulong flags;
}
