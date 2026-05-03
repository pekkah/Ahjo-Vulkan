namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineInputAssemblyStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineInputAssemblyStateCreateFlags")]
    public uint flags;

    public VkPrimitiveTopology topology;

    [NativeTypeName("VkBool32")]
    public uint primitiveRestartEnable;
}
