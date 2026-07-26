using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Vma.Native;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkBuffer</c> paired with the VMA allocation that backs it and a
/// reference to the <see cref="Allocator"/> that produced both. Always
/// VMA-allocated; the wrapper has no raw <c>VkBuffer</c> path. Pairing all
/// three on one struct keeps disposal and mapping local — the caller never
/// has to thread the allocator separately.
/// </summary>
/// <remarks>
/// <para>Three pointers + cached metadata; passes by value through registers
/// on x64. <c>default(Buffer)</c> is a legal null handle: <see cref="IsNull"/>
/// returns <see langword="true"/> and <see cref="Dispose"/> is a no-op.
/// Double-dispose is undefined behavior — the struct can't null its own
/// fields (they're <c>readonly</c>).</para>
/// <para>Equality is intentionally not implemented via
/// <see cref="IEquatable{Buffer}"/>. The handle pointer is the identity, but
/// adding equality would imply that two structs sharing the same handle are
/// "the same buffer," which is misleading on a copy-by-value type that can
/// outlive its owning allocator.</para>
/// </remarks>
public readonly unsafe struct Buffer : IVulkanHandle<Buffer>, IDisposable
{
    public readonly VkBuffer_T*      Handle;
    public readonly VmaAllocation_T* AllocationHandle;
    public readonly Allocator        Owner;
    public readonly ulong            Size;
    public readonly BufferUsage      Usage;
    public readonly bool             IsHostVisible;

    /// <summary>
    /// <see langword="true"/> when the backing memory carries
    /// <c>VK_MEMORY_PROPERTY_HOST_COHERENT_BIT</c>. Coherent allocations
    /// don't need <see cref="Flush"/>/<see cref="Invalidate"/> bracketing
    /// around host reads/writes; non-coherent allocations (typical on
    /// mobile/UMA targets and some BAR setups) do. The Flush/Invalidate
    /// helpers short-circuit when this is <see langword="true"/>, so it's
    /// safe to call them unconditionally.
    /// </summary>
    public readonly bool IsHostCoherent;

    /// <summary>
    /// Persistent mapped pointer when the buffer was allocated with
    /// <see cref="AllocationFlags.Mapped"/>; <see langword="null"/> otherwise.
    /// Lets <see cref="AsSpan{T}"/> and <see cref="Map{T}"/> skip the
    /// <c>vmaMapMemory</c>/<c>vmaUnmapMemory</c> dance for persistent maps.
    /// </summary>
    internal readonly void* PersistentMapped;

    internal Buffer(
        VkBuffer_T*      handle,
        VmaAllocation_T* allocation,
        Allocator        owner,
        ulong            size,
        BufferUsage      usage,
        bool             isHostVisible,
        bool             isHostCoherent,
        void*            persistentMapped)
    {
        Handle           = handle;
        AllocationHandle = allocation;
        Owner            = owner;
        Size             = size;
        Usage            = usage;
        IsHostVisible    = isHostVisible;
        IsHostCoherent   = isHostCoherent;
        PersistentMapped = persistentMapped;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_BUFFER;

    public static Buffer FromRaw(nint handle) =>
        new((VkBuffer_T*)handle, null, default, 0, BufferUsage.None, false, false, null);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    /// <summary>
    /// <see langword="true"/> when this struct owns the VMA allocation —
    /// i.e. <see cref="Dispose"/> destroys it. <see langword="false"/> for
    /// <see cref="FromRaw"/>-constructed (borrowed) handles and
    /// <c>default</c>.
    /// </summary>
    public bool OwnsHandle => !Owner.IsNull;

    /// <summary>
    /// GPU virtual address for use with <c>bufferDeviceAddress</c> features.
    /// Caller must have created the buffer with <see cref="BufferUsage.ShaderDeviceAddress"/>.
    /// </summary>
    /// <summary>
    /// Whether this handle owns the memory behind it as well as the resource. False for a
    /// resource created into a shared <see cref="MemoryBlock"/> by the aliasing creators:
    /// it owns its own <c>Vk*</c> object, but the block owns the bytes, so disposing it
    /// frees nothing and every operation that addresses the ALLOCATION rather than the
    /// resource is a no-op.
    /// </summary>
    public bool OwnsMemory => AllocationHandle != null;

    public ulong GetDeviceAddress(Device device)
    {
        var info = new VkBufferDeviceAddressInfo
        {
            sType  = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
            buffer = Handle,
        };
        return Vk.vkGetBufferDeviceAddress(device.Handle, &info);
    }

    /// <summary>
    /// Allocation-free view of a persistent-mapped buffer as a
    /// <see cref="Span{T}"/>. Throws on a non-persistent or non-host-visible
    /// buffer; use <see cref="Map{T}"/> if you need the
    /// <c>vmaMapMemory</c>-on-demand path.
    /// </summary>
    /// <remarks>
    /// <para>The <i>view</i> is nearly free — a null check, a span
    /// construction over the already-cached <c>pMappedData</c>, and a cast
    /// that is a length division. What is <b>not</b> free is reading through
    /// it: host read latency is a property of the memory type VMA chose for
    /// the allocation, not of this method.</para>
    /// <para><see cref="AllocationFlags.HostAccessSequentialWrite"/> promises
    /// VMA that the CPU only writes this memory, sequentially, and never reads
    /// it back — which lets VMA select an <b>uncached, write-combined</b>
    /// memory type. Writes stay fast, but a host read from such an allocation
    /// costs orders of magnitude more than a cached one: on the
    /// <c>docs/benchmarks.md</c> baseline host, a store-then-read-back of the
    /// same element measures <b>173.4 ns</b> against <b>1.5 ns</b> for the
    /// identical code on an <see cref="AllocationFlags.HostAccessRandom"/>
    /// allocation. Never read a sequential-write buffer from the CPU; it'll
    /// hit uncached memory and your perf numbers will look haunted. Callers
    /// who have to read should allocate
    /// <see cref="AllocationFlags.HostAccessRandom"/>, which makes VMA prefer
    /// a <c>HOST_CACHED</c> type. Watch out for implicit reads too — a
    /// read-modify-write like <c>span[i] += x</c> is a read.</para>
    /// <para><see cref="Flush"/>/<see cref="Invalidate"/> will not help here.
    /// They make host writes visible to the device, and device writes visible
    /// to the host, on <i>non-coherent</i> allocations; they do nothing about
    /// the read <i>latency</i> of an uncached, write-combined memory type. On
    /// the coherent allocations where this cost usually shows up the wrapper
    /// short-circuits them entirely (see <see cref="IsHostCoherent"/>).</para>
    /// </remarks>
    public Span<T> AsSpan<T>() where T : unmanaged
    {
        if (PersistentMapped == null)
            throw new InvalidOperationException(
                "Buffer.AsSpan<T>() requires AllocationFlags.Mapped at create time. " +
                "Use Buffer.Map<T>() for non-persistent host-visible buffers.");
        return MemoryMarshal.Cast<byte, T>(new Span<byte>(PersistentMapped, checked((int)Size)));
    }

    /// <summary>Read-only counterpart of <see cref="AsSpan{T}"/>.</summary>
    /// <remarks>
    /// A read-only view does <b>not</b> make reads cheap. This is the surface
    /// that invites the mistake: reading through it on an
    /// <see cref="AllocationFlags.HostAccessSequentialWrite"/> allocation hits
    /// uncached, write-combined memory and costs orders of magnitude more than
    /// a cached read — see <see cref="AsSpan{T}"/>'s remarks for the measured
    /// figures, and prefer <see cref="AllocationFlags.HostAccessRandom"/> when
    /// the CPU has to read. The wrapper cannot detect the mistake for you: it
    /// hands out a span, and the read then happens with no wrapper frame
    /// involved.
    /// </remarks>
    public ReadOnlySpan<T> AsReadOnlySpan<T>() where T : unmanaged => AsSpan<T>();

    /// <summary>
    /// Maps the buffer's host-visible memory and returns a
    /// <see cref="MappedRegion{T}"/>. For persistent-mapped buffers the
    /// VMA pointer is reused without an additional <c>vmaMapMemory</c>
    /// call, and disposal is a no-op.
    /// </summary>
    public MappedRegion<T> Map<T>() where T : unmanaged
    {
        if (!IsHostVisible)
            throw new InvalidOperationException(
                "Buffer.Map<T>() requires a host-visible allocation (AutoPreferHost or HostAccess* flags).");

        int length = checked((int)(Size / (uint)sizeof(T)));

        if (PersistentMapped != null)
            return new MappedRegion<T>(Owner.Handle, AllocationHandle, PersistentMapped, length, persistent: true);

        void* data = null;
        VmaApi.vmaMapMemory(Owner.Handle, AllocationHandle, &data).ThrowIfFailed();
        return new MappedRegion<T>(Owner.Handle, AllocationHandle, data, length, persistent: false);
    }

    /// <summary>
    /// Pushes prior host writes to GPU-visible memory by wrapping
    /// <c>vmaFlushAllocation</c>. No-op on <see cref="IsHostCoherent"/>
    /// allocations (the spec guarantees coherent writes are visible
    /// without explicit flushing). The default <paramref name="size"/>
    /// of <c>UInt64.MaxValue</c> maps to <c>VK_WHOLE_SIZE</c> — flush
    /// from <paramref name="offset"/> to the end of the allocation.
    /// </summary>
    public void Flush(ulong offset = 0, ulong size = ulong.MaxValue)
    {
        // !OwnsMemory is an aliasing view into a MemoryBlock: there is no allocation of its
        // own to flush, and the block's owner is the only party that can meaningfully do it.
        if (Handle == null || IsHostCoherent || !OwnsMemory) return;
        VmaApi.vmaFlushAllocation(Owner.Handle, AllocationHandle, offset, size).ThrowIfFailed();
    }

    /// <summary>
    /// Pulls prior GPU writes into host-visible memory by wrapping
    /// <c>vmaInvalidateAllocation</c>. Symmetrical counterpart to
    /// <see cref="Flush"/>; same coherent-skip and
    /// <c>VK_WHOLE_SIZE</c> defaulting rules.
    /// </summary>
    public void Invalidate(ulong offset = 0, ulong size = ulong.MaxValue)
    {
        if (Handle == null || IsHostCoherent || !OwnsMemory) return;
        VmaApi.vmaInvalidateAllocation(Owner.Handle, AllocationHandle, offset, size).ThrowIfFailed();
    }

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no owning Allocator — the
        // caller owns the lifetime; calling vmaDestroyBuffer through a null
        // allocator would crash. Skip the destroy.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        // A null AllocationHandle (an aliasing view — see OwnsMemory) is deliberate, not a
        // missing case: VMA documents both arguments of vmaDestroyBuffer as optional, so
        // this destroys the buffer and frees no memory, which is exactly the contract.
        VmaApi.vmaDestroyBuffer(Owner.Handle, Handle, AllocationHandle);
    }
}
