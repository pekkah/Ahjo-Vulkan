using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Device-level coverage of the <c>VK_EXT_device_fault</c> /
/// <c>VK_KHR_device_fault</c> readout (#242): entry-point gating in
/// <c>Internal/DeviceFunctionTable</c>, <see cref="Device.DeviceFaultApi"/>
/// selection, the <see cref="Device.IsLost"/> gate on
/// <see cref="Device.TryGetDeviceFault(TimeSpan, out DeviceFaultReport)"/>,
/// argument validation, and one real-driver call of the KHR reports protocol.
/// The protocols themselves are covered driverless by
/// <see cref="DeviceFaultReaderTests"/>.
/// </summary>
/// <remarks>
/// <para><b>Forbidden.</b> No test here calls <c>MarkLost()</c> →
/// <c>TryGetDeviceFault()</c>, <c>DeviceFaultReader.ReadExt</c>, or
/// <c>DeviceFaultReader.ReadKhrDebugInfo</c> on an extension-enabled healthy
/// device: each reaches a command whose valid usage requires a lost device
/// (VUID-vkGetDeviceFaultInfoEXT-device-07336,
/// VUID-vkGetDeviceFaultDebugInfoKHR-device-12383) on hardware that is not
/// lost. Under validation the 1.4.363 layer would flag it — it tracks loss from
/// real <c>VK_ERROR_DEVICE_LOST</c> returns and knows nothing of the wrapper's
/// <c>MarkLost</c> seam. They are the obvious tests to write by mistake. The
/// same rule (VUID-12383) covers the shader-abort cases (#245): no test calls
/// <c>ReadKhrDebugInfo</c> with abort chaining on a healthy device; they only
/// create the device and check what was captured.</para>
/// <para><b>Also forbidden.</b> No test enables <c>deviceFaultVendorBinary</c>
/// on an adapter that reports 0 to "prove" the
/// <see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/> gate. That
/// would test the driver, and a failing <c>vkCreateDevice</c> pollutes
/// validation captures.</para>
/// <para><b>Tiers.</b> The no-extension, disposed and timeout cases are
/// <c>[gate:driver]</c> and run on any host with an ICD. Everything that needs
/// either extension is <c>[gate:feature]</c>: the hosted Windows CI runner has
/// neither, so a CI run that reports every one of those as skipped is the
/// expected outcome, not a failure to fix. The validation cases additionally
/// carry <c>[gate:validation]</c>; the KHR ones require a layer of at least
/// 1.4.363, the first that knows <c>VK_KHR_device_fault</c>, and skip (not
/// fail) on an older one — an older layer reporting an unknown sType is a gap
/// in the oracle, not a wrapper defect. The vendor-binary cases (#246) follow
/// the same tiers: <c>ExtVendorBinary_EnabledExactlyWhenReported_CreatesDevice</c>
/// is <c>[gate:feature]</c>, its validated twin adds <c>[gate:validation]</c>,
/// and the KHR one adds the 1.4.363 floor. Each runs on <b>every</b> adapter
/// advertising the extension, enabling <c>deviceFaultVendorBinary</c> exactly
/// when <see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/>
/// reports it — on a host with mixed adapters that runs both the 1 and the 0
/// branch.</para>
/// <para><b>Polling (#244).</b> The <see cref="Device.TryPollDeviceFaults"/>
/// cases call only the reports command, which is legal on a healthy device (it
/// has no lost-state valid usage), so they run on healthy hardware without
/// touching the forbidden list above.</para>
/// </remarks>
public sealed unsafe class DeviceFaultTests(ITestOutputHelper output)
{
    // Packed 1.4.363: the pinned Vulkan-Headers version
    // (Directory.Build.props:18) and the first validation layer verified to
    // know VK_KHR_device_fault.
    internal const uint MinKhrLayer = (1u << 22) | (4u << 12) | 363u;

    private const string ExtSkipReason  = "No GPU exposes VK_EXT_device_fault with the deviceFault feature.";
    private const string KhrSkipReason  = "No GPU exposes VK_KHR_device_fault with the deviceFault feature.";
    private const string BothSkipReason =
        "No GPU exposes both VK_EXT_device_fault and VK_KHR_device_fault with the deviceFault feature.";
    private const string ShaderAbortSkipReason =
        "No GPU exposes VK_KHR_shader_abort with the shaderAbort feature.";

    // ---- [gate:driver] ----

    [Fact]
    public void NoExtension_ApiNone_TryGetFalse_EvenAfterMarkLost()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.Equal(DeviceFaultApi.None, device.DeviceFaultApi);
        Assert.False(device.IsDeviceFaultEnabled);
        Assert.False(device.TryGetDeviceFault(out DeviceFaultReport? before));
        Assert.Null(before);

        // Safe: with neither extension enabled there is no pointer to call.
        device.MarkLost();
        Assert.False(device.TryGetDeviceFault(out DeviceFaultReport? after));
        Assert.Null(after);
    }

    [Fact]
    public void Disposed_TryGetDeviceFault_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var device = CreateGraphicsDevice(instance, out _);
        device.Dispose();

        Assert.Throws<ObjectDisposedException>(() => device.TryGetDeviceFault(out _));
        Assert.Throws<ObjectDisposedException>(() => device.TryGetDeviceFault(TimeSpan.Zero, out _));
    }

    [Fact]
    public void NegativeOrInfiniteTimeout_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryGetDeviceFault(TimeSpan.FromMilliseconds(-1), out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryGetDeviceFault(Timeout.InfiniteTimeSpan, out _));
    }

    /// <summary>
    /// <c>ToVulkanTimeout</c> saturates any span whose nanosecond count
    /// overflows to <c>UINT64_MAX</c> — Vulkan's infinite wait — so those are
    /// refused too. The check runs before the extension and
    /// <see cref="Device.IsLost"/> gates, which is what makes it testable on a
    /// healthy device with no device-fault extension.
    /// </summary>
    [Fact]
    public void UnboundedTimeout_MaxValueOrOverflowing_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryGetDeviceFault(TimeSpan.MaxValue, out _));
        // The first span past the ns overflow boundary.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryGetDeviceFault(TimeSpan.FromTicks(long.MaxValue / 100 + 1), out _));

        // A large but representable span is accepted: no extension ⇒ false.
        Assert.False(device.TryGetDeviceFault(TimeSpan.FromTicks(long.MaxValue / 100), out _));
        Assert.False(device.TryGetDeviceFault(TimeSpan.FromDays(365 * 100), out _));
    }

    [Fact]
    public void Poll_NoKhrExtension_ReturnsFalse_EmptyArray()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.False(device.TryPollDeviceFaults(TimeSpan.Zero, out DeviceFaultEntry[] before));
        Assert.NotNull(before);
        Assert.Empty(before);

        // Safe: with no KHR extension enabled there is no pointer to call.
        device.MarkLost();
        Assert.False(device.TryPollDeviceFaults(TimeSpan.Zero, out DeviceFaultEntry[] after));
        Assert.NotNull(after);
        Assert.Empty(after);
    }

    [Fact]
    public void Poll_Disposed_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var device = CreateGraphicsDevice(instance, out _);
        device.Dispose();

        Assert.Throws<ObjectDisposedException>(() => device.TryPollDeviceFaults(TimeSpan.Zero, out _));
    }

    [Fact]
    public void Poll_NegativeInfiniteOrSaturatingTimeout_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryPollDeviceFaults(TimeSpan.FromMilliseconds(-1), out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryPollDeviceFaults(Timeout.InfiniteTimeSpan, out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => device.TryPollDeviceFaults(TimeSpan.MaxValue, out _));
    }

    // ---- [gate:feature] ----

    [Fact]
    public void ExtOnly_ResolvesEntryPoint_ApiExt()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: true, khr: false, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, ExtSkipReason);

        output.WriteLine("EXT-only device: " + GpuName(device!));
        Assert.True(device!.Functions.GetDeviceFaultInfo != null);
        Assert.True(device.Functions.GetDeviceFaultReports == null);
        Assert.True(device.Functions.GetDeviceFaultDebugInfo == null);
        Assert.Equal(DeviceFaultApi.Ext, device.DeviceFaultApi);
        Assert.True(device.IsDeviceFaultEnabled);
    }

    [Fact]
    public void KhrOnly_ResolvesBothEntryPoints_ApiKhr()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        output.WriteLine("KHR-only device: " + GpuName(device!));
        Assert.True(device!.Functions.GetDeviceFaultInfo == null);
        Assert.True(device.Functions.GetDeviceFaultReports != null);
        Assert.True(device.Functions.GetDeviceFaultDebugInfo != null);
        Assert.Equal(DeviceFaultApi.Khr, device.DeviceFaultApi);
        Assert.True(device.IsDeviceFaultEnabled);
    }

    [Fact]
    public void Both_ApiKhr()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: true, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, BothSkipReason);

        output.WriteLine("EXT+KHR device: " + GpuName(device!));
        Assert.True(device!.Functions.GetDeviceFaultInfo != null);
        Assert.True(device.Functions.GetDeviceFaultReports != null);
        Assert.True(device.Functions.GetDeviceFaultDebugInfo != null);
        Assert.Equal(DeviceFaultApi.Khr, device.DeviceFaultApi);
    }

    [Fact]
    public void HealthyDevice_TryGetReturnsFalse()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);

        using (Device? extDevice = TryCreateFaultDevice(instance, ext: true, khr: false, pushFeatures: true, out _))
        {
            TestGate.RequireDeviceFeature(extDevice is not null, ExtSkipReason);
            Assert.False(extDevice!.IsLost);
            // If the IsLost gate were missing, the read would run and return true.
            Assert.False(extDevice.TryGetDeviceFault(out DeviceFaultReport? report));
            Assert.Null(report);
        }

        using (Device? khrDevice = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _))
        {
            TestGate.RequireDeviceFeature(khrDevice is not null, KhrSkipReason);
            Assert.False(khrDevice!.IsLost);
            // If the IsLost gate were missing, the read would run and return true.
            Assert.False(khrDevice.TryGetDeviceFault(TimeSpan.Zero, out DeviceFaultReport? report));
            Assert.Null(report);
        }
    }

    [Fact]
    public void Khr_RealDriver_ReportsCallOnHealthyDevice_TimesOut()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        // Legal per the spec: vkGetDeviceFaultReportsKHR has no lost-state
        // valid usage. Never do this with ReadExt or ReadKhrDebugInfo — both
        // reach a command that requires a lost device (07336 / 12383).
        VkResult r = DeviceFaultReader.ReadKhrEntries(
            device!.Functions.GetDeviceFaultReports, device.Handle, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        output.WriteLine(
            $"{GpuName(device)}: vkGetDeviceFaultReportsKHR (timeout 0, healthy device) returned {r}; " +
            $"entries={entries.Length}, incomplete={incomplete}");
        Assert.True((int)r >= 0, $"vkGetDeviceFaultReportsKHR returned {r}");
        Assert.Equal(VkResult.VK_TIMEOUT, r);
        Assert.Empty(entries);
        Assert.False(incomplete);
    }

    [Fact]
    public void Poll_HealthyKhrDevice_RealDriver_ReturnsFalse()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        bool polled = device!.TryPollDeviceFaults(TimeSpan.Zero, out DeviceFaultEntry[] entries);

        output.WriteLine(
            $"{GpuName(device)}: TryPollDeviceFaults(TimeSpan.Zero, healthy device) returned {polled}; " +
            $"entries={entries.Length}, IsLost={device.IsLost}");
        Assert.False(polled);
        Assert.NotNull(entries);
        Assert.Empty(entries);
        Assert.False(device.IsLost);
    }

    [Fact]
    public void Poll_AfterMarkLost_ReturnsFalse_WithoutDriverCall()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        // Safe: the poll's IsLost gate runs before any driver call, and the
        // reports command would be legal anyway. Never follow this with
        // TryGetDeviceFault — that is the forbidden test (class remarks).
        device!.MarkLost();
        Assert.False(device.TryPollDeviceFaults(TimeSpan.Zero, out DeviceFaultEntry[] entries));
        Assert.NotNull(entries);
        Assert.Empty(entries);
    }

    /// <summary>
    /// The no-fault poll is the one per-frame-callable device-fault path, so
    /// it must allocate nothing (#244). The
    /// <c>BuildAccelerationStructures_OneBuildOneGeometry_IsZeroAllocation</c>
    /// shape.
    /// </summary>
    [Fact]
    public void Poll_NoFault_IsZeroAllocation()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);
        // Without these the poll would return before its lock and driver call,
        // and a zero delta would prove nothing.
        Assert.True(device!.Functions.GetDeviceFaultReports != null);
        Assert.False(device.IsLost);

        // Warm: JIT + tier-up on every path the measured loop touches.
        for (int i = 0; i < 32; i++)
            device.TryPollDeviceFaults(TimeSpan.Zero, out _);

        // Two measured passes: a tier-1 -> tier-2 promotion can still fire on
        // the first measurement-sized loop and charge a one-shot allocation to
        // this thread. Only the second is asserted on.
        long before1 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++)
            device.TryPollDeviceFaults(TimeSpan.Zero, out _);
        _ = GC.GetAllocatedBytesForCurrentThread() - before1;

        long before2 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++)
            device.TryPollDeviceFaults(TimeSpan.Zero, out _);
        long after2 = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after2 - before2);
        Assert.False(device.IsLost);
    }

    /// <summary>
    /// A contended poll with a zero timeout returns at once, and allocates
    /// nothing (a per-frame poll behind a background poller takes this path).
    /// The contention must come from another thread: <see cref="Monitor"/> is
    /// reentrant, so a lock held by the test thread itself would let the poll
    /// through.
    /// </summary>
    [Fact]
    public void Poll_ContendedLock_TimeoutZero_ReturnsFalseImmediately()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        using var acquired = new ManualResetEventSlim();
        using var release  = new ManualResetEventSlim();
        object faultLock = device!.FaultReadLockForTests;
        var holder = new Thread(() =>
        {
            lock (faultLock)
            {
                acquired.Set();
                release.Wait();
            }
        });
        holder.Start();
        try
        {
            acquired.Wait(TestContext.Current.CancellationToken);
            Assert.False(device.TryPollDeviceFaults(TimeSpan.Zero, out DeviceFaultEntry[] entries));
            Assert.NotNull(entries);
            Assert.Empty(entries);

            // Zero allocation on the contended path, the
            // Poll_NoFault_IsZeroAllocation shape: warm, then two measured
            // passes, asserting on the second.
            for (int i = 0; i < 32; i++)
                device.TryPollDeviceFaults(TimeSpan.Zero, out _);

            long before1 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                device.TryPollDeviceFaults(TimeSpan.Zero, out _);
            _ = GC.GetAllocatedBytesForCurrentThread() - before1;

            long before2 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                device.TryPollDeviceFaults(TimeSpan.Zero, out _);
            long after2 = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0, after2 - before2);
        }
        finally
        {
            // Before the device's using scope ends: Dispose takes the same lock.
            release.Set();
            holder.Join();
        }
    }

    [Fact]
    public void ShaderAbort_Enabled_CapturedOnDevice()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateShaderAbortDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, ShaderAbortSkipReason);

        output.WriteLine(
            $"{GpuName(device!)}: ShaderAbortExtensionEnabled={device!.ShaderAbortExtensionEnabled}, " +
            $"FaultEntryPoints.ShaderAbortMessages={device.FaultEntryPoints.ShaderAbortMessages}");
        Assert.True(device.ShaderAbortExtensionEnabled);
        Assert.True(device.FaultEntryPoints.ShaderAbortMessages);
        Assert.Equal(DeviceFaultApi.Khr, device.DeviceFaultApi);

        // A plain KHR device (no VK_KHR_shader_abort) reports false for both.
        using Device? plain = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(plain is not null, KhrSkipReason);
        Assert.False(plain!.ShaderAbortExtensionEnabled);
        Assert.False(plain.FaultEntryPoints.ShaderAbortMessages);
    }

    // ---- [gate:validation] + [gate:feature] ----

    [Fact]
    public void ExtOnly_CreatesCleanly_UnderValidation()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        Device? device = TryCreateFaultDevice(instance, ext: true, khr: false, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, ExtSkipReason);

        output.WriteLine(
            $"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}; device {GpuName(device!)}");
        Assert.Equal(DeviceFaultApi.Ext, device!.DeviceFaultApi);
        device.Dispose();
        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void Both_CreateCleanly_UnderValidation()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(MinKhrLayer, "The KHR device-fault feature struct needs a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        Device? device = TryCreateFaultDevice(instance, ext: true, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, BothSkipReason);

        output.WriteLine(
            $"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}; device {GpuName(device!)}");
        Assert.Equal(DeviceFaultApi.Khr, device!.DeviceFaultApi);
        device.Dispose();
        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void Khr_CreatesCleanly_UnderValidation()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(MinKhrLayer, "The KHR device-fault feature struct needs a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        output.WriteLine(
            $"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}; device {GpuName(device!)}");
        Assert.Equal(DeviceFaultApi.Khr, device!.DeviceFaultApi);
        device.Dispose();
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// The real-driver reports call under a validated instance. The 1.4.363
    /// layer checks extension enablement, each element's sType/pNext and
    /// <c>VUID-vkGetDeviceFaultReportsKHR-pFaultCounts-arraylength</c> on this
    /// call, so a clean run proves those rules hold for the wrapper's structs.
    /// </summary>
    [Fact]
    public void Khr_RealDriverReportsCall_UnderValidation_Clean()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(MinKhrLayer, "The KHR reports call needs a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        // Legal: no lost-state valid usage on the reports command (see the
        // non-validated twin above). Never ReadExt / ReadKhrDebugInfo here.
        VkResult r = DeviceFaultReader.ReadKhrEntries(
            device!.Functions.GetDeviceFaultReports, device.Handle, 0,
            out DeviceFaultEntry[] entries, out bool incomplete);

        output.WriteLine(
            $"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}; " +
            $"{GpuName(device)}: vkGetDeviceFaultReportsKHR (timeout 0, healthy device) returned {r}; " +
            $"entries={entries.Length}, incomplete={incomplete}");
        Assert.True((int)r >= 0, $"vkGetDeviceFaultReportsKHR returned {r}");
        Assert.Equal(VkResult.VK_TIMEOUT, r);
        Assert.Empty(entries);
        device.Dispose();
        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void Poll_RealDriver_UnderValidation_Clean()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(MinKhrLayer, "The KHR reports call needs a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        Device? device = TryCreateFaultDevice(instance, ext: false, khr: true, pushFeatures: true, out _);
        TestGate.RequireDeviceFeature(device is not null, KhrSkipReason);

        bool polled = device!.TryPollDeviceFaults(TimeSpan.Zero, out DeviceFaultEntry[] entries);

        output.WriteLine(
            $"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}; " +
            $"{GpuName(device)}: TryPollDeviceFaults(TimeSpan.Zero, healthy device) returned {polled}; " +
            $"entries={entries.Length}");
        Assert.False(polled);
        Assert.Empty(entries);
        Assert.False(device.IsLost);
        device.Dispose();
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// The shader-abort create chain under the layer: the three extensions
    /// (the layer enforces the <c>VK_KHR_shader_constant_data</c> dependency
    /// as VUID-vkCreateDevice-ppEnabledExtensionNames-01387) and both feature
    /// structs. Create and dispose only — never a debug-info read (VUID-12383).
    /// </summary>
    [Fact]
    public void ShaderAbort_CreatesCleanly_UnderValidation()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(MinKhrLayer, "The KHR device-fault and shader-abort feature structs need a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        Device? device = TryCreateShaderAbortDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, ShaderAbortSkipReason);

        output.WriteLine(
            $"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}; device {GpuName(device!)}");
        Assert.True(device!.ShaderAbortExtensionEnabled);
        device.Dispose();
        AssertNoValidationErrors(errors);
    }

    // ---- #246: deviceFaultVendorBinary, enabled exactly when reported ----
    //
    // Every adapter advertising the extension, not the first one: on the dev
    // host that runs both branches (RTX 4070 Ti reports 1, the AMD iGPU 0).
    // Never enable the bit where the query reported 0 (class remarks).

    [Fact]
    public void ExtVendorBinary_EnabledExactlyWhenReported_CreatesDevice()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var adapters = PhysicalDeviceFeaturesTests.WalkAdapters(instance);
        TestGate.RequireDeviceFeature(adapters.Exists(a => a.HasExtFault && a.HasGraphics), ExtSkipReason);

        foreach (var a in adapters)
        {
            if (!a.HasExtFault || !a.HasGraphics) continue;

            Assert.True(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(
                VulkanExtensions.ExtDeviceFault, out var f));
            using var device = CreateFaultDeviceOn(a.Gpu, a.GraphicsFamily, khr: false, f.deviceFaultVendorBinary);
            Assert.Equal(DeviceFaultApi.Ext, device.DeviceFaultApi);
            output.WriteLine($"{a.Name}: deviceFaultVendorBinary={f.deviceFaultVendorBinary}");
        }
    }

    [Fact]
    public void ExtVendorBinary_EnabledExactlyWhenReported_UnderValidation_Clean()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        var adapters = PhysicalDeviceFeaturesTests.WalkAdapters(instance);
        TestGate.RequireDeviceFeature(adapters.Exists(a => a.HasExtFault && a.HasGraphics), ExtSkipReason);

        output.WriteLine($"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}");
        foreach (var a in adapters)
        {
            if (!a.HasExtFault || !a.HasGraphics) continue;

            Assert.True(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(
                VulkanExtensions.ExtDeviceFault, out var f));
            using var device = CreateFaultDeviceOn(a.Gpu, a.GraphicsFamily, khr: false, f.deviceFaultVendorBinary);
            Assert.Equal(DeviceFaultApi.Ext, device.DeviceFaultApi);
            output.WriteLine($"{a.Name}: deviceFaultVendorBinary={f.deviceFaultVendorBinary}");
        }

        // Every device was disposed at the end of its iteration.
        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void KhrVendorBinary_EnabledExactlyWhenReported_UnderValidation_Clean()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer(MinKhrLayer, "The KHR device-fault feature struct needs a layer that knows VK_KHR_device_fault.");

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        var adapters = PhysicalDeviceFeaturesTests.WalkAdapters(instance);
        TestGate.RequireDeviceFeature(adapters.Exists(a => a.HasKhrFault && a.HasGraphics), KhrSkipReason);

        output.WriteLine($"Validation layer {TestGate.Fmt(VulkanEnvironment.ValidationLayerSpecVersion)}");
        foreach (var a in adapters)
        {
            if (!a.HasKhrFault || !a.HasGraphics) continue;

            Assert.True(a.Gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(
                VulkanExtensions.KhrDeviceFault, out var f));
            using var device = CreateFaultDeviceOn(a.Gpu, a.GraphicsFamily, khr: true, f.deviceFaultVendorBinary);
            Assert.Equal(DeviceFaultApi.Khr, device.DeviceFaultApi);
            output.WriteLine($"{a.Name}: KHR deviceFaultVendorBinary={f.deviceFaultVendorBinary}");
        }

        // Every device was disposed at the end of its iteration.
        AssertNoValidationErrors(errors);
    }

    // ---- Helpers (adapted from MeshShaderTests) ----

    private static string GpuName(Device device)
    {
        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(device.PhysicalDevice.Handle, &props);
        return Utf8.FromBounded(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref props.deviceName.e0, 256));
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

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    f = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }

    /// <summary>
    /// Creates a device with the requested device-fault extension(s) and, when
    /// <paramref name="pushFeatures"/> is set, the matching
    /// <c>VkPhysicalDeviceFaultFeaturesEXT</c> / <c>…KHR</c> with
    /// <c>deviceFault = 1</c> only — or returns <see langword="null"/> when no
    /// GPU on this host can supply them. <c>deviceFaultVendorBinary</c> is
    /// still deliberately not requested <b>here</b>, because this helper
    /// screens by extension only: a GPU without the bit (the AMD iGPU on the
    /// dev host) would turn the whole tier into
    /// <c>VK_ERROR_FEATURE_NOT_PRESENT</c> skips. The vendor-binary cases
    /// request it through <see cref="CreateFaultDeviceOn"/>, after
    /// <see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/> has
    /// reported it. The picker screens on
    /// <see cref="PhysicalDeviceInfo.SupportsExtension"/>, so a host whose
    /// first graphics-capable GPU lacks the extension still finds one that has
    /// it.
    /// </summary>
    private static Device? TryCreateFaultDevice(Instance instance, bool ext, bool khr, bool pushFeatures, out uint family)
    {
        uint f = uint.MaxValue;
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                if (ext && !info.SupportsExtension(DeviceExtensionNames.ExtDeviceFault)) return false;
                if (khr && !info.SupportsExtension(DeviceExtensionNames.KhrDeviceFault)) return false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    if (info.QueueFamilies[i].SupportsGraphics)
                    {
                        f = info.QueueFamilies[i].Index;
                        return true;
                    }
                }
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            family = 0;
            return null;
        }

        family = f;
        var extensions = new List<Utf8Name>(2);
        if (ext) extensions.Add(VulkanExtensions.ExtDeviceFault);
        if (khr) extensions.Add(VulkanExtensions.KhrDeviceFault);

        try
        {
            return gpu.CreateDevice(new DeviceDescription
            {
                Queues     = [new QueueRequest(f, count: 1, priority: 1.0f)],
                Extensions = extensions.ToArray(),
                // Not `static`: the closure over ext/khr/pushFeatures picks the
                // feature structs. Setup-time in a test, so the capture costs
                // nothing that matters.
                ConfigureFeatures = (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceFeatures2 _,
                    ref VkPhysicalDeviceVulkan12Features _,
                    ref VkPhysicalDeviceVulkan13Features _,
                    ref VkPhysicalDeviceVulkan14Features _) =>
                {
                    if (!pushFeatures) return;
                    if (ext)
                    {
                        ref var extFeatures = ref chain.Push<VkPhysicalDeviceFaultFeaturesEXT>();
                        extFeatures.deviceFault = 1;
                    }
                    if (khr)
                    {
                        ref var khrFeatures = ref chain.Push<VkPhysicalDeviceFaultFeaturesKHR>();
                        khrFeatures.deviceFault = 1;
                    }
                },
            });
        }
        catch (VulkanException ex) when (
            ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT ||
            ex.Result == VkResult.VK_ERROR_FEATURE_NOT_PRESENT)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a device with <c>VK_KHR_device_fault</c>,
    /// <c>VK_KHR_shader_abort</c> and its dependency
    /// <c>VK_KHR_shader_constant_data</c>, pushing
    /// <c>VkPhysicalDeviceFaultFeaturesKHR</c> (<c>deviceFault = 1</c>) and
    /// <c>VkPhysicalDeviceShaderAbortFeaturesKHR</c> (<c>shaderAbort = 1</c>) —
    /// or returns <see langword="null"/> when no GPU on this host can supply
    /// them. The <see cref="TryCreateFaultDevice"/> shape.
    /// </summary>
    private static Device? TryCreateShaderAbortDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                if (!info.SupportsExtension(DeviceExtensionNames.KhrDeviceFault)) return false;
                if (!info.SupportsExtension(DeviceExtensionNames.KhrShaderAbort)) return false;
                if (!info.SupportsExtension(DeviceExtensionNames.KhrShaderConstantData)) return false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    if (info.QueueFamilies[i].SupportsGraphics)
                    {
                        f = info.QueueFamilies[i].Index;
                        return true;
                    }
                }
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            family = 0;
            return null;
        }

        family = f;
        try
        {
            return gpu.CreateDevice(new DeviceDescription
            {
                Queues     = [new QueueRequest(f, count: 1, priority: 1.0f)],
                Extensions =
                [
                    VulkanExtensions.KhrDeviceFault,
                    VulkanExtensions.KhrShaderAbort,
                    VulkanExtensions.KhrShaderConstantData,
                ],
                ConfigureFeatures = static (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceFeatures2 _,
                    ref VkPhysicalDeviceVulkan12Features _,
                    ref VkPhysicalDeviceVulkan13Features _,
                    ref VkPhysicalDeviceVulkan14Features _) =>
                {
                    ref var fault = ref chain.Push<VkPhysicalDeviceFaultFeaturesKHR>();
                    fault.deviceFault = 1;
                    ref var abort = ref chain.Push<VkPhysicalDeviceShaderAbortFeaturesKHR>();
                    abort.shaderAbort = 1;
                },
            });
        }
        catch (VulkanException ex) when (
            ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT ||
            ex.Result == VkResult.VK_ERROR_FEATURE_NOT_PRESENT)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a device on <paramref name="gpu"/> with exactly one device-fault
    /// extension — KHR when <paramref name="khr"/>, EXT otherwise — and the
    /// matching feature struct with <c>deviceFault = 1</c> and
    /// <c>deviceFaultVendorBinary = <paramref name="vendorBinary"/></c>.
    /// </summary>
    /// <remarks>
    /// Deliberately does <b>not</b> catch <c>VK_ERROR_FEATURE_NOT_PRESENT</c>:
    /// the callers pass the value
    /// <see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/>
    /// reported, so that exception is the failure the vendor-binary cases exist
    /// to rule out.
    /// </remarks>
    private static Device CreateFaultDeviceOn(PhysicalDevice gpu, uint family, bool khr, uint vendorBinary)
        => gpu.CreateDevice(new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = [khr ? VulkanExtensions.KhrDeviceFault : VulkanExtensions.ExtDeviceFault],
            // Not `static`: the closure captures khr/vendorBinary — the
            // queried answer reaching the configurer, which does not receive
            // the PhysicalDevice. Setup-time, so the capture is fine.
            ConfigureFeatures = (
                ref ChainBuilder<VkDeviceCreateInfo> chain,
                ref VkPhysicalDeviceFeatures2 _,
                ref VkPhysicalDeviceVulkan12Features _,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                if (khr)
                {
                    ref var k = ref chain.Push<VkPhysicalDeviceFaultFeaturesKHR>();
                    k.deviceFault             = 1;
                    k.deviceFaultVendorBinary = vendorBinary;
                }
                else
                {
                    ref var e = ref chain.Push<VkPhysicalDeviceFaultFeaturesEXT>();
                    e.deviceFault             = 1;
                    e.deviceFaultVendorBinary = vendorBinary;
                }
            },
        });
}
