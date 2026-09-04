using Xunit;

namespace Ahjo.Vulkan.Ngx.Native.Tests;

/// <summary>
/// What a host with no NVIDIA driver can honestly assert about the shim.
/// <para>
/// Four of these need nothing from the driver at all —
/// <c>ahjo_ngx_version_api</c>, <c>ahjo_ngx_layout</c>,
/// <c>ahjo_ngx_result_to_utf8</c> and the <c>StructSize</c> guard are pure
/// shim code (<c>result_to_utf8</c> reaches <c>GetNGXResultAsString</c>, which
/// is a static string table inside NVIDIA's client library, not a driver
/// call). The fifth calls into NGX proper and requires only that it fails
/// cleanly.
/// </para>
/// <para>
/// Nothing here evaluates DLSS, and nothing here can: that needs an NVIDIA
/// driver, which no GitHub-hosted runner has. Real
/// <c>GetFeatureRequirements</c> / create / evaluate coverage is a
/// local-NVIDIA-hardware item, recorded as such in <c>docs/ci-coverage.md</c>.
/// </para>
/// <para>
/// One environment variable gates this suite.
/// <c>AHJO_NGX_REQUIRE_SHIM=1</c> says <i>the shim must exist</i> — it turns
/// an unloadable <c>ahjo_ngx</c> from a skip into a failure. The
/// <c>ngx-native</c> lane sets it; a developer box normally does not.
/// </para>
/// </summary>
public sealed unsafe class NgxSmokeTests
{
    [Fact]
    public void VersionApi_MatchesTheGeneratedBindings()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // The shim compiled NVSDK_NGX_VERSION_API_MACRO from the pinned
        // header; the bindings generated NVSDK_NGX_Version_API from the same
        // one. Equality is what proves the binary and the committed C# came
        // from the same NgxVersion — the analogue of Slang's
        // BuildTag_MatchesPinnedVersion. A stale ahjo_ngx.dll left in a bin/
        // directory after a pin bump fails here rather than misbehaving later.
        Assert.Equal((uint)NVSDK_NGX_Version.NVSDK_NGX_Version_API, NgxApi.ahjo_ngx_version_api());
        Assert.Equal(0x15u, NgxApi.ahjo_ngx_version_api());
    }

    [Theory]
    [InlineData(NVSDK_NGX_Result.NVSDK_NGX_Result_Success)]
    [InlineData(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotFound)]
    [InlineData(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported)]
    public void ResultToUtf8_ProducesAsciiForKnownResults(NVSDK_NGX_Result result)
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // stackalloc, not a rented array: this is the shape a managed caller
        // uses to format an NGX error without allocating.
        Span<byte> buffer = stackalloc byte[128];
        buffer.Fill(0xCC);

        uint written;
        fixed (byte* p = buffer)
        {
            written = NgxApi.ahjo_ngx_result_to_utf8(result, (sbyte*)p, (uint)buffer.Length);
        }

        Assert.InRange(written, 2u, (uint)buffer.Length);

        var nul = buffer.IndexOf((byte)0);
        Assert.True(nul > 0, $"ahjo_ngx_result_to_utf8({result}) produced no NUL-terminated string.");
        Assert.Equal(written, (uint)nul + 1);

        foreach (var b in buffer[..nul])
        {
            Assert.InRange(b, (byte)0x20, (byte)0x7E);
        }
    }

    [Fact]
    public void ResultToUtf8_WithNullBuffer_ReturnsRequiredSize()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        const NVSDK_NGX_Result Result = NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotFound;

        Span<byte> buffer = stackalloc byte[128];
        uint written;
        fixed (byte* p = buffer)
        {
            written = NgxApi.ahjo_ngx_result_to_utf8(Result, (sbyte*)p, (uint)buffer.Length);
        }

        var required = NgxApi.ahjo_ngx_result_to_utf8(Result, null, 0);

        Assert.Equal(written, required);
    }

    [Fact]
    public void LayoutQuery_UnknownId_ReturnsSentinel()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // 31, and not an arbitrary large value like 0xDEAD. AhjoNgxLayoutId's
        // largest enumerator is AHJO_NGX_LAYOUT_COUNT == 23, so per [dcl.enum]
        // the type's value range is 0..31 — the smallest bit-field that holds
        // every enumerator. Passing 0xDEAD to a parameter of that type is
        // undefined behaviour on the C++ side, in precisely the `default:` arm
        // that is being tested, where a compiler is entitled to assume the
        // value is in range and delete the arm.
        //
        // 31 is in range, is not an enumerator, and is therefore unhandled —
        // so it exercises the same `default:` arm and the same 0xFFFFFFFF
        // sentinel with no UB. Don't "tidy" it back to a bigger number.
        //
        // The boundary is EXCLUSIVE, and the off-by-one matters. Enumerators
        // are contiguous 0..COUNT, so COUNT == 31 would make 31 an enumerator
        // — AHJO_NGX_LAYOUT_COUNT itself, which has its own `case` label in
        // ahjo_ngx.cpp. The assertion below would still pass, because that
        // case returns the same sentinel, but it would be reached through the
        // COUNT arm rather than `default:` and this test would have silently
        // stopped probing the thing its name claims.
        Assert.True(
            (uint)AhjoNgxLayoutId.AHJO_NGX_LAYOUT_COUNT < 31,
            $"AHJO_NGX_LAYOUT_COUNT is now {(uint)AhjoNgxLayoutId.AHJO_NGX_LAYOUT_COUNT}, so 31 is a valid "
            + "enumerator and no longer probes the default: arm. Pick a new in-range unhandled value: the "
            + "enum's range is 0 .. (2^ceil(log2(COUNT+1)) - 1), and the probe must sit above COUNT.");

        Assert.Equal(0xFFFFFFFFu, NgxApi.ahjo_ngx_layout((AhjoNgxLayoutId)31));
    }

    [Fact]
    public void Utf8ParameterNameConstantsAreNulTerminated()
    {
        // No shim needed: this is a property of the GENERATED C#, not of the
        // native library, so it holds — and must hold — on a machine with no
        // NGX SDK staged at all.
        //
        // The 204 NVSDK_NGX_Parameter_* constants are the reason
        // tools/generate-ngx.rsp turns generate-macro-bindings on. They are
        // emitted as `ReadOnlySpan<byte> … => "Width"u8;`, and Phase 2 will
        // hand them to NVSDK_NGX_Parameter_SetUI and friends as `const char*`
        // straight out of `fixed`, per-frame, with no copy and no
        // Encoding.UTF8.GetBytes (repo invariant 1).
        //
        // That is correct ONLY because the compiler NUL-terminates a u8
        // literal in the emitted data blob — a terminator that is not counted
        // in Length and so is invisible to every span-level assertion.
        // Nothing else in this repository checks it. A future ClangSharp that
        // emitted these as byte[] initializers, or a regen that changed their
        // form, would otherwise surface as NGX reading past the end of the
        // blob in Phase 2, at speed, in a render loop.
        //
        // A representative sample rather than all 204: they are all emitted by
        // the same code path, so one shape failing means all of them fail.
        AssertNulTerminated(NgxApi.NVSDK_NGX_Parameter_Width, nameof(NgxApi.NVSDK_NGX_Parameter_Width));
        AssertNulTerminated(NgxApi.NVSDK_NGX_Parameter_Height, nameof(NgxApi.NVSDK_NGX_Parameter_Height));
        AssertNulTerminated(NgxApi.NVSDK_NGX_Parameter_Sharpness, nameof(NgxApi.NVSDK_NGX_Parameter_Sharpness));
        // A dotted name, which is the shape the DLSS hint constants use.
        AssertNulTerminated(NgxApi.NVSDK_NGX_Parameter_Jitter_Offset_X, nameof(NgxApi.NVSDK_NGX_Parameter_Jitter_Offset_X));

        static void AssertNulTerminated(ReadOnlySpan<byte> constant, string name)
        {
            Assert.False(constant.IsEmpty, $"{name} is empty; the generated constants are missing.");
            Assert.Equal(-1, constant.IndexOf((byte)0));

            fixed (byte* p = constant)
            {
                Assert.True(
                    p[constant.Length] == 0,
                    $"{name} is not NUL-terminated in the emitted data blob. Phase 2 passes these "
                    + "constants to NGX as const char* directly out of `fixed`, so without the "
                    + "terminator NGX reads past the end of the blob. Whatever changed the generated "
                    + "form has to be fixed in tools/generate-ngx.rsp, not worked around at the call site.");
            }
        }
    }

    [Fact]
    public void InitInfo_WithWrongStructSize_IsRejected()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // A stale ahjo_ngx on a consumer's search path is what this guard
        // catches. Passing null Vulkan handles is safe precisely because the
        // guard must reject before anything reaches NGX — if this test ever
        // faults instead of returning, the guard has moved or been removed.
        var info = new AhjoNgxInitInfo { StructSize = 4 };

        var result = NgxApi.ahjo_ngx_vulkan_init_utf8(&info, null, null, null, null, null);

        Assert.Equal(NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_InvalidParameter, result);
    }

    [Fact]
    public void GetFeatureInstanceExtensionRequirements_ReturnsCleanly_OnAnyHost()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // The one call in this suite that reaches NGX proper. It takes no
        // Vulkan object at all (nvsdk_ngx_vk.h:646) and is documented as
        // callable before VkInstance creation, which is what makes it the only
        // NGX entry point a lane with no loader and no ICD may call.
        // GetFeatureRequirements, by contrast, takes a VkInstance such a lane
        // deliberately has no ICD to create, and passing NULL would be
        // undefined behaviour inside NGX rather than a test.
        //
        // THIS CALL IS DRIVER-INDEPENDENT, and that is measured rather than
        // assumed. Issue #216's spec (OPEN-1) originally guessed the opposite
        // — that with no NVIDIA driver there would be no NGX core library to
        // load and so Success could not be the answer. CI disproved it: a
        // driverless windows-latest runner returns exactly what an RTX 4070 Ti
        // with driver 610.47 returns — Success, extensionCount 1,
        // VK_KHR_get_physical_device_properties2 specVersion 2.
        //
        // The two hosts agreeing to the byte is the evidence. This is a
        // pre-instance static query answered out of NVIDIA's static client
        // library; it never loads the driver-side NGX core at all. Do not
        // reintroduce a driver-conditional expectation here — there was one,
        // gated on an AHJO_NGX_EXPECT_NO_DRIVER variable, and it was removed
        // along with the variable once CI measured both host kinds.
        //
        // So there is ONE assertion, and it holds everywhere: the call must
        // return rather than fault or hang, and a Success must come with a
        // usable count and a non-null array. A Success carrying a garbage
        // count would be worse than a clean failure, because a caller would go
        // on to read it.
        var appDataPath = Path.Combine(Path.GetTempPath(), "ahjo-ngx-tests");
        Directory.CreateDirectory(appDataPath);

        // The trailing \0 in each literal is REQUIRED and is not decoration.
        // "…"u8.ToArray() copies exactly ReadOnlySpan<byte>.Length bytes, and
        // the terminator the compiler places after a u8 literal in the
        // assembly's data blob is NOT part of Length — so without it these
        // arrays reach NGX unterminated, and NGX strlen's and GUID-validates
        // both of them. That reads past the end of a pinned GC array and
        // "works" only for as long as the following heap bytes happen to be
        // zero.
        //
        // Note that this is specific to .ToArray(). Passing the span itself
        // through `fixed` keeps the blob's terminator, which is exactly what
        // Utf8ParameterNameConstantsAreNulTerminated pins for the 204
        // generated parameter-name constants.
        var projectId = "ahjo-vulkan-ngx-native-tests\0"u8.ToArray();
        var engineVersion = "0.0.0\0"u8.ToArray();
        var pathBytes = System.Text.Encoding.UTF8.GetBytes(appDataPath + "\0");

        uint extensionCount = 0xDEADBEEF;
        Ahjo.Vulkan.Native.VkExtensionProperties* extensions = null;
        NVSDK_NGX_Result result;

        fixed (byte* projectIdPtr = projectId)
        fixed (byte* engineVersionPtr = engineVersion)
        fixed (byte* appDataPathPtr = pathBytes)
        {
            var info = new AhjoNgxInitInfo
            {
                StructSize = (uint)sizeof(AhjoNgxInitInfo),
                IdentifierType = NVSDK_NGX_Application_Identifier_Type.NVSDK_NGX_Application_Identifier_Type_Project_Id,
                ProjectId = (sbyte*)projectIdPtr,
                EngineType = NVSDK_NGX_EngineType.NVSDK_NGX_ENGINE_TYPE_CUSTOM,
                EngineVersion = (sbyte*)engineVersionPtr,
                ApplicationDataPath = (sbyte*)appDataPathPtr,
                FeatureSearchPaths = null,
                FeatureSearchPathCount = 0,
                LogCallback = null,
                MinimumLoggingLevel = NVSDK_NGX_Logging_Level.NVSDK_NGX_LOGGING_LEVEL_OFF,
                DisableOtherLoggingSinks = 0,
            };

            result = NgxApi.ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8(
                NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
                &info,
                &extensionCount,
                &extensions);
        }

        // Reaching this line at all is the first half of the assertion: the
        // call returned rather than faulting or hanging.
        if (result == NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
        {
            // 64 is a sanity bound, not a spec limit: the measured answer for
            // SuperSampling is 1, and a real requirement list is a handful of
            // extensions. A count outside this range means NGX reported
            // Success without writing a meaningful result, which is the one
            // outcome a caller cannot defend against.
            Assert.True(
                extensionCount is > 0 and <= 64,
                $"ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8 returned Success "
                + $"but wrote extensionCount={extensionCount}, which is not a plausible extension count. "
                + "A Success carrying a garbage count is worse than a clean failure, because a caller "
                + "will go on to read that many VkExtensionProperties.");

            Assert.True(
                extensions != null,
                "ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8 returned Success "
                + $"with extensionCount={extensionCount} but a null extension array.");
        }
    }
}
