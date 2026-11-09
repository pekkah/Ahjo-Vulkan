// HelloFrameGen's UI and composite passes.
//
// Four entry points, compiled at run time by Ahjo.Vulkan.Slang; the order of
// the EntryPoints array in OverlayPipelines.cs is the order Spirv(i) indexes:
// uiVertexMain, uiFragmentMain, compositeVertexMain, compositeFragmentMain.
//
// The contract both passes serve is the DLSS-FG guide's (l. 742-754):
//
//     Backbuffer = UI + (1 - UI.a) * Hudless
//
// with UI PREMULTIPLIED by its alpha, every non-UI pixel of UI equal to 0, and
// every non-UI pixel of Backbuffer exactly equal to Hudless.
//
// Everything here works in the ENCODED (sRGB) values the targets store. The
// cube shader already encodes Hudless; the UI colour below is authored in
// encoded space and premultiplied there; and the composite applies the formula
// to the raw texels. That makes the formula hold on exactly what Frame
// Generation reads, and makes non-UI pixels bit-identical by construction:
// UI = (0,0,0,0) there, so the composite writes Hudless's own 8-bit value back.

// ---- UI: one translucent, premultiplied quad. -------------------------------

struct UiOut
{
    float4 position : SV_Position;
};

// A HUD panel across the lower-left of the screen, in NDC (y down).
static const float2 kUiCorners[6] =
{
    float2(-0.90, 0.55), float2(-0.20, 0.55), float2(-0.20, 0.85),
    float2(-0.90, 0.55), float2(-0.20, 0.85), float2(-0.90, 0.85),
};

[shader("vertex")]
UiOut uiVertexMain(uint vertexIndex : SV_VertexID)
{
    UiOut output;
    output.position = float4(kUiCorners[vertexIndex], 0.0, 1.0);
    return output;
}

[shader("fragment")]
float4 uiFragmentMain(UiOut input) : SV_Target
{
    // An amber panel at 60% opacity. Encoded colour, premultiplied by alpha.
    const float  alpha = 0.6;
    const float3 color = float3(0.95, 0.70, 0.15);
    return float4(color * alpha, alpha);
}

// ---- Composite: Backbuffer = UI + (1 - UI.a) * Hudless. ---------------------

[[vk::binding(0, 0)]] Texture2D<float4> hudless;
[[vk::binding(1, 0)]] Texture2D<float4> ui;

struct CompositeOut
{
    float4 position : SV_Position;
};

[shader("vertex")]
CompositeOut compositeVertexMain(uint vertexIndex : SV_VertexID)
{
    // (-1,-1), (3,-1), (-1,3): one triangle covering the viewport.
    float2 xy = float2((vertexIndex << 1) & 2, vertexIndex & 2) * 2.0 - 1.0;
    CompositeOut output;
    output.position = float4(xy, 0.0, 1.0);
    return output;
}

[shader("fragment")]
float4 compositeFragmentMain(CompositeOut input) : SV_Target
{
    // Load, not Sample: same extent, one texel per pixel, no filtering.
    int3 texel = int3(int2(input.position.xy), 0);
    float4 h = hudless.Load(texel);
    float4 u = ui.Load(texel);
    return float4(u.rgb + (1.0 - u.a) * h.rgb, 1.0);
}
