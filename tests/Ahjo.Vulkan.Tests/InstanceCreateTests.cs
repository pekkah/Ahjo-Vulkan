using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class InstanceCreateTests
{
    [Fact]
    public void Create_MinimalDescription_Succeeds()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
        });

        Assert.True(instance.Handle != null);
    }

    [Fact]
    public void Create_DefaultsApiVersionWhenZero()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        Assert.True(instance.Handle != null);
    }
}
