using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class PhysicalDeviceInfoTests
{
    [Fact]
    public void SupportsExtension_KnownName_ReturnsTrue()
    {
        Assert.True(BuildAndQuery("VK_TEST_known"u8, "VK_TEST_known"u8));
    }

    [Fact]
    public void SupportsExtension_UnknownName_ReturnsFalse()
    {
        Assert.False(BuildAndQuery("VK_TEST_known"u8, "VK_TEST_other"u8));
    }

    [Fact]
    public void SupportsExtension_PrefixOnly_ReturnsFalse()
    {
        // Querying "VK_TEST" against a buffer holding "VK_TEST_known"
        // must reject because the buffer's NUL is at position 13, not 7.
        Assert.False(BuildAndQuery("VK_TEST_known"u8, "VK_TEST"u8));
    }

    [Fact]
    public void SupportsExtension_EmptyExtensionList_ReturnsFalse()
    {
        var props = default(VkPhysicalDeviceProperties);
        var feats = default(VkPhysicalDeviceFeatures);
        var f11   = default(VkPhysicalDeviceVulkan11Features);
        var f12   = default(VkPhysicalDeviceVulkan12Features);
        var f13   = default(VkPhysicalDeviceVulkan13Features);
        var f14   = default(VkPhysicalDeviceVulkan14Features);
        var mem   = default(VkPhysicalDeviceMemoryProperties);
        Span<VkExtensionProperties> exts  = [];
        Span<QueueFamilyInfo>       qfs   = [];

        var info = new PhysicalDeviceInfo(
            device: null!, properties: in props, features: in feats,
            features11: in f11, features12: in f12, features13: in f13, features14: in f14,
            memory: in mem, queueFamilies: qfs, extensions: exts, name: default);

        Assert.False(info.SupportsExtension("VK_KHR_swapchain"u8));
    }

    private static bool BuildAndQuery(ReadOnlySpan<byte> bufferContent, ReadOnlySpan<byte> query)
    {
        var ext = default(VkExtensionProperties);
        for (int i = 0; i < bufferContent.Length; i++)
            ext.extensionName[i] = (sbyte)bufferContent[i];
        // 256-byte buffer was zero-initialised → NUL terminator already in place at bufferContent.Length.

        Span<VkExtensionProperties> exts = [ext];
        Span<QueueFamilyInfo>       qfs  = [];

        var props = default(VkPhysicalDeviceProperties);
        var feats = default(VkPhysicalDeviceFeatures);
        var f11   = default(VkPhysicalDeviceVulkan11Features);
        var f12   = default(VkPhysicalDeviceVulkan12Features);
        var f13   = default(VkPhysicalDeviceVulkan13Features);
        var f14   = default(VkPhysicalDeviceVulkan14Features);
        var mem   = default(VkPhysicalDeviceMemoryProperties);

        var info = new PhysicalDeviceInfo(
            device: null!, properties: in props, features: in feats,
            features11: in f11, features12: in f12, features13: in f13, features14: in f14,
            memory: in mem, queueFamilies: qfs, extensions: exts, name: default);

        return info.SupportsExtension(query);
    }
}
