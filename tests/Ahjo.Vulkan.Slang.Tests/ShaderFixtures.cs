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

    /// <summary>
    /// An <c>interface</c> is declared and implemented, but dispatched
    /// <em>statically</em> — no interface-typed parameter anywhere.
    /// </summary>
    /// <remarks>
    /// The control for <c>SlangProgram.SpecializationParameterCount</c>: this
    /// shape reports <c>0</c>, the same as a fully concrete program, which is
    /// what makes the count worth reporting. A refusal keyed on "this program
    /// declares an interface" would reject this one for nothing.
    /// </remarks>
    public const string StaticDispatchInterfaceModule = """
        interface ISurface
        {
            float4 shade(float3 normal);
        };

        struct Glossy : ISurface
        {
            float4 tint;
            float4 shade(float3 normal) { return tint * float4(normal, 1.0); }
        };

        [shader("fragment")]
        float4 fragmentMain(float3 normal : NORMAL) : SV_Target
        {
            Glossy g;
            g.tint = float4(1.0, 1.0, 1.0, 1.0);
            return g.shade(normal);
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
    /// Three unbounded (bindless) arrays in set 0, plus an ordinary set and a
    /// push-constant block — issue #176's shape.
    /// </summary>
    /// <remarks>
    /// <para>The ordinary set and the push-constant block are the point: the
    /// issue is not that an unbounded array cannot be sized, it is that one of
    /// them used to make the <em>rest</em> of the program unreflectable. Every
    /// array is indexed with <c>gPush.index</c> so nothing folds away — the
    /// fixture-file rule that an unread global survives reflection but not
    /// codegen.</para>
    /// <para>The second set is a <c>ParameterBlock</c> rather than the
    /// <c>[[vk::binding(0, 1)]] ConstantBuffer&lt;Xform&gt;</c> the plan
    /// sketched, because when this fixture was written that shape was
    /// misreported by reflection and the misreport had nothing to do with
    /// bindless arrays: a global-scope <c>ConstantBuffer&lt;T&gt;</c> carrying
    /// an explicit <c>[[vk::binding(n, space)]]</c> with <c>space &gt; 0</c> was
    /// reported at set <b>0</b> while the emitted SPIR-V decorated it
    /// <c>DescriptorSet = space</c>. That was its own defect — issue #180, now
    /// fixed, and <see cref="ReflectionExplicitSpaceConstantBuffer"/> is the
    /// fixture for that shape. <b>This one stays on the
    /// <c>ParameterBlock</c></b>: it is issue #176's fixture, it should test one
    /// thing, and its cross-check row already passes.</para>
    /// </remarks>
    public const string ReflectionBindlessArrays = """
        struct Push  { float4 tint; uint index; };
        struct Xform { float4x4 mvp; };

        Texture2D                gTextures[];
        SamplerState             gSamplers[];
        StructuredBuffer<float4> gBuffers[];

        ParameterBlock<Xform> gXform;

        [[vk::push_constant]] ConstantBuffer<Push> gPush;

        [shader("vertex")]
        float4 vertexMain(float3 position : POSITION) : SV_Position
        {
            return mul(gXform.mvp, float4(position, 1.0)) + gPush.tint;
        }

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gTextures[gPush.index].Sample(gSamplers[gPush.index], uv)
                 * gBuffers[gPush.index][0]
                 * gPush.tint;
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
    /// A compute entry point with a non-default <c>[numthreads]</c>, plus an
    /// annotated and an unannotated storage image.
    /// </summary>
    /// <remarks>
    /// Serves two facts at once: <c>ThreadGroupSize</c> is only observable on a
    /// compute stage, and <c>[[vk::image_format]]</c> is only observable on a
    /// mutable texture. <c>8, 4, 1</c> rather than a square group so a
    /// transposed read is visible.
    /// </remarks>
    public const string ReflectionComputeStorageImage = """
        [[vk::image_format("rgba8")]] RWTexture2D<float4> gAnnotated;
        RWTexture2D<float4>                               gPlain;

        [shader("compute")]
        [numthreads(8, 4, 1)]
        void computeMain(uint3 tid : SV_DispatchThreadID)
        {
            gAnnotated[tid.xy] = gPlain[tid.xy];
        }
        """;

    /// <summary>
    /// Issue #175's own shape: a material block with a nested struct of scalars
    /// and vectors, a matrix, and two resources that are not members.
    /// </summary>
    /// <remarks>
    /// <para>The <c>float4x4</c> is there so the matrix-layout and matrix-stride
    /// members have a subject, and the two resources are there so the
    /// "a field with zero UNIFORM size is not a member" rule has one.</para>
    /// <para><b><c>Tint</c> comes before <c>Params</c> on purpose, and moving it
    /// blinds the suite.</b> SPIR-V decorates a nested struct's members relative
    /// to that struct; <c>SlangBufferMember.Offset</c> is relative to the
    /// buffer. With <c>Params</c> at offset 0 the two conventions coincide, and
    /// <c>BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv</c> passes
    /// whether the walk accumulates offsets or not — verified by mutating
    /// <c>AppendMembers</c> to pass <c>baseOffset</c> instead of <c>offset</c>,
    /// which left 96/96 green. <c>Tint</c> pushes <c>Params</c> to 16 so every
    /// <c>Params.*</c> member's buffer offset differs from its SPIR-V one, and
    /// that same mutation now fails two tests —
    /// <c>BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv</c> and
    /// <c>BufferLayout_MaterialBlock_HasGoldenSizeAndOffsets</c>. This is issue
    /// #175's own failure mode — an assertion that cannot move — reproduced
    /// inside the test written to prevent it.</para>
    /// </remarks>
    public const string ReflectionMaterialBlock = """
        struct MaterialParams
        {
            float3 BaseColor;
            float  Roughness;
            float  Metallic;
            float2 UvScale;
            uint   Flags;
        };

        struct MaterialBlock
        {
            float4            Tint;
            MaterialParams    Params;
            float4x4          Transform;
            Texture2D<float4> BaseColorMap;
            SamplerState      Sampler;
        };

        ParameterBlock<MaterialBlock> gMaterial;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            float2 scaled = uv * gMaterial.Params.UvScale.xy;
            float4 base   = gMaterial.BaseColorMap.Sample(gMaterial.Sampler, scaled);

            float4 tinted = base
                          * gMaterial.Tint
                          * float4(gMaterial.Params.BaseColor, 1.0)
                          * gMaterial.Params.Roughness
                          * gMaterial.Params.Metallic
                          * float(gMaterial.Params.Flags);

            return mul(gMaterial.Transform, tinted);
        }
        """;

    /// <summary>
    /// <see cref="ReflectionMaterialBlock"/>, character-for-character, except
    /// that <c>UvScale</c> is a <c>float4</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>This is the fault-sweep mutation from issue #175, kept as a
    /// fixture.</b> Widening that one member changes the block's size and every
    /// member offset after it, and when the issue was filed <em>no assertion in
    /// a 97-test reflection suite moved</em> — the change was structurally
    /// unobservable through this API.</para>
    /// <para><c>BufferLayout_WideningAMember_ChangesSizeAndSubsequentOffsets</c>
    /// compiles both and asserts the two layouts differ. That test cannot be
    /// made green by editing a constant, which is the point of keeping a second
    /// copy of a shader that is otherwise identical.</para>
    /// </remarks>
    public const string ReflectionMaterialBlockWidened = """
        struct MaterialParams
        {
            float3 BaseColor;
            float  Roughness;
            float  Metallic;
            float4 UvScale;
            uint   Flags;
        };

        struct MaterialBlock
        {
            float4            Tint;
            MaterialParams    Params;
            float4x4          Transform;
            Texture2D<float4> BaseColorMap;
            SamplerState      Sampler;
        };

        ParameterBlock<MaterialBlock> gMaterial;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            float2 scaled = uv * gMaterial.Params.UvScale.xy;
            float4 base   = gMaterial.BaseColorMap.Sample(gMaterial.Sampler, scaled);

            float4 tinted = base
                          * gMaterial.Tint
                          * float4(gMaterial.Params.BaseColor, 1.0)
                          * gMaterial.Params.Roughness
                          * gMaterial.Params.Metallic
                          * float(gMaterial.Params.Flags);

            return mul(gMaterial.Transform, tinted);
        }
        """;

    /// <summary>
    /// Issue #180's own module: a global-scope <c>ConstantBuffer&lt;T&gt;</c>
    /// carrying an explicit <c>[[vk::binding(0, 1)]]</c>, beside a texture and a
    /// sampler in space 0.
    /// </summary>
    /// <remarks>
    /// Measured on <c>v2026.14.1</c> / win-x64, the emitted SPIR-V decorates
    /// <c>gAlbedo (0,0)</c>, <c>gSampler (0,1)</c> and <c>gXform (1,0)</c>.
    /// Before #180 reflection reported <b>two bindings at <c>(0,0)</c></b> — the
    /// texture and the constant buffer — no set 1 at all, and the texture
    /// renamed <c>gXform</c>, because the mis-keyed buffer overwrote it in the
    /// binding-range facts dictionary.
    /// </remarks>
    public const string ReflectionExplicitSpaceConstantBuffer = """
        struct Xform { float4x4 mvp; };

        [[vk::binding(0, 0)]] Texture2D<float4>     gAlbedo;
        [[vk::binding(1, 0)]] SamplerState          gSampler;
        [[vk::binding(0, 1)]] ConstantBuffer<Xform> gXform;

        [shader("vertex")]
        float4 vertexMain(float3 position : POSITION) : SV_Position
        {
            return mul(gXform.mvp, float4(position, 1.0));
        }

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gAlbedo.Sample(gSampler, uv) * gXform.mvp[0];
        }
        """;

    /// <summary>
    /// The same defect where a descriptor-set record for space 1
    /// <b>already exists</b>: a texture is declared at
    /// <c>[[vk::binding(0, 1)]]</c> and the constant buffer at
    /// <c>[[vk::binding(1, 1)]]</c> is still emitted into space 0's record.
    /// </summary>
    /// <remarks>
    /// This is what proves the defect is not "Slang forgot to make a set
    /// record". Measured before #180: <c>(0,1)</c> was reported twice — once as
    /// the sampler and once as the constant buffer, both named
    /// <c>gSampler</c> — while <c>gOther</c> at <c>(1,0)</c> was correct.
    /// </remarks>
    public const string ReflectionExplicitSpaceMixed = """
        struct Xform { float4x4 mvp; };

        [[vk::binding(0, 0)]] Texture2D<float4>     gAlbedo;
        [[vk::binding(1, 0)]] SamplerState          gSampler;
        [[vk::binding(0, 1)]] Texture2D<float4>     gOther;
        [[vk::binding(1, 1)]] ConstantBuffer<Xform> gXform;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gAlbedo.Sample(gSampler, uv)
                 * gOther.Sample(gSampler, uv)
                 * gXform.mvp[0];
        }
        """;

    /// <summary>
    /// Two constant buffers in two distinct non-zero spaces — the shape where
    /// the defect loses a whole binding rather than merely misplacing it.
    /// </summary>
    /// <remarks>
    /// Before #180 both folded to <c>(0,0)</c> and reflection reported the
    /// <em>same</em> binding twice, both named <c>gB</c>: <c>gA</c> was
    /// unrecoverable through the API entirely, and so was its buffer layout.
    /// The two members are named differently on purpose so
    /// <c>TryGetBufferLayout</c> can tell which buffer it returned.
    /// </remarks>
    public const string ReflectionExplicitSpaceTwoConstantBuffers = """
        struct A { float4 a; };
        struct B { float4 b; };

        [[vk::binding(0, 1)]] ConstantBuffer<A> gA;
        [[vk::binding(0, 2)]] ConstantBuffer<B> gB;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gA.a + gB.b;
        }
        """;

    /// <summary>
    /// The same defect one level deeper: a <c>ConstantBuffer&lt;T&gt;</c> inside
    /// a plain struct global — not a <c>ParameterBlock</c> — placed at
    /// <c>[[vk::binding(0, 1)]]</c>.
    /// </summary>
    /// <remarks>
    /// <para>SPIR-V decorates <c>gAlbedo (0,0)</c>, <c>gBundle.tex (1,0)</c> and
    /// <c>gBundle.cb (1,1)</c>; before #180 the constant buffer keyed to
    /// <c>(0,1)</c>.</para>
    /// <para><b>This is the fixture that pins the span rule</b> of
    /// <c>CollectSpaceCorrections</c>: <c>gBundle</c> is one field owning
    /// <em>two</em> binding ranges, so a correction that only ever looked at
    /// <c>getFieldBindingRangeOffset(f)</c> itself would repair <c>tex</c> and
    /// leave <c>cb</c> behind.</para>
    /// </remarks>
    public const string ReflectionExplicitSpaceStructGlobal = """
        struct Xform { float4x4 mvp; };

        struct Bundle
        {
            Texture2D<float4>     tex;
            ConstantBuffer<Xform> cb;
        };

        [[vk::binding(0, 1)]] Bundle            gBundle;
        [[vk::binding(0, 0)]] Texture2D<float4> gAlbedo;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gBundle.tex.Load(int3(0, 0, 0)) * gBundle.cb.mvp[0] * gAlbedo.Load(int3(0, 0, 0));
        }
        """;

    /// <summary>
    /// Issue #180's second shape: <b>loose global uniform data</b> beside an
    /// explicitly-bound texture and constant buffer.
    /// </summary>
    /// <remarks>
    /// <para><c>float4 gTint;</c> is the whole trigger. With it,
    /// <c>spReflection_getGlobalParamsTypeLayout</c> returns a
    /// <c>SLANG_TYPE_KIND_CONSTANT_BUFFER</c> wrapper instead of a struct, whose
    /// descriptor-set records list the element's ranges plus an implicit buffer
    /// at a bogus index offset. Deleting that one line — or moving it into an
    /// explicit <c>ConstantBuffer&lt;Tint&gt;</c> — returns the scope to
    /// <c>SLANG_TYPE_KIND_STRUCT</c>, which is why
    /// <see cref="ReflectionExplicitSpaceConstantBuffer"/> was unaffected.</para>
    /// <para>Measured on <c>v2026.14.1</c> / win-x64, the emitted SPIR-V
    /// decorates <c>gAlbedo (0,0)</c>, <c>globalParams (0,1)</c> and
    /// <c>gXform (1,0)</c>. Before the unwrap, reflection reported <b>three
    /// bindings all at slot 0 of set 0</b>, all with an empty <c>Name</c>, and no
    /// set 1 at all.</para>
    /// </remarks>
    public const string ReflectionLooseGlobalsWithExplicitSpace = """
        struct Xform { float4x4 mvp; };

        float4 gTint;

        [[vk::binding(0, 0)]] Texture2D<float4>     gAlbedo;
        [[vk::binding(0, 1)]] ConstantBuffer<Xform> gXform;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gAlbedo.Load(int3(0, 0, 0)) * gXform.mvp[0] * gTint;
        }
        """;

    /// <summary>
    /// The degenerate control: the constant-buffer wrapper with nothing else in
    /// it.
    /// </summary>
    /// <remarks>
    /// SPIR-V puts <c>globalParams</c> at <c>(0,0)</c>, which is where reflection
    /// reported it <em>by accident</em> before the unwrap — the wrapper's own
    /// index offset is 0 in every shape, and here 0 happens to be right. So this
    /// fixture's assertion is carried by the <b>buffer layout</b>: two members,
    /// <c>gTint</c> at offset 0 and <c>gScale</c> at 16. That asymmetry is
    /// deliberate — it is what proves the sibling tests assert the slot Slang
    /// reports rather than a constant.
    /// </remarks>
    public const string ReflectionLooseGlobalsOnly = """
        float4 gTint;
        float  gScale;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gTint * gScale;
        }
        """;

    /// <summary>
    /// Proof that the wrapper defect has nothing to do with
    /// <c>[[vk::binding]]</c>: <b>no explicit space anywhere</b>.
    /// </summary>
    /// <remarks>
    /// <para>Measured on <c>v2026.14.1</c> / win-x64:
    /// <c>globalParams (0,0)</c>, <c>gAlbedo (0,1)</c>, <c>gSampler (0,2)</c>.
    /// <b>The implicit buffer takes slot 0 and pushes the resources up</b>
    /// whenever nothing is explicitly bound there — the opposite of
    /// <see cref="ReflectionLooseGlobalsWithExplicitSpace"/>, where
    /// <c>[[vk::binding(0, 0)]]</c> pins the texture to 0 and the implicit buffer
    /// lands at 1. Verified independent of declaration order: moving
    /// <c>float4 gTint;</c> below the two resources changes nothing.</para>
    /// <para>So this fixture's discriminating assertion is <b>not</b> the
    /// implicit buffer's slot — before the unwrap, reflection reported it at
    /// <c>(0,0)</c>, which is where it belongs. What was broken here is
    /// everything else: the wrapper's records gave three bindings all at slot 0,
    /// all unnamed, so the texture and the sampler collided on top of it. That is
    /// what makes this the fixture proving the wrapper defect is independent of
    /// <c>[[vk::binding]]</c> — any module with loose global uniform data and at
    /// least one resource was affected, not just issue #180's original
    /// explicit-space shape.</para>
    /// </remarks>
    public const string ReflectionLooseGlobalsNoExplicitBinding = """
        float4 gTint;

        Texture2D    gAlbedo;
        SamplerState gSampler;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gAlbedo.Sample(gSampler, uv) * gTint;
        }
        """;

    /// <summary>
    /// The combined shape: loose global uniform data <b>and</b> a
    /// <c>ParameterBlock</c>.
    /// </summary>
    /// <remarks>
    /// <para>The one combination worth a fixture of its own, because it is where
    /// the two mechanisms could interact: unwrapping changes what the top-level
    /// <c>Walk</c> is handed, while a block's set number comes from an offset
    /// accumulated <em>separately</em>, on the sub-object range's variable layout.
    /// A wrapper that shifted the element's own indices — the way a block's
    /// implicit buffer shifts its listed ranges — would show up here as the block
    /// landing in the wrong set.</para>
    /// <para>Measured on <c>v2026.14.1</c> / win-x64, the emitted SPIR-V
    /// decorates <c>globalParams (0,0)</c>, <c>gAlbedo (0,1)</c> and the block's
    /// implicit uniform buffer <c>gXform (1,0)</c>. <b>Two implicit buffers, in
    /// two sets, placed by two different rules</b> — the global one at the slot
    /// <c>getGlobalParamsVarLayout</c> reports (here 0, because nothing is
    /// explicitly bound there), the block's at slot 0 of its own space by
    /// construction — and neither displaces the other. The block still lands in
    /// set 1: unwrapping the global scope does not disturb the sub-object range
    /// offset the block's set number accumulates from.</para>
    /// </remarks>
    public const string ReflectionLooseGlobalsWithParameterBlock = """
        struct Xform { float4x4 mvp; };

        float4 gTint;

        Texture2D<float4> gAlbedo;

        ParameterBlock<Xform> gXform;

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return gAlbedo.Load(int3(0, 0, 0)) * gXform.mvp[0] * gTint;
        }
        """;

    /// <summary>
    /// Issue #183's shape: a zero-length resource array, with two ordinary
    /// resources behind it so the slot it reserves is observable.
    /// </summary>
    /// <remarks>
    /// <para>Measured on <c>v2026.14.1</c> / win-x64: reflection reports
    /// <c>(0,0) gTex Fixed(0)</c>, <c>(0,1) gSampler Fixed(1)</c> and
    /// <c>(0,2) gReal Fixed(1)</c>, while the emitted SPIR-V decorates only
    /// <c>(0,1)</c> and <c>(0,2)</c> — Slang emits no variable for the empty
    /// array.</para>
    /// <para><b>Deleting the array entirely moves <c>gSampler</c> to 0 and
    /// <c>gReal</c> to 1.</b> That is what makes <c>gTex[0]</c> a reserved slot
    /// rather than a non-event, and it is why the two other resources are in the
    /// fixture at all: without them nothing would distinguish "the binding
    /// number was consumed" from "the declaration vanished".</para>
    /// </remarks>
    public const string ReflectionZeroLengthArray = """
        Texture2D    gTex[0];
        SamplerState gSampler;
        Texture2D    gReal;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gReal.Sample(gSampler, uv);
        }
        """;

    /// <summary>
    /// <see cref="ReflectionZeroLengthArray"/>, character-for-character, except
    /// that the length comes from a constant.
    /// </summary>
    /// <remarks>
    /// <para>This is <c>Texture2D gMaps[NUM_MAPS];</c> with <c>NUM_MAPS = 0</c> —
    /// generated or parameterized shader code, not a typo. Measured on
    /// <c>v2026.14.1</c> / win-x64, reflection reports the identical three
    /// bindings, so the shape is reachable without anyone typing <c>[0]</c>.
    /// That is what makes issue #183 worth a fix rather than a note.</para>
    /// <para>Kept as a near-duplicate on purpose, for the same reason
    /// <see cref="ReflectionMaterialBlockWidened"/> is: the claim is about two
    /// spellings producing the same reflection, and only two shaders can carry
    /// it.</para>
    /// </remarks>
    public const string ReflectionZeroLengthArrayFromConstant = """
        static const int N = 0;
        Texture2D        gTex[N];
        SamplerState     gSampler;
        Texture2D        gReal;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gReal.Sample(gSampler, uv);
        }
        """;

    /// <summary>
    /// The degenerate set: a zero-length array and nothing else.
    /// </summary>
    /// <remarks>
    /// Measured on <c>v2026.14.1</c> / win-x64: reflection reports one set with
    /// one binding, <c>(0,0) gTex Fixed(0)</c>, and the emitted SPIR-V decorates
    /// <b>nothing</b>. Vulkan's layout for that set has zero bindings, and
    /// <c>Device.CreateDescriptorSetLayout</c> makes one from an empty
    /// <c>Bindings</c> span (<c>src/Ahjo.Vulkan/Lifecycle/Device.cs</c>, issue
    /// #191) — so <c>MapBindings</c> returns an empty array here rather than
    /// refusing. Deliberately <b>not</b> a row in
    /// <c>Reflection_CoversEverySetAndBinding_TheSpirvDecorates</c>: with no
    /// decoration to iterate, that theory would assert nothing at all.
    /// </remarks>
    public const string ReflectionZeroLengthArrayOnly = """
        Texture2D gTex[0];

        [shader("fragment")]
        float4 fragmentMain() : SV_Target
        {
            return float4(1.0, 0.0, 0.0, 1.0);
        }
        """;

    /// <summary>
    /// One dead set beside a live one: the zero-length array takes a space of
    /// its own.
    /// </summary>
    /// <remarks>
    /// Measured on <c>v2026.14.1</c> / win-x64: reflection reports
    /// <c>(0,0) gReal</c>, <c>(0,1) gSampler</c> and <c>(1,0) gTex Fixed(0)</c>,
    /// and the emitted SPIR-V decorates <c>(0,0)</c> and <c>(0,1)</c>. So one
    /// dead array does not make the program unmappable — only the set that
    /// consists entirely of dead arrays, which is the #176 consistency claim.
    /// The explicit space also shows #180's <c>CollectSpaceCorrections</c> is
    /// unaffected by a zero count: the binding is keyed to set 1, its declared
    /// space.
    /// </remarks>
    public const string ReflectionZeroLengthArrayInOwnSet = """
        [[vk::binding(0, 1)]] Texture2D gTex[0];

        Texture2D    gReal;
        SamplerState gSampler;

        [shader("fragment")]
        float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
        {
            return gReal.Sample(gSampler, uv);
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
