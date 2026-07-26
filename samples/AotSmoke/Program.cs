using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.AotSmoke;

/// <summary>
/// Issue 28 — Native AOT validation smoke. Mirrors
/// <c>HeadlessTriangle</c> (allocator + buffer + image + pipeline +
/// cmd recorder + fence wait + PNG dump) so any reflection /
/// dynamic-codegen creep in the wrapper surfaces at <c>dotnet publish</c>
/// time as a trim warning or ILC error.
/// </summary>
/// <remarks>
/// Diverges from HeadlessTriangle only in shape: hard-coded output
/// path, no argv parsing (AOT trimming on a 1-line argv hookup adds no
/// signal). The single drawn frame is enough to exercise every wrapper
/// path the package's downstream consumers will hit at startup.
/// </remarks>
internal static unsafe class Program
{
    private const uint Width  = 256;
    private const uint Height = 256;

    private static int Main()
    {
        Console.WriteLine($"Ahjo.Vulkan AOT smoke — native AOT? {!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");

        // Probe for a working Vulkan ICD before doing any other work.
        // AOT publishing the wrapper is meaningful regression coverage on its
        // own, so a host with no usable ICD still exits 0 — but only when
        // nothing was declared. A lane that sets AHJO_VULKAN_TIER to
        // `software` or above provisions an ICD on purpose (the Windows lane's
        // does not currently answer — issue 152), and there a driverless run
        // means the provisioning broke, so it exits non-zero instead of
        // reporting a green smoke run that executed nothing. Issue 158.
        if (!HasVulkanDriver())
        {
            // Read AHJO_VULKAN_TIER locally and deliberately: samples must not
            // depend on tests/Shared/*.cs, and an env-var read plus a string
            // compare is the whole requirement. Do not "fix" this duplication
            // by linking the test sources in — it would put xunit on a
            // PublishAot=true sample.
            string? tier = Environment.GetEnvironmentVariable("AHJO_VULKAN_TIER");
            bool declaresDevice = !string.IsNullOrWhiteSpace(tier)
                && !string.Equals(tier.Trim(), "none", StringComparison.OrdinalIgnoreCase);
            if (declaresDevice)
            {
                Console.Error.WriteLine(
                    $"AHJO_VULKAN_TIER={tier} requires a Vulkan device, but none was found; " +
                    "AOT publish succeeded, smoke run did not execute.");
                // 2 is the missing-shader path, 3 the fence timeout below.
                return 4;
            }

            Console.WriteLine("No Vulkan driver detected; AOT publish verified, skipping smoke run.");
            return 0;
        }

        string outPath    = Path.Combine(AppContext.BaseDirectory, "aot-smoke.png");
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "triangle.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "triangle.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var vertBlob = SpirvBlob.Load(vertSpv);
        using var fragBlob = SpirvBlob.Load(fragSpv);
        using var vMod     = device.CreateShaderModule(vertBlob.Words);
        using var fMod     = device.CreateShaderModule(fragBlob.Words);
        using var layout   = device.CreatePipelineLayout(default);

        ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        using var pipeline = device.BuildGraphicsPipeline()
            .WithStages(in vMod, in fMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = Width, Height = Height, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
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
        var fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                ColorAttachment[] color = [new ColorAttachment
                {
                    View       = view,
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0.10f, 0.15f, 0.20f, 1.0f),
                }];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = Width, height = Height } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = Width, height = Height, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = Width, height = Height } });
                rec.BindPipeline(in pipeline);
                rec.Draw(vertexCount: 3);
                rec.EndRendering();

                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage: Stage.AllTransfer,           dstAccess: Access.TransferRead));

                rec.CopyImageToBuffer(in image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    in readback,
                    BufferImageCopy.WholeImage(in image));

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            if (fence.Wait(TimeSpan.FromSeconds(10)) != WaitState.Signaled)
            {
                Console.Error.WriteLine("GPU work did not complete within 10 seconds.");
                return 3;
            }
        }
        finally { fencePool.Release(fence); }

        ReadOnlySpan<byte> pixels = readback.AsReadOnlySpan<byte>();
        PngWriter.Write(outPath, pixels, (int)Width, (int)Height);
        Console.WriteLine($"Wrote {outPath} ({Width}x{Height}, {pixels.Length:N0} bytes pre-encode).");
        return 0;
    }

    private static VkClearColorValue ClearColor(float r, float g, float b, float a)
    {
        var c = new VkClearColorValue();
        c.float32[0] = r;
        c.float32[1] = g;
        c.float32[2] = b;
        c.float32[3] = a;
        return c;
    }

    private static bool HasVulkanDriver()
    {
        // Match the wrapper-test harness: try vkCreateInstance with a
        // default ApplicationInfo and tear it down on success.
        // DllNotFoundException = no vulkan-1 loader; INCOMPATIBLE_DRIVER
        // / INITIALIZATION_FAILED = loader present but no usable ICD.
        try
        {
            VkInstance_T* raw = null;
            var ai = new VkApplicationInfo
            {
                sType      = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
                apiVersion = (1u << 22) | (3u << 12), // 1.3
            };
            var ci = new VkInstanceCreateInfo
            {
                sType            = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &ai,
            };
            var r = Vk.vkCreateInstance(&ci, null, &raw);
            if (r == VkResult.VK_SUCCESS)
            {
                Vk.vkDestroyInstance(raw, null);
                return true;
            }
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint chosen = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    chosen = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = chosen;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
