namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// The shader sources the suite compiles. Kept in one place so a test reads as
/// an assertion about the API rather than as a wall of HLSL.
/// </summary>
internal static class ShaderFixtures
{
    /// <summary>Every SPIR-V module starts with this magic number.</summary>
    public const uint SpirvMagic = 0x07230203;

    /// <summary>One <c>[shader("vertex")]</c> entry point, nothing else.</summary>
    public const string VertexOnly = """
        [shader("vertex")]
        float4 vertexMain(uint vertexIndex : SV_VertexID) : SV_Position
        {
            return float4(float(vertexIndex), 0.0, 0.0, 1.0);
        }
        """;

    /// <summary>A vertex and a fragment entry point, in that declaration order.</summary>
    public const string VertexAndFragment = """
        struct VOut
        {
            float4 position : SV_Position;
            float3 color    : COLOR;
        };

        [shader("vertex")]
        VOut vertexMain(uint vertexIndex : SV_VertexID)
        {
            VOut output;
            output.position = float4(float(vertexIndex), 0.0, 0.0, 1.0);
            output.color    = float3(1.0, 0.0, 0.0);
            return output;
        }

        [shader("fragment")]
        float4 fragmentMain(VOut input) : SV_Target
        {
            return float4(input.color, 1.0);
        }
        """;

    /// <summary>
    /// References an identifier that does not exist. Slang reports
    /// <c>error[E30015]: undefined identifier</c> and — critically — signals
    /// the failure by returning a null module rather than by a result code.
    /// </summary>
    public const string SyntaxError = """
        [shader("vertex")]
        float4 vertexMain() : SV_Position
        {
            return notAThing;
        }
        """;

    /// <summary>
    /// Compiles, and produces <c>warning[E41000]: unreachable code detected</c>
    /// while doing so — the fixture for "a non-empty diagnostics blob on a
    /// successful call is a warning set, not an error".
    /// </summary>
    public const string ProducesWarning = """
        [shader("vertex")]
        float4 vertexMain() : SV_Position
        {
            return float4(1.0, 0.0, 0.0, 1.0);
            float unreachable = 2.0;
        }
        """;

    /// <summary>A module with something worth importing: a public type and a public function.</summary>
    public const string CommonModule = """
        public struct Tint
        {
            public float4 rgba;
        };

        public float4 applyTint(Tint tint, float4 color)
        {
            return color * tint.rgba;
        }
        """;

    /// <summary>Imports <c>common</c> by module name — no file system involved.</summary>
    public const string ImportsCommonModule = """
        import common;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            Tint tint;
            tint.rgba = float4(1.0, 1.0, 1.0, 1.0);
            return applyTint(tint, float4(0.5, 0.5, 0.5, 1.0));
        }
        """;
}
