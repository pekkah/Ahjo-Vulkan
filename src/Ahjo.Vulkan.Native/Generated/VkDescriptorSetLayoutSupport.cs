namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetLayoutSupport
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint supported;
}
