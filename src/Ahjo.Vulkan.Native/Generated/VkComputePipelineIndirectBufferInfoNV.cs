namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkComputePipelineIndirectBufferInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceAddress")]
    public ulong deviceAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong size;

    [NativeTypeName("VkDeviceAddress")]
    public ulong pipelineDeviceAddressCaptureReplay;
}
