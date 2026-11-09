// HelloFrameGen's scene shader: a textured, lit cube that writes the HUD-less
// colour and the motion vectors DLSS Frame Generation needs, plus a background
// pass that writes the camera's motion for every pixel the cube does not cover.
//
// Four entry points, compiled at run time by Ahjo.Vulkan.Slang. The order of
// the EntryPoints array on the C# side (CubePipeline.cs) is the order Spirv(i)
// indexes: vertexMain, fragmentMain, backgroundVertexMain, backgroundFragmentMain.
//
// Conventions (the HelloDlaa derivation, issue #219 spec D2; do not re-derive):
//   * System.Numerics row-vector matrices, so every multiply is mul(v, m) and
//     every matrix is declared row_major.
//   * The projection carries `proj.M22 *= -1f`, giving a y-DOWN NDC and a
//     [0,1] depth range (near 0, far 1).
//   * Pixel space has its origin at the top-left, +X right, +Y down, which is
//     what the y-down NDC above already produces.
//   * There is NO NEGATION anywhere in this file.
//   * No jitter: Frame Generation runs here without Super Resolution, so the
//     rasterized position IS the unjittered current clip position.

struct FrameUniforms
{
    row_major float4x4 currentMvp;      // model * view * proj
    row_major float4x4 previousMvp;     // last frame's
    row_major float4x4 model;
    row_major float4x4 clipToPrevClip;  // Invert(view * proj) * prevView * prevProj
    float2             renderExtent;    // (renderWidth, renderHeight)
    float2             pad;
};

[[vk::binding(0, 0)]] ConstantBuffer<FrameUniforms> frame;
[[vk::binding(1, 0)]] Sampler2D<float4> albedo;     // combined image sampler

struct VsIn
{
    float3 position : POSITION;
    float2 uv       : TEXCOORD0;
    float3 normal   : NORMAL;
};

struct VsOut
{
    float4 position : SV_Position;
    float4 curClip  : TEXCOORD0;     // current clip position
    float4 prevClip : TEXCOORD1;     // previous clip position
    float2 uv       : TEXCOORD2;
    float3 normalWs : TEXCOORD3;
};

struct PsOut
{
    float4 color  : SV_Target0;      // Hudless: the scene with no UI
    float2 motion : SV_Target1;      // motion vectors, render pixels
};

[shader("vertex")]
VsOut vertexMain(VsIn input)
{
    VsOut output;

    float4 p = float4(input.position, 1.0);

    output.position = mul(p, frame.currentMvp);
    output.curClip  = output.position;
    output.prevClip = mul(p, frame.previousMvp);

    output.uv       = input.uv;
    output.normalWs = normalize(mul(float4(input.normal, 0.0), frame.model).xyz);

    return output;
}

// The exact sRGB transfer function, not pow(c, 1/2.2). The colour targets are
// R8G8B8A8_UNORM and Frame Generation runs in LDR mode, so the colour it reads
// must be perceptually encoded, exactly as DLSS SR's LDR mode requires
// (docs/ngx-notes.md §6). An _SRGB target cannot be used at all: the outputs
// share the backbuffer's format and need storage usage.
float3 encodeSrgb(float3 linearColor)
{
    float3 c  = saturate(linearColor);
    float3 lo = c * 12.92;
    float3 hi = 1.055 * pow(c, 1.0 / 2.4) - 0.055;
    return select(c <= 0.0031308, lo, hi);
}

// Motion vector for one pixel: PREVIOUS MINUS CURRENT, in pixels of the motion
// resource (DLSS-FG guide, "Motion Vectors (pMVecs)"; the same convention as
// Super Resolution, docs/ngx-notes.md §4.2).
float2 motionPixels(float4 curClip, float4 prevClip)
{
    float2 curUV  = curClip.xy  / curClip.w  * 0.5 + 0.5;
    float2 prevUV = prevClip.xy / prevClip.w * 0.5 + 0.5;
    return (prevUV - curUV) * frame.renderExtent;
}

[shader("fragment")]
PsOut fragmentMain(VsOut input)
{
    PsOut output;

    // Shade, then encode. The texture's own texels are authored
    // already-encoded (see CubeScene), so this is a single encode.
    const float3 lightDirection = normalize(float3(0.4, 0.7, -0.55));
    float3 n = normalize(input.normalWs);
    float  nDotL = saturate(dot(n, lightDirection));
    float3 shaded = albedo.Sample(input.uv).rgb * (0.25 + 0.75 * nDotL);
    output.color  = float4(encodeSrgb(shaded), 1.0);

    output.motion = motionPixels(input.curClip, input.prevClip);
    return output;
}

// ---- The background pass. ---------------------------------------------------
//
// A full-screen triangle at depth 1.0 (the far plane), drawn AFTER the cube
// with depth test LESS_OR_EQUAL and no depth write, so it lands only on the
// pixels the cube left at the cleared depth of 1.0. It writes the background
// colour and the motion the CAMERA alone gives a far-plane pixel, through the
// same ClipToPrevClip matrix Frame Generation is handed.
//
// Without it the background would carry zero motion while the camera orbits,
// and the dev DLL's "Clip to Previous Clip Matrix Test" would flag it under
// either matrix orientation, which is exactly the comparison #253 PR C step
// 19a makes.

struct BgOut
{
    float4 position : SV_Position;
    float4 curClip  : TEXCOORD0;
};

[shader("vertex")]
BgOut backgroundVertexMain(uint vertexIndex : SV_VertexID)
{
    // (-1,-1), (3,-1), (-1,3): one triangle covering the viewport.
    float2 xy = float2((vertexIndex << 1) & 2, vertexIndex & 2) * 2.0 - 1.0;
    BgOut output;
    output.position = float4(xy, 1.0, 1.0);
    output.curClip  = output.position;
    return output;
}

[shader("fragment")]
PsOut backgroundFragmentMain(BgOut input)
{
    PsOut output;
    // The clear colour HelloDlaa uses, written already-encoded.
    output.color  = float4(0.05, 0.06, 0.09, 1.0);
    output.motion = motionPixels(input.curClip, mul(input.curClip, frame.clipToPrevClip));
    return output;
}
