using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Native.Tests;

/// <summary>
/// Pins the auto-init behavior introduced for issue #94: every IChainRoot
/// and IChainable struct gets a parameterless ctor that writes the correct
/// sType, so consumer code using `new VkX { ... }` object-initializer
/// syntax doesn't ship a zero-valued sType to the driver.
///
/// Spot-checks across a few representative shapes:
///  - plain IChainRoot
///  - root with multiple fields the initializer touches
///  - IChainable that's only ever an extender
///  - IChainable that's also a root (lives in Root.g.cs only)
/// </summary>
public class ChainStructDefaultSTypeTests
{
    [Fact]
    public void NewExpression_Root_SetsSType()
    {
        var info = new VkCommandPoolCreateInfo();

        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO, info.sType);
    }

    [Fact]
    public void ObjectInitializer_Root_PreservesAutoSType()
    {
        var info = new VkCommandPoolCreateInfo
        {
            flags = (uint)VkCommandPoolCreateFlagBits.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT,
            queueFamilyIndex = 7,
        };

        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO, info.sType);
        Assert.Equal((uint)VkCommandPoolCreateFlagBits.VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT, info.flags);
        Assert.Equal(7u, info.queueFamilyIndex);
    }

    [Fact]
    public void ObjectInitializer_Chainable_SetsSType()
    {
        var info = new VkPipelineRenderingCreateInfo
        {
            colorAttachmentCount = 1,
        };

        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO, info.sType);
    }

    [Fact]
    public void NewExpression_DualRoleStruct_SetsSType()
    {
        // VkPhysicalDeviceFeatures2 is both an IChainRoot (extended by feature
        // structs) and an IChainable<VkDeviceCreateInfo> (chained onto Device
        // creation). The parameterless ctor lives only in Root.g.cs to avoid
        // declaring it twice across partials.
        var info = new VkPhysicalDeviceFeatures2();

        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2, info.sType);
    }

    [Fact]
    public void Default_StillZeroInits_NotCovered()
    {
        // default(T) doesn't run user-defined struct ctors; documents the
        // known boundary of the auto-init fix so a future contributor doesn't
        // expect it to cover this case.
        var info = default(VkCommandPoolCreateInfo);

        Assert.Equal(default, info.sType);
    }
}
