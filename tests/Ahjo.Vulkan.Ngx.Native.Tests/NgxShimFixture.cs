using System.Runtime.InteropServices;

using Xunit;

namespace Ahjo.Vulkan.Ngx.Native.Tests;

/// <summary>
/// Decides, once per process, whether the <c>ahjo_ngx</c> shim is present —
/// and what an absence means.
/// <para>
/// The NGX SDK is proprietary and licence-encumbered, so nothing in this repo
/// downloads it as a side effect of a build. A contributor who has never run
/// <c>./tools/setup-ngx.ps1</c> has no shim, and this whole suite should skip
/// for them rather than fail.
/// </para>
/// <para>
/// That is exactly the shape of hole issue #158 was opened about, though: a
/// lane that skips everything reports green while executing nothing. So the
/// <c>ngx-native</c> CI lane sets <c>AHJO_NGX_REQUIRE_SHIM=1</c>, which turns
/// "the shim was not there" from a skip into a failure. It is the same idea as
/// <c>AHJO_VULKAN_TIER</c>, applied to a suite that has no Vulkan tier to
/// declare because the shim links no loader.
/// </para>
/// <para>
/// Resolution goes through <see cref="NativeLibrary"/> by name against the
/// bindings' own assembly, so this loads the very binary the
/// <c>DllImport</c>s will call rather than some other copy on the search path.
/// No reflection anywhere: the suite's subject is an AOT-clean binding.
/// </para>
/// </summary>
internal static class NgxShimFixture
{
    private const string RequireShimVariable = "AHJO_NGX_REQUIRE_SHIM";

    private static readonly nint LibraryHandle = TryLoadShim();

    /// <summary>The loaded shim, or 0 when it could not be loaded.</summary>
    public static nint Handle => LibraryHandle;

    /// <summary>Whether the shim loaded.</summary>
    public static bool IsAvailable => LibraryHandle != 0;

    private static nint TryLoadShim()
    {
        // "ahjo_ngx" is the same name the generated DllImports use, resolved
        // against the same assembly.
        return NativeLibrary.TryLoad("ahjo_ngx", typeof(NgxApi).Assembly, null, out var handle)
            ? handle
            : 0;
    }

    /// <summary>
    /// Called at the top of every test when <see cref="IsAvailable"/> is
    /// false. Throws when the lane declared that a shim must be here;
    /// otherwise records a skip.
    /// </summary>
    public static void SkipOrFail()
    {
        var required = Environment.GetEnvironmentVariable(RequireShimVariable);

        if (string.Equals(required, "1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AHJO_NGX_REQUIRE_SHIM=1 but ahjo_ngx could not be loaded. "
                + "The lane that sets this variable is required to have built the shim.");
        }

        Assert.Skip(
            "ahjo_ngx is not built. Run ./tools/setup-ngx.ps1 then rebuild src/Ahjo.Vulkan.Ngx.Native.");
    }
}
