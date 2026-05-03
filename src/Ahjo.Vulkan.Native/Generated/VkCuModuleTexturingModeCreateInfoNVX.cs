namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCuModuleTexturingModeCreateInfoNVX
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint use64bitTexturing;
}
