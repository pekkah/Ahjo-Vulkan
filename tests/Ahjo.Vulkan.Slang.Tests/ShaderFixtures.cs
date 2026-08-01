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

    /// <summary>
    /// Deliberately wasteful, so that "the optimizer ran" is observable as a
    /// smaller module rather than inferred.
    /// </summary>
    /// <remarks>
    /// <para>Three kinds of slack an optimizer is expected to remove: a
    /// fixed-trip loop whose body folds to a constant, a chain of locals
    /// nothing reads, and two arithmetic identities. A trivial passthrough
    /// shader gives the optimizer nothing to do and emits the same words at
    /// every level — which is exactly how a silently absent
    /// <c>spirv-opt</c> passed a "valid SPIR-V at every level" assertion for a
    /// whole phase.</para>
    /// <para>Measured on <c>v2026.14.1</c> / linux-x64 with
    /// <c>slang-glslang</c> present: 317 words at
    /// <see cref="SlangOptimizationLevel.None"/>, 313 at
    /// <see cref="SlangOptimizationLevel.Default"/>, 245 at
    /// <see cref="SlangOptimizationLevel.High"/> and
    /// <see cref="SlangOptimizationLevel.Maximal"/>. With that library
    /// withheld: 317 at all four.</para>
    /// </remarks>
    public const string RedundantVertex = """
        [shader("vertex")]
        float4 vertexMain(uint vertexIndex : SV_VertexID) : SV_Position
        {
            float4 accum = float4(0.0, 0.0, 0.0, 0.0);

            for (int i = 0; i < 8; ++i)
            {
                accum += float4(float(i), float(i) * 2.0, float(i) * 3.0, 1.0);
            }

            float deadA = accum.x * accum.y + accum.z;
            float deadB = deadA * deadA - deadA;
            float deadC = deadB * deadB + deadB * 3.0;

            float4 result = accum * float(vertexIndex);

            result = result + float4(0.0, 0.0, 0.0, 0.0);
            result = result * 1.0;

            return result;
        }
        """;

    /// <summary>
    /// A vertex-shaped function with <b>no</b> <c>[shader("…")]</c> attribute.
    /// </summary>
    /// <remarks>
    /// The legitimate use of
    /// <c>SlangModule.FindEntryPoint(string, ShaderStages)</c>: the requested
    /// stage is what lets Slang find and check a function that declares none,
    /// so <c>DefinedEntryPointCount</c> is 0 and the lookup still succeeds.
    /// </remarks>
    public const string UnattributedVertex = """
        float4 unattributedMain(uint vertexIndex : SV_VertexID) : SV_Position
        {
            return float4(float(vertexIndex), 0.0, 0.0, 1.0);
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

    /// <summary>
    /// Composition fixture, module 1 of 3: shared camera state in a
    /// <c>ParameterBlock</c> plus a helper the other two call.
    /// </summary>
    public const string ComposeCommonModule = """
        public struct CameraData
        {
            public float4x4 viewProjection;
            public float4   position;
        };

        public ParameterBlock<CameraData> gCamera;

        public float4 transformPoint(float3 p)
        {
            return mul(gCamera.viewProjection, float4(p, 1.0));
        }
        """;

    /// <summary>Composition fixture, module 2 of 3: the vertex stage.</summary>
    public const string ComposeGeometryModule = """
        import composeCommon;

        public StructuredBuffer<float3> gPositions;

        [shader("vertex")]
        float4 vertexMain(uint vertexIndex : SV_VertexID) : SV_Position
        {
            return transformPoint(gPositions[vertexIndex]);
        }
        """;

    /// <summary>Composition fixture, module 3 of 3: the fragment stage.</summary>
    public const string ComposeMaterialModule = """
        import composeCommon;

        public struct MaterialParams
        {
            public float4 baseColor;
            public float  roughness;
        };

        public ParameterBlock<MaterialParams> gMaterial;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gMaterial.baseColor * gCamera.position * gMaterial.roughness;
        }
        """;

    /// <summary>
    /// An interface-typed <c>ParameterBlock</c> with two implementations —
    /// the shape that needs a type conformance to generate code, and the shape
    /// <c>IComponentType::specialize</c> crashes on.
    /// </summary>
    public const string InterfaceSurfaceModule = """
        interface ISurface
        {
            float4 shade(float3 normal);
        };

        struct Glossy : ISurface
        {
            float4 tint;
            float4 shade(float3 normal) { return tint * float4(normal, 1.0); }
        };

        struct Matte : ISurface
        {
            float4 albedo;
            float4 shade(float3 normal) { return albedo; }
        };

        ParameterBlock<ISurface> gSurface;

        [shader("fragment")]
        float4 fragmentMain(float3 normal : NORMAL) : SV_Target
        {
            return gSurface.shade(normal);
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
