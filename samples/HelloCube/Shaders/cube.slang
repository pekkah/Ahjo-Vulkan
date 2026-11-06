// HelloCube's shader: a textured cube with a mild per-face tint.
//
// Compiled at build time by the shared SlangShader step (#171): vertexMain ->
// cube.vert.spv, fragmentMain -> cube.frag.spv.
//
// Matrix convention (spec E4, same as HelloDlaa/Shaders/cube.slang):
// System.Numerics row-vector matrices, so the matrix is declared row_major and
// multiplied as mul(v, m). That reproduces glslc's `mvp * v` on the bytes
// Program.cs pushes, with no transpose and no compiler flag.

struct PushConstants
{
    row_major float4x4 mvp;
};

[[vk::push_constant]] ConstantBuffer<PushConstants> pc;

[[vk::binding(0, 0)]] Sampler2D uTexture;

struct VIn
{
    [[vk::location(0)]] float3 position : POSITION;
    [[vk::location(1)]] float2 uv       : TEXCOORD0;
    [[vk::location(2)]] float3 faceTint : COLOR;
};

struct VOut
{
    float4 position : SV_Position;
    [[vk::location(0)]] float2 uv       : TEXCOORD0;
    [[vk::location(1)]] float3 faceTint : COLOR;
};

[shader("vertex")]
VOut vertexMain(VIn input)
{
    VOut output;
    output.position = mul(float4(input.position, 1.0), pc.mvp);
    output.uv       = input.uv;
    output.faceTint = input.faceTint;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VOut input) : SV_Target
{
    float3 sampled = uTexture.Sample(input.uv).rgb;
    // Mild face tint kept around 0.92..1.0 so the wood pattern stays
    // readable on every face but adjacent sides don't blur into one
    // continuous surface when the cube tumbles.
    float3 tint = lerp(float3(1.0), input.faceTint, 0.25);
    return float4(sampled * tint, 1.0);
}
