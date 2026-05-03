namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceTexelBufferAlignmentProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDeviceSize")]
    public ulong storageTexelBufferOffsetAlignmentBytes;

    [NativeTypeName("VkBool32")]
    public uint storageTexelBufferOffsetSingleTexelAlignment;

    [NativeTypeName("VkDeviceSize")]
    public ulong uniformTexelBufferOffsetAlignmentBytes;

    [NativeTypeName("VkBool32")]
    public uint uniformTexelBufferOffsetSingleTexelAlignment;
}
