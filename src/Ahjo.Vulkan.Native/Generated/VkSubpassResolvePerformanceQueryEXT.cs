namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassResolvePerformanceQueryEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint optimal;
}
