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
    /// Wraps <c>vkGetPhysicalDeviceFormatProperties</c>. Returns the
    /// linear / optimal / buffer feature bitmasks the device advertises
    /// for <paramref name="format"/>. Used to decide whether a candidate
    /// format supports a given operation (linear-filterable blit source,
    /// renderable color attachment, etc.) before committing to it.
    /// </summary>
    public VkFormatProperties GetFormatProperties(VkFormat format)
    {
        VkFormatProperties props;
        Vk.vkGetPhysicalDeviceFormatProperties(Handle, format, &props);
        return props;
    }

    /// <summary>
    /// Convenience predicate over <see cref="GetFormatProperties"/>:
    /// <see langword="true"/> when the device's
    /// <c>optimalTilingFeatures</c> mask for <paramref name="format"/>
    /// covers every bit in <paramref name="feature"/>. Pass an OR of
    /// flags to require multiple features at once
    /// (e.g. <c>BlitSrc | SampledImageFilterLinear</c> for runtime mip
    /// generation).
    /// </summary>
    public bool SupportsOptimalTilingFeature(VkFormat format, VkFormatFeatureFlagBits feature)
    {
        VkFormatProperties props;
        Vk.vkGetPhysicalDeviceFormatProperties(Handle, format, &props);
        return ((VkFormatFeatureFlagBits)props.optimalTilingFeatures & feature) == feature;
    }

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
    /// Creates a Vulkan device with the wrapper's default feature set
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
    /// The selected GPU reports <c>apiVersion &lt; 1.3</c>, or
    /// <c>vkCreateDevice</c> failed (driver mismatch, OOM, feature not
    /// present on the device, etc.).
    /// </exception>
    /// <remarks>
    /// <para><b>Vulkan 1.3+ required.</b> The wrapper unconditionally enables
    /// the 1.3 promotional features <c>synchronization2</c> and
    /// <c>dynamicRendering</c>, and records through the core 1.3 entry
    /// points (<c>vkCmdBeginRendering</c> etc.) and structure types
    /// (<c>VkRenderingInfo</c>, <c>VkPipelineRenderingCreateInfo</c>).
    /// On a 1.2 device those entry points are unresolved, the feature
    /// chain is rejected, and record-time calls would fault on null
    /// function pointers. The version is asserted up front so the
    /// failure mode is a clear exception rather than a deep crash —
    /// <c>VK_KHR_dynamic_rendering</c> on 1.2 is not supported as a
    /// fallback.</para>
    /// <para>1.4 promotional features (<c>pushDescriptor</c>) are enabled
    /// when the device is 1.4+; on a 1.3 device the chain still
    /// includes the 1.4 features struct and the driver will silently
    /// ignore unsupported bits, which is permitted by the spec when the
    /// instance/device is 1.4-aware.</para>
    /// </remarks>
    public Device CreateDevice(in DeviceDescription desc)
    {
        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(Handle, &props);
        if (props.apiVersion < VulkanVersion.V1_3.Packed)
        {
            var v = new VulkanVersion(props.apiVersion);
            throw new VulkanException(VkResult.VK_ERROR_INCOMPATIBLE_DRIVER,
                $"Ahjo.Vulkan requires a Vulkan 1.3+ device. Selected GPU reports {v.Major}.{v.Minor}.{v.Patch}.");
        }

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

        // Hand the configurer ref access to the wrapper's pre-pushed
        // structs so it can flip additional 1.2/1.3/1.4 bits in-place
        // without producing a duplicate-sType chain (issue 53). The
        // duplicate-sType validator below still catches callers that
        // push their own copy through `chain`.
        desc.ConfigureFeatures?.Invoke(ref chain, ref f12, ref f13, ref f14);

        // Vulkan disallows two pNext nodes with the same sType inside a
        // single chain. The wrapper pre-pushes the 1.2/1.3/1.4 promoted-
        // feature structs above; a configurer that pushes its own copy
        // (e.g. to enable a 1.3 bit the wrapper doesn't set, like
        // maintenance4) would silently violate the rule and the driver
        // would either crash or reject the chain with an opaque error.
        // Walk the chain once after the configurer runs and reject the
        // duplicate up front with a message that names the sType so the
        // caller knows which struct to drop and which wrapper-managed
        // ref to mutate instead.
        ValidateNoDuplicateSTypes((VkBaseOutStructure*)chain.Head);

        dci.queueCreateInfoCount    = (uint)desc.Queues.Length;
        dci.pQueueCreateInfos       = (VkDeviceQueueCreateInfo*)Unsafe.AsPointer(ref qcis[0]);
        dci.enabledExtensionCount   = (uint)desc.Extensions.Length;
        dci.ppEnabledExtensionNames = desc.Extensions.Length > 0
            ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
            : null;
        // pEnabledFeatures intentionally null — features driven through the chain only.

        VkDevice_T* raw = null;
        Vk.vkCreateDevice(Handle, chain.Head, null, &raw).ThrowIfFailed();

        // Anything between vkCreateDevice and the final return that
        // throws (managed OOM during the queue array, DeviceFunctionTable
        // construction, …) leaves the live VkDevice with no managed
        // owner — the Device finalizer would only catch it on GC, by
        // which point the GPU has stayed busy for an indeterminate time.
        // Destroy and rethrow so the failure path matches the success
        // path's resource accounting.
        try
        {
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
        catch
        {
            Vk.vkDestroyDevice(raw, null);
            throw;
        }
    }

    /// <summary>
    /// Walks the <c>VkDeviceCreateInfo</c> pNext chain and throws
    /// <see cref="ArgumentException"/> if any sType appears more than
    /// once. Linear in chain length; chain depth is small (the wrapper's
    /// three pre-pushed structs plus whatever the configurer adds), so
    /// the O(n²) inner loop is fine.
    /// </summary>
    private static void ValidateNoDuplicateSTypes(VkBaseOutStructure* head)
    {
        for (VkBaseOutStructure* a = head; a != null; a = a->pNext)
        {
            for (VkBaseOutStructure* b = a->pNext; b != null; b = b->pNext)
            {
                if (a->sType != b->sType) continue;

                string hint = a->sType switch
                {
                    VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES or
                    VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES or
                    VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_4_FEATURES =>
                        " The wrapper pre-pushes the 1.2/1.3/1.4 promoted-feature structs " +
                        "before invoking ConfigureFeatures and hands them to the configurer " +
                        "as ref parameters (features12/features13/features14) — flip extra " +
                        "bits on those refs instead of pushing a second copy. Push only the " +
                        "per-feature extension structs the wrapper does not own.",
                    _ => string.Empty,
                };

                throw new ArgumentException(
                    $"VkDeviceCreateInfo pNext chain contains duplicate sType {a->sType}; the Vulkan spec disallows two structs of the same type in a single chain.{hint}");
            }
        }
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

            // VUID-VkDeviceCreateInfo-queueFamilyIndex-02802: each
            // VkDeviceQueueCreateInfo's queueFamilyIndex must be unique
            // within the call. The driver-side error message is opaque
            // ("queueFamilyIndex is not unique"); raise it here as a
            // clear ArgumentException pointing at the duplicate so the
            // caller knows which entries to merge.
            for (int j = 0; j < i; j++)
            {
                if (queues[j].FamilyIndex == req.FamilyIndex)
                    throw new ArgumentException(
                        $"QueueRequest entries {j} and {i} both target family {req.FamilyIndex}; merge them into a single request with the combined queue count.");
            }
        }
    }
}
