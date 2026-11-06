// The triangle HeadlessExport renders into its exportable image: a hard-coded
// NDC triangle whose corner SV_VertexID picks, with per-vertex colours
// forwarded so the fragment stage interpolates them. Same source as
// ../../HeadlessTriangle/Shaders/triangle.slang.
//
// Compiled at build time by the shared SlangShader step (#171): vertexMain ->
// triangle.vert.spv, fragmentMain -> triangle.frag.spv.
//
// SV_VertexID maps onto Vulkan's VertexIndex minus BaseVertex, so the module
// declares the SPIR-V DrawParameters capability. That is why Program.cs
// enables shaderDrawParameters (#168).

struct VOut
{
    float4 position : SV_Position;
    [[vk::location(0)]] float3 color : COLOR;
};

static const float2 kPositions[3] =
{
    float2(-0.6, -0.5),
    float2( 0.6, -0.5),
    float2( 0.0,  0.6),
};

static const float3 kColors[3] =
{
    float3(1.0, 0.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, 1.0),
};

[shader("vertex")]
VOut vertexMain(uint vertexIndex : SV_VertexID)
{
    VOut output;
    output.position = float4(kPositions[vertexIndex], 0.0, 1.0);
    output.color    = kColors[vertexIndex];
    return output;
}

[shader("fragment")]
float4 fragmentMain(VOut input) : SV_Target
{
    return float4(input.color, 1.0);
}
