using System.Buffers;
using Ahjo.Vulkan.Vma.Native;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan;

/// <summary>
/// VMA-mapped region surfaced as a first-class <see cref="MemoryManager{T}"/>
/// so callers can pass mapped GPU memory to APIs that take
/// <see cref="Memory{T}"/> (async pipelines, <c>System.IO.Pipelines</c>,
/// SIMD libraries) without losing the lifetime story to a stack-only
/// <c>Span&lt;T&gt;</c>. Returned by <see cref="Buffer.Map{T}"/>;
/// <see cref="MemoryManager{T}.Dispose()"/> calls <c>vmaUnmapMemory</c>.
/// </summary>
/// <remarks>
/// <para>The mapped pointer is valid for the lifetime of the wrapping
/// <see cref="Buffer"/> — VMA does not move host-visible allocations.
/// <see cref="Pin"/> therefore returns a <see cref="MemoryHandle"/> over
/// the existing pointer rather than registering a new GC pin; <see cref="Unpin"/>
/// is a no-op.</para>
/// <para><b>Persistent mapping.</b> When the buffer was created with
/// <see cref="AllocationFlags.Mapped"/>, VMA hands the host pointer back
/// at allocation time and the wrapper skips the
/// <c>vmaMapMemory</c>/<c>vmaUnmapMemory</c> calls entirely — the dispose
/// path is a no-op in that mode.</para>
/// </remarks>
public sealed unsafe class MappedRegion<T> : MemoryManager<T>
    where T : unmanaged
{
    private readonly VmaAllocator_T*  _allocator;
    private readonly VmaAllocation_T* _allocation;
    private readonly bool             _persistent;
    private          void*            _data;
    private readonly int              _length;

    internal MappedRegion(
        VmaAllocator_T*  allocator,
        VmaAllocation_T* allocation,
        void*            data,
        int              length,
        bool             persistent)
    {
        _allocator  = allocator;
        _allocation = allocation;
        _data       = data;
        _length     = length;
        _persistent = persistent;
    }

    public override Span<T> GetSpan() => new(_data, _length);

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        // Pin must hand out a pointer to a valid element. _length is the
        // count of T's, so any index >= _length is past the end — even
        // _length itself, which would yield a one-past-the-end pointer
        // that the caller could legally dereference through MemoryHandle.
        if ((uint)elementIndex >= (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        return new MemoryHandle((T*)_data + elementIndex, default, this);
    }

    public override void Unpin() { /* VMA pin is permanent for the buffer's lifetime. */ }

    protected override void Dispose(bool disposing)
    {
        if (_data == null) return;
        // MemoryManager<T> has no default finalizer and MappedRegion
        // doesn't declare one, so this only ever runs via explicit
        // Dispose (disposing == true). A forgotten Dispose leaks the
        // vmaMapMemory ref-count until the owning Buffer/Allocator is
        // destroyed, at which point VMA cleans the map state regardless
        // — so the leak is bounded to the buffer's lifetime, not the
        // process's.
        if (!_persistent)
            VmaApi.vmaUnmapMemory(_allocator, _allocation);
        _data = null;
    }
}
