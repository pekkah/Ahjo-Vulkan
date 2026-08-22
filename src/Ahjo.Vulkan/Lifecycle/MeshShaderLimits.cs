namespace Ahjo.Vulkan;

/// <summary>
/// The <c>VkPhysicalDeviceMeshShaderPropertiesEXT</c> fields that bound a
/// <see cref="CommandRecorder.DrawMeshTasks"/> dispatch — the subset a caller
/// issuing mesh draws actually has to obey. Read with
/// <see cref="PhysicalDevice.TryGetMeshShaderLimits"/>.
/// </summary>
/// <remarks>
/// <para><b>Task limits or mesh limits, never both.</b> Which half of this type
/// applies is decided by the <i>bound pipeline</i>, not by the draw call. When
/// the pipeline has a task stage
/// (<see cref="GraphicsPipelineBuilder.WithTaskStage"/>), each
/// <c>groupCount*</c> argument of
/// <see cref="CommandRecorder.DrawMeshTasks"/> is bounded by the
/// <c>MaxTask*</c> members and their product by
/// <see cref="MaxTaskWorkGroupTotalCount"/>
/// (<c>VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322</c>/<c>-07323</c>/<c>-07324</c>/<c>-07325</c>).
/// When it does not, the identical bounds apply against the <c>MaxMesh*</c>
/// members and <see cref="MaxMeshWorkGroupTotalCount"/>
/// (<c>-07326</c>/<c>-07327</c>/<c>-07328</c>/<c>-07329</c>). The two sets
/// differ on real hardware, so reading the wrong one is a silent VUID
/// violation with undefined behaviour — the recorder deliberately does not
/// track the bound pipeline's stages and therefore cannot pick for you. Making
/// that choice nameable at the call site is the entire reason this type exists
/// rather than the raw struct.</para>
/// <para><b>Narrow on purpose</b>, the same policy as
/// <see cref="DeviceMemoryLimits"/>. Left out: <c>maxMeshOutputVertices</c>,
/// <c>maxMeshOutputPrimitives</c>, <c>maxTaskWorkGroupSize</c> /
/// <c>maxMeshWorkGroupSize</c>, the payload and shared-memory sizes, and the
/// four <c>prefers*</c> hints. Those are shader-<i>authoring</i> constants —
/// they bound an <c>out</c> declaration or a <c>local_size_*</c> in the shader
/// source — not per-draw dispatch bounds, and a caller who needs them reads the
/// raw struct in one line through
/// <c>PhysicalDevice.TryGetProperties&lt;VkPhysicalDeviceMeshShaderPropertiesEXT&gt;</c>.
/// Widening the projection later is additive.</para>
/// <para>The X/Y/Z members flatten the generated <c>[InlineArray(3)]</c>
/// buffers <c>maxTaskWorkGroupCount</c> / <c>maxMeshWorkGroupCount</c>, index
/// 0/1/2 in that order.</para>
/// <para>This type lives in <c>Lifecycle/</c> rather than <c>Recording/</c>: it
/// is a device-capability record produced by <see cref="PhysicalDevice"/> at
/// setup time, and <c>Recording/</c> is the zero-per-frame-allocation directory
/// where a setup-time record would misfile.</para>
/// </remarks>
public readonly record struct MeshShaderLimits
{
    /// <summary>
    /// Maximum <c>groupCountX</c> when the bound pipeline HAS a task stage
    /// (<c>VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322</c>). Ignore this member
    /// and read <see cref="MaxMeshWorkGroupCountX"/> when it does not.
    /// </summary>
    public uint MaxTaskWorkGroupCountX { get; init; }

    /// <summary>
    /// Maximum <c>groupCountY</c> when the bound pipeline HAS a task stage
    /// (<c>-07323</c>). See <see cref="MaxMeshWorkGroupCountY"/> otherwise.
    /// </summary>
    public uint MaxTaskWorkGroupCountY { get; init; }

    /// <summary>
    /// Maximum <c>groupCountZ</c> when the bound pipeline HAS a task stage
    /// (<c>-07324</c>). See <see cref="MaxMeshWorkGroupCountZ"/> otherwise.
    /// </summary>
    public uint MaxTaskWorkGroupCountZ { get; init; }

    /// <summary>
    /// Maximum <c>groupCountX × groupCountY × groupCountZ</c> when the bound
    /// pipeline HAS a task stage (<c>-07325</c>). Bounded independently of the
    /// per-axis limits — a dispatch can satisfy all three and still exceed
    /// this. See <see cref="MaxMeshWorkGroupTotalCount"/> otherwise.
    /// </summary>
    public uint MaxTaskWorkGroupTotalCount { get; init; }

    /// <summary>
    /// Maximum total invocations in one task workgroup — the product of the
    /// task shader's <c>local_size_*</c>. A shader-side bound listed here
    /// because it pairs with the task counts above; the draw call itself does
    /// not carry it.
    /// </summary>
    public uint MaxTaskWorkGroupInvocations { get; init; }

    /// <summary>
    /// Maximum <c>groupCountX</c> when the bound pipeline has NO task stage
    /// (<c>VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07326</c>). Ignore this member
    /// and read <see cref="MaxTaskWorkGroupCountX"/> when it does.
    /// </summary>
    public uint MaxMeshWorkGroupCountX { get; init; }

    /// <summary>
    /// Maximum <c>groupCountY</c> when the bound pipeline has NO task stage
    /// (<c>-07327</c>). See <see cref="MaxTaskWorkGroupCountY"/> otherwise.
    /// </summary>
    public uint MaxMeshWorkGroupCountY { get; init; }

    /// <summary>
    /// Maximum <c>groupCountZ</c> when the bound pipeline has NO task stage
    /// (<c>-07328</c>). See <see cref="MaxTaskWorkGroupCountZ"/> otherwise.
    /// </summary>
    public uint MaxMeshWorkGroupCountZ { get; init; }

    /// <summary>
    /// Maximum <c>groupCountX × groupCountY × groupCountZ</c> when the bound
    /// pipeline has NO task stage (<c>-07329</c>). Bounded independently of the
    /// per-axis limits. See <see cref="MaxTaskWorkGroupTotalCount"/> otherwise.
    /// </summary>
    public uint MaxMeshWorkGroupTotalCount { get; init; }

    /// <summary>
    /// Maximum total invocations in one mesh workgroup — the product of the
    /// mesh shader's <c>local_size_*</c>. A shader-side bound listed here
    /// because it pairs with the mesh counts above.
    /// </summary>
    public uint MaxMeshWorkGroupInvocations { get; init; }
}
