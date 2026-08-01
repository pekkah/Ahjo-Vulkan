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

    /// <summary>
    /// The plain global-scope shape: a constant buffer, a texture, a sampler, a
    /// mutable structured buffer and a push-constant block, plus a vertex stage
    /// with two real varying inputs.
    /// </summary>
    /// <remarks>
    /// Every parameter is <em>read</em> by one of the two entry points on
    /// purpose. An unread global survives reflection but not code generation,
    /// and a test that asserts against the emitted SPIR-V would then be
    /// asserting against a module the optimizer emptied.
    /// </remarks>
    public const string ReflectionGlobals = """
        struct Xform { float4x4 mvp; };
        struct Push  { float4 tint; };

        ConstantBuffer<Xform>      gXform;
        Texture2D                  gAlbedo;
        SamplerState               gSampler;
        RWStructuredBuffer<float4> gOut;

        [[vk::push_constant]] ConstantBuffer<Push> gPush;

        struct VSIn  { float3 position : POSITION; float2 uv : TEXCOORD0; };
        struct VSOut { float4 position : SV_Position; float2 uv : TEXCOORD0; };

        [shader("vertex")]
        VSOut vertexMain(VSIn input)
        {
            VSOut output;
            output.position = mul(gXform.mvp, float4(input.position, 1.0));
            output.uv       = input.uv;
            return output;
        }

        [shader("fragment")]
        float4 fragmentMain(VSOut input) : SV_Target
        {
            gOut[0] = gPush.tint;
            return gAlbedo.Sample(gSampler, input.uv) * gPush.tint;
        }
        """;

    /// <summary>A four-element texture array — the <c>Count &gt; 1</c> case.</summary>
    public const string ReflectionTextureArray = """
        Texture2D    gMaps[4];
        SamplerState gSampler;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gMaps[0].Sample(gSampler, uv)
                 + gMaps[1].Sample(gSampler, uv)
                 + gMaps[2].Sample(gSampler, uv)
                 + gMaps[3].Sample(gSampler, uv);
        }
        """;

    /// <summary>
    /// A populated global scope plus two <c>ParameterBlock</c>s — the
    /// multi-descriptor-set shape a material system produces.
    /// </summary>
    public const string ReflectionTwoBlocks = """
        struct Xform { float4x4 mvp; };
        struct MatA  { float4 baseColor; };
        struct MatB  { float4 emissive;  };

        ConstantBuffer<Xform> gXform;
        Texture2D             gAlbedo;
        SamplerState          gSampler;

        ParameterBlock<MatA> gA;
        ParameterBlock<MatB> gB;

        [shader("vertex")]
        float4 vertexMain(float3 position : POSITION) : SV_Position
        {
            return mul(gXform.mvp, float4(position, 1.0));
        }

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gAlbedo.Sample(gSampler, uv) * gA.baseColor + gB.emissive;
        }
        """;

    /// <summary>
    /// Two blocks, one whose element carries ordinary data and one whose
    /// element is all resources.
    /// </summary>
    /// <remarks>
    /// The asymmetry this pins: Slang gives the first an implicit uniform
    /// buffer at binding 0 of its space and shifts its listed ranges up by one,
    /// while reporting no descriptor range for that buffer at all. The second
    /// gets no such buffer and starts at binding 0 with its first resource.
    /// </remarks>
    public const string ReflectionBlockOrdinaryData = """
        struct WithData
        {
            Texture2D    maps[4];
            SamplerState samp;
            float4       factors;
            float        roughness;
        };

        struct WithoutData
        {
            StructuredBuffer<float4> buf;
            Texture2D                tex;
            SamplerState             samp;
        };

        ParameterBlock<WithData>    gWith;
        ParameterBlock<WithoutData> gWithout;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gWith.maps[0].Sample(gWith.samp, uv) * gWith.factors * gWith.roughness
                 + gWithout.tex.Sample(gWithout.samp, uv) * gWithout.buf[0];
        }
        """;

    /// <summary>
    /// A <c>ParameterBlock</c> containing another <c>ParameterBlock</c>, behind
    /// two sibling blocks so the nesting has a non-zero base to accumulate onto.
    /// </summary>
    public const string ReflectionNestedBlock = """
        struct Xform { float4x4 mvp; };
        struct MatA  { float4 baseColor; };
        struct MatB  { float4 emissive;  };

        struct Inner
        {
            Texture2D    t;
            SamplerState s;
            float4       k;
        };

        struct Outer
        {
            ParameterBlock<Inner> inner;
            Texture2D             o;
            SamplerState          os;
            float4                tint;
        };

        ConstantBuffer<Xform> gXform;
        Texture2D             gAlbedo;
        SamplerState          gSampler;

        ParameterBlock<MatA>  gA;
        ParameterBlock<MatB>  gB;
        ParameterBlock<Outer> gNested;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gAlbedo.Sample(gSampler, uv)
                 * gA.baseColor + gB.emissive
                 + gNested.o.Sample(gNested.os, uv) * gNested.tint
                 + gNested.inner.t.Sample(gNested.inner.s, uv) * gNested.inner.k;
        }
        """;

    /// <summary>
    /// A global scope that declares <em>only</em> <c>ParameterBlock</c>s — the
    /// natural shape once a material system has put everything in a block.
    /// </summary>
    /// <remarks>
    /// The first block lands in set <b>0</b>, not set 1: "the global scope owns
    /// space 0 and blocks start at 1" is false whenever the global scope has no
    /// descriptors of its own.
    /// </remarks>
    public const string ReflectionOnlyBlocks = """
        struct MatA { float4 baseColor; };
        struct MatB { float4 emissive;  };

        ParameterBlock<MatA> gA;
        ParameterBlock<MatB> gB;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gA.baseColor + gB.emissive;
        }
        """;

    /// <summary>
    /// Explicit <c>[[vk::binding]]</c> placing two resources in spaces 0 and 2,
    /// leaving space 1 empty.
    /// </summary>
    public const string ReflectionSparseSets = """
        [[vk::binding(3, 0)]] Texture2D    gTex;
        [[vk::binding(7, 2)]] SamplerState gSamp;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gTex.Sample(gSamp, uv);
        }
        """;

    /// <summary>
    /// A vertex entry point taking a struct of real varying inputs alongside
    /// two system values.
    /// </summary>
    /// <remarks>
    /// <c>SV_InstanceID</c> and <c>SV_VertexID</c> report parameter category
    /// <c>NONE</c> and offset 0. Emitting them as attributes would put a
    /// phantom attribute at location 0, on top of the real <c>POSITION</c>.
    /// </remarks>
    public const string ReflectionSystemValueInputs = """
        struct VSIn
        {
            float3 pos     : POSITION;
            float2 uv      : TEXCOORD0;
            float4 tangent : TANGENT;
        };

        [shader("vertex")]
        float4 vertexMain(VSIn vin, uint iid : SV_InstanceID, uint vid : SV_VertexID) : SV_Position
        {
            return float4(vin.pos, vin.uv.x) + vin.tangent * float(iid + vid);
        }
        """;

    /// <summary>A matrix-typed vertex input — OPEN-6's guard.</summary>
    public const string ReflectionMatrixVertexInput = """
        struct VSIn
        {
            float3   pos      : POSITION;
            float4x4 instance : INSTANCEXF;
        };

        [shader("vertex")]
        float4 vertexMain(VSIn vin) : SV_Position
        {
            return mul(vin.instance, float4(vin.pos, 1.0));
        }
        """;

    /// <summary>
    /// Two modules each declaring a push-constant block — OPEN-5's guard.
    /// Module 1 of 2.
    /// </summary>
    public const string ReflectionPushConstantA = """
        public struct PushA { public float4 tint; };

        [[vk::push_constant]] public ConstantBuffer<PushA> gPushA;

        [shader("vertex")]
        float4 vertexMain(float3 position : POSITION) : SV_Position
        {
            return float4(position, 1.0) + gPushA.tint;
        }
        """;

    /// <summary>Two push-constant blocks — module 2 of 2.</summary>
    public const string ReflectionPushConstantB = """
        public struct PushB { public float2 scale; };

        [[vk::push_constant]] public ConstantBuffer<PushB> gPushB;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return float4(gPushB.scale, 0.0, 1.0);
        }
        """;
}
