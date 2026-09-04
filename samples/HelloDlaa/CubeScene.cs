using System.Numerics;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Samples.HelloDlaa;

/// <summary>
/// One vertex of the cube. Four per face, so UVs and normals are per-face and
/// the faces stay flat-shaded and independently textured.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CubeVertex
{
    public Vector3 Position;
    public Vector2 Uv;
    public Vector3 Normal;

    public const uint PositionOffset = 0;
    public const uint UvOffset       = 12;
    public const uint NormalOffset   = 20;

    public CubeVertex(Vector3 position, Vector2 uv, Vector3 normal)
    {
        Position = position;
        Uv       = uv;
        Normal   = normal;
    }
}

/// <summary>
/// Everything that does not depend on the render extent — the cube geometry,
/// the procedural mip-mapped texture and the per-slot uniform ring — plus the
/// one thing that does: the mip-biased sampler.
/// </summary>
internal sealed unsafe class CubeScene : IDisposable
{
    /// <summary>
    /// The per-frame uniform block. Field order and types match
    /// <c>FrameUniforms</c> in <c>cube.slang</c>, which declares every matrix
    /// <c>row_major</c> so these raw <see cref="Matrix4x4"/> bytes land as rows
    /// and <c>mul(v, m)</c> is the correct multiply order.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FrameUniforms
    {
        public Matrix4x4 JitteredMvp;
        public Matrix4x4 CurrentMvp;
        public Matrix4x4 PreviousMvp;
        public Matrix4x4 Model;
        public Vector2   RenderExtent;
        public Vector2   Pad;
    }

    private const uint TextureSize = 512;
    private const uint TextureMips = 10;   // log2(512) + 1

    private readonly Device   _device;
    private readonly Buffer   _vertexBuffer;
    private readonly Buffer   _indexBuffer;
    private readonly Image    _texture;
    private readonly ImageView _textureView;
    private readonly Buffer[] _uniforms;
    private Sampler          _sampler;

    public ref readonly Buffer    VertexBuffer => ref _vertexBuffer;
    public ref readonly Buffer    IndexBuffer  => ref _indexBuffer;
    public ref readonly ImageView TextureView  => ref _textureView;

    public uint    IndexCount { get; }
    public Sampler Sampler    => _sampler;

    /// <summary>The bias currently baked into <see cref="Sampler"/>.</summary>
    public float MipLodBias { get; private set; }

    public CubeScene(Device device, uint queueFamily, uint framesInFlight)
    {
        _device = device;
        Allocator allocator = device.Allocator;

        CubeVertex[] vertices = BuildVertices();
        ushort[]     indices  = BuildIndices();
        IndexCount = (uint)indices.Length;

        _vertexBuffer = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)(vertices.Length * sizeof(CubeVertex)),
                Usage = BufferUsage.VertexBuffer | BufferUsage.TransferDst,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        _indexBuffer = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)(indices.Length * sizeof(ushort)),
                Usage = BufferUsage.IndexBuffer | BufferUsage.TransferDst,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        var queue = device.GetQueue(queueFamily, 0);
        using (var pool  = new CommandBufferPool(device, queueFamily))
        using (var batch = new StagingBatch(allocator))
        {
            batch.EnqueueUpload<CubeVertex>(vertices, in _vertexBuffer);
            batch.EnqueueUpload<ushort>(indices, in _indexBuffer);
            batch.Flush(queue, pool);
        }

        // ---- The texture. High-frequency ON PURPOSE (spec D6): it is what
        // makes DLAA's reconstruction visible, what makes a jitter or
        // motion-vector sign error show up as shimmer instead of hiding, and
        // what makes the mip-bias question answerable by looking.
        _texture = allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = TextureSize, Height = TextureSize, Depth = 1,
                MipLevels     = TextureMips, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                // TransferSrc is required by GenerateMips: it blits mip i-1
                // into mip i, so the image is both a source and a destination.
                Usage         = ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        _textureView = _texture.CreateView(device, new ImageViewDescription
        {
            ViewType       = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = TextureMips,
            BaseArrayLayer = 0, LayerCount = 1,
        });

        UploadTexture(device, queueFamily, in _texture, BuildTexture());

        _sampler = CreateSampler(0f);

        // ---- The per-slot uniform ring. Every buffer the CPU writes during a
        // frame and the GPU reads during the same frame needs FramesInFlight
        // copies — FrameRing.BeginFrame has already waited on slot K's fence
        // before handing it back, which is what makes writing slot K safe.
        _uniforms = new Buffer[framesInFlight];
        for (uint i = 0; i < framesInFlight; i++)
        {
            _uniforms[i] = allocator.CreateBuffer(
                new BufferDescription
                {
                    Size  = (ulong)sizeof(FrameUniforms),
                    Usage = BufferUsage.UniformBuffer,
                },
                new AllocationDescription
                {
                    Usage = MemoryUsage.AutoPreferHost,
                    Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
                });
        }
    }

    public Buffer Uniforms(uint slot) => _uniforms[slot];

    /// <summary>Writes slot <paramref name="slot"/>'s uniform block through
    /// the persistent map. No allocation, no map/unmap.</summary>
    public void WriteUniforms(uint slot, in FrameUniforms values)
    {
        Buffer buffer = _uniforms[slot];
        buffer.AsSpan<FrameUniforms>()[0] = values;
        buffer.Flush();
    }

    /// <summary>
    /// Rebuilds the sampler at a new mip bias. Setup-time and resize-time only
    /// — never per frame; <c>vkCreateSampler</c> is not a hot-path call.
    /// </summary>
    public void SetMipLodBias(float bias)
    {
        if (!_sampler.IsNull && bias == MipLodBias) return;

        Sampler replacement = CreateSampler(bias);
        _sampler.Dispose();
        _sampler = replacement;
    }

    private Sampler CreateSampler(float bias)
    {
        MipLodBias = bias;
        return _device.CreateSampler(new SamplerDescription
        {
            MagFilter    = VkFilter.VK_FILTER_LINEAR,
            MinFilter    = VkFilter.VK_FILTER_LINEAR,
            MipmapMode   = VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_LINEAR,
            AddressModeU = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeV = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeW = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            // Guide §3.5's bias. Anisotropy stays off (MaxAnisotropy = 1) so
            // the bias is the only thing moving the sampled mip level and the
            // observation the verification step records is unambiguous.
            MipLodBias    = bias,
            MaxAnisotropy = 1f,
            MinLod        = 0f,
            MaxLod        = TextureMips,
            BorderColor   = VkBorderColor.VK_BORDER_COLOR_FLOAT_OPAQUE_BLACK,
        });
    }

    private static void UploadTexture(Device device, uint queueFamily, in Image image, byte[] pixels)
    {
        using var staging = device.Allocator.CreateBuffer(
            new BufferDescription { Size = (ulong)pixels.Length, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });
        pixels.AsSpan().CopyTo(staging.AsSpan<byte>());
        staging.Flush();

        using var pool = new CommandBufferPool(device, queueFamily);
        var queue = device.GetQueue(queueFamily, 0);

        Image local = image;
        Buffer source = staging;
        queue.ImmediateSubmit(pool, (ref CommandRecorder rec) =>
        {
            // UNDEFINED → TRANSFER_DST on mip 0 only; GenerateMips rotates the
            // rest of the chain itself.
            rec.PipelineBarrier(new ImageBarrier
            {
                Image          = (nint)local.Handle,
                SrcStage       = Stage.TopOfPipe, SrcAccess = Access.None,
                DstStage       = Stage.Copy,      DstAccess = Access.TransferWrite,
                OldLayout      = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                NewLayout      = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                BaseMipLevel   = 0, LevelCount = 1,
                BaseArrayLayer = 0, LayerCount = 1,
            });

            rec.CopyBufferToImage(in source, in local,
                VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                BufferImageCopy.WholeImage(in local));

            rec.GenerateMips(in local,
                finalLayout: VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
        });
    }

    /// <summary>
    /// A 512x512 RGBA8 pattern: a 4-texel checker over the left half, a
    /// 32-texel checker over the right half, and single-texel grid lines every
    /// 64 texels in a contrasting colour.
    /// </summary>
    /// <remarks>
    /// The values are written <b>already sRGB-encoded</b>. The shader's encode
    /// (see <c>cube.slang</c>) applies to the shaded result, not to the texture
    /// fetch, and the view is UNORM, so there is exactly one encode in the
    /// chain. Picking the constants in encoded space is also what keeps the
    /// contrast high, which is what the aliasing observation needs.
    /// </remarks>
    private static byte[] BuildTexture()
    {
        const int size = (int)TextureSize;
        var pixels = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cell = x < size / 2 ? 4 : 32;
                bool light = (((x / cell) + (y / cell)) & 1) == 0;

                byte r, g, b;
                if (x % 64 == 0 || y % 64 == 0)
                {
                    // Grid line — a saturated orange, unmistakable against both
                    // checker tones and a good witness for chroma shimmer.
                    r = 255; g = 140; b = 0;
                }
                else if (light)
                {
                    r = 236; g = 236; b = 240;
                }
                else
                {
                    r = 24; g = 24; b = 30;
                }

                int i = (y * size + x) * 4;
                pixels[i + 0] = r;
                pixels[i + 1] = g;
                pixels[i + 2] = b;
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// 24 vertices, four per face. Every face is wound <b>counter-clockwise
    /// when viewed from outside</b> — the conventional right-hand-rule
    /// ordering, so the emitted normal and the winding agree. The pipeline
    /// pairs that with <c>VK_FRONT_FACE_CLOCKWISE</c>, because
    /// <c>proj.M22 *= -1f</c> flips Y between NDC and the framebuffer and
    /// therefore reverses the apparent winding exactly once.
    /// </summary>
    private static CubeVertex[] BuildVertices()
    {
        var vertices = new CubeVertex[24];
        int at = 0;

        // u x v == n for every face, so (p0,p1,p2)/(p0,p2,p3) is CCW-from-outside.
        AddFace(vertices, ref at, new Vector3( 1,  0,  0), new Vector3( 0, 0, -1), new Vector3(0, 1, 0));  // +X
        AddFace(vertices, ref at, new Vector3(-1,  0,  0), new Vector3( 0, 0,  1), new Vector3(0, 1, 0));  // -X
        AddFace(vertices, ref at, new Vector3( 0,  1,  0), new Vector3( 1, 0,  0), new Vector3(0, 0, -1)); // +Y
        AddFace(vertices, ref at, new Vector3( 0, -1,  0), new Vector3( 1, 0,  0), new Vector3(0, 0,  1)); // -Y
        AddFace(vertices, ref at, new Vector3( 0,  0,  1), new Vector3( 1, 0,  0), new Vector3(0, 1, 0));  // +Z
        AddFace(vertices, ref at, new Vector3( 0,  0, -1), new Vector3(-1, 0,  0), new Vector3(0, 1, 0));  // -Z

        return vertices;

        static void AddFace(CubeVertex[] target, ref int at, Vector3 normal, Vector3 u, Vector3 v)
        {
            Vector3 centre = normal;
            target[at++] = new CubeVertex(centre - u - v, new Vector2(0, 1), normal);
            target[at++] = new CubeVertex(centre + u - v, new Vector2(1, 1), normal);
            target[at++] = new CubeVertex(centre + u + v, new Vector2(1, 0), normal);
            target[at++] = new CubeVertex(centre - u + v, new Vector2(0, 0), normal);
        }
    }

    private static ushort[] BuildIndices()
    {
        var indices = new ushort[36];
        for (int face = 0; face < 6; face++)
        {
            int b = face * 4;
            int o = face * 6;
            indices[o + 0] = (ushort)(b + 0);
            indices[o + 1] = (ushort)(b + 1);
            indices[o + 2] = (ushort)(b + 2);
            indices[o + 3] = (ushort)(b + 0);
            indices[o + 4] = (ushort)(b + 2);
            indices[o + 5] = (ushort)(b + 3);
        }
        return indices;
    }

    public void Dispose()
    {
        for (int i = 0; i < _uniforms.Length; i++) _uniforms[i].Dispose();
        _sampler.Dispose();
        _textureView.Dispose();
        _texture.Dispose();
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
    }
}
