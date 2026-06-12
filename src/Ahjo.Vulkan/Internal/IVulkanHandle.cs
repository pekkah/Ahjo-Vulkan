using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Marker contract every wrapper handle type implements. Lets infrastructure
/// (debug naming, pool keys, generic helpers) dispatch on a handle's
/// Vulkan object type and round-trip the raw handle without reflection.
/// </summary>
/// <remarks>
/// <para><b>Invariants every implementer upholds.</b></para>
/// <list type="bullet">
///   <item><description><c>readonly struct</c> holding one (or two, for VMA-backed
///     resources) raw handles — pointer-typedef'd <c>Vk*_T*</c> for buffers,
///     images, and other dispatchable/non-dispatchable handles, plus an
///     optional <c>VmaAllocation_T*</c> for VMA-backed resources. A handle
///     may additionally carry <b>one managed reference field</b> for
///     creation-time metadata (e.g. <see cref="PipelineLayout"/>'s declared
///     push ranges/set layouts) — allocated at create time only, never per
///     frame.</description></item>
///   <item><description><c>default(T)</c> is a legal null handle. <see cref="IsNull"/>
///     returns <see langword="true"/> for it; passing a null handle to
///     destroy/free APIs is a no-op per Vulkan spec.</description></item>
///   <item><description><b>Ownership is part of the contract</b> (issue #118):
///     a handle destroys its Vulkan object on <c>Dispose</c> iff
///     <see cref="OwnsHandle"/> is <see langword="true"/>. <see cref="FromRaw"/>
///     and <c>default(T)</c> produce <i>borrowed</i> handles —
///     <see cref="OwnsHandle"/> is <see langword="false"/>, <c>Dispose</c> is
///     a no-op, and members that must dispatch through the owning
///     device/allocator throw <see cref="InvalidOperationException"/> instead
///     of passing a null owner to the loader. Pool-owned types
///     (<see cref="Fence"/>, semaphores, <see cref="DescriptorSet"/>) report
///     <see langword="false"/> always: the pool owns the object, the struct
///     never does.</description></item>
///   <item><description>Copy-by-value. Handles have no finalizer and don't own
///     unmanaged memory through SafeHandle; the wrapper assumes deterministic
///     <c>Dispose</c> at the call site.</description></item>
///   <item><description>Double-dispose is undefined behavior. The struct doesn't
///     guard against it because zeroing the field on dispose costs a write
///     on every release and offers no real safety on copy-by-value handles.</description></item>
/// </list>
/// <para><see cref="RawHandle"/> is <see cref="ulong"/> so 32-bit pointer-typedef'd
/// dispatchable handles (e.g. <c>VkInstance</c>) and 64-bit non-dispatchable
/// handles (e.g. <c>VkSemaphore</c>) round-trip through one slot.</para>
/// <para><see cref="FromRaw"/> exists as a static abstract entry point so
/// helpers like debug-naming or pool keys can construct a handle from a
/// raw value through constrained generics — calls devirtualize via the JIT.</para>
/// <para>The constraint is <c>struct</c>, not <c>unmanaged</c> (issue #118):
/// nothing in the wrapper requires handles to be pointer-pure (hot paths
/// unwrap to raw <c>nint</c> at method entry; pools store raw handles), and
/// <c>unmanaged</c> forced creation-time metadata into process-global side
/// tables keyed by raw pointer values. <c>struct</c> keeps copy-by-value and
/// the <c>default(T)</c>-is-null-handle convention.</para>
/// </remarks>
public interface IVulkanHandle<TSelf>
    where TSelf : struct, IVulkanHandle<TSelf>
{
    static abstract VkObjectType ObjectType { get; }

    static abstract TSelf FromRaw(nint handle);

    ulong RawHandle { get; }

    bool IsNull { get; }

    /// <summary>
    /// <see langword="true"/> when this handle owns the underlying Vulkan
    /// object — i.e. <c>Dispose</c> destroys it. <see langword="false"/> for
    /// <see cref="FromRaw"/>-constructed (borrowed) handles, <c>default</c>
    /// values, and pool-owned types.
    /// </summary>
    bool OwnsHandle { get; }
}
