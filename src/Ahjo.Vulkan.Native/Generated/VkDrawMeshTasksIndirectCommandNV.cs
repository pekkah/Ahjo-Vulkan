namespace Ahjo.Vulkan.Native;

public partial struct VkDrawMeshTasksIndirectCommandNV
{
    [NativeTypeName("uint32_t")]
    public uint taskCount;

    [NativeTypeName("uint32_t")]
    public uint firstTask;
}
