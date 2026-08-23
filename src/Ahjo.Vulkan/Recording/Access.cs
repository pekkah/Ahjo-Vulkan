namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkAccessFlags2</c> (synchronization2,
/// 1.3 core). 64-bit because sync2 expands the flag space past
/// <c>uint</c>. Only the bits the wrapper exercises today are listed;
/// extend as needed.
/// </summary>
[Flags]
public enum Access : ulong
{
    None                          = 0,
    IndirectCommandRead           = 0x00000001,
    IndexRead                     = 0x00000002,
    VertexAttributeRead           = 0x00000004,
    UniformRead                   = 0x00000008,
    InputAttachmentRead           = 0x00000010,
    ShaderRead                    = 0x00000020,
    ShaderWrite                   = 0x00000040,
    ColorAttachmentRead           = 0x00000080,
    ColorAttachmentWrite          = 0x00000100,
    DepthStencilAttachmentRead    = 0x00000200,
    DepthStencilAttachmentWrite   = 0x00000400,
    TransferRead                  = 0x00000800,
    TransferWrite                 = 0x00001000,
    HostRead                      = 0x00002000,
    HostWrite                     = 0x00004000,
    MemoryRead                    = 0x00008000,
    MemoryWrite                   = 0x00010000,

    /// <summary>
    /// <c>VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_KHR</c> — reads of an
    /// acceleration structure: a ray-query traversal (paired with the shader
    /// stage that runs it, <see cref="Stage.ComputeShader"/> /
    /// <see cref="Stage.FragmentShader"/>), and a compacted-size query, a
    /// compaction copy's source or an
    /// <see cref="AccelerationStructureBuildMode.Update"/> build's source
    /// (all paired with <see cref="Stage.AccelerationStructureBuild"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="Stage.AccelerationStructureBuild"/> is the portable stage
    /// for the copy case as well — <see cref="Stage.AccelerationStructureCopy"/>
    /// is valid here too but requires <c>VK_KHR_ray_tracing_maintenance1</c>,
    /// which this wrapper's enable recipe does not turn on.
    /// </remarks>
    AccelerationStructureRead     = 0x00200000,

    /// <summary>
    /// <c>VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_KHR</c> — writes to an
    /// acceleration structure: a build's <em>or</em> a copy's destination,
    /// both paired with <see cref="Stage.AccelerationStructureBuild"/>. Every
    /// consumer of a freshly built structure needs a barrier from this to
    /// <see cref="AccelerationStructureRead"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Stage.AccelerationStructureCopy"/> is also valid for the
    /// copy case and is narrower, but it requires
    /// <c>VK_KHR_ray_tracing_maintenance1</c> — which this wrapper's enable
    /// recipe does not turn on, so
    /// <see cref="Stage.AccelerationStructureBuild"/> is the portable pairing.
    /// </remarks>
    AccelerationStructureWrite    = 0x00400000,

    ShaderSampledRead             = 0x100000000,
    ShaderStorageRead             = 0x200000000,
    ShaderStorageWrite            = 0x400000000,
}
