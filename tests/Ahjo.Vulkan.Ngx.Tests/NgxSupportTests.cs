using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The capability queries, over a staged <c>ahjo_ngx</c> shim.
/// </summary>
/// <remarks>
/// <para>Only <see cref="NgxSupport.TryGetInstanceExtensions"/> is exercised
/// here, and that is not an oversight: it is the one NGX entry point answerable
/// without a <c>VkInstance</c>, because it is a static query served out of
/// NVIDIA's client library rather than out of the driver (#216 OPEN-1,
/// resolved by measurement on two host kinds). The other three take a live
/// instance and belong with the hardware suite.</para>
/// <para>The native-level version of this assertion already runs in the
/// <c>ngx-native</c> lane
/// (<c>tests/Ahjo.Vulkan.Ngx.Native.Tests/NgxSmokeTests.cs</c>); what this one
/// adds is the wrapper's <b>copy</b> path over it — that the names survive
/// being lifted out of NGX's undocumented-lifetime array into storage this
/// package owns.</para>
/// </remarks>
public sealed unsafe class NgxSupportTests
{
    [Fact]
    public void TryGetInstanceExtensions_ReturnsAPlausibleSet()
    {
        TestGate.RequirePlatform(
            NgxTestEnvironment.ShimPresent,
            "ahjo_ngx shim not staged — DLSS is opt-in; run ./tools/setup-ngx.ps1.");

        NgxDescription description = NgxTestEnvironment.Description;
        Assert.True(NgxSupport.TryGetInstanceExtensions(in description, out NgxExtensionSet? extensions));

        using (extensions)
        {
            Assert.True(extensions.Count >= 1);
            Assert.Equal(extensions.Count, extensions.Names.Length);

            for (int i = 0; i < extensions.Count; i++)
            {
                Assert.False(extensions.Names[i].IsNull);
                // A copied name that lost its first byte, or its terminator,
                // would show up here rather than as a driver fault later.
                Assert.NotEqual(0, extensions.Names[i].Ptr[0]);
            }
        }
    }
}
