using System.Diagnostics;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Owner of a <c>VkDevice</c>. <c>sealed class</c> for the same reasons
/// as <see cref="Instance"/>: created once per app, never copied,
/// deterministic <see cref="Dispose"/> calls <c>vkDeviceWaitIdle</c> +
/// <c>vkDestroyDevice</c>, finalizer backstops a missed dispose (a leaked
/// device leaves the GPU busy for the rest of the process).
/// </summary>
/// <remarks>
/// <para><b>Thread safety.</b> Disposal is not thread-safe — do not call
/// <see cref="Dispose"/> concurrently from multiple threads. Vulkan calls
/// made through the wrapped handle follow the spec's external-sync rules
/// (the underlying <c>VkDevice</c> is externally synchronizable).</para>
/// </remarks>
public sealed unsafe class Device : IDisposable
{
    internal readonly VkDevice_T*         Handle;
    internal readonly DeviceFunctionTable Functions;
    public   readonly PhysicalDevice      PhysicalDevice;
    private  readonly Queue[]             _queues;
    private  Allocator                    _allocator;
    private  bool                         _allocatorCreated;
    private  bool                         _disposed;

    internal Device(VkDevice_T* handle, PhysicalDevice physicalDevice, Queue[] queues)
    {
        Handle         = handle;
        Functions      = new DeviceFunctionTable(handle);
        PhysicalDevice = physicalDevice;
        _queues        = queues;
    }

    /// <summary>
    /// The device's VMA allocator. Created on first access and disposed
    /// during <see cref="Dispose"/>; do not call <see cref="Allocator.Dispose"/>
    /// on the returned struct — the device owns the lifetime.
    /// </summary>
    public Allocator Allocator
    {
        get
        {
            if (!_allocatorCreated)
            {
                _allocator        = Ahjo.Vulkan.Allocator.Create(this);
                _allocatorCreated = true;
            }
            return _allocator;
        }
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DEVICE;

    /// <summary>
    /// Returns the cached <see cref="Queue"/> for the requested
    /// <c>(familyIndex, queueIndex)</c>. The pair must match a
    /// <see cref="QueueRequest"/> that was passed via
    /// <see cref="DeviceDescription.Queues"/>; otherwise an
    /// <see cref="ArgumentException"/> guides the caller to declare it.
    /// </summary>
    public Queue GetQueue(uint familyIndex, uint queueIndex)
    {
        var queues = _queues;
        for (int i = 0; i < queues.Length; i++)
        {
            if (queues[i].FamilyIndex == familyIndex && queues[i].QueueIndex == queueIndex)
                return queues[i];
        }

        throw new ArgumentException(
            $"No queue requested at (family: {familyIndex}, index: {queueIndex}). " +
            "Add a corresponding QueueRequest to DeviceDescription.Queues.");
    }

    /// <summary>Wraps <c>vkDeviceWaitIdle</c>.</summary>
    public void WaitIdle()
    {
        Vk.vkDeviceWaitIdle(Handle).ThrowIfFailed();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != null)
        {
            // Best-effort wait-idle before destroy; Dispose mustn't throw on
            // the success path. A failing wait-idle (lost device, OOM)
            // already implies the device is going away — destroy still runs.
            Vk.vkDeviceWaitIdle(Handle);
            // Allocator must die before the VkDevice — vmaDestroyAllocator
            // calls into the device's function table.
            if (_allocatorCreated) _allocator.Dispose();
            Vk.vkDestroyDevice(Handle, null);
        }
        GC.SuppressFinalize(this);
    }

    ~Device()
    {
        Debug.Fail("Device was not disposed.");
        Dispose();
    }
}
