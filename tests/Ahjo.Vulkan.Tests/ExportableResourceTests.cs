using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the issue-143 cross-API interop surface: <see cref="ExportableImage"/>
/// and <see cref="ExportableSemaphore"/> — creating an exportable resource and
/// pulling its OS handle. The end-to-end export paths gate on the device
/// exposing the platform export extension (<c>VK_KHR_external_memory_win32</c>
/// / <c>_fd</c>, <c>VK_KHR_external_semaphore_win32</c> / <c>_fd</c>), which
/// software rasterizers (SwiftShader, lavapipe) don't, so they self-skip
/// there — the same shape as the platform surface round-trips.
/// </summary>
public sealed unsafe class ExportableResourceTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static Utf8Name ExternalMemoryExt =>
        IsWindows ? VulkanExtensions.KhrExternalMemoryWin32 : VulkanExtensions.KhrExternalMemoryFd;

    private static Utf8Name ExternalSemaphoreExt =>
        IsWindows ? VulkanExtensions.KhrExternalSemaphoreWin32 : VulkanExtensions.KhrExternalSemaphoreFd;

    // ---- Driver-free argument / null-handle guards ----

    [Fact]
    public void ExportableImage_Create_NullDevice_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ExportableImage.Create(null!, new ImageDescription
            {
                Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM, Width = 4, Height = 4,
                Usage  = ImageUsage.ColorAttachment,
            }));
    }

    [Fact]
    public void ExportableImage_Default_IsNull_DisposeIsNoOp()
    {
        ExportableImage img = default;
        Assert.True(img.IsNull);
        img.Dispose();
        Assert.Throws<InvalidOperationException>(() => img.ExportOpaqueWin32Handle());
        Assert.Throws<InvalidOperationException>(() => img.ExportOpaqueFd());
    }

    [Fact]
    public void ExportableSemaphore_CreateBinary_NullDevice_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExportableSemaphore.CreateBinary(null!));
    }

    [Fact]
    public void ExportableSemaphore_CreateTimeline_NullDevice_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExportableSemaphore.CreateTimeline(null!));
    }

    [Fact]
    public void ExportableSemaphore_Default_IsNull_DisposeIsNoOp()
    {
        ExportableSemaphore sem = default;
        Assert.True(sem.IsNull);
        sem.Dispose();
        Assert.Throws<InvalidOperationException>(() => sem.AsBinary());
        Assert.Throws<InvalidOperationException>(() => sem.AsTimeline());
    }

    // ---- Driver + export-extension round-trips ----

    [Fact]
    public void ExportableImage_Create_Export_RoundTrip()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using Device? device = TryCreateDeviceWith(instance, ExternalMemoryExt, out uint _);
        Assert.SkipWhen(device is null, "Device does not expose the external-memory export extension.");

        using var exportable = ExportableImage.Create(device!, new ImageDescription
        {
            Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            Width  = 64, Height = 64,
            Tiling = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
            Usage  = ImageUsage.ColorAttachment | ImageUsage.TransferSrc,
        });

        Assert.False(exportable.IsNull);
        Assert.False(exportable.Image.IsNull);
        // Dedicated allocation: whole VkDeviceMemory holds exactly this image at offset 0.
        Assert.Equal(0ul, exportable.MemoryOffset);
        Assert.True(exportable.MemorySize >= 64ul * 64ul * 4ul);

        if (IsWindows)
        {
            Assert.Equal(ExternalHandleType.OpaqueWin32, exportable.HandleType);
            nint handle = exportable.ExportOpaqueWin32Handle();
            Assert.NotEqual(0, handle);
            CloseWin32Handle(handle);
            // Wrong-flavor export is rejected before touching the driver.
            Assert.Throws<InvalidOperationException>(() => exportable.ExportOpaqueFd());
        }
        else
        {
            Assert.Equal(ExternalHandleType.OpaqueFd, exportable.HandleType);
            int fd = exportable.ExportOpaqueFd();
            Assert.True(fd >= 0);
            CloseFd(fd);
            Assert.Throws<InvalidOperationException>(() => exportable.ExportOpaqueWin32Handle());
        }
    }

    [Fact]
    public void ExportableSemaphore_Binary_Export_RoundTrip()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using Device? device = TryCreateDeviceWith(instance, ExternalSemaphoreExt, out uint _);
        Assert.SkipWhen(device is null, "Device does not expose the external-semaphore export extension.");

        using var sem = ExportableSemaphore.CreateBinary(device!);
        Assert.False(sem.IsNull);
        Assert.False(sem.IsTimeline);
        Assert.False(sem.AsBinary().IsNull);
        Assert.Throws<InvalidOperationException>(() => sem.AsTimeline());

        if (IsWindows)
        {
            nint handle = sem.ExportOpaqueWin32Handle();
            Assert.NotEqual(0, handle);
            CloseWin32Handle(handle);
        }
        else
        {
            int fd = sem.ExportOpaqueFd();
            Assert.True(fd >= 0);
            CloseFd(fd);
        }
    }

    [Fact]
    public void ExportableSemaphore_Timeline_Export_RoundTrip()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using Device? device = TryCreateDeviceWith(instance, ExternalSemaphoreExt, out uint _);
        Assert.SkipWhen(device is null, "Device does not expose the external-semaphore export extension.");

        using var sem = ExportableSemaphore.CreateTimeline(device!, initialValue: 7);
        Assert.False(sem.IsNull);
        Assert.True(sem.IsTimeline);

        TimelineSemaphore timeline = sem.AsTimeline();
        Assert.Equal(7ul, timeline.Value);
        Assert.Throws<InvalidOperationException>(() => sem.AsBinary());

        if (IsWindows)
        {
            nint handle = sem.ExportOpaqueWin32Handle();
            Assert.NotEqual(0, handle);
            CloseWin32Handle(handle);
        }
        else
        {
            int fd = sem.ExportOpaqueFd();
            Assert.True(fd >= 0);
            CloseFd(fd);
        }
    }

    // Creates a device with the requested extension enabled, or returns null
    // when the driver reports VK_ERROR_EXTENSION_NOT_PRESENT — the clean skip
    // signal for a host without external-memory support.
    private static Device? TryCreateDeviceWith(Instance instance, Utf8Name extension, out uint gfxFamily)
    {
        uint family = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        gfxFamily = family;

        Utf8Name[] extensions = [extension];
        try
        {
            return gpu.CreateDevice(new DeviceDescription
            {
                Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
                Extensions = extensions,
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT)
        {
            return null;
        }
    }

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    private static void CloseWin32Handle(nint handle)
    {
        if (handle != 0) CloseHandle(handle);
    }

    private static void CloseFd(int fd)
    {
        if (fd >= 0) close(fd);
    }
}
