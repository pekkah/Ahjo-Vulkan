using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// GPU-only binary semaphore. <c>readonly struct</c>; copy-by-value;
/// lifetime owned by <see cref="SemaphorePool"/>. There is intentionally
/// no <c>Wait</c> API on the host side — binary semaphores are signaled
/// and waited on by submissions, never by the CPU. For CPU-side waiting
/// use <see cref="Fence"/> or <see cref="TimelineSemaphore"/>.
/// </summary>
/// <remarks>
/// <c>default(BinarySemaphore)</c> is a legal null handle. Distinct type
/// from <see cref="TimelineSemaphore"/> because the use sites
/// (image-acquired / rendering-done vs. cross-frame ordering) are not
/// interchangeable — encoding that in the type system catches misuse at
/// compile time instead of validation runtime.
/// </remarks>
public readonly unsafe struct BinarySemaphore : IVulkanHandle<BinarySemaphore>
{
    public readonly VkSemaphore_T* Handle;

    internal BinarySemaphore(VkSemaphore_T* handle) { Handle = handle; }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_SEMAPHORE;
    public static BinarySemaphore FromRaw(nint handle) => new((VkSemaphore_T*)handle);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <summary>
    /// Always <see langword="false"/>: <see cref="SemaphorePool"/> owns the
    /// <c>VkSemaphore</c>'s lifetime; the struct never destroys it.
    /// </summary>
    public bool OwnsHandle => false;
}
