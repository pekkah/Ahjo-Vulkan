// HelloVma's shader.
//
// vertexMain reads positions + colours from the vertex buffer that HelloVma
// uploads to device-local memory through StagingBatch. No push constants, no
// transform — the triangle is already in clip space.
//
// fragmentMain's Tint UBO comes from a host-visible persistent-mapped buffer
// that HelloVma writes directly through Buffer.AsSpan<Tint>(). Demonstrates
// the typical "small per-frame uniform" memory pattern.
//
// Compiled at build time by the shared SlangShader step (#171): vertexMain ->
// vma.vert.spv, fragmentMain -> vma.frag.spv.

struct Tint
{
    float4 tint;
};

[[vk::binding(0, 0)]] ConstantBuffer<Tint> u;

struct VIn
{
    [[vk::location(0)]] float2 position : POSITION;
    [[vk::location(1)]] float3 color    : COLOR;
};

struct VOut
{
    float4 position : SV_Position;
    [[vk::location(0)]] float3 color : COLOR;
};

[shader("vertex")]
VOut vertexMain(VIn input)
{
    VOut output;
    output.position = float4(input.position, 0.0, 1.0);
    output.color    = input.color;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VOut input) : SV_Target
{
    return float4(input.color * u.tint.rgb, u.tint.a);
}
