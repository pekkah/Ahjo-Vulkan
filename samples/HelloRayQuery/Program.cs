using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HelloRayQuery;

/// <summary>
/// Issue 207: the whole <c>VK_KHR_acceleration_structure</c> chain, composed
/// once and actually traversed. Builds a two-triangle BLAS, references it from
/// a one-instance TLAS, binds the TLAS through the acceleration-structure
/// descriptor write, ray-queries it from a Slang compute shader, and saves the
/// result as <c>rayquery.png</c>.
/// </summary>
/// <remarks>
/// <para><b>Why a sample and not a test.</b> Every piece below has unit
/// coverage in <c>AccelerationStructureTests</c>; none of it is covered
/// <em>composed</em>, and the shader half cannot be covered at all —
/// <c>VK_KHR_ray_query</c> defines zero entry points, so the wrapper has no
/// ray-query API to test. More to the point, the validation layer cannot tell a
/// correct build from a well-formed garbage one: probing it while closing issue
/// 206 showed it validates <c>primitiveCount</c> but is blind to
/// <c>primitiveOffset</c> (offsets of 1, 4 and 9999 all recorded silently).
/// Only traversal distinguishes them, so the image this writes is the
/// assertion — see <see cref="CheckTraversal"/>, which fails the run rather
/// than leaving a wrong render to the eye.</para>
/// <para><b>Requires an RT-capable device</b>
/// (<c>VK_KHR_acceleration_structure</c> + <c>VK_KHR_ray_query</c> +
/// <c>VK_KHR_deferred_host_operations</c>). CI hosts have none, and software
/// rasterizers are not honest coverage (<c>.github/CLAUDE.md</c>), so this
/// builds in CI but is gated at run time: no device, skip line, exit 0.</para>
/// <para>Exit codes: <c>0</c> rendered or skipped, <c>2</c> no usable shader,
/// <c>3</c> the render disagreed with the geometry that was built.</para>
/// </remarks>
internal static unsafe class Program
{
    private const uint Width  = 512;
    private const uint Height = 512;

    /// <summary>
    /// Two triangles at <c>z = 0</c>, well separated in X so the gap between
    /// them is unambiguously a miss region, and inset from the NDC edges so the
    /// corners are too. <see cref="CheckTraversal"/> samples all three.
    /// </summary>
    internal static void WriteVertices(Span<float> v)
    {
        // Triangle 0 — left.
        v[0]  = -0.80f; v[1]  = -0.50f; v[2]  = 0.0f;
        v[3]  = -0.20f; v[4]  = -0.50f; v[5]  = 0.0f;
        v[6]  = -0.50f; v[7]  =  0.55f; v[8]  = 0.0f;
        // Triangle 1 — right.
        v[9]  =  0.20f; v[10] = -0.50f; v[11] = 0.0f;
        v[12] =  0.80f; v[13] = -0.50f; v[14] = 0.0f;
        v[15] =  0.50f; v[16] =  0.55f; v[17] = 0.0f;
    }

    /// <summary>Bytes <see cref="WriteVertices"/> needs: 6 vertices x 3 floats.</summary>
    internal const int VertexBytes = 6 * 3 * sizeof(float);

    private static int Main(string[] args)
    {
        string outPath = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "rayquery.png");
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "rayquery.slang");

        if (!File.Exists(shaderPath))
        {
            Console.Error.WriteLine($"Missing shader source. Expected:\n  {shaderPath}");
            return 2;
        }

        // Validation stays on permanently: this sample exists to demonstrate a
        // correct barrier and descriptor recipe, so the layer's opinion of it
        // is part of the output, not a debugging aid.
        int layerErrors = 0;
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) == 0)
                    return;
                Interlocked.Increment(ref layerErrors);
                Console.Error.WriteLine("[validation] " + m.Message);
            },
        });

        using Device? device = TryCreateRayQueryDevice(instance, out uint family, out string deviceName);
        if (device is null)
        {
            Console.WriteLine(
                "No device on this host exposes VK_KHR_acceleration_structure + VK_KHR_ray_query + "
                + "VK_KHR_deferred_host_operations with the accelerationStructure / rayQuery / "
                + "bufferDeviceAddress features. Ray tracing is unavailable here (expected on CI "
                + "runners and software rasterizers). Skipping.");
            return 0;
        }

        Console.WriteLine($"Device: {deviceName}");

        using var pipelineParts = new RayQueryPipeline(device, shaderPath);
        if (pipelineParts.Failed)
        {
            return 2;
        }

        using var scene = new RayQueryScene(device, family);
        Console.WriteLine($"BLAS at 0x{scene.BlasAddress:X}, TLAS at 0x{scene.TlasAddress:X}");

        byte[] pixels = Render(device, family, in scene, in pipelineParts);

        if (!CheckTraversal(pixels, out string verdict))
        {
            Console.Error.WriteLine(verdict);
            return 3;
        }

        Console.WriteLine(verdict);
        PngWriter.Write(outPath, pixels, (int)Width, (int)Height);
        Console.WriteLine($"Wrote {outPath}");

        int errors = Volatile.Read(ref layerErrors);
        if (errors != 0)
        {
            Console.Error.WriteLine($"{errors} validation error(s) recorded — see above.");
            return 3;
        }

        Console.WriteLine("Validation: clean.");
        return 0;
    }

    // ---- Device ----

    /// <summary>
    /// The RT device, or <see langword="null"/> when this host has none — the
    /// clean skip signal. Screens on the extensions with
    /// <see cref="PhysicalDeviceInfo.SupportsExtension"/> so a machine whose
    /// first graphics GPU is not RT-capable still finds the one that is.
    /// </summary>
    private static Device? TryCreateRayQueryDevice(
        Instance instance, out uint family, out string deviceName)
    {
        uint chosen = uint.MaxValue;
        string name = "<unknown>";
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                // "…"u8 literals rather than the VulkanExtensions.Khr*
                // constants: those are Utf8Name, and PhysicalDeviceInfo (a ref
                // struct handed to the picker) only screens on
                // ReadOnlySpan<byte>. Same read-only-data-segment bytes either
                // way — invariant #1 holds.
                if (!info.SupportsExtension("VK_KHR_acceleration_structure"u8)) return false;
                if (!info.SupportsExtension("VK_KHR_ray_query"u8)) return false;
                if (!info.SupportsExtension("VK_KHR_deferred_host_operations"u8)) return false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    // Builds require compute
                    // (VUID-vkCmdBuildAccelerationStructuresKHR-commandBuffer-cmdpool);
                    // the dispatch does too.
                    if (info.QueueFamilies[i].SupportsGraphics && info.QueueFamilies[i].SupportsCompute)
                    {
                        chosen = info.QueueFamilies[i].Index;
                        name   = System.Text.Encoding.UTF8.GetString(info.Name);
                        return true;
                    }
                }
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            family     = 0;
            deviceName = "<none>";
            return null;
        }

        family     = chosen;
        deviceName = name;
        Utf8Name[] extensions =
        [
            VulkanExtensions.KhrAccelerationStructure,
            VulkanExtensions.KhrDeferredHostOperations,
            VulkanExtensions.KhrRayQuery,
        ];

        try
        {
            return gpu.CreateDevice(new DeviceDescription
            {
                Queues            = [new QueueRequest(chosen, count: 1, priority: 1.0f)],
                Extensions        = extensions,
                ConfigureFeatures = static (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceFeatures2 _,
                    ref VkPhysicalDeviceVulkan12Features f12,
                    ref VkPhysicalDeviceVulkan13Features __,
                    ref VkPhysicalDeviceVulkan14Features ___) =>
                {
                    // Build inputs, scratch and the TLAS instance reference are
                    // all device addresses.
                    f12.bufferDeviceAddress = 1;
                    ref var asFeatures = ref chain.Push<VkPhysicalDeviceAccelerationStructureFeaturesKHR>();
                    asFeatures.accelerationStructure = 1;
                    ref var rq = ref chain.Push<VkPhysicalDeviceRayQueryFeaturesKHR>();
                    rq.rayQuery = 1;
                },
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_FEATURE_NOT_PRESENT
                                      || ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT)
        {
            return null;
        }
    }

    // ---- Render ----

    private static byte[] Render(
        Device device, uint family, in RayQueryScene scene, in RayQueryPipeline parts)
    {
        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Width, Height = Height, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.Storage | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType     = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect       = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel = 0, LevelCount = 1, BaseArrayLayer = 0, LayerCount = 1,
        });

        const uint Bytes = Width * Height * 4;
        using var readback = device.Allocator.CreateBuffer(
            new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferDst },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Fence fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                scene.RecordBuilds(ref rec);

                // The barrier this sample exists to demonstrate. Everything in
                // the repository before it was build -> build (a compacted-size
                // query, or a later build reading the BLAS); this is the one a
                // consumer actually needs: make the finished TLAS visible to a
                // shader that traverses it.
                //
                // Stage.AccelerationStructureCopy is deliberately NOT used
                // anywhere here — it needs VK_KHR_ray_tracing_maintenance1,
                // which this sample does not enable.
                rec.PipelineBarrier(
                    [
                        new MemoryBarrier
                        {
                            SrcStage  = Stage.AccelerationStructureBuild,
                            SrcAccess = Access.AccelerationStructureWrite,
                            DstStage  = Stage.ComputeShader,
                            DstAccess = Access.AccelerationStructureRead,
                        },
                    ],
                    default, default);

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    srcStage: Stage.TopOfPipe,     srcAccess: Access.None,
                    dstStage: Stage.ComputeShader, dstAccess: Access.ShaderStorageWrite));

                rec.BindPipeline(in parts.Pipeline);

                AccelerationStructure tlas = scene.Tlas;
                var storageImage = new ImageDescriptorWrite(in view, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL);
                rec.PushDescriptorSet(
                    VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                    in parts.Layout, set: 0,
                    [
                        DescriptorWrite.AccelerationStructure(
                            binding: 0, arrayElement: 0, in tlas),
                        DescriptorWrite.Image(
                            binding: 1, arrayElement: 0,
                            VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE,
                            in storageImage),
                    ]);

                // 8x8 threads per group, matching [numthreads] in the shader.
                rec.Dispatch((Width + 7) / 8, (Height + 7) / 8, 1);

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.ComputeShader, srcAccess: Access.ShaderStorageWrite,
                    dstStage: Stage.AllTransfer,   dstAccess: Access.TransferRead));

                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in readback,
                    BufferImageCopy.WholeImage(in image));

                device.GetQueue(family, 0).Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            if (fence.Wait(TimeSpan.FromSeconds(30)) != WaitState.Signaled)
            {
                throw new TimeoutException("The ray-query dispatch did not complete within 30s.");
            }
        }
        finally { fencePool.Release(fence); }

        return readback.AsReadOnlySpan<byte>()[..(int)Bytes].ToArray();
    }

    // ---- The assertion ----

    /// <summary>
    /// The render is the test. Sample one pixel at the centroid of each
    /// triangle and one in a corner that no triangle covers: the first two must
    /// be hits, the third a miss. A wrong vertex stride, a wrong build range, a
    /// mis-set instance transform or a missing barrier all break at least one of
    /// the three — and none of them is something the validation layer reports.
    /// </summary>
    private static bool CheckTraversal(byte[] pixels, out string verdict)
    {
        // Centroids of the two triangles in NDC, and a corner that is outside
        // both by a wide margin.
        (int x, int y) leftHit  = NdcToPixel(-0.50f, -0.15f);
        (int x, int y) rightHit = NdcToPixel(0.50f, -0.15f);
        (int x, int y) miss     = NdcToPixel(-0.95f, 0.90f);

        bool leftIsHit  = IsHit(pixels, leftHit);
        bool rightIsHit = IsHit(pixels, rightHit);
        bool missIsHit  = IsHit(pixels, miss);

        if (leftIsHit && rightIsHit && !missIsHit)
        {
            verdict = "Traversal check: both triangle centroids hit, background missed — "
                    + "the image matches the geometry that was built.";
            return true;
        }

        verdict =
            "Traversal check FAILED — the render does not match the geometry that was built.\n"
            + $"  triangle 0 centroid {Describe(pixels, leftHit)}  expected a hit\n"
            + $"  triangle 1 centroid {Describe(pixels, rightHit)}  expected a hit\n"
            + $"  background          {Describe(pixels, miss)}  expected a miss";
        return false;
    }

    private static (int X, int Y) NdcToPixel(float ndcX, float ndcY)
        => ((int)((ndcX * 0.5f + 0.5f) * Width), (int)((0.5f - ndcY * 0.5f) * Height));

    /// <summary>
    /// A miss writes the shader's constant background; anything else is a hit.
    /// Comparing against the background rather than testing for brightness
    /// keeps this honest for a barycentric shade that happens to be dark.
    /// </summary>
    private static bool IsHit(byte[] pixels, (int X, int Y) at)
    {
        int i = (at.Y * (int)Width + at.X) * 4;
        // kMissColor = (0.05, 0.07, 0.10) in UNORM, with a tolerance for the
        // sRGB-free linear round trip.
        return !(Near(pixels[i], 13) && Near(pixels[i + 1], 18) && Near(pixels[i + 2], 26));

        static bool Near(byte actual, int expected) => Math.Abs(actual - expected) <= 2;
    }

    private static string Describe(byte[] pixels, (int X, int Y) at)
    {
        int i = (at.Y * (int)Width + at.X) * 4;
        return $"({at.X},{at.Y}) = rgba({pixels[i]},{pixels[i + 1]},{pixels[i + 2]},{pixels[i + 3]})";
    }
}
