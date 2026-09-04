using Ahjo.Vulkan.Ngx.Native;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The wrapper's public NGX enums hand-copy their numeric values from the
/// generated ones rather than aliasing them, so the public API reads
/// idiomatically and does not leak <c>NVSDK_NGX_</c>-prefixed member names.
/// Correct today — but an <c>NgxVersion</c> bump plus a regen could renumber a
/// native member and silently desynchronize a shadow value.
/// </summary>
/// <remarks>
/// <para>Every pair is spelled out. No reflection (issue #122): the failure
/// message names the offending member, and the suite stays irrelevant to trim
/// and AOT.</para>
/// <para>Each enum also gets a <b>member-count</b> assertion. Values drifting is
/// one failure mode; a pin bump <i>adding</i> a member the shadow does not
/// carry is the other, and only a count catches it. When one of these fails,
/// the fix is a decision — carry the new member or record why not — never just
/// bumping the number.</para>
/// <para>Needs no device, no driver and no shim.</para>
/// </remarks>
public sealed class NgxShadowEnumDriftTests
{
    [Fact]
    public void DlssQualityMode_MatchesNative()
    {
        Assert.Equal((uint)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_MaxPerf, (uint)DlssQualityMode.MaxPerformance);
        Assert.Equal((uint)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_Balanced, (uint)DlssQualityMode.Balanced);
        Assert.Equal((uint)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_MaxQuality, (uint)DlssQualityMode.MaxQuality);
        Assert.Equal((uint)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_UltraPerformance, (uint)DlssQualityMode.UltraPerformance);
        Assert.Equal((uint)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_UltraQuality, (uint)DlssQualityMode.UltraQuality);
        Assert.Equal((uint)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_DLAA, (uint)DlssQualityMode.Dlaa);
    }

    [Fact]
    public void DlssQualityMode_ShadowsEveryNativeMember()
    {
        // Six native members, six shadowed. A seventh in the SDK is a decision
        // to make, not a number to bump.
        Assert.Equal(6, Enum.GetValues<NVSDK_NGX_PerfQuality_Value>().Length);
        Assert.Equal(6, Enum.GetValues<DlssQualityMode>().Length);
    }

    [Fact]
    public void DlssFeatureFlags_MatchesNative()
    {
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_None, (int)DlssFeatureFlags.None);
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_IsHDR, (int)DlssFeatureFlags.Hdr);
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_MVLowRes, (int)DlssFeatureFlags.MotionVectorsLowRes);
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_MVJittered, (int)DlssFeatureFlags.MotionVectorsJittered);
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_DepthInverted, (int)DlssFeatureFlags.DepthInverted);
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_AutoExposure, (int)DlssFeatureFlags.AutoExposure);
        Assert.Equal((int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_AlphaUpscaling, (int)DlssFeatureFlags.AlphaUpscaling);
    }

    [Fact]
    public void DlssFeatureFlags_OmitsFourNativeMembersDeliberately()
    {
        // 11 native, 7 shadowed. The four omissions are DoSharpening
        // (sharpening is deprecated — guide §3.5, #214), IsInvalid (a sentinel,
        // not a flag) and the two Reserved_* members.
        Assert.Equal(11, Enum.GetValues<NVSDK_NGX_DLSS_Feature_Flags>().Length);
        Assert.Equal(7, Enum.GetValues<DlssFeatureFlags>().Length);

        // Pin the omissions by value, so a regen that renumbers one into a
        // shadowed slot fails here rather than silently changing meaning.
        Assert.Equal(1 << 5, (int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_DoSharpening);
        Assert.Equal(1 << 4, (int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_Reserved_0);
        Assert.Equal(1 << 8, (int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_Reserved_8);
        Assert.Equal(1 << 31, (int)NVSDK_NGX_DLSS_Feature_Flags.NVSDK_NGX_DLSS_Feature_Flags_IsInvalid);
    }

    [Fact]
    public void DlssPreset_MatchesNative()
    {
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_Default, (uint)DlssPreset.Default);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_E, (uint)DlssPreset.E);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_F, (uint)DlssPreset.F);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_G, (uint)DlssPreset.G);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_J, (uint)DlssPreset.J);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_K, (uint)DlssPreset.K);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_L, (uint)DlssPreset.L);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_M, (uint)DlssPreset.M);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_N, (uint)DlssPreset.N);
        Assert.Equal((uint)NVSDK_NGX_DLSS_Hint_Render_Preset.NVSDK_NGX_DLSS_Hint_Render_Preset_O, (uint)DlssPreset.O);
    }

    [Fact]
    public void DlssPreset_OmitsTheTwoReservedMembers()
    {
        // 12 native, 10 shadowed: H_Reserved and I_Reserved are NVIDIA's.
        Assert.Equal(12, Enum.GetValues<NVSDK_NGX_DLSS_Hint_Render_Preset>().Length);
        Assert.Equal(10, Enum.GetValues<DlssPreset>().Length);
    }

    [Fact]
    public void NgxLoggingLevel_MatchesNative()
    {
        Assert.Equal((uint)NVSDK_NGX_Logging_Level.NVSDK_NGX_LOGGING_LEVEL_OFF, (uint)NgxLoggingLevel.Off);
        Assert.Equal((uint)NVSDK_NGX_Logging_Level.NVSDK_NGX_LOGGING_LEVEL_ON, (uint)NgxLoggingLevel.On);
        Assert.Equal((uint)NVSDK_NGX_Logging_Level.NVSDK_NGX_LOGGING_LEVEL_VERBOSE, (uint)NgxLoggingLevel.Verbose);
    }

    [Fact]
    public void NgxLoggingLevel_OmitsTheTerminatingCount()
    {
        // 4 native, 3 shadowed: _NUM is a count, not a level.
        Assert.Equal(4, Enum.GetValues<NVSDK_NGX_Logging_Level>().Length);
        Assert.Equal(3, Enum.GetValues<NgxLoggingLevel>().Length);
    }

    [Fact]
    public void NgxFeatureSupport_MatchesNative()
    {
        Assert.Equal((uint)NVSDK_NGX_Feature_Support_Result.NVSDK_NGX_FeatureSupportResult_Supported, (uint)NgxFeatureSupport.Supported);
        Assert.Equal((uint)NVSDK_NGX_Feature_Support_Result.NVSDK_NGX_FeatureSupportResult_CheckNotPresent, (uint)NgxFeatureSupport.CheckNotPresent);
        Assert.Equal((uint)NVSDK_NGX_Feature_Support_Result.NVSDK_NGX_FeatureSupportResult_DriverVersionUnsupported, (uint)NgxFeatureSupport.DriverVersionUnsupported);
        Assert.Equal((uint)NVSDK_NGX_Feature_Support_Result.NVSDK_NGX_FeatureSupportResult_AdapterUnsupported, (uint)NgxFeatureSupport.AdapterUnsupported);
        Assert.Equal((uint)NVSDK_NGX_Feature_Support_Result.NVSDK_NGX_FeatureSupportResult_OSVersionBelowMinimumSupported, (uint)NgxFeatureSupport.OsVersionBelowMinimum);
        Assert.Equal((uint)NVSDK_NGX_Feature_Support_Result.NVSDK_NGX_FeatureSupportResult_NotImplemented, (uint)NgxFeatureSupport.NotImplemented);
    }

    [Fact]
    public void NgxFeatureSupport_ShadowsEveryNativeMember()
    {
        Assert.Equal(6, Enum.GetValues<NVSDK_NGX_Feature_Support_Result>().Length);
        Assert.Equal(6, Enum.GetValues<NgxFeatureSupport>().Length);
    }
}
