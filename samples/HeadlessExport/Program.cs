using System.Runtime.InteropServices;
using Ahjo.Vulkan;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Utilities;

namespace Ahjo.Vulkan.Samples.HeadlessExport;

/// <summary>
/// Issue 143 reference: the same offscreen RGB-triangle render as
/// <c>HeadlessTriangle</c>, but the render target is an
/// <see cref="ExportableImage"/> whose backing memory is pulled out as an
/// OS handle (a Win32 NT <c>HANDLE</c> or a POSIX fd) for zero-copy GPU
/// interop — the "render in Ahjo, present in Avalonia/D3D" path. Prints the
/// exported handle and, to prove the shared image actually holds the render,
/// also copies it back through a host buffer and writes <c>export.png</c>.
/// </summary>
/// <remarks>
/// Requires <c>VK_KHR_external_memory_win32</c> (Windows) or
/// <c>VK_KHR_external_memory_fd</c> (Linux) on the device. Software
/// rasterizers (SwiftShader, lavapipe) typically don't expose these, so the
/// sample prints a skip line and exits 0 rather than failing.
/// </remarks>
internal static unsafe class Program
{
    private const uint Width  = 512;
    private const uint Height = 512;

    private static int Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "export.png");
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "triangle.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "triangle.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
        {
            Console.Error.WriteLine($"Missing compiled shaders. Expected:\n  {vertSpv}\n  {fragSpv}");
            return 2;
        }

        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        Utf8Name externalMemoryExt = isWindows
            ? VulkanExtensions.KhrExternalMemoryWin32
            : VulkanExtensions.KhrExternalMemoryFd;

        using var instance = Instance.Create(default);

        Device device;
        uint family;
        try
        {
            device = CreateGraphicsDevice(instance, externalMemoryExt, out family);
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT)
        {
            Console.WriteLine(
                $"Device does not expose {(isWindows ? "VK_KHR_external_memory_win32" : "VK_KHR_external_memory_fd")}; " +
                "external-memory export is unsupported on this driver (expected on SwiftShader / lavapipe). Skipping.");
            return 0;
        }

        using (device)
        {
            using var vertBlob = SpirvBlob.Load(vertSpv);
            using var fragBlob = SpirvBlob.Load(fragSpv);
            using var vMod   = device.CreateShaderModule(vertBlob.Words);
            using var fMod   = device.CreateShaderModule(fragBlob.Words);
            using var layout = device.CreatePipelineLayout(default);

            ReadOnlySpan<VkFormat> colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
            using var pipeline = device.BuildGraphicsPipeline()
                .WithStages(in vMod, in fMod)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build();

            // ---- Exportable render target ----
            using var exportable = ExportableImage.Create(device, new ImageDescription
            {
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Width, Height = Height,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            });
            Image image = exportable.Image;

            using var view = image.CreateView(device, new ImageViewDescription
            {
                ViewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
                Aspect   = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            });

            const uint Bytes = Width * Height * 4;
            using var readback = device.Allocator.CreateBuffer(
                new BufferDescription { Size = Bytes, Usage = BufferUsage.TransferDst },
                new AllocationDescription
                {
                    Usage = MemoryUsage.AutoPreferHost,
                    Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
                });

            // ---- Record + submit ----
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
                        ClearColor = ClearColor(0.05f, 0.07f, 0.10f, 1.0f),
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

            // ---- Export the shared handle ----
            // This is the payload a compositor imports (Avalonia's
            // ImportGpuImage / PlatformGraphicsExternalImageProperties). The
            // caller owns the returned handle and must close it once every
            // importer is done — here we just report it and let the OS reclaim
            // it at process exit.
            if (exportable.HandleType == ExternalHandleType.OpaqueWin32)
            {
                nint handle = exportable.ExportOpaqueWin32Handle();
                Console.WriteLine($"Exported OPAQUE_WIN32 handle: 0x{handle:X} " +
                    $"(memory {exportable.MemorySize:N0} bytes at offset {exportable.MemoryOffset}).");
            }
            else
            {
                int fd = exportable.ExportOpaqueFd();
                Console.WriteLine($"Exported OPAQUE_FD handle: fd {fd} " +
                    $"(memory {exportable.MemorySize:N0} bytes at offset {exportable.MemoryOffset}).");
            }

            // ---- Proof the shared image holds the render ----
            ReadOnlySpan<byte> pixels = readback.AsReadOnlySpan<byte>();
            PngWriter.Write(outPath, pixels, (int)Width, (int)Height);
            Console.WriteLine($"Wrote {outPath} ({Width}x{Height}) from the exported image's contents.");
        }

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

    private static Device CreateGraphicsDevice(Instance instance, Utf8Name externalMemoryExt, out uint family)
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
        Utf8Name[] extensions = [externalMemoryExt];
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = extensions,
        });
    }
}
