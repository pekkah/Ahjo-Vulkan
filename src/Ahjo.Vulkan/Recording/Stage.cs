namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkPipelineStageFlags2</c> (synchronization2,
/// 1.3 core). 64-bit because sync2 expands the flag space past
/// <c>uint</c>; covers the bits the wrapper exercises today plus the
/// common scopes (transfer, color attachment, compute / fragment shader,
/// host).
/// </summary>
/// <remarks>
/// <b>Ray query has no stage of its own.</b> A ray-query traversal executes
/// inside whatever shader stage issues it, so it is synchronized against
/// <see cref="ComputeShader"/> or <see cref="FragmentShader"/> (paired with
/// <see cref="Access.AccelerationStructureRead"/>) — never against an
/// RT-pipeline stage. That is why
/// <c>VK_PIPELINE_STAGE_2_RAY_TRACING_SHADER_BIT_KHR</c> is deliberately
/// absent from this enum: it is the ray-tracing-<i>pipeline</i> stage, and
/// offering it to a ray-query consumer would invite a barrier that
/// synchronizes nothing they run. <see cref="AccelerationStructureBuild"/> and
/// <see cref="AccelerationStructureCopy"/>, by contrast, are the stages the
/// build and copy <em>commands</em> themselves execute in and are the producer
/// side of every acceleration-structure barrier.
/// </remarks>
[Flags]
public enum Stage : ulong
{
    None                  = 0,
    TopOfPipe             = 0x00000001,
    DrawIndirect          = 0x00000002,
    VertexInput           = 0x00000004,
    VertexShader          = 0x00000008,
    TessellationControl   = 0x00000010,
    TessellationEval      = 0x00000020,
    GeometryShader        = 0x00000040,
    FragmentShader        = 0x00000080,
    EarlyFragmentTests    = 0x00000100,
    LateFragmentTests     = 0x00000200,
    ColorAttachmentOutput = 0x00000400,
    ComputeShader         = 0x00000800,
    AllTransfer           = 0x00001000,
    BottomOfPipe          = 0x00002000,
    Host                  = 0x00004000,
    AllGraphics           = 0x00008000,
    AllCommands           = 0x00010000,

    /// <summary>
    /// <c>VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_KHR</c> — the
    /// stage <see cref="CommandRecorder.BuildAccelerationStructures"/>,
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/>
    /// <b>and <see cref="CommandRecorder.CopyAccelerationStructure"/></b>
    /// execute in. Pair with
    /// <see cref="Access.AccelerationStructureWrite"/> on the producer side of
    /// a build or copy, and with
    /// <see cref="Access.AccelerationStructureRead"/> when a later build, a
    /// compacted-size query or a compaction copy reads the result.
    /// </summary>
    /// <remarks>
    /// <b>This is the portable choice — use it for compaction too.</b> The
    /// spec lists <c>vkCmdCopyAccelerationStructureKHR</c> under this stage as
    /// well as under <see cref="AccelerationStructureCopy"/>, and it is
    /// available with plain <c>VK_KHR_acceleration_structure</c>, which
    /// <see cref="VulkanExtensions.KhrAccelerationStructure"/>'s enable recipe
    /// already gets you. <see cref="AccelerationStructureCopy"/> needs an
    /// extra extension most callers will not have enabled.
    /// </remarks>
    AccelerationStructureBuild = 0x02000000,

    /// <summary>
    /// <c>VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_COPY_BIT_KHR</c> — the
    /// narrower stage <see cref="CommandRecorder.CopyAccelerationStructure"/>
    /// executes in.
    /// </summary>
    /// <remarks>
    /// <para><b>Requires <c>VK_KHR_ray_tracing_maintenance1</c> and its
    /// <c>rayTracingMaintenance1</c> feature</b>, which is where this bit was
    /// added — <b>not</b> <c>VK_KHR_acceleration_structure</c>. Using it
    /// without that feature is a validation error
    /// (<c>VUID-VkMemoryBarrier2-srcStageMask-10752</c> /
    /// <c>-dstStageMask-10752</c>), and the enable recipe on
    /// <see cref="VulkanExtensions.KhrAccelerationStructure"/> does
    /// <em>not</em> include it. Enable that extension and feature yourself
    /// before using this bit.</para>
    /// <para><b>Prefer <see cref="AccelerationStructureBuild"/> for a
    /// compaction barrier.</b> It covers
    /// <c>vkCmdCopyAccelerationStructureKHR</c> too and costs no extra
    /// extension; this bit only buys a narrower scope. It is offered because
    /// a caller who <em>has</em> <c>rayTracingMaintenance1</c> should be able
    /// to express that narrower scope without dropping to the raw enum.</para>
    /// </remarks>
    AccelerationStructureCopy = 0x10000000,

    Copy                  = 0x100000000,
    Resolve               = 0x200000000,
    Blit                  = 0x400000000,
    Clear                 = 0x800000000,
    IndexInput            = 0x1000000000,
    VertexAttributeInput  = 0x2000000000,
}
