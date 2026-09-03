using System.Runtime.InteropServices;

using Xunit;

namespace Ahjo.Vulkan.Ngx.Native.Tests;

/// <summary>
/// The drift guard for a contract written down in three places.
/// <para>
/// The shim's export list lives in <c>native/ngx/src/ahjo_ngx.def</c>
/// (Windows <c>/DEF:</c>), <c>native/ngx/src/ahjo_ngx.map</c> (the Linux
/// version script) and <see cref="RequiredExports"/> below. Three
/// hand-maintained lists is the cost of the hybrid export contract — 20
/// symbols re-exported verbatim from NVIDIA's static library, which no
/// compiler can check for us because we write no code for them — and this
/// test is what buys it back.
/// </para>
/// <para>
/// Any of the four surfaces drifting (the two files, this list, or the
/// shipped binary) fails here with the missing name in the message, instead
/// of somewhere downstream as an <see cref="EntryPointNotFoundException"/>
/// from whichever call site happened to run first.
/// </para>
/// <para>
/// The list is a literal array and resolution goes through
/// <see cref="NativeLibrary"/> by name — no reflection, no
/// <c>Assembly.GetTypes()</c> — so this test is Native AOT clean by
/// construction and stays that way.
/// </para>
/// </summary>
public sealed class NgxExportDriftTests
{
    /// <summary>
    /// The 27 symbols <c>ahjo_ngx</c> exports, and no others.
    /// </summary>
    private static readonly string[] RequiredExports =
    [
        // ── 20 verbatim re-exports from nvsdk_ngx_s.lib / libnvsdk_ngx.a ──
        //
        // The 12 non-D3D parameter accessors. These are real defined text
        // symbols with C linkage in both static libraries, which is what makes
        // "bind the exported C accessors, never the NVSDK_NGX_Parameter
        // vtable" the SDK's own supported path rather than a workaround.
        //
        // The four SetD3d1{1,2}Resource / GetD3d1{1,2}Resource siblings are
        // deliberately absent: they take ID3D11Resource / ID3D12Resource,
        // which a Vulkan package has no business naming.
        "NVSDK_NGX_Parameter_SetULL",
        "NVSDK_NGX_Parameter_SetF",
        "NVSDK_NGX_Parameter_SetD",
        "NVSDK_NGX_Parameter_SetUI",
        "NVSDK_NGX_Parameter_SetI",
        "NVSDK_NGX_Parameter_SetVoidPointer",
        "NVSDK_NGX_Parameter_GetULL",
        "NVSDK_NGX_Parameter_GetF",
        "NVSDK_NGX_Parameter_GetD",
        "NVSDK_NGX_Parameter_GetUI",
        "NVSDK_NGX_Parameter_GetI",
        "NVSDK_NGX_Parameter_GetVoidPointer",

        // The 8 Vulkan lifecycle entry points that carry no wchar_t.
        //
        // Shutdown1 and CreateFeature1 are the multi-device forms; the
        // un-suffixed Shutdown and CreateFeature are superseded and stay
        // unexported. NVSDK_NGX_VULKAN_GetParameters is deprecated in favour
        // of AllocateParameters + GetCapabilityParameters and is only
        // declared behind NGX_ENABLE_DEPRECATED_GET_PARAMETERS anyway.
        //
        // EvaluateFeature_C, not EvaluateFeature: the latter is C++-only (it
        // has a default argument). This is the per-frame call, and it is a
        // direct export with no shim frame precisely so it stays that way.
        "NVSDK_NGX_VULKAN_Shutdown1",
        "NVSDK_NGX_VULKAN_AllocateParameters",
        "NVSDK_NGX_VULKAN_GetCapabilityParameters",
        "NVSDK_NGX_VULKAN_DestroyParameters",
        "NVSDK_NGX_VULKAN_GetScratchBufferSize",
        "NVSDK_NGX_VULKAN_CreateFeature1",
        "NVSDK_NGX_VULKAN_ReleaseFeature",
        "NVSDK_NGX_VULKAN_EvaluateFeature_C",

        // ── 7 ahjo_ngx_* additions ──
        //
        // Three utilities, then the four entry points that replace NGX calls
        // taking wchar_t (2 bytes on Windows, 4 on Linux) with UTF-8
        // equivalents. Those four are Init plus all three discovery calls —
        // i.e. four of the five things a DLSS integration must call before it
        // can render.
        "ahjo_ngx_version_api",
        "ahjo_ngx_layout",
        "ahjo_ngx_result_to_utf8",
        "ahjo_ngx_vulkan_init_utf8",
        "ahjo_ngx_vulkan_get_feature_requirements_utf8",
        "ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8",
        "ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8",
    ];

    [Fact]
    public void EveryRequiredExport_IsPresentInTheShippedShim()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        var missing = new List<string>();

        foreach (var name in RequiredExports)
        {
            if (!NativeLibrary.TryGetExport(NgxShimFixture.Handle, name, out _))
            {
                missing.Add(name);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"The shipped ahjo_ngx shim is missing {missing.Count} export(s): "
            + string.Join(", ", missing)
            + ". Either the export list in native/ngx/src/ahjo_ngx.def / .map lost a name, "
            + "or an NgxVersion bump removed a symbol from NVIDIA's static client library. "
            + "Do not delete the entry here to get green — find out which.");
    }

    [Fact]
    public void DefFile_ListsExactlyTheRequiredExports()
    {
        // Deliberately NOT gated on the shim loading: the .def is a source
        // file copied to the output directory, so this half of the drift
        // check works on a machine with no NGX SDK at all.
        var listed = ParseDef(ReadExportFile("ahjo_ngx.def"));

        AssertSetsMatch(listed, "native/ngx/src/ahjo_ngx.def");
    }

    [Fact]
    public void MapFile_ListsExactlyTheRequiredExports()
    {
        var listed = ParseMap(ReadExportFile("ahjo_ngx.map"));

        AssertSetsMatch(listed, "native/ngx/src/ahjo_ngx.map");
    }

    private static string[] ReadExportFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);

        Assert.True(
            File.Exists(path),
            $"{fileName} was not copied to the test output directory ({path}). "
            + "The None/CopyToOutputDirectory items in Ahjo.Vulkan.Ngx.Native.Tests.csproj are what put it there.");

        return File.ReadAllLines(path);
    }

    /// <summary>
    /// Everything after the <c>EXPORTS</c> line that is not a comment.
    /// </summary>
    private static HashSet<string> ParseDef(string[] lines)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var inExports = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith(';')) { continue; }

            if (line.StartsWith("EXPORTS", StringComparison.Ordinal)) { inExports = true; continue; }
            if (line.StartsWith("LIBRARY", StringComparison.Ordinal)) { continue; }

            if (inExports) { names.Add(line); }
        }

        return names;
    }

    /// <summary>
    /// The semicolon-terminated names between <c>global:</c> and
    /// <c>local:</c>.
    /// </summary>
    private static HashSet<string> ParseMap(string[] lines)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var inGlobal = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0) { continue; }

            if (line.StartsWith("global:", StringComparison.Ordinal)) { inGlobal = true; continue; }
            if (line.StartsWith("local:", StringComparison.Ordinal)) { break; }

            if (inGlobal) { names.Add(line.TrimEnd(';')); }
        }

        return names;
    }

    private static void AssertSetsMatch(HashSet<string> listed, string origin)
    {
        var expected = new HashSet<string>(RequiredExports, StringComparer.Ordinal);

        var missingFromFile = expected.Except(listed).Order(StringComparer.Ordinal).ToArray();
        var extraInFile = listed.Except(expected).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            missingFromFile.Length == 0 && extraInFile.Length == 0,
            $"{origin} and NgxExportDriftTests.RequiredExports disagree."
            + (missingFromFile.Length > 0 ? $" Missing from the file: {string.Join(", ", missingFromFile)}." : string.Empty)
            + (extraInFile.Length > 0 ? $" Present in the file but not expected: {string.Join(", ", extraInFile)}." : string.Empty)
            + " The export list is written in three places on purpose; keep all three equal.");
    }
}
