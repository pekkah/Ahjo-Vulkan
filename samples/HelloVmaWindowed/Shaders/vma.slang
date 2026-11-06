// HelloVmaWindowed's shader.
//
// vertexMain reads positions + colours from the static device-local vertex
// buffer uploaded once at startup. The per-frame UBO supplies a 2D rotation +
// uniform scale + aspect correction baked into a matrix, plus a tint vector
// consumed by fragmentMain.
//
// Compiled at build time by the shared SlangShader step (#171): vertexMain ->
// vma.vert.spv, fragmentMain -> vma.frag.spv.
//
// Matrix convention (spec E4, same as HelloDlaa/Shaders/cube.slang):
// System.Numerics row-vector matrices, so the matrix is declared row_major and
// multiplied as mul(v, m). That reproduces glslc's `transform * v` on the
// bytes Program.cs writes, with no transpose and no compiler flag.

struct Frame
{
    row_major float4x4 transform;
    float4             tint;
};

[[vk::binding(0, 0)]] ConstantBuffer<Frame> u;

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
    output.position = mul(float4(input.position, 0.0, 1.0), u.transform);
    output.color    = input.color;
    return output;
}

[shader("fragment")]
float4 fragmentMain(VOut input) : SV_Target
{
    return float4(input.color * u.tint.rgb, u.tint.a);
}
