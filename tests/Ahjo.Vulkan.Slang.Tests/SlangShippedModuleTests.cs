using System.Runtime.InteropServices;

using Ahjo.Vulkan.Testing;

using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// Deployment, not the compiler API: every native Slang module this package
/// ships on win-x64 must be the one the process actually loaded — from the
/// application directory, not some other copy the loader found on
/// <c>PATH</c> (issue #169).
/// </summary>
/// <remarks>
/// <para>This is a <em>location</em> check, not a behaviour check. Slang
/// loads its modules by bare name, and the Windows loader falls back to
/// <c>PATH</c> after the application directory. A Vulkan SDK's <c>Bin</c>
/// directory ships <c>slang.dll</c>, <c>slang-compiler.dll</c> and
/// <c>slang-glslang.dll</c>, so on a machine with the SDK installed the
/// behaviour tests (<c>Optimization_ChangesTheEmittedSpirv</c>,
/// <c>OptimizationLevels_ReachTheDownstreamCompiler</c>) stay green with ours
/// deleted. A green <c>Optimization</c> run there proves nothing about
/// staging; this test does, on any machine, SDK or not
/// (<c>docs/design/specs/2026-11-13-issue-169-slang-native-module-provenance-design.md</c>,
/// E1 and E3).</para>
/// <para>Version is out of scope: a stale copy left in the output directory
/// passes here. That is #256.</para>
/// <para>The lookup is <c>kernel32!GetModuleHandleW</c> +
/// <c>GetModuleFileNameW</c> through source-generated <c>[LibraryImport]</c>,
/// the spec's pre-agreed fallback. The first version enumerated
/// <c>Process.Modules</c>, which flaked in the parallel suite.</para>
/// </remarks>
public sealed partial class SlangShippedModuleTests
{
    // The win-x64 shipped set. Must match the None items in
    // src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj (win-x64 ItemGroup)
    // and _SlangPackFile there.
    private static readonly string[] WindowsShippedModules = ["slang.dll", "slang-compiler.dll", "slang-glslang.dll"];

    private readonly ITestOutputHelper _output;

    public SlangShippedModuleTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// After a <see cref="SlangOptimizationLevel.Maximal"/> compile, each
    /// shipped win-x64 module is loaded, and loaded from
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <remarks>
    /// <para>The lookup has a lifetime window (spec E4).
    /// <c>slang-glslang</c> is loaded lazily, by <see cref="SlangProgram.Spirv"/>
    /// at an optimizing level, and unloaded when the
    /// <see cref="SlangCompiler"/> is disposed. So the compile, session and
    /// program are held by <c>using</c> declarations for the whole method,
    /// and the SPIR-V is fetched before any module is looked up.</para>
    /// </remarks>
    [Fact]
    public void ShippedNativeModules_LoadFromTheApplicationDirectory()
    {
        TestGate.RequirePlatform(
            OperatingSystem.IsWindows(),
            "Module provenance is asserted for the win-x64 shipped set; the linux-x64 load path is unmeasured (#169).");

        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(new SlangSessionDescription { Optimization = SlangOptimizationLevel.Maximal });
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "provenance",
            Source = ShaderFixtures.RedundantVertex,
        });

        // This is what loads slang-glslang; it must come before any lookup.
        ReadOnlySpan<uint> spirv = program.Spirv(0);

        Assert.False(spirv.IsEmpty);

        foreach (string name in WindowsShippedModules)
        {
            string? path = FindLoadedModulePath(name);

            Assert.True(
                path is not null,
                $"{name} is not loaded after a Maximal compile. Slang loads it by exactly this name, so either it is missing from the test output with no other copy reachable, or Slang now asks for a different file name than the one we ship (src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj).");

            Assert.True(
                IsInApplicationDirectory(path),
                $"{name} was loaded from {path}, not from the application directory {AppContext.BaseDirectory}. Ours is missing from the test output and the loader found another copy on PATH; a Vulkan SDK's Bin ships all three. Check the shipped set in src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj (#169).");

            _output.WriteLine($"{name} -> {path}");
        }
    }

    // A lookup by name, not an enumeration. Process.Modules enumerates via
    // EnumProcessModules, and inside the parallel suite that raced other test
    // classes loading and unloading slang-glslang: 2 of 10 full runs failed
    // with Win32Exception "The operation completed successfully" (#169). The
    // loader matches base names case-insensitively, and this test holds its own
    // SlangCompiler, so the module cannot unload between the two calls.
    private static string? FindLoadedModulePath(string fileName)
    {
        nint module = GetModuleHandleW(fileName);
        if (module == 0)
        {
            return null;
        }

        // Long-path maximum; GetModuleFileNameW truncates rather than fails.
        char[] buffer = new char[32_767];
        uint length = GetModuleFileNameW(module, buffer, (uint)buffer.Length);

        Assert.True(
            length > 0 && length < buffer.Length,
            $"GetModuleFileNameW failed for {fileName} (length {length}, error {Marshal.GetLastPInvokeError()}).");

        return new string(buffer, 0, (int)length);
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint GetModuleHandleW(string moduleName);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint GetModuleFileNameW(nint module, [Out] char[] fileName, uint size);

    // Directory equality, not a prefix match: a subdirectory of the output is
    // not the application directory. Windows-only, hence case-insensitive.
    private static bool IsInApplicationDirectory(string modulePath)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetDirectoryName(modulePath)!)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),
            StringComparison.OrdinalIgnoreCase);
}
