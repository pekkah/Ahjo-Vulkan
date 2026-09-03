using Xunit;

namespace Ahjo.Vulkan.Ngx.Native.Tests;

/// <summary>
/// Proves that the C# view of NGX's structs is byte-for-byte the C++ one, on
/// whichever toolchain built the shim.
/// <para>
/// <b>Sizes alone cannot verify these layouts.</b>
/// <see cref="NVSDK_NGX_Resource_VK"/> ends with a 4-byte enum and a 1-byte
/// bool inside 8 bytes of tail padding, so swapping <c>Type</c> and
/// <c>ReadWrite</c> leaves <c>sizeof</c> at 56 while changing the meaning of
/// every DLSS resource binding. Offsets are therefore load-bearing, and every
/// field of every struct on the evaluate path has its own assertion.
/// </para>
/// <para>
/// Each case asserts twice: against <c>ahjo_ngx_layout</c> (what the C++
/// compiler computed) <b>and</b> against the measured literal. Asserting only
/// the first would pass a matching pair of wrong values — if a struct were
/// reordered in an upstream header, both sides would move together and the
/// test would stay green while the binding silently changed meaning.
/// </para>
/// <para>
/// Offsets are read with pointer arithmetic rather than
/// <c>Marshal.OffsetOf</c>, which is unusable under
/// <c>DisableRuntimeMarshalling</c>, and with no reflection — the suite stays
/// AOT-shaped like the rest of the repo.
/// </para>
/// </summary>
public sealed unsafe class NgxStructLayoutTests
{
    /// <summary>
    /// Every layout id this suite actually asserts on.
    /// <c>EveryLayoutId_IsCoveredByThisSuite</c> compares its length against
    /// <c>AHJO_NGX_LAYOUT_COUNT</c>, so adding a native id without adding a
    /// managed assertion fails there.
    /// </summary>
    private static readonly AhjoNgxLayoutId[] CoveredIds =
    [
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_SIZE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_ALIGN,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_RESOURCE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_TYPE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_READWRITE,

        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_SIZE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_ALIGN,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE_VIEW,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_SUBRESOURCE_RANGE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_FORMAT,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_WIDTH,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_HEIGHT,

        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_SIZE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_ALIGN,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_BUFFER,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_SIZE_IN_BYTES,

        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_SIZE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_ALIGN,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_FEATURE_SUPPORTED,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_HW_ARCHITECTURE,
        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_OS_VERSION,

        AhjoNgxLayoutId.AHJO_NGX_LAYOUT_INIT_INFO_SIZE,
    ];

    private const uint UnknownIdSentinel = 0xFFFFFFFFu;

    [Fact]
    public void ResourceVk_LayoutMatchesTheShim()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        NVSDK_NGX_Resource_VK value = default;
        var start = (byte*)&value;

        AssertLayout("sizeof(NVSDK_NGX_Resource_VK)", 56, (uint)sizeof(NVSDK_NGX_Resource_VK),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_SIZE);
        // No managed counterpart for alignment: C# has no alignof. The shim's
        // value is the only source, so this one is pinned by the literal.
        AssertNative("alignof(NVSDK_NGX_Resource_VK)", 8,
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_ALIGN);

        AssertLayout("NVSDK_NGX_Resource_VK.Resource", 0, (uint)((byte*)&value.Resource - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_RESOURCE);
        AssertLayout("NVSDK_NGX_Resource_VK.Type", 48, (uint)((byte*)&value.Type - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_TYPE);
        AssertLayout("NVSDK_NGX_Resource_VK.ReadWrite", 52, (uint)((byte*)&value.ReadWrite - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_READWRITE);
    }

    [Fact]
    public void ImageViewInfoVk_LayoutMatchesTheShim()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        NVSDK_NGX_ImageViewInfo_VK value = default;
        var start = (byte*)&value;

        AssertLayout("sizeof(NVSDK_NGX_ImageViewInfo_VK)", 48, (uint)sizeof(NVSDK_NGX_ImageViewInfo_VK),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_SIZE);
        AssertNative("alignof(NVSDK_NGX_ImageViewInfo_VK)", 8,
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_ALIGN);

        AssertLayout("NVSDK_NGX_ImageViewInfo_VK.ImageView", 0, (uint)((byte*)&value.ImageView - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE_VIEW);
        AssertLayout("NVSDK_NGX_ImageViewInfo_VK.Image", 8, (uint)((byte*)&value.Image - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE);
        AssertLayout("NVSDK_NGX_ImageViewInfo_VK.SubresourceRange", 16, (uint)((byte*)&value.SubresourceRange - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_SUBRESOURCE_RANGE);
        AssertLayout("NVSDK_NGX_ImageViewInfo_VK.Format", 36, (uint)((byte*)&value.Format - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_FORMAT);
        AssertLayout("NVSDK_NGX_ImageViewInfo_VK.Width", 40, (uint)((byte*)&value.Width - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_WIDTH);
        AssertLayout("NVSDK_NGX_ImageViewInfo_VK.Height", 44, (uint)((byte*)&value.Height - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_HEIGHT);
    }

    [Fact]
    public void BufferInfoVk_LayoutMatchesTheShim()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        NVSDK_NGX_BufferInfo_VK value = default;
        var start = (byte*)&value;

        AssertLayout("sizeof(NVSDK_NGX_BufferInfo_VK)", 16, (uint)sizeof(NVSDK_NGX_BufferInfo_VK),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_SIZE);
        AssertNative("alignof(NVSDK_NGX_BufferInfo_VK)", 8,
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_ALIGN);

        AssertLayout("NVSDK_NGX_BufferInfo_VK.Buffer", 0, (uint)((byte*)&value.Buffer - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_BUFFER);
        AssertLayout("NVSDK_NGX_BufferInfo_VK.SizeInBytes", 8, (uint)((byte*)&value.SizeInBytes - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_SIZE_IN_BYTES);
    }

    [Fact]
    public void FeatureRequirement_LayoutMatchesTheShim()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        NVSDK_NGX_FeatureRequirement value = default;
        var start = (byte*)&value;

        // 264, not 263: MinOSVersion is char[255] starting at offset 8, and
        // the struct's 4-byte alignment rounds 263 up.
        AssertLayout("sizeof(NVSDK_NGX_FeatureRequirement)", 264, (uint)sizeof(NVSDK_NGX_FeatureRequirement),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_SIZE);
        AssertNative("alignof(NVSDK_NGX_FeatureRequirement)", 4,
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_ALIGN);

        AssertLayout("NVSDK_NGX_FeatureRequirement.FeatureSupported", 0, (uint)((byte*)&value.FeatureSupported - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_FEATURE_SUPPORTED);
        AssertLayout("NVSDK_NGX_FeatureRequirement.MinHWArchitecture", 4, (uint)((byte*)&value.MinHWArchitecture - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_HW_ARCHITECTURE);
        AssertLayout("NVSDK_NGX_FeatureRequirement.MinOSVersion", 8, (uint)((byte*)&value.MinOSVersion - start),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_OS_VERSION);
    }

    [Fact]
    public void InitInfo_SizeMatchesTheShim()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // This is the oracle for the StructSize guard in
        // ahjo_ngx_vulkan_init_utf8: managed callers set
        // StructSize = sizeof(AhjoNgxInitInfo), and the shim rejects anything
        // that does not equal its own. If these two ever disagree, every call
        // into the shim returns FAIL_InvalidParameter and this test says why.
        AssertLayout("sizeof(AhjoNgxInitInfo)", 80, (uint)sizeof(AhjoNgxInitInfo),
            AhjoNgxLayoutId.AHJO_NGX_LAYOUT_INIT_INFO_SIZE);
    }

    [Fact]
    public void EveryLayoutId_IsCoveredByThisSuite()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        var count = (uint)AhjoNgxLayoutId.AHJO_NGX_LAYOUT_COUNT;

        for (uint id = 0; id < count; id++)
        {
            Assert.True(
                NgxApi.ahjo_ngx_layout((AhjoNgxLayoutId)id) != UnknownIdSentinel,
                $"ahjo_ngx_layout returned the unknown-id sentinel for id {id} "
                + $"({(AhjoNgxLayoutId)id}), which is below AHJO_NGX_LAYOUT_COUNT. "
                + "The switch in native/ngx/src/ahjo_ngx.cpp is missing an arm.");
        }

        Assert.True(
            CoveredIds.Length == count,
            $"AHJO_NGX_LAYOUT_COUNT is {count} but this suite asserts on {CoveredIds.Length} id(s). "
            + "A layout id was added natively without a managed assertion — add it to CoveredIds "
            + "and to the test that owns its struct.");
    }

    [Fact]
    public void ReadWriteField_IsAOneByteBoolAtOffset52()
    {
        if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }

        // Pins the finding that ClangSharp emits `bool ReadWrite`, not `byte`.
        // The struct is only ever handed to NGX through
        // NVSDK_NGX_Parameter_SetVoidPointer(…, void*), so
        // DisableRuntimeMarshalling's blittable-only rule is never engaged and
        // C# bool is fine — but that means callers write `= true`, not `= 1`,
        // and this test is what stops that answer drifting back.
        Assert.Equal(1, sizeof(bool));

        NVSDK_NGX_Resource_VK value = default;
        var start = (byte*)&value;

        var offset = (uint)((byte*)&value.ReadWrite - start);
        Assert.Equal(52u, offset);
        Assert.Equal(offset, NgxApi.ahjo_ngx_layout(AhjoNgxLayoutId.AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_READWRITE));

        Assert.Equal(0, start[offset]);
        value.ReadWrite = true;
        Assert.NotEqual(0, start[offset]);
    }

    /// <summary>
    /// Asserts a value against both the shim and the measured literal.
    /// </summary>
    private static void AssertLayout(string what, uint expected, uint managed, AhjoNgxLayoutId id)
    {
        var native = NgxApi.ahjo_ngx_layout(id);

        Assert.True(
            native == expected && managed == expected,
            $"{what}: expected {expected}, shim reported {native}, managed reported {managed}. "
            + "If the shim and managed values agree with each other but not with the literal, "
            + "an upstream header changed the layout and Phase 2's resource bindings need re-checking.");
    }

    /// <summary>
    /// Asserts a shim-only value (alignment; C# has no <c>alignof</c>).
    /// </summary>
    private static void AssertNative(string what, uint expected, AhjoNgxLayoutId id)
    {
        var native = NgxApi.ahjo_ngx_layout(id);

        Assert.True(
            native == expected,
            $"{what}: expected {expected}, shim reported {native}. "
            + "C# has no alignof, so the literal is the only oracle for this one.");
    }
}
