using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class InstanceFunctionTableTests
{
    [Fact]
    public void Resolve_KnownExtension_ReturnsNonNull()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

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
        TestGate.RequireDriver();

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
        });

        ReadOnlySpan<byte> nope = "vkDoesNotExist"u8;
        Assert.True(instance.Functions.Resolve(Utf8Name.FromLiteral(nope)) == null);
    }
}
