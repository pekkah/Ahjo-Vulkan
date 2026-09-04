using System.Buffers;
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

    /// <summary>
    /// The <see cref="Instance"/> this physical device was enumerated from.
    /// </summary>
    /// <remarks>
    /// Public (issue #218) because a satellite package has to hand a
    /// third-party API the <c>VkInstance</c> + <c>VkPhysicalDevice</c> +
    /// <c>VkDevice</c> triple, and the edge from a device back to its instance
    /// was the one hop with no public path: <see cref="Instance.RawHandle"/>
    /// is public, but nothing reached the <see cref="Instance"/> itself.
    /// <c>Ahjo.Vulkan.Ngx</c> is the first such consumer —
    /// <c>NgxContext.Create(Device, …)</c> takes a device alone precisely
    /// because this field exists, which makes "an instance the device did not
    /// come from" unrepresentable rather than merely documented. The instance's
    /// own handle stays <c>internal</c>, so nothing further leaks.
    /// </remarks>
    public readonly Instance Instance;

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
    /// <see langword="true"/> when this GPU advertises the named <b>device</b>
    /// extension. The after-the-picker counterpart to
    /// <see cref="PhysicalDeviceInfo.SupportsExtension"/>, which is only
    /// reachable inside <see cref="Instance.PickPhysicalDevice"/> because
    /// <see cref="PhysicalDeviceInfo"/> is a <c>ref struct</c> that cannot
    /// escape the callback.
    /// </summary>
    /// <remarks>
    /// Setup-time: issues <c>vkEnumerateDeviceExtensionProperties</c> on every
    /// call and caches nothing, the same policy as
    /// <see cref="GetMemoryLimits"/> and <c>Device.TimestampPeriod</c>. Rents
    /// its scratch from <see cref="ArrayPool{T}.Shared"/> and returns it, the
    /// same shape as <see cref="Instance.IsExtensionSupported(ReadOnlySpan{byte})"/>.
    /// An empty name answers <see langword="false"/> — the capability answer,
    /// not an error.
    /// </remarks>
    /// <param name="utf8ExtensionName">
    /// The extension name as UTF-8 bytes, without the trailing NUL (a
    /// <c>"…"u8</c> literal).
    /// </param>
    public bool SupportsExtension(ReadOnlySpan<byte> utf8ExtensionName)
    {
        if (utf8ExtensionName.IsEmpty) return false;

        uint count = 0;
        Vk.vkEnumerateDeviceExtensionProperties(Handle, null, &count, null).ThrowIfErrored();
        if (count == 0) return false;

        var pool = ArrayPool<VkExtensionProperties>.Shared;
        var buf  = pool.Rent((int)count);
        try
        {
            fixed (VkExtensionProperties* p = buf)
            {
                Vk.vkEnumerateDeviceExtensionProperties(Handle, null, &count, p).ThrowIfErrored();
                for (int i = 0; i < (int)count; i++)
                {
                    if (PhysicalDeviceInfo.NameEquals((sbyte*)&p[i].extensionName.e0, utf8ExtensionName))
                        return true;
                }
            }
        }
        finally { pool.Return(buf); }

        return false;
    }

    /// <inheritdoc cref="SupportsExtension(ReadOnlySpan{byte})"/>
    /// <param name="extension">
    /// The extension name as a process-lifetime <see cref="Utf8Name"/> — e.g.
    /// <see cref="VulkanExtensions.ExtMeshShader"/>. A null name answers
    /// <see langword="false"/>.
    /// </param>
    public bool SupportsExtension(Utf8Name extension)
        => !extension.IsNull
           && SupportsExtension(
               MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)extension.Ptr));

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
    /// <summary>
    /// The <c>VkPhysicalDeviceLimits</c> fields that govern sub-allocation — chiefly
    /// <see cref="DeviceMemoryLimits.BufferImageGranularity"/>, which a caller packing
    /// resources into one <see cref="MemoryBlock"/> must obey.
    /// </summary>
    /// <remarks>
    /// The full limits struct is reachable only through <c>PhysicalDeviceInfo</c>, which is
    /// a <c>ref struct</c> that cannot escape the device-picker callback — so a caller that
    /// needs a limit AFTER the device exists has nowhere to read it from. This is that
    /// accessor, kept narrow to the memory-relevant subset rather than surfacing everything.
    /// </remarks>
    public unsafe DeviceMemoryLimits GetMemoryLimits()
    {
        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(Handle, &props);
        return new DeviceMemoryLimits
        {
            BufferImageGranularity = props.limits.bufferImageGranularity,
            MinUniformBufferOffsetAlignment = props.limits.minUniformBufferOffsetAlignment,
            MinStorageBufferOffsetAlignment = props.limits.minStorageBufferOffsetAlignment,
            NonCoherentAtomSize = props.limits.nonCoherentAtomSize,
            MaxMemoryAllocationCount = props.limits.maxMemoryAllocationCount,
        };
    }

    // ---- Chained property queries ----

    /// <summary>
    /// Reads one <c>VkPhysicalDeviceProperties2</c> <c>pNext</c> extension
    /// struct, but only when this GPU advertises
    /// <paramref name="utf8ExtensionName"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The properties struct to read. The
    /// <c>IChainable&lt;VkPhysicalDeviceProperties2&gt;</c> constraint is
    /// generated from <c>vk.xml</c>'s <c>structextends</c> attribute, so a
    /// struct Vulkan does not permit on this chain root is a <b>compile</b>
    /// error, and <c>sType</c> is written from <c>T.SType</c> — the caller
    /// never supplies one and structurally cannot supply a wrong one.
    /// </typeparam>
    /// <param name="utf8ExtensionName">
    /// The device extension that owns <typeparamref name="T"/>, as UTF-8 bytes
    /// (a <c>"…"u8</c> literal).
    /// </param>
    /// <param name="properties">
    /// The filled struct on <see langword="true"/>; <c>default</c> on
    /// <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the gate passed and the driver filled the
    /// node.
    /// </returns>
    /// <remarks>
    /// <para><b>What the gate means.</b> When the gate fails this returns
    /// <see langword="false"/> and leaves <paramref name="properties"/> at
    /// <c>default</c> <b>without</b> issuing the chained query at all. The
    /// wrapper refuses to put an <c>sType</c> a driver may not recognise into a
    /// <c>vkGetPhysicalDeviceProperties2</c> chain: the spec says
    /// implementations must skip unrecognized <c>pNext</c> nodes, but real ICDs
    /// have been observed not to — see the SwiftShader note in
    /// <c>Instance.PickPhysicalDevice</c>, where an unrecognized
    /// <c>VkPhysicalDeviceVulkan14Features</c> in a read-back chain produced
    /// cumulative state damage and later SIGSEGVs in unrelated entry points. A
    /// <see langword="false"/> result therefore means "not supported", never
    /// "supported but zero".</para>
    /// <para><b>Which overload to use.</b> An extension-only struct
    /// (<c>VkPhysicalDeviceMeshShaderPropertiesEXT</c>,
    /// <c>VkPhysicalDeviceAccelerationStructurePropertiesKHR</c>) takes the
    /// name overloads. A core-promoted struct
    /// (<c>VkPhysicalDeviceVulkan11Properties</c> …
    /// <c>VkPhysicalDeviceVulkan14Properties</c>) takes
    /// <see cref="TryGetProperties{T}(VulkanVersion, out T)"/>; there is no
    /// extension to name. A struct that is <i>both</i> — e.g.
    /// <c>VkPhysicalDeviceDriverProperties</c>, from
    /// <c>VK_KHR_driver_properties</c> and promoted to Vulkan 1.2 — also takes
    /// the version overload, because a device that supports it through core
    /// promotion is not required to keep advertising the extension.</para>
    /// <para><b>Cost.</b> Setup-time, and nothing is cached. The
    /// <see cref="TryGetProperties{T}(VulkanVersion, out T)"/> overload issues
    /// <b>two</b> native queries when the gate passes
    /// (<c>vkGetPhysicalDeviceProperties</c> for the api version, then
    /// <c>vkGetPhysicalDeviceProperties2</c>) and one when it fails; it
    /// allocates nothing (one <c>stackalloc</c>). The name overloads issue
    /// <b>three</b> when the gate passes —
    /// <c>vkEnumerateDeviceExtensionProperties</c> twice (count, then fill)
    /// inside <see cref="SupportsExtension(ReadOnlySpan{byte})"/>, then
    /// <c>vkGetPhysicalDeviceProperties2</c> — and rent and return a pooled
    /// array for the extension list. Same no-caching policy as
    /// <see cref="GetMemoryLimits"/> and <c>Device.TimestampPeriod</c>. Not
    /// for a per-frame path.</para>
    /// <para>The returned struct's <c>pNext</c> is <see langword="null"/> by
    /// construction — it is the chain tail — so nothing here hands the caller a
    /// pointer into a dead stack frame.</para>
    /// <para>Example:</para>
    /// <code>
    /// if (gpu.TryGetProperties&lt;VkPhysicalDeviceMeshShaderPropertiesEXT&gt;(
    ///         VulkanExtensions.ExtMeshShader, out var raw))
    ///     uint maxVerts = raw.maxMeshOutputVertices;
    /// </code>
    /// For that struct's per-draw workgroup bounds specifically, prefer the
    /// typed projection <see cref="TryGetMeshShaderLimits"/>; for
    /// <c>VkPhysicalDeviceAccelerationStructurePropertiesKHR</c>'s
    /// scratch-alignment and capacity values, prefer
    /// <see cref="TryGetAccelerationStructureLimits"/>.
    /// </remarks>
    public bool TryGetProperties<T>(ReadOnlySpan<byte> utf8ExtensionName, out T properties)
        where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
    {
        if (!SupportsExtension(utf8ExtensionName))
        {
            properties = default;
            return false;
        }

        QueryChained(out properties);
        return true;
    }

    /// <inheritdoc cref="TryGetProperties{T}(ReadOnlySpan{byte}, out T)"/>
    /// <param name="extension">
    /// The device extension that owns <typeparamref name="T"/>, as a
    /// process-lifetime <see cref="Utf8Name"/> — e.g.
    /// <see cref="VulkanExtensions.ExtMeshShader"/>. A null name gates out.
    /// </param>
    /// <param name="properties">
    /// The filled struct on <see langword="true"/>; <c>default</c> on
    /// <see langword="false"/>.
    /// </param>
    public bool TryGetProperties<T>(Utf8Name extension, out T properties)
        where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
    {
        if (extension.IsNull)
        {
            properties = default;
            return false;
        }

        return TryGetProperties(
            MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)extension.Ptr),
            out properties);
    }

    /// <inheritdoc cref="TryGetProperties{T}(ReadOnlySpan{byte}, out T)"/>
    /// <param name="minimumApiVersion">
    /// The core Vulkan version that promoted <typeparamref name="T"/> — the
    /// version whose header <i>defines</i> the struct, which is not always the
    /// version its name suggests: <c>VkPhysicalDeviceVulkan11Properties</c> is
    /// Vulkan <b>1.2</b>, because the "11" names the feature set it
    /// aggregates. The gate is this GPU's
    /// <c>VkPhysicalDeviceProperties.apiVersion</c>, read with a plain
    /// (un-chained) <c>vkGetPhysicalDeviceProperties</c>.
    /// <para>The gate reads the <b>device</b> api version only. It assumes the
    /// <see cref="Instance"/> this GPU came from was itself created at
    /// <paramref name="minimumApiVersion"/> or above —
    /// <c>vkGetPhysicalDeviceProperties2</c> is an instance-scope entry point,
    /// and the validation layer's stateless <c>pNext</c> checks for
    /// physical-device commands key off the <i>instance</i> version. The
    /// default satisfies this
    /// (<see cref="InstanceDescription.ApiVersion"/> defaults to
    /// <c>V1_4</c>); a caller who deliberately lowers it is responsible for
    /// not querying above it.</para>
    /// </param>
    /// <param name="properties">
    /// The filled struct on <see langword="true"/>; <c>default</c> on
    /// <see langword="false"/>.
    /// </param>
    public bool TryGetProperties<T>(VulkanVersion minimumApiVersion, out T properties)
        where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
    {
        if (ReadApiVersion() < minimumApiVersion.Packed)
        {
            properties = default;
            return false;
        }

        QueryChained(out properties);
        return true;
    }

    /// <summary>
    /// The gate-free two-node query: root + <typeparamref name="T"/>, one
    /// <c>vkGetPhysicalDeviceProperties2</c>, copy the node out by value.
    /// Private because calling it without a gate is exactly the hazard the
    /// public overloads exist to prevent.
    /// </summary>
    /// <remarks>
    /// The scratch is sized from two compile-time-known struct sizes plus 16
    /// bytes — two 8-byte absolute-address pads, one per node, per
    /// <c>ChainBuilder.Reserve</c>. ILC constant-folds both size terms per
    /// instantiation. No <c>Clear()</c> is needed: <c>Reserve</c> zeroes each
    /// slot before <c>WriteHeader</c> runs.
    /// </remarks>
    private void QueryChained<T>(out T properties)
        where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
    {
        Span<byte> scratch = stackalloc byte[
            sizeof(VkPhysicalDeviceProperties2) + Unsafe.SizeOf<T>() + 16];

        var chain = ChainBuilder.For<VkPhysicalDeviceProperties2>(scratch);
        chain.Root();
        ref T node = ref chain.Push<T>();
        Vk.vkGetPhysicalDeviceProperties2(Handle, chain.Head);
        properties = node;
    }

    /// <summary>
    /// The <c>VkPhysicalDeviceMeshShaderPropertiesEXT</c> workgroup bounds a
    /// <see cref="CommandRecorder.DrawMeshTasks"/> dispatch must obey, as a
    /// flat <see cref="MeshShaderLimits"/>.
    /// </summary>
    /// <param name="limits">
    /// The projection on <see langword="true"/>; <c>default</c> on
    /// <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when this GPU does not advertise
    /// <c>VK_EXT_mesh_shader</c>.
    /// </returns>
    /// <remarks>
    /// <see langword="false"/> means the <b>physical device</b> does not
    /// advertise the extension. It does <b>not</b> mean the extension was
    /// enabled on any <see cref="Device"/> — this is a physical-device query,
    /// and the limits are deliberately readable before
    /// <see cref="CreateDevice"/> so a caller can size its dispatch while it is
    /// still choosing a GPU. To actually record a mesh draw, pass
    /// <see cref="VulkanExtensions.ExtMeshShader"/> in
    /// <see cref="DeviceDescription.Extensions"/> and enable the
    /// <c>meshShader</c> feature as well.
    /// <para><b>Cost.</b> The name-gated
    /// <see cref="TryGetProperties{T}(ReadOnlySpan{byte}, out T)"/> path:
    /// three native queries when the extension is present
    /// (<c>vkEnumerateDeviceExtensionProperties</c> twice, then
    /// <c>vkGetPhysicalDeviceProperties2</c>), two when it is not, and no
    /// caching. Setup-time — read it once and keep the struct.</para>
    /// </remarks>
    public bool TryGetMeshShaderLimits(out MeshShaderLimits limits)
    {
        if (!TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(
                DeviceExtensionNames.MeshShader, out var p))
        {
            limits = default;
            return false;
        }

        limits = new MeshShaderLimits
        {
            MaxTaskWorkGroupCountX      = p.maxTaskWorkGroupCount[0],
            MaxTaskWorkGroupCountY      = p.maxTaskWorkGroupCount[1],
            MaxTaskWorkGroupCountZ      = p.maxTaskWorkGroupCount[2],
            MaxTaskWorkGroupTotalCount  = p.maxTaskWorkGroupTotalCount,
            MaxTaskWorkGroupInvocations = p.maxTaskWorkGroupInvocations,

            MaxMeshWorkGroupCountX      = p.maxMeshWorkGroupCount[0],
            MaxMeshWorkGroupCountY      = p.maxMeshWorkGroupCount[1],
            MaxMeshWorkGroupCountZ      = p.maxMeshWorkGroupCount[2],
            MaxMeshWorkGroupTotalCount  = p.maxMeshWorkGroupTotalCount,
            MaxMeshWorkGroupInvocations = p.maxMeshWorkGroupInvocations,
        };
        return true;
    }

    /// <summary>
    /// The <c>VkPhysicalDeviceAccelerationStructurePropertiesKHR</c> values an
    /// acceleration-structure consumer has to obey — above all the
    /// scratch-address alignment every
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> must satisfy —
    /// as a flat <see cref="AccelerationStructureLimits"/>.
    /// </summary>
    /// <param name="limits">
    /// The projection on <see langword="true"/>; <c>default</c> on
    /// <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when this GPU does not advertise
    /// <c>VK_KHR_acceleration_structure</c>.
    /// </returns>
    /// <remarks>
    /// <see langword="false"/> means the <b>physical device</b> does not
    /// advertise the extension. It does <b>not</b> mean the extension was
    /// enabled on any <see cref="Device"/> — this is a physical-device query,
    /// and the limits are deliberately readable before
    /// <see cref="CreateDevice"/> so a caller can reject a GPU on
    /// <see cref="AccelerationStructureLimits.MaxInstanceCount"/> while it is
    /// still choosing one. To actually build an acceleration structure, enable
    /// all three extensions and the three features —
    /// <see cref="VulkanExtensions.KhrAccelerationStructure"/> carries the full
    /// recipe.
    /// <para><b>Cost.</b> The name-gated
    /// <see cref="TryGetProperties{T}(ReadOnlySpan{byte}, out T)"/> path:
    /// three native queries when the extension is present
    /// (<c>vkEnumerateDeviceExtensionProperties</c> twice, then
    /// <c>vkGetPhysicalDeviceProperties2</c>), two when it is not, and no
    /// caching. Setup-time — read it once and keep the struct.</para>
    /// </remarks>
    public bool TryGetAccelerationStructureLimits(out AccelerationStructureLimits limits)
    {
        if (!TryGetProperties<VkPhysicalDeviceAccelerationStructurePropertiesKHR>(
                DeviceExtensionNames.AccelerationStructure, out var p))
        {
            limits = default;
            return false;
        }

        limits = new AccelerationStructureLimits
        {
            MinScratchOffsetAlignment = p.minAccelerationStructureScratchOffsetAlignment,
            MaxGeometryCount          = p.maxGeometryCount,
            MaxInstanceCount          = p.maxInstanceCount,
            MaxPrimitiveCount         = p.maxPrimitiveCount,
            MaxPerStageDescriptorAccelerationStructures =
                p.maxPerStageDescriptorAccelerationStructures,
            MaxDescriptorSetAccelerationStructures =
                p.maxDescriptorSetAccelerationStructures,
        };
        return true;
    }

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

        // The one place where "VMA wants a budget" and "the device enables the
        // extension that supplies it" are both in scope. VMA silently degrades
        // to its own estimates when the extension is missing, so without this
        // check the mistake surfaces only as numbers that look plausible and
        // are wrong (issue #218).
        if (AhjoValidation.IsEnabled && desc.Allocator.EnableMemoryBudget && !ContainsExtension(desc.Extensions, "VK_EXT_memory_budget"u8))
        {
            AhjoValidation.Fail("PhysicalDevice.CreateDevice",
                "AllocatorDescription.EnableMemoryBudget is set but VK_EXT_memory_budget is not in DeviceDescription.Extensions. " +
                "VMA needs the device extension enabled at vkCreateDevice time; add VulkanExtensions.ExtMemoryBudget to the list.");
        }

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

        // 1.0-era core feature flags ride on VkPhysicalDeviceFeatures2.
        // The wrapper queries the device first and only enables the
        // intersection of the "3D-game baseline" with what's actually
        // advertised — vkCreateDevice rejects requests for unsupported
        // features, so we cannot just flip every bit unconditionally.
        // Bits explicitly handled here:
        //   samplerAnisotropy   - anisotropic mip filtering on textures
        //   depthClamp          - clamp instead of clip in shadow passes
        //   fillModeNonSolid    - wireframe debug rendering
        //   independentBlend    - per-attachment blend state for MRT
        //   imageCubeArray      - cubemap arrays
        // Coverage on desktop Vulkan-1.2+ drivers is essentially universal
        // for these; anything the device lacks stays at zero and the
        // configurer can opt back in (or read the resulting struct to see
        // what landed).
        VkPhysicalDeviceFeatures supported;
        Vk.vkGetPhysicalDeviceFeatures(Handle, &supported);

        ref var f2 = ref chain.Push<VkPhysicalDeviceFeatures2>();
        f2.features.samplerAnisotropy = supported.samplerAnisotropy;
        f2.features.depthClamp        = supported.depthClamp;
        f2.features.fillModeNonSolid  = supported.fillModeNonSolid;
        f2.features.independentBlend  = supported.independentBlend;
        f2.features.imageCubeArray    = supported.imageCubeArray;

        ref var f12 = ref chain.Push<VkPhysicalDeviceVulkan12Features>();
        f12.bufferDeviceAddress         = 1;
        f12.timelineSemaphore           = 1;
        // Required to use VK_IMAGE_LAYOUT_DEPTH_ATTACHMENT_OPTIMAL (and the
        // matching stencil/read-only variants) on depth-only / stencil-only
        // images. Without it any depth-attachment workflow has to fall back
        // to the legacy combined VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL,
        // and barriers carrying the modern layouts are silently invalid —
        // a frequent foot-gun for first-time 3D-game-style samples.
        f12.separateDepthStencilLayouts = 1;
        ref var f13 = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
        f13.synchronization2 = 1;
        f13.dynamicRendering = 1;

        // The 1.4 features struct only lands in the chain when the device
        // actually advertises Vulkan 1.4. On a 1.3 (or older) ICD, sType 55
        // is unrecognized — most loaders pass the chain through, then the
        // driver either silently ignores it (SwiftShader on Linux logs
        // "UNSUPPORTED: pCreateInfo->pNext sType = 55" and walks on) or
        // SIGSEGVs later when downstream paths assume the requested
        // features were granted. Same shape as the VMA apiVersion clamp
        // in Allocator.Create. Callers needing pushDescriptor on a
        // sub-1.4 device should request VK_KHR_push_descriptor via
        // DeviceDescription.Extensions and skip f14 entirely.
        VkPhysicalDeviceProperties deviceProps;
        Vk.vkGetPhysicalDeviceProperties(Handle, &deviceProps);
        VkPhysicalDeviceVulkan14Features f14Local = default;
        ref VkPhysicalDeviceVulkan14Features f14 = ref f14Local;
        if (deviceProps.apiVersion >= VulkanVersion.V1_4.Packed)
        {
            f14 = ref chain.Push<VkPhysicalDeviceVulkan14Features>();
            f14.pushDescriptor = 1;
        }

        // Hand the configurer ref access to the wrapper's pre-pushed
        // structs so it can flip additional bits in-place without
        // producing a duplicate-sType chain (issue 53). The duplicate-
        // sType validator below still catches callers that push their
        // own copy through `chain`. On a sub-1.4 device, mutations to
        // f14 are silently dropped because the struct is a stack local
        // not threaded into the chain.
        desc.ConfigureFeatures?.Invoke(ref chain, ref f2, ref f12, ref f13, ref f14);

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
            var device = new Device(raw, physicalDevice: this, queues, desc.Extensions, desc.Allocator);
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
    /// <see langword="true"/> when <paramref name="extensions"/> contains
    /// <paramref name="name"/>. Byte-wise comparison straight off each
    /// <see cref="Utf8Name.Ptr"/> — no decoding, no allocation. Cold path
    /// (device creation, validation on), over a handful of names.
    /// </summary>
    internal static bool ContainsExtension(ReadOnlySpan<Utf8Name> extensions, ReadOnlySpan<byte> name)
    {
        for (int i = 0; i < extensions.Length; i++)
        {
            sbyte* p = extensions[i].Ptr;
            if (p == null) continue;

            int j = 0;
            while (j < name.Length && (byte)p[j] == name[j]) j++;
            // Matched every byte AND the candidate ends there: "VK_EXT_memory"
            // must not match a longer name that starts with it.
            if (j == name.Length && p[j] == 0) return true;
        }
        return false;
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

    /// <summary>
    /// Packed <c>VkPhysicalDeviceProperties.apiVersion</c>, read into a stack
    /// struct. Deliberately the un-chained
    /// <c>vkGetPhysicalDeviceProperties</c>: this is the call that decides
    /// whether a node is safe to put in a <c>VkPhysicalDeviceProperties2</c>
    /// chain at all, so it cannot itself use one.
    /// </summary>
    private uint ReadApiVersion()
    {
        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(Handle, &props);
        return props.apiVersion;
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
