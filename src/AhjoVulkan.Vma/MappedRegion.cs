namespace Ahjo.Vulkan.Vma;

/// <summary>
/// Scoped <c>vmaMapMemory</c> / <c>vmaUnmapMemory</c> pair. Living as a
/// <c>ref struct</c> forces stack allocation, so the unmap can't outlive
/// the scope and the mapped pointer can't escape into a heap object.
/// </summary>
public ref struct MappedRegion : IDisposable
{
    private readonly Allocator _allocator;
    private readonly Allocation _allocation;
    private nint _data;

    internal MappedRegion(Allocator allocator, Allocation allocation, nint data)
    {
        _allocator = allocator;
        _allocation = allocation;
        _data = data;
    }

    public unsafe Span<byte> AsSpan(int length) => new((void*)_data, length);

    public unsafe Span<T> AsSpan<T>(int count) where T : unmanaged
        => new((void*)_data, count);

    public nint Pointer => _data;

    public void Dispose()
    {
        if (_data == 0)
        {
            return;
        }
        // TODO: Vma.vmaUnmapMemory(_allocator.Handle, _allocation.Handle);
        _data = 0;
    }
}
