using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkPhysicalDevice</c>. Owned by an
/// <see cref="Instance"/> and produced exclusively by
/// <see cref="Instance.PickPhysicalDevice"/>; <see cref="Instance"/> caches
/// one managed instance per native handle, so reference equality matches
/// "same GPU."
/// </summary>
/// <remarks>
/// Owner-class shape (sealed class) rather than struct + <c>IVulkanHandle&lt;&gt;</c>:
/// physical devices are created 1–3 times per process and are never debug-named
/// or pooled, so the generic-dispatch infrastructure that
/// <c>IVulkanHandle&lt;TSelf&gt;</c> exists for is inert here. Resource handles
/// (Buffer, Image, …) keep the struct + interface convention.
/// </remarks>
public sealed unsafe class PhysicalDevice
{
    internal readonly VkPhysicalDevice_T* Handle;
    internal readonly Instance            Instance;

    internal PhysicalDevice(Instance instance, VkPhysicalDevice_T* handle)
    {
        Instance = instance;
        Handle   = handle;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;

    /// <summary>
    /// Wraps <c>vkGetPhysicalDeviceSurfaceSupportKHR</c>. Returns
    /// <see langword="true"/> when the queue family at
    /// <paramref name="queueFamilyIndex"/> on this physical device can
    /// present to <paramref name="surface"/>. The instance that built
    /// the surface MUST have <see cref="VulkanExtensions.KhrSurface"/>
    /// enabled — otherwise the extension entry-point isn't loaded.
    /// </summary>
    public bool SupportsPresent(uint queueFamilyIndex, in Surface surface)
    {
        if (surface.IsNull) throw new ArgumentException("Surface is null.", nameof(surface));
        uint supported = 0;
        Vk.vkGetPhysicalDeviceSurfaceSupportKHR(Handle, queueFamilyIndex, surface.Handle, &supported)
            .ThrowIfFailed();
        return supported != 0;
    }

    /// <summary>
    /// Creates a Vulkan device with the wrapper's 1.4 default feature set
    /// (<c>synchronization2</c>, <c>dynamicRendering</c>,
    /// <c>timelineSemaphore</c>, <c>bufferDeviceAddress</c>,
    /// <c>pushDescriptor</c>) plus any additional features the caller pushes
    /// via <see cref="DeviceDescription.ConfigureFeatures"/>. Validates queue
    /// requests against this physical device's queue families before the
    /// native call.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="DeviceDescription.Queues"/> is empty, or a queue request
    /// references a non-existent family / requests more queues than the
    /// family supports.
    /// </exception>
    /// <exception cref="VulkanException">
    /// <c>vkCreateDevice</c> failed (driver mismatch, OOM, feature not
    /// present on the device, etc.).
    /// </exception>
    /// <remarks>
    /// Assumes a Vulkan 1.4 device. The default feature set lights up every
    /// 1.4 promotional feature; on a 1.3 device the driver may reject the
    /// chain with <c>VK_ERROR_FEATURE_NOT_PRESENT</c>.
    /// </remarks>
    public Device CreateDevice(in DeviceDescription desc)
    {
        ValidateQueues(desc.Queues);

        int totalQueues = 0;
        for (int i = 0; i < desc.Queues.Length; i++)
            totalQueues += (int)desc.Queues[i].Count;

        Span<float>                   priorities = stackalloc float[totalQueues];
        Span<VkDeviceQueueCreateInfo> qcis       = stackalloc VkDeviceQueueCreateInfo[desc.Queues.Length];
        int prioCursor = 0;
        for (int i = 0; i < desc.Queues.Length; i++)
        {
            ref readonly QueueRequest req = ref desc.Queues[i];
            for (int p = 0; p < (int)req.Count; p++)
                priorities[prioCursor + p] = req.Priority;

            qcis[i] = new VkDeviceQueueCreateInfo
            {
                sType            = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                queueFamilyIndex = req.FamilyIndex,
                queueCount       = req.Count,
                pQueuePriorities = (float*)Unsafe.AsPointer(ref priorities[prioCursor]),
            };
            prioCursor += (int)req.Count;
        }

        Span<nint> extPtrs = stackalloc nint[desc.Extensions.Length];
        for (int i = 0; i < desc.Extensions.Length; i++)
            extPtrs[i] = (nint)desc.Extensions[i].Ptr;

        Span<byte> chainBuf = stackalloc byte[2048];
        var chain = ChainBuilder.For<VkDeviceCreateInfo>(chainBuf);
        ref VkDeviceCreateInfo dci = ref chain.Root();

        ref var f12 = ref chain.Push<VkPhysicalDeviceVulkan12Features>();
        f12.bufferDeviceAddress = 1;
        f12.timelineSemaphore   = 1;
        ref var f13 = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
        f13.synchronization2 = 1;
        f13.dynamicRendering = 1;
        ref var f14 = ref chain.Push<VkPhysicalDeviceVulkan14Features>();
        f14.pushDescriptor = 1;

        desc.ConfigureFeatures?.Invoke(ref chain);

        dci.queueCreateInfoCount    = (uint)desc.Queues.Length;
        dci.pQueueCreateInfos       = (VkDeviceQueueCreateInfo*)Unsafe.AsPointer(ref qcis[0]);
        dci.enabledExtensionCount   = (uint)desc.Extensions.Length;
        dci.ppEnabledExtensionNames = desc.Extensions.Length > 0
            ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
            : null;
        // pEnabledFeatures intentionally null — features driven through the chain only.

        VkDevice_T* raw = null;
        Vk.vkCreateDevice(Handle, chain.Head, null, &raw).ThrowIfFailed();

        Queue[] queues = new Queue[totalQueues];
        var device = new Device(raw, physicalDevice: this, queues);
        int qSlot = 0;
        for (int i = 0; i < desc.Queues.Length; i++)
        {
            QueueRequest req = desc.Queues[i];
            for (uint q = 0; q < req.Count; q++)
            {
                VkQueue_T* qh = null;
                Vk.vkGetDeviceQueue(raw, req.FamilyIndex, q, &qh);
                queues[qSlot++] = new Queue(device, qh, req.FamilyIndex, q);
            }
        }
        return device;
    }

    private void ValidateQueues(ReadOnlySpan<QueueRequest> queues)
    {
        if (queues.IsEmpty)
            throw new ArgumentException("DeviceDescription.Queues must contain at least one entry.");

        uint familyCount = 0;
        Vk.vkGetPhysicalDeviceQueueFamilyProperties2(Handle, &familyCount, null);

        Span<VkQueueFamilyProperties2> qfp = stackalloc VkQueueFamilyProperties2[16];
        if (familyCount > qfp.Length)
            throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
                $"Physical device reports {familyCount} queue families; wrapper ceiling is 16.");
        for (int i = 0; i < (int)familyCount; i++)
            qfp[i].sType = VkStructureType.VK_STRUCTURE_TYPE_QUEUE_FAMILY_PROPERTIES_2;
        fixed (VkQueueFamilyProperties2* qp = qfp)
            Vk.vkGetPhysicalDeviceQueueFamilyProperties2(Handle, &familyCount, qp);

        for (int i = 0; i < queues.Length; i++)
        {
            ref readonly var req = ref queues[i];
            if (req.FamilyIndex >= familyCount)
                throw new ArgumentException(
                    $"QueueRequest.FamilyIndex {req.FamilyIndex} is out of range (device has {familyCount} families).");
            uint avail = qfp[(int)req.FamilyIndex].queueFamilyProperties.queueCount;
            if (req.Count > avail)
                throw new ArgumentException(
                    $"QueueRequest at family {req.FamilyIndex} requests {req.Count} queues but family supports {avail}.");
        }
    }
}
