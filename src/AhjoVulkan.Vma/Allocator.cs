namespace Ahjo.Vulkan.Vma;

/// <summary>
/// Top-level VMA context. One per <c>VkDevice</c>. Owns memory pools,
/// statistics, and the dynamic Vulkan function table VMA uses to call
/// down into the loader.
/// </summary>
/// <remarks>
/// <para><c>default(Allocator)</c> is a legal null handle. Disposal is via
/// <see cref="Dispose"/> (calls <c>vmaDestroyAllocator</c>); double-dispose
/// is undefined behavior. Copy-by-value is intentional — every wrapper in
/// this assembly follows the struct-handle pattern.</para>
/// <para>Skeleton only — implementation lands together with the generated
/// <c>AhjoVulkan.Vma.Native</c> bindings.</para>
/// </remarks>
public readonly struct Allocator : IDisposable
{
    internal readonly nint Handle;

    internal Allocator(nint handle)
    {
        Handle = handle;
    }

    public bool IsNull => Handle == 0;

    public void Dispose()
    {
        // TODO: call Vma.vmaDestroyAllocator(Handle) once bindings are generated.
    }
}
