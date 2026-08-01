// Fixture for Compile_FromFile_ProducesSpirv. Deliberately the same
// hard-coded NDC triangle as samples/HeadlessTriangle/Shaders/triangle.vert
// so the two can be eyeballed against each other: gl_VertexIndex becomes
// SV_VertexID, the `out vec3 vColor` becomes a struct member with a semantic,
// and the fragment stage joins the same file.

struct VOut
{
    float4 position : SV_Position;
    float3 color    : COLOR;
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
