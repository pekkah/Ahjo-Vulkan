namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkInitializePerformanceApiInfoINTEL
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public void* pUserData;
}
