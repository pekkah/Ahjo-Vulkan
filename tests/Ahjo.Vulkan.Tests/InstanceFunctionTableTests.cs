using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class InstanceFunctionTableTests
{
    [Fact]
    public void Resolve_KnownExtension_ReturnsNonNull()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
        });

        Assert.True(instance.Functions.CreateDebugUtilsMessenger != null);
        Assert.True(instance.Functions.DestroyDebugUtilsMessenger != null);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
        });

        ReadOnlySpan<byte> nope = "vkDoesNotExist"u8;
        Assert.True(instance.Functions.Resolve(Utf8Name.FromLiteral(nope)) == null);
    }
}
