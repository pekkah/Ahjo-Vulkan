namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkConditionalRenderingBeginInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* buffer;

    [NativeTypeName("VkDeviceSize")]
    public ulong offset;

    [NativeTypeName("VkConditionalRenderingFlagsEXT")]
    public uint flags;
}
