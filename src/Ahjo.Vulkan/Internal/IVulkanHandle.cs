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
///     optional VMA <see cref="Allocation"/>.</description></item>
///   <item><description><c>default(T)</c> is a legal null handle. <see cref="IsNull"/>
///     returns <see langword="true"/> for it; passing a null handle to
///     destroy/free APIs is a no-op per Vulkan spec.</description></item>
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
/// </remarks>
public interface IVulkanHandle<TSelf>
    where TSelf : unmanaged, IVulkanHandle<TSelf>
{
    static abstract VkObjectType ObjectType { get; }

    static abstract TSelf FromRaw(nint handle);

    ulong RawHandle { get; }

    bool IsNull { get; }
}
