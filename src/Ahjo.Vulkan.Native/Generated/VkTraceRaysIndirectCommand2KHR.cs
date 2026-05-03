namespace Ahjo.Vulkan.Native;

public partial struct VkTraceRaysIndirectCommand2KHR
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong raygenShaderRecordAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong raygenShaderRecordSize;

    [NativeTypeName("VkDeviceAddress")]
    public ulong missShaderBindingTableAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong missShaderBindingTableSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong missShaderBindingTableStride;

    [NativeTypeName("VkDeviceAddress")]
    public ulong hitShaderBindingTableAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong hitShaderBindingTableSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong hitShaderBindingTableStride;

    [NativeTypeName("VkDeviceAddress")]
    public ulong callableShaderBindingTableAddress;

    [NativeTypeName("VkDeviceSize")]
    public ulong callableShaderBindingTableSize;

    [NativeTypeName("VkDeviceSize")]
    public ulong callableShaderBindingTableStride;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint depth;
}
