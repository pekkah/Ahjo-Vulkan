using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkAccelerationStructureKHR</c> — a BLAS or TLAS living in a
/// <b>caller-owned</b> range of a caller-owned <see cref="Buffer"/>. Minted by
/// <see cref="Device.CreateAccelerationStructure"/>, built by
/// <see cref="CommandRecorder.BuildAccelerationStructures"/>, read by a
/// ray-query shader through a
/// <see cref="DescriptorWrite.AccelerationStructure"/> binding or by a TLAS
/// instance entry through <see cref="GetDeviceAddress"/>.
/// </summary>
/// <remarks>
/// <para><b>It does not own its buffer. Ever.</b>
/// <see cref="Device.CreateAccelerationStructure"/> takes a buffer, an offset
/// and a size and stores <em>none</em> of them beyond
/// <see cref="Size"/> — Vulkan's own model binds an acceleration structure to
/// a <i>range</i> of a caller-supplied buffer, and suballocating many BLASes
/// into a few large buffers is the standard pattern. The
/// <see cref="Buffer"/> is a VMA-backed handle already owned by an
/// <see cref="Allocator"/>, so an acceleration structure that owned it would
/// be inventing a second owner. <b>The caller must keep the buffer alive
/// strictly longer than the acceleration structure, and must not let a second
/// acceleration structure or any other resource alias the same
/// range.</b></para>
/// <para><b>Ownership.</b> Caller-owned, like its neighbours
/// <see cref="Event"/> and <see cref="QueryPool"/>: <see cref="Dispose"/>
/// destroys the <c>VkAccelerationStructureKHR</c> (but frees no memory — the
/// buffer's owner does that). Unlike those two it cannot destroy through a
/// static <c>[DllImport]</c>: <c>vkDestroyAccelerationStructureKHR</c> belongs
/// to a device extension and must be reached through
/// <c>vkGetDeviceProcAddr</c>, so the handle copies that one function pointer
/// at creation. Copying a pointer rather than holding a managed
/// <see cref="Device"/> reference is what keeps this struct <b>unmanaged</b>,
/// which is what lets
/// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/> take a
/// <c>ReadOnlySpan&lt;AccelerationStructure&gt;</c> the caller can
/// <c>stackalloc</c>.</para>
/// <para><b>Lifetime.</b> Do not dispose while a submission that references
/// the structure is still pending
/// (<c>VUID-vkDestroyAccelerationStructureKHR-accelerationStructure-02442</c>) —
/// the wrapper does <b>not</b> stop you, because there is no submission
/// tracking anywhere in this repo. <c>VK_LAYER_KHRONOS_validation</c> catches
/// destroy-in-use; the <c>AHJO_VULKAN_TIER=validation</c> lane is where that
/// gets caught. <c>default(AccelerationStructure)</c> is a legal null handle
/// (<see cref="IsNull"/> is <see langword="true"/>, <see cref="Dispose"/> is a
/// no-op); double-dispose is undefined behavior — the standard handle
/// contract, see <see cref="IVulkanHandle{TSelf}"/>.</para>
/// <para><b>What must outlive an in-flight build.</b> The destination
/// structure <em>and its buffer</em>; the source structure and its buffer for
/// an <see cref="AccelerationStructureBuildMode.Update"/>; the scratch range;
/// every input buffer behind an address in the geometry span; and, for
/// compaction, the query pool.</para>
/// <para><b><see cref="Size"/> on a borrowed handle means "unknown".</b>
/// <see cref="FromRaw"/> and <c>default</c> carry a <see cref="Size"/> of 0
/// because the wrapper never learns a borrowed structure's size — read 0 as
/// <em>unknown</em>, never as <em>empty</em> (a zero-sized acceleration
/// structure cannot be created).</para>
/// </remarks>
public readonly unsafe struct AccelerationStructure
    : IVulkanHandle<AccelerationStructure>, IDisposable
{
    public readonly VkAccelerationStructureKHR_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    // vkDestroyAccelerationStructureKHR, copied from the owning device's
    // function table at creation. A managed Device field here would make the
    // struct managed and forfeit stackalloc over it — see the type remarks.
    private readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkAccelerationStructureKHR_T*, VkAllocationCallbacks*, void> _destroy;

    private readonly ulong _size;

    internal AccelerationStructure(
        VkAccelerationStructureKHR_T* handle,
        VkDevice_T*                   device,
        delegate* unmanaged[Stdcall]<
            VkDevice_T*, VkAccelerationStructureKHR_T*, VkAllocationCallbacks*, void> destroy,
        ulong                         size)
    {
        Handle       = handle;
        DeviceHandle = device;
        _destroy     = destroy;
        _size        = size;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType =>
        VkObjectType.VK_OBJECT_TYPE_ACCELERATION_STRUCTURE_KHR;

    public static AccelerationStructure FromRaw(nint handle) =>
        new((VkAccelerationStructureKHR_T*)handle, null, null, 0);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    /// <summary>
    /// Size in bytes of the backing-buffer range this structure was created
    /// over, as passed to <see cref="Device.CreateAccelerationStructure"/>. 0
    /// for a borrowed (<see cref="FromRaw"/> / <c>default</c>) handle, where it
    /// means <em>unknown</em> rather than <em>empty</em> — a zero-sized
    /// acceleration structure cannot be created. Note this is the
    /// <em>allocated</em> size, not the compacted size: read that back with
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/>.
    /// </summary>
    public ulong Size => _size;

    /// <summary>
    /// The structure's device address via
    /// <c>vkGetAccelerationStructureDeviceAddressKHR</c> — the value a TLAS
    /// instance entry's <c>accelerationStructureReference</c> field takes.
    /// Mirrors <see cref="Buffer.GetDeviceAddress"/>: the dispatching device is
    /// a parameter rather than a stored field.
    /// </summary>
    /// <remarks>
    /// <para><b>The hazard validation cannot catch.</b> Once this value is
    /// written into a TLAS instance buffer it is <b>just a number</b>. No
    /// layer, no driver and no tool can tell that the BLAS behind it was
    /// destroyed: traversal will read freed memory with no diagnostic at all.
    /// The rules that follow from that:</para>
    /// <list type="bullet">
    ///   <item><description>Every BLAS must outlive every TLAS built over
    ///     it.</description></item>
    ///   <item><description>A TLAS must be fully rebuilt
    ///     (<see cref="AccelerationStructureBuildMode.Build"/>, not
    ///     <see cref="AccelerationStructureBuildMode.Update"/>) after any
    ///     referenced BLAS is destroyed or recreated.</description></item>
    ///   <item><description><b>Compaction changes the address.</b> A compacted
    ///     copy lives in a different buffer and therefore reports a different
    ///     device address, so every TLAS over a BLAS that was compacted must be
    ///     rebuilt against the new value.</description></item>
    /// </list>
    /// <para>Requires <c>VK_KHR_acceleration_structure</c> on
    /// <paramref name="device"/>; throws
    /// <see cref="InvalidOperationException"/> when it was not enabled. Also
    /// requires the <c>bufferDeviceAddress</c> feature, which the wrapper
    /// cannot check.</para>
    /// </remarks>
    /// <param name="device">
    /// The device that owns this structure. A borrowed
    /// (<see cref="FromRaw"/>) handle has no owning device of its own, but the
    /// device passed here is what dispatches, so a borrowed handle is still
    /// usable — only a null <see cref="Handle"/> is rejected.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">This is a null handle.</exception>
    /// <exception cref="InvalidOperationException">
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on
    /// <paramref name="device"/>.
    /// </exception>
    public ulong GetDeviceAddress(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (Handle == null)
            throw new ArgumentException(
                "AccelerationStructure.GetDeviceAddress requires a non-null handle; this is a null "
                + "(default) AccelerationStructure.", nameof(device));

        var fn = device.Functions.GetAccelerationStructureDeviceAddress;
        if (fn == null)
            throw new InvalidOperationException(
                "AccelerationStructure.GetDeviceAddress is not available on this device. "
                + AccelerationStructureSupport.EnableInstructions);

        var info = new VkAccelerationStructureDeviceAddressInfoKHR
        {
            sType                 = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR,
            accelerationStructure = Handle,
        };
        return fn(device.Handle, &info);
    }

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the caller
        // owns the lifetime; calling vkDestroyAccelerationStructureKHR with a
        // null device handle would crash on every loader.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        // _destroy is non-null exactly when DeviceHandle is: both are written
        // together by the internal constructor, which is only reachable from
        // Device.CreateAccelerationStructure, and that path cannot run unless
        // the extension resolved both pointers. FromRaw nulls both. So no
        // separate null check is needed here.
        _destroy(DeviceHandle, Handle, null);
    }
}
