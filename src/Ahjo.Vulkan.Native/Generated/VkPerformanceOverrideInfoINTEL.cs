namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPerformanceOverrideInfoINTEL
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkPerformanceOverrideTypeINTEL type;

    [NativeTypeName("VkBool32")]
    public uint enable;

    [NativeTypeName("uint64_t")]
    public ulong parameter;
}
