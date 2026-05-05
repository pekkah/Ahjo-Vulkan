namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkBufferUsageFlagBits</c>. The bit values
/// are identical to the underlying enum so a
/// <see cref="System.Runtime.InteropServices.MemoryMarshal.As{T,U}(System.Span{T})"/>
/// or simple cast roundtrips into the <c>VkBufferUsageFlags</c> field VMA
/// expects with no conversion cost.
/// </summary>
[Flags]
public enum BufferUsage : uint
{
    None                                = 0,
    TransferSrc                         = 0x00000001,
    TransferDst                         = 0x00000002,
    UniformTexelBuffer                  = 0x00000004,
    StorageTexelBuffer                  = 0x00000008,
    UniformBuffer                       = 0x00000010,
    StorageBuffer                       = 0x00000020,
    IndexBuffer                         = 0x00000040,
    VertexBuffer                        = 0x00000080,
    IndirectBuffer                      = 0x00000100,
    ShaderDeviceAddress                 = 0x00020000,
    AccelerationStructureBuildInputReadOnly = 0x00080000,
    AccelerationStructureStorage        = 0x00100000,
    ShaderBindingTable                  = 0x00000400,
}
