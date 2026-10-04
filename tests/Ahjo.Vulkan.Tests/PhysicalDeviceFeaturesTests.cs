using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="PhysicalDevice.TryGetFeatures{T}(ReadOnlySpan{byte}, out T)"/>
/// and its two siblings — the gated, pre-<c>vkCreateDevice</c> read of one
/// <c>VkPhysicalDeviceFeatures2</c> <c>pNext</c> struct (#246).
/// </summary>
/// <remarks>
/// <para>The mechanism is covered <b>unconditionally</b> at
/// <c>[gate:driver]</c>, on <b>every</b> adapter the host exposes. The core
/// feature structs are the oracle that the driver actually filled the node:
/// <c>VkPhysicalDeviceVulkan11Features</c> (core Vulkan <b>1.2</b> — the "11"
/// names the feature set, not the version that defines it), <c>…12</c> and
/// <c>…13</c> must byte-match the picker's eager read past the 16-byte header,
/// and the wrapper's device floor is 1.3, so every host with an ICD the wrapper
/// will talk to exposes all three. The extension gate is asserted as a
/// <i>relationship</i> (<c>TryGetFeatures(…) == SupportsExtension(…)</c>) for
/// both device-fault structs, so it is green on hosts with and without
/// either extension.</para>
/// <para>Only the "required feature is reported" case is
/// <c>[gate:feature]</c>, and the validation cases additionally carry
/// <c>[gate:validation]</c>; the KHR one requires a layer of at least
/// <see cref="DeviceFaultTests.MinKhrLayer"/> (1.4.363). The hosted Windows CI
/// runner exposes neither device-fault extension, so a CI run that reports the
/// <c>[gate:feature]</c> case as skipped is the expected outcome, not a failure
/// to fix. The device-create proof of #246 — enabling
/// <c>deviceFaultVendorBinary</c> exactly when reported — lives in
/// <see cref="DeviceFaultTests"/>.</para>
/// <para><b>Forbidden.</b> No test enables <c>deviceFaultVendorBinary</c> on an
/// adapter that reports 0 to "prove" the gate. That would test the driver, and
/// a failing <c>vkCreateDevice</c> pollutes validation captures.</para>
/// </remarks>
public sealed unsafe class PhysicalDeviceFeaturesTests(ITestOutputHelper output)
{
    // ---- [gate:driver] — the mechanism, on every adapter. ----

    [Fact]
    public void TryGetFeatures_CoreStructs_MatchPickerView()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        foreach (var a in WalkAdapters(instance))
        {
            bool v12 = a.ApiVersion >= VulkanVersion.V1_2.Packed;
            bool v13 = a.ApiVersion >= VulkanVersion.V1_3.Packed;

            // V1_2, not V1_1: VkPhysicalDeviceVulkan11Features is defined by
            // Vulkan 1.2.
            bool ok11 = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan11Features>(VulkanVersion.V1_2, out var f11);
            AssertQueried(v12, ok11, in f11, a.F11);

            bool ok12 = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan12Features>(VulkanVersion.V1_2, out var f12);
            AssertQueried(v12, ok12, in f12, a.F12);

            bool ok13 = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan13Features>(VulkanVersion.V1_3, out var f13);
            AssertQueried(v13, ok13, in f13, a.F13);

            output.WriteLine(
                $"{a.Name} (api {TestGate.Fmt(a.ApiVersion)}): Vulkan11={ok11} Vulkan12={ok12} Vulkan13={ok13}");
        }
    }

    [Fact]
    public void TryGetFeatures_Vulkan14_VersionGate_MatchesDeviceApiVersion()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        foreach (var a in WalkAdapters(instance))
        {
            // A 1.3 device must never see the sType-55 node — the SwiftShader
            // failure Instance.PickPhysicalDevice gates against, on this root.
            bool expected = a.ApiVersion >= VulkanVersion.V1_4.Packed;
            bool ok = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan14Features>(VulkanVersion.V1_4, out var f14);
            AssertQueried(expected, ok, in f14, a.F14);
        }
    }

    [Fact]
    public void TryGetFeatures_DeviceFaultStructs_GateMatchesSupportsExtension()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        foreach (var a in WalkAdapters(instance))
        {
            bool extAdvertised = a.Gpu.SupportsExtension(VulkanExtensions.ExtDeviceFault);
            bool khrAdvertised = a.Gpu.SupportsExtension(VulkanExtensions.KhrDeviceFault);

            bool extByName = a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(
                VulkanExtensions.ExtDeviceFault, out var extA);
            bool extBySpan = a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(
                "VK_EXT_device_fault"u8, out var extB);
            AssertGated(extAdvertised, extByName, in extA);
            AssertGated(extAdvertised, extBySpan, in extB);

            bool khrByName = a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(
                VulkanExtensions.KhrDeviceFault, out var khrA);
            bool khrBySpan = a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(
                "VK_KHR_device_fault"u8, out var khrB);
            AssertGated(khrAdvertised, khrByName, in khrA);
            AssertGated(khrAdvertised, khrBySpan, in khrB);

            string ext = extByName
                ? $"EXT deviceFault={extA.deviceFault} deviceFaultVendorBinary={extA.deviceFaultVendorBinary}"
                : "EXT not advertised";
            string khr = khrByName
                ? $"KHR deviceFault={khrA.deviceFault} deviceFaultVendorBinary={khrA.deviceFaultVendorBinary} " +
                  $"deviceFaultReportMasked={khrA.deviceFaultReportMasked} " +
                  $"deviceFaultDeviceLostOnMasked={khrA.deviceFaultDeviceLostOnMasked}"
                : "KHR not advertised";
            output.WriteLine($"{a.Name}: {ext}; {khr}");
        }
    }

    [Fact]
    public void TryGetFeatures_NullOrEmptyName_ReturnsFalse()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        foreach (var a in WalkAdapters(instance))
        {
            Assert.False(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(default(Utf8Name), out var byName));
            AssertAllZero(in byName);

            Assert.False(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(ReadOnlySpan<byte>.Empty, out var bySpan));
            AssertAllZero(in bySpan);
        }
    }

    [Fact]
    public void TryGetFeatures_UnadvertisedName_ReturnsFalse()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        foreach (var a in WalkAdapters(instance))
        {
            Assert.False(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(
                "VK_EXT_this_does_not_exist"u8, out var f));
            AssertAllZero(in f);
        }
    }

    // ---- [gate:feature] ----

    [Fact]
    public void DeviceFault_RequiredFeature_ReportedWhereverAdvertised()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var adapters = WalkAdapters(instance);
        TestGate.RequireDeviceFeature(
            adapters.Exists(a => a.HasExtFault || a.HasKhrFault),
            "No GPU exposes VK_EXT_device_fault or VK_KHR_device_fault.");

        foreach (var a in adapters)
        {
            // vk.xml:27467 / :30973 — deviceFault is the one required feature
            // of each extension, so it is reported wherever the extension is.
            if (a.HasExtFault)
            {
                Assert.True(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(
                    VulkanExtensions.ExtDeviceFault, out var ext));
                Assert.Equal(1u, ext.deviceFault);
            }
            if (a.HasKhrFault)
            {
                Assert.True(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(
                    VulkanExtensions.KhrDeviceFault, out var khr));
                Assert.Equal(1u, khr.deviceFault);
            }
        }
    }

    // ---- [gate:validation] ----

    [Fact]
    public void TryGetFeatures_CoreAndExt_UnderValidation_Clean()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        output.WriteLine($"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}");

        foreach (var a in WalkAdapters(instance))
        {
            bool ok11 = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan11Features>(VulkanVersion.V1_2, out _);
            bool ok12 = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan12Features>(VulkanVersion.V1_2, out _);
            bool ok13 = a.Gpu.TryGetFeatures<VkPhysicalDeviceVulkan13Features>(VulkanVersion.V1_3, out _);
            bool okExt = a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(VulkanExtensions.ExtDeviceFault, out _);
            output.WriteLine($"{a.Name}: Vulkan11={ok11} Vulkan12={ok12} Vulkan13={ok13} EXT fault={okExt}");
        }

        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void TryGetFeatures_Khr_UnderValidation_Clean()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(
            DeviceFaultTests.MinKhrLayer,
            "The KHR device-fault feature struct needs a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        output.WriteLine($"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}");

        foreach (var a in WalkAdapters(instance))
        {
            // Gated out (no native call with the KHR node) on an adapter that
            // does not advertise VK_KHR_device_fault.
            bool okKhr = a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(VulkanExtensions.KhrDeviceFault, out _);
            output.WriteLine($"{a.Name}: KHR fault={okKhr}");
        }

        AssertNoValidationErrors(errors);
    }

    // ---- Helpers (internal static: DeviceFaultTests reuses them) ----

    /// <summary>
    /// One adapter as the picker saw it. Every feature struct is a by-value
    /// copy: the <see cref="PhysicalDeviceInfo"/> refs die when the callback
    /// returns. <see cref="F14"/> is <c>default</c> when
    /// <see cref="ApiVersion"/> is below 1.4, matching the picker, which does
    /// not put the 1.4 node in its chain there.
    /// </summary>
    internal readonly record struct AdapterSnapshot(
        PhysicalDevice                   Gpu,
        string                           Name,
        uint                             ApiVersion,
        uint                             GraphicsFamily,
        bool                             HasGraphics,
        bool                             HasExtFault,
        bool                             HasKhrFault,
        VkPhysicalDeviceVulkan11Features F11,
        VkPhysicalDeviceVulkan12Features F12,
        VkPhysicalDeviceVulkan13Features F13,
        VkPhysicalDeviceVulkan14Features F14);

    /// <summary>
    /// Every adapter on the host, via an all-<see langword="false"/>
    /// <see cref="Instance.PickPhysicalDevice"/> walk (the Ahjo
    /// <c>SnapshotAdapters</c> pattern). The picker throws
    /// <c>VK_ERROR_INITIALIZATION_FAILED</c> when nothing matched, which is the
    /// expected end of the walk once at least one adapter was seen.
    /// </summary>
    internal static List<AdapterSnapshot> WalkAdapters(Instance instance)
    {
        var list = new List<AdapterSnapshot>();
        try
        {
            instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                uint family = uint.MaxValue;
                bool hasGraphics = false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    if (info.QueueFamilies[i].SupportsGraphics)
                    {
                        family = info.QueueFamilies[i].Index;
                        hasGraphics = true;
                        break;
                    }
                }

                uint api = info.Properties.apiVersion;
                list.Add(new AdapterSnapshot(
                    Gpu:            info.Device,
                    Name:           Utf8.FromBounded(MemoryMarshal.Cast<byte, sbyte>(info.Name)),
                    ApiVersion:     api,
                    GraphicsFamily: family,
                    HasGraphics:    hasGraphics,
                    HasExtFault:    info.SupportsExtension(DeviceExtensionNames.ExtDeviceFault),
                    HasKhrFault:    info.SupportsExtension(DeviceExtensionNames.KhrDeviceFault),
                    F11:            info.Features11,
                    F12:            info.Features12,
                    F13:            info.Features13,
                    F14:            api >= VulkanVersion.V1_4.Packed ? info.Features14 : default));
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED && list.Count > 0)
        {
            // Walked every adapter; none was "picked", by design.
        }
        return list;
    }

    /// <summary>
    /// A version- or name-gated query against its expected gate result: on
    /// <see langword="true"/> the header was written from <c>T.SType</c>, the
    /// tail's <c>pNext</c> is null, and the body matches the picker's read; on
    /// <see langword="false"/> the struct came back untouched.
    /// </summary>
    private static void AssertQueried<T>(bool expected, bool ok, in T queried, in T snapshot)
        where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>
    {
        AssertGated(expected, ok, in queried);
        if (ok) AssertBodyEqual(in snapshot, in queried);
    }

    private static void AssertGated<T>(bool expected, bool ok, in T queried)
        where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>
    {
        Assert.Equal(expected, ok);
        if (!ok)
        {
            AssertAllZero(in queried);
            return;
        }

        ref readonly VkBaseOutStructure header =
            ref Unsafe.As<T, VkBaseOutStructure>(ref Unsafe.AsRef(in queried));
        // The mechanism wrote sType from T.SType — the caller never passed one.
        Assert.Equal(T.SType, header.sType);
        // The node was the chain tail, so nothing points into the dead frame.
        Assert.True(header.pNext == null);
    }

    /// <summary>
    /// Byte-compares two copies of a features struct past the 16-byte
    /// <c>sType</c>/<c>pNext</c> header (<c>sizeof(VkBaseOutStructure)</c>):
    /// the picker's copy has a non-null <c>pNext</c>, the query's is null.
    /// </summary>
    /// <remarks>
    /// Comparing trailing padding is safe because both sides are zeroed before
    /// the driver writes: <c>ChainBuilder.Reserve</c> clears each slot
    /// (<c>Memory/ChainBuilder.cs</c>), and the picker clears its features
    /// scratch before building the chain (<c>Lifecycle/Instance.cs</c>,
    /// <c>featuresChain.Clear()</c>). The by-value copies land in
    /// zero-initialised storage, so padding is zero whether or not a copy
    /// carries it.
    /// </remarks>
    internal static void AssertBodyEqual<T>(in T expected, in T actual) where T : unmanaged
    {
        int header = sizeof(VkBaseOutStructure);
        ReadOnlySpan<byte> e = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in expected), 1))[header..];
        ReadOnlySpan<byte> a = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in actual), 1))[header..];
        for (int i = 0; i < e.Length; i++)
        {
            if (e[i] != a[i])
                Assert.Fail(
                    $"{typeof(T).Name}: byte {i + header} differs — picker view {e[i]}, TryGetFeatures {a[i]}.");
        }
    }

    /// <summary>
    /// Byte-wise "came back untouched". <c>Assert.Equal(default, x)</c> is not
    /// usable on the generated structs: some carry <c>[InlineArray]</c> fields,
    /// and the runtime throws <see cref="NotSupportedException"/> from
    /// <c>ValueType.Equals</c> for those rather than comparing them.
    /// </summary>
    internal static void AssertAllZero<T>(in T value) where T : unmanaged
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in value), 1));
        for (int i = 0; i < bytes.Length; i++)
            Assert.Equal(0, bytes[i]);
    }

    private static Instance CreateValidatedInstance(List<DebugMessage> errors)
        => Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

    private static void AssertNoValidationErrors(List<DebugMessage> errors)
    {
        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }
}
