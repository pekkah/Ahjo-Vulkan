using Ahjo.Vulkan.Testing;
using Xunit;
using Xunit.Sdk;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// The probe must never throw. <see cref="Lazy{T}"/> caches a factory exception
/// and rethrows it on every subsequent <c>.Value</c>, so a single throw inside
/// the probe would turn all ~231 <c>TestGate.RequireDriver()</c> gates into
/// <em>errors</em> instead of skips — reported as <c>outcome="Failed"</c>, which
/// the CI coverage summary counts as neither a coverage gap nor an unclassified
/// skip. The table would report zero gaps in the exact situation it exists for,
/// and <c>VulkanTierContractTests</c> would error too, so its one actionable
/// message would never print.
/// </summary>
public class VulkanEnvironmentProbeTests
{
    [Fact]
    public void GuardProbe_WrongArchitectureLoader_ReportsNoneInsteadOfThrowing()
    {
        // What a wrong-architecture vulkan-1.dll on the search path produces.
        // Not a DllNotFoundException, so the pre-review probe let it escape.
        var observed = VulkanEnvironment.GuardProbe(
            () => throw new BadImageFormatException("An attempt was made to load a program with an incorrect format."));

        Assert.Equal(VulkanCapability.None, observed.Capability);
        Assert.StartsWith("vulkan probe threw BadImageFormatException: ", observed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardProbe_LoaderWithoutEntryPoint_ReportsNoneInsteadOfThrowing()
    {
        var observed = VulkanEnvironment.GuardProbe(
            () => throw new EntryPointNotFoundException("Unable to find an entry point named 'vkCreateInstance'."));

        Assert.Equal(VulkanCapability.None, observed.Capability);
        Assert.Contains("EntryPointNotFoundException", observed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardProbe_NoLoader_KeepsTheFriendlierDetail()
    {
        var observed = VulkanEnvironment.GuardProbe(() => throw new DllNotFoundException("vulkan-1"));

        Assert.Equal(VulkanCapability.None, observed.Capability);
        Assert.Equal("no vulkan-1 loader on this host", observed.Detail);
    }

    /// <summary>
    /// The consequence that matters: a probe that reports
    /// <see cref="VulkanCapability.None"/> makes the gates <em>skip</em>. Proven
    /// through the same <c>Assert.SkipUnless</c> primitive
    /// <see cref="TestGate.RequireDriver"/> uses, since the real gate reads the
    /// cached process-wide probe and this host has a driver.
    /// </summary>
    [Fact]
    public void NoneCapability_MakesADriverGateSkipRatherThanFail()
    {
        var observed = VulkanEnvironment.GuardProbe(() => throw new BadImageFormatException("wrong arch"));
        bool hasDriver = observed.Capability >= VulkanCapability.Software;

        // Caught by hand, deliberately not with Assert.Throws<SkipException>:
        // xunit rethrows its own control-flow exceptions out of Assert.Throws, so
        // that version skipped *this* test and reported a phantom [gate:driver]
        // skip — a 226th driver gap on a host that has a driver, which would
        // falsely trip the coverage summary's miswired-gate check at tier
        // `software`. The gate's own exception must never reach the framework here.
        SkipException? skip = null;
        try
        {
            Assert.SkipUnless(hasDriver, "[gate:driver] No Vulkan driver on host.");
        }
        catch (SkipException ex)
        {
            skip = ex;
        }

        Assert.NotNull(skip);
        Assert.Contains("[gate:driver] No Vulkan driver on host.", skip.Message, StringComparison.Ordinal);
        // And a failure would have been a different exception entirely.
        Assert.IsType<SkipException>(skip);
    }

    /// <summary>
    /// The layer bit is an instance-level fact, probed independently of the
    /// device type (issue #158 review, F3) — but it must still agree with the
    /// ladder on the two rungs where the ladder constrains it.
    /// </summary>
    /// <remarks>
    /// The case F3 is actually about — a CPU ICD <em>with</em> the layer
    /// installed, where reading the bit off the ladder would report
    /// "layer not installed" for ten driver+validation-gated tests — is not
    /// reachable on a host with a hardware driver. It is only observable on a
    /// software-ICD host such as the <c>vma-linux</c> lane.
    /// </remarks>
    [Fact]
    public void ValidationLayer_AgreesWithTheLadderWhereTheLadderConstrainsIt()
    {
        // Deliberately ungated: it asserts something at every rung, including
        // `none`, so it must not add a 226th [gate:driver] skip to the coverage
        // table on a driverless lane.
        switch (VulkanEnvironment.Observed)
        {
            case VulkanCapability.Validation:
                Assert.True(VulkanEnvironment.HasValidationLayer,
                    "Observed reached Validation, so the layer must be reported present.");
                break;
            case VulkanCapability.Hardware:
                Assert.False(VulkanEnvironment.HasValidationLayer,
                    "Observed stopped at Hardware, which only happens when the layer probe said no.");
                break;
            case VulkanCapability.Software:
                // The F3 case: unconstrained by the ladder. The layer bit may be
                // either value here, and that is the whole point — it is probed,
                // not derived.
                Assert.True(VulkanEnvironment.IsSoftwareDriver);
                break;
            default:
                // No driver: the HasDriver coupling F3 keeps must report the
                // driver gap, not a layer gap.
                Assert.False(VulkanEnvironment.HasDriver);
                Assert.False(VulkanEnvironment.HasValidationLayer);
                break;
        }
    }
}
