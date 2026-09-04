// HelloDlaa's one shader file: a textured, lit cube that also writes the
// motion vectors DLSS needs.
//
// Two entry points, compiled at run time by Ahjo.Vulkan.Slang. The order of
// the EntryPoints array on the C# side is the order Spirv(i) indexes, so
// vertexMain must stay first.
//
// Conventions (issue #219 spec D2 — a closed derivation, do not re-derive):
//   * System.Numerics row-vector matrices, so every multiply is mul(v, m) and
//     every matrix is declared row_major.
//   * The projection carries `proj.M22 *= -1f`, giving a y-DOWN NDC and a
//     [0,1] depth range (near 0, far 1).
//   * DLSS pixel space has its origin at the top-left, +X right, +Y down —
//     which is what the y-down NDC above already produces.
//   * There is NO NEGATION anywhere in this file. If you find yourself adding
//     one, re-read spec D2 instead.

struct FrameUniforms
{
    row_major float4x4 jitteredMvp;    // model * view * projJittered
    row_major float4x4 currentMvp;     // model * view * proj   (UNJITTERED)
    row_major float4x4 previousMvp;    // last frame's, unjittered
    row_major float4x4 model;
    float2             renderExtent;   // (renderWidth, renderHeight)
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
    float4 position : SV_Position;   // JITTERED clip position — this is what rasterizes
    float4 curClip  : TEXCOORD0;     // unjittered current clip position
    float4 prevClip : TEXCOORD1;     // unjittered previous clip position
    float2 uv       : TEXCOORD2;
    float3 normalWs : TEXCOORD3;
};

struct PsOut
{
    float4 color  : SV_Target0;
    float2 motion : SV_Target1;
};

[shader("vertex")]
VsOut vertexMain(VsIn input)
{
    VsOut output;

    float4 p = float4(input.position, 1.0);

    // Row-vector composition throughout: `mul(v, m)` matches System.Numerics'
    // `v * M`, which is why the C# side can hand the Matrix4x4 bytes straight
    // over with `row_major` on the declaration and no transpose.
    output.position = mul(p, frame.jitteredMvp);
    output.curClip  = mul(p, frame.currentMvp);
    output.prevClip = mul(p, frame.previousMvp);

    output.uv       = input.uv;
    output.normalWs = normalize(mul(float4(input.normal, 0.0), frame.model).xyz);

    return output;
}

// The exact sRGB transfer function, not pow(c, 1/2.2). The colour attachment
// is R8G8B8A8_UNORM and DLSS runs in LDR mode, which the programming guide
// §3.1.2 says requires a PERCEPTUALLY encoded colour buffer — linear values in
// LDR mode "exhibit visible color banding, color shifting or other visual
// artifacts". The gamma-2.2 approximation is close but wrong in the toe, and
// the toe is where the banding shows; an _SRGB attachment would be worse
// still, because the hardware would decode on the sampled read NGX performs
// and hand DLSS linear values anyway.
float3 encodeSrgb(float3 linearColor)
{
    float3 c  = saturate(linearColor);
    float3 lo = c * 12.92;
    float3 hi = 1.055 * pow(c, 1.0 / 2.4) - 0.055;
    return select(c <= 0.0031308, lo, hi);
}

[shader("fragment")]
PsOut fragmentMain(VsOut input)
{
    PsOut output;

    // 1. Shade, then encode. The encode applies to the SHADED RESULT; the
    //    texture's own texels are authored already-encoded (see CubeScene),
    //    so this is a single encode, not a double one.
    const float3 lightDirection = normalize(float3(0.4, 0.7, -0.55));
    float3 n = normalize(input.normalWs);
    float  nDotL = saturate(dot(n, lightDirection));
    float3 shaded = albedo.Sample(input.uv).rgb * (0.25 + 0.75 * nDotL);
    output.color = float4(encodeSrgb(shaded), 1.0);   // 3. Alpha 1.0.

    // 2. Motion vectors — spec D2 step 4, verbatim.
    //
    //    PREVIOUS MINUS CURRENT: guide §3.6 defines the vector so that adding
    //    it to the pixel's current location gives where that pixel was last
    //    frame.
    //
    //    Multiplied by the render extent because §3.6.1 wants
    //    RENDER-RESOLUTION PIXELS, not UV. (MotionVectorScaleX/Y on the
    //    evaluate stay at 1.0 for exactly this reason.)
    //
    //    +X right / +Y down, which the M22-flipped projection already gives —
    //    so there is DELIBERATELY NO NEGATION here. Both clip positions come
    //    from UNJITTERED matrices, which is why DlssFeatureFlags
    //    .MotionVectorsJittered stays clear.
    float2 curUV  = input.curClip.xy  / input.curClip.w  * 0.5 + 0.5;
    float2 prevUV = input.prevClip.xy / input.prevClip.w * 0.5 + 0.5;
    output.motion = (prevUV - curUV) * frame.renderExtent;

    return output;
}
