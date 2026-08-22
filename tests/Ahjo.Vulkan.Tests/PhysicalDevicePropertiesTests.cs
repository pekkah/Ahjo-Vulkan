using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers <see cref="PhysicalDevice.TryGetProperties{T}(ReadOnlySpan{byte}, out T)"/>
/// and its siblings, <see cref="PhysicalDevice.SupportsExtension(ReadOnlySpan{byte})"/>
/// and the <see cref="MeshShaderLimits"/> projection.
/// </summary>
/// <remarks>
/// <para>The general mechanism is covered <b>unconditionally</b> at
/// <c>[gate:driver]</c>, because <c>VkPhysicalDeviceVulkan11Properties</c> is
/// core Vulkan <b>1.2</b> — the "11" names the feature set it aggregates, not
/// the version that defines it (<c>vulkan_core.h</c> declares it between
/// <c>VK_VERSION_1_2</c> and <c>VK_VERSION_1_3</c>) — and the wrapper's device
/// floor is 1.3, so every host with an ICD the wrapper will talk to must
/// expose it. The extension gate is also
/// covered unconditionally, by asserting the <i>relationship</i>
/// (<c>TryGetProperties(…) == SupportsExtension(…)</c>) rather than the
/// capability, so the test is green on a mesh host and on a host without one
/// alike.</para>
/// <para>Only the mesh projection itself is <c>[gate:feature]</c>. A CI run
/// that reports those two as skipped is the expected outcome, not a failure to
/// fix.</para>
/// </remarks>
public sealed unsafe class PhysicalDevicePropertiesTests
{
    // ---- [gate:driver] — the mechanism, unconditionally. ----

    [Fact]
    public void TryGetProperties_CoreVulkan11Properties_Succeeds()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = PickAnyDevice(instance, out _);

        // V1_2, not V1_1: VkPhysicalDeviceVulkan11Properties is defined by
        // Vulkan 1.2. Gating it on 1.1 would let sType 50 into a read-back
        // chain on a 1.1 ICD that never learned it — the exact
        // "UNSUPPORTED: curExtension->sType" hazard the gate exists to stop.
        Assert.True(gpu.TryGetProperties<VkPhysicalDeviceVulkan11Properties>(
            VulkanVersion.V1_2, out var p));

        // The mechanism wrote sType from T.SType — the caller never passed one.
        Assert.Equal(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_1_PROPERTIES,
            p.sType);

        // The copy-out cannot hand back a pointer into the dead stack frame
        // that backed the chain: the node was the tail and WriteHeader nulled
        // its pNext.
        Assert.True(p.pNext == null);

        // The driver actually filled the node, rather than the struct simply
        // coming back zeroed. subgroupSize is required to be a power of two.
        Assert.NotEqual(0u, p.subgroupSize);
        Assert.Equal(0u, p.subgroupSize & (p.subgroupSize - 1));
        Assert.NotEqual(0ul, p.maxMemoryAllocationSize);
    }

    [Fact]
    public void TryGetProperties_ExtensionGate_MatchesSupportsExtension()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = PickAnyDevice(instance, out _);

        bool ok = gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(
            VulkanExtensions.ExtMeshShader, out var mesh);

        Assert.Equal(gpu.SupportsExtension(DeviceExtensionNames.MeshShader), ok);
        if (!ok) AssertAllZero(in mesh);
    }

    [Fact]
    public void TryGetProperties_VersionGate_MatchesDeviceApiVersion()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = PickAnyDevice(instance, out uint apiVersion);

        // A 1.3 device must never see the sType-55-class 1.4 nodes — the
        // SwiftShader failure Instance.PickPhysicalDevice gates against, in
        // assertion form.
        Assert.Equal(
            apiVersion >= VulkanVersion.V1_4.Packed,
            gpu.TryGetProperties<VkPhysicalDeviceVulkan14Properties>(VulkanVersion.V1_4, out _));
    }

    [Fact]
    public void TryGetProperties_NullUtf8Name_ReturnsFalse()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = PickAnyDevice(instance, out _);

        Assert.False(gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(
            default(Utf8Name), out var p));
        AssertAllZero(in p);
    }

    [Fact]
    public void SupportsExtension_AgreesWithPickerView()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        bool fromPicker = false;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            fromPicker = info.SupportsExtension(DeviceExtensionNames.MeshShader);
            return true;
        });

        Assert.Equal(fromPicker, gpu.SupportsExtension(DeviceExtensionNames.MeshShader));
        Assert.False(gpu.SupportsExtension("VK_EXT_this_does_not_exist"u8));
        Assert.False(gpu.SupportsExtension(default(Utf8Name)));
    }

    [Fact]
    public void TryGetMeshShaderLimits_WithoutExtension_ReturnsFalse()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = PickAnyDevice(instance, out _);

        // Only assert the negative on a host where the negative is real.
        TestGate.RequireDeviceFeature(
            !gpu.SupportsExtension(DeviceExtensionNames.MeshShader),
            "Device advertises VK_EXT_mesh_shader; the without-extension negative is not testable here.");

        Assert.False(gpu.TryGetMeshShaderLimits(out var limits));
        Assert.Equal(default, limits);
    }

    // ---- [gate:feature] — the mesh projection. ----
    //
    // No Device is created: these are physical-device queries and the limits
    // are readable before vkCreateDevice on purpose.

    [Fact]
    public void TryGetMeshShaderLimits_MatchesRawProperties()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = TryPickMeshDevice(instance);
        TestGate.RequireDeviceFeature(gpu is not null, "Device does not expose VK_EXT_mesh_shader.");

        Assert.True(gpu!.TryGetMeshShaderLimits(out var limits));
        Assert.True(gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(
            VulkanExtensions.ExtMeshShader, out var raw));

        Assert.Equal(raw.maxTaskWorkGroupCount[0], limits.MaxTaskWorkGroupCountX);
        Assert.Equal(raw.maxTaskWorkGroupCount[1], limits.MaxTaskWorkGroupCountY);
        Assert.Equal(raw.maxTaskWorkGroupCount[2], limits.MaxTaskWorkGroupCountZ);
        Assert.Equal(raw.maxTaskWorkGroupTotalCount, limits.MaxTaskWorkGroupTotalCount);
        Assert.Equal(raw.maxTaskWorkGroupInvocations, limits.MaxTaskWorkGroupInvocations);

        Assert.Equal(raw.maxMeshWorkGroupCount[0], limits.MaxMeshWorkGroupCountX);
        Assert.Equal(raw.maxMeshWorkGroupCount[1], limits.MaxMeshWorkGroupCountY);
        Assert.Equal(raw.maxMeshWorkGroupCount[2], limits.MaxMeshWorkGroupCountZ);
        Assert.Equal(raw.maxMeshWorkGroupTotalCount, limits.MaxMeshWorkGroupTotalCount);
        Assert.Equal(raw.maxMeshWorkGroupInvocations, limits.MaxMeshWorkGroupInvocations);
    }

    [Fact]
    public void TryGetMeshShaderLimits_AllLimitsNonZero()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = TryPickMeshDevice(instance);
        TestGate.RequireDeviceFeature(gpu is not null, "Device does not expose VK_EXT_mesh_shader.");

        Assert.True(gpu!.TryGetMeshShaderLimits(out var limits));

        // A conformant VK_EXT_mesh_shader implementation cannot report zero for
        // any of these. A floor of 1, not a spec constant.
        Assert.NotEqual(0u, limits.MaxTaskWorkGroupCountX);
        Assert.NotEqual(0u, limits.MaxTaskWorkGroupCountY);
        Assert.NotEqual(0u, limits.MaxTaskWorkGroupCountZ);
        Assert.NotEqual(0u, limits.MaxTaskWorkGroupTotalCount);
        Assert.NotEqual(0u, limits.MaxTaskWorkGroupInvocations);
        Assert.NotEqual(0u, limits.MaxMeshWorkGroupCountX);
        Assert.NotEqual(0u, limits.MaxMeshWorkGroupCountY);
        Assert.NotEqual(0u, limits.MaxMeshWorkGroupCountZ);
        Assert.NotEqual(0u, limits.MaxMeshWorkGroupTotalCount);
        Assert.NotEqual(0u, limits.MaxMeshWorkGroupInvocations);
    }

    /// <summary>
    /// Byte-wise "came back untouched". <c>Assert.Equal(default, x)</c> is not
    /// usable on the generated properties structs: they carry
    /// <c>[InlineArray]</c> fields, and the runtime throws
    /// <see cref="NotSupportedException"/> from <c>ValueType.Equals</c> for
    /// those rather than comparing them.
    /// </summary>
    private static void AssertAllZero<T>(in T value) where T : unmanaged
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in value), 1));
        for (int i = 0; i < bytes.Length; i++)
            Assert.Equal(0, bytes[i]);
    }

    /// <summary>
    /// Accepts the first candidate GPU and reports its packed
    /// <c>apiVersion</c> out of the picker — the only place
    /// <see cref="PhysicalDeviceInfo.Properties"/> is reachable.
    /// </summary>
    private static PhysicalDevice PickAnyDevice(Instance instance, out uint apiVersion)
    {
        uint v = 0;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            v = info.Properties.apiVersion;
            return true;
        });
        apiVersion = v;
        return gpu;
    }

    /// <summary>
    /// The first GPU on the host advertising <c>VK_EXT_mesh_shader</c>, or
    /// <see langword="null"/> when there is none — the clean skip signal for
    /// the mesh tier. Unlike <c>MeshShaderTests.TryCreateMeshDevice</c> this
    /// stops at the physical device: no <c>vkCreateDevice</c>, no feature
    /// request.
    /// </summary>
    private static PhysicalDevice? TryPickMeshDevice(Instance instance)
    {
        try
        {
            return instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
                info.SupportsExtension(DeviceExtensionNames.MeshShader));
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            return null;
        }
    }
}
