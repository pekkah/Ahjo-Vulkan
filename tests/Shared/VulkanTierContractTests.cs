using Xunit;

namespace Ahjo.Vulkan.Testing;

/// <summary>
/// The single test that fails when a lane's declared <c>AHJO_VULKAN_TIER</c> is
/// above what the host can actually do. Every gate in the suite skips rather
/// than fails, so without this test a lane that loses its ICD reports green
/// while executing none of the GPU code it exists to cover — issue #158.
/// </summary>
/// <remarks>
/// Linked into every suite that touches Vulkan, so each <c>dotnet test</c>
/// invocation carries its own proof. Deliberately absent from
/// <c>Ahjo.Vulkan.Ktx.Native.Tests</c>, which must pass with no loader at all.
/// </remarks>
public sealed class VulkanTierContractTests
{
    private readonly ITestOutputHelper _output;

    public VulkanTierContractTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DeclaredTier_IsSatisfiedByHost()
    {
        VulkanCapability declared = VulkanEnvironment.Declared;
        VulkanCapability observed = VulkanEnvironment.Observed;

        if (observed >= declared)
        {
            // Printed even on a pass: this line is the evidence a contributor
            // quotes when claiming a validation-layer oracle actually ran.
            _output.WriteLine(
                $"{VulkanEnvironment.TierVariable} declared={VulkanEnvironment.Name(declared)} " +
                $"observed={VulkanEnvironment.Name(observed)} ({VulkanEnvironment.ObservedDetail})");
            return;
        }

        Assert.Fail(
            $"{VulkanEnvironment.TierVariable}={VulkanEnvironment.Name(declared)} was declared, but this host " +
            $"only reaches '{VulkanEnvironment.Name(observed)}':\n" +
            $"{VulkanEnvironment.ObservedDetail}.\n" +
            "Driver-gated tests will have skipped instead of running. Fix this lane's Vulkan\n" +
            "provisioning. Do not lower the declared tier to make CI green — see\n" +
            "docs/ci-coverage.md and .github/CLAUDE.md.");
    }
}
