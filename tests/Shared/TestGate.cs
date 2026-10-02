using Xunit;

namespace Ahjo.Vulkan.Testing;

/// <summary>
/// The one way a test in this repo is allowed to skip. Every reason string
/// carries a machine-readable <c>[gate:&lt;class&gt;]</c> prefix so CI can tell a
/// permanent platform skip (a Wayland test on Windows) from a coverage gap (a
/// driver-gated test with no ICD). The <c>Vulkan coverage summary</c> step in
/// <c>.github/workflows/ci.yml</c> <em>fails the job</em> on any skip that
/// carries no class, which is what stops the classification rotting.
/// </summary>
/// <remarks>
/// Every method here skips and never fails, even when the host is below the
/// tier this lane declared. <c>VulkanTierContractTests</c> is the single point
/// of failure for that case — 231 red tests would bury the one actionable
/// message. See <c>docs/ci-coverage.md</c>.
/// </remarks>
internal static class TestGate
{
    /// <summary>Coverage gap: no usable ICD answered on this host.</summary>
    public static void RequireDriver()
        => Assert.SkipUnless(VulkanEnvironment.HasDriver, "[gate:driver] No Vulkan driver on host.");

    /// <summary>
    /// Coverage gap: the ICD that answered is a software rasterizer. Implies a
    /// driver — call <see cref="RequireDriver"/> first, as the call sites do.
    /// </summary>
    public static void RequireHardwareDriver(string reason)
        => Assert.SkipWhen(VulkanEnvironment.IsSoftwareDriver, $"[gate:hardware] {reason}");

    /// <summary>Coverage gap: <c>VK_LAYER_KHRONOS_validation</c> is the test's only oracle and it is absent.</summary>
    public static void RequireValidationLayer()
        => Assert.SkipUnless(
            VulkanEnvironment.HasValidationLayer,
            "[gate:validation] VK_LAYER_KHRONOS_validation is not installed.");

    /// <summary>
    /// Coverage gap: the validation layer is present but older than
    /// <paramref name="minimumSpecVersion"/> (a packed Vulkan version), so it
    /// does not know the extension under test and cannot serve as its oracle.
    /// Applies <see cref="RequireValidationLayer()"/> first.
    /// </summary>
    public static void RequireValidationLayer(uint minimumSpecVersion, string reason)
    {
        RequireValidationLayer();
        uint found = VulkanEnvironment.ValidationLayerSpecVersion;
        Assert.SkipWhen(
            found < minimumSpecVersion,
            $"[gate:validation] {reason} Requires VK_LAYER_KHRONOS_validation >= {Fmt(minimumSpecVersion)}; found {Fmt(found)}.");
    }

    /// <summary>Formats a packed Vulkan version as <c>major.minor.patch</c>.</summary>
    internal static string Fmt(uint version)
        => $"{(version >> 22) & 0x7Fu}.{(version >> 12) & 0x3FFu}.{version & 0xFFFu}";

    /// <summary>Toolchain gap: the build could not compile this shader because <c>glslc</c> was absent.</summary>
    public static void RequireSpirv(string spvPath)
        => Assert.SkipUnless(
            File.Exists(spvPath),
            $"[gate:spirv] Compiled shader missing: {spvPath} (glslc not on PATH at build time).");

    /// <summary>
    /// Correct and permanent: this test targets an OS, window system or
    /// instance extension the host does not have.
    /// </summary>
    public static void RequirePlatform(bool condition, string reason)
        => Assert.SkipUnless(condition, $"[gate:platform] {reason}");

    /// <summary>Correct: the device does not advertise the optional feature under test.</summary>
    public static void RequireDeviceFeature(bool condition, string reason)
        => Assert.SkipUnless(condition, $"[gate:feature] {reason}");

    /// <summary>
    /// Correct: the host cannot present the configuration under test at all.
    /// Always skips — for the cases with no boolean to hand.
    /// </summary>
    public static void Unsupported(string reason)
        => Assert.Skip($"[gate:feature] {reason}");
}
