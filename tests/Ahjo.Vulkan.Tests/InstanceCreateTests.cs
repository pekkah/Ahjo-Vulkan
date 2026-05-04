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

    [Fact]
    public void Create_WithValidation_DefaultCallback_FiresOnUnknownExtension()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var stderr = new StringWriter();
        var oldErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            VulkanException? ex = null;
            try
            {
                ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
                {
                    Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
                };
                using var _ = Instance.Create(new InstanceDescription
                {
                    ApiVersion = VulkanVersion.V1_4,
                    EnableValidation = true,
                    Extensions = bogus,
                });
            }
            catch (VulkanException e)
            {
                ex = e;
            }
            Assert.NotNull(ex);
        }
        finally
        {
            Console.SetError(oldErr);
        }

        Assert.NotEmpty(stderr.ToString());
    }
}
