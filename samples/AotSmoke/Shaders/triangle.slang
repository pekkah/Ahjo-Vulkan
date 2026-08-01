// The Slang equivalent of ../../HeadlessTriangle/Shaders/triangle.{vert,frag},
// which this sample also links (see AotSmoke.csproj). Same hard-coded NDC
// triangle, same per-vertex colours, same interpolation — so the PNG this
// smoke run writes is the one HeadlessTriangle writes.
//
// It exists so `dotnet publish -p:PublishAot=true` covers Ahjo.Vulkan.Slang
// and its binding: compiling a shader at run time is the one thing in this
// repository that could plausibly need reflection or dynamic codegen, and ILC
// is the only tool that proves it does not.

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
