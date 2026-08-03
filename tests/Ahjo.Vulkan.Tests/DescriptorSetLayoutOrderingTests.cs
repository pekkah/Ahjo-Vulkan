using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Device-free coverage for the layout-builder guard added in issue #186:
/// <see cref="Device.ValidateVariableDescriptorCountOrdering"/> enforces
/// VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004. The
/// rule is a pure function of the binding list, so it is checked directly
/// rather than through <c>CreateDescriptorSetLayout</c> (which needs a device).
/// </summary>
public sealed class DescriptorSetLayoutOrderingTests
{
    private static DescriptorBinding Binding(uint slot, DescriptorBindingFlags flags = DescriptorBindingFlags.None)
        => new()
        {
            Slot         = slot,
            Type         = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
            Count        = 1,
            Stages       = ShaderStages.Fragment,
            BindingFlags = flags,
        };

    [Fact]
    public void NoFlags_IsAccepted()
    {
        DescriptorBinding[] bindings = [Binding(0), Binding(1), Binding(2)];
        Device.ValidateVariableDescriptorCountOrdering(bindings);
    }

    [Fact]
    public void VariableCount_AsSoleBinding_IsAccepted()
    {
        DescriptorBinding[] bindings = [Binding(0, DescriptorBindingFlags.VariableDescriptorCount)];
        Device.ValidateVariableDescriptorCountOrdering(bindings);
    }

    [Fact]
    public void VariableCount_OnHighestBinding_IsAccepted()
    {
        DescriptorBinding[] bindings =
        [
            Binding(0),
            Binding(1),
            Binding(5, DescriptorBindingFlags.VariableDescriptorCount),
        ];
        Device.ValidateVariableDescriptorCountOrdering(bindings);
    }

    [Fact]
    public void VariableCount_NotHighestBinding_Throws()
    {
        DescriptorBinding[] bindings =
        [
            Binding(0, DescriptorBindingFlags.VariableDescriptorCount),
            Binding(5),
        ];
        var ex = Assert.Throws<System.ArgumentException>(
            () => Device.ValidateVariableDescriptorCountOrdering(bindings));
        Assert.Contains("pBindingFlags-03004", ex.Message);
    }

    [Fact]
    public void VariableCount_MidSetBelowAHigherPlainBinding_Throws()
    {
        // The flagged binding sits mid-set; a higher plain binding follows it.
        DescriptorBinding[] bindings =
        [
            Binding(0),
            Binding(2, DescriptorBindingFlags.VariableDescriptorCount | DescriptorBindingFlags.PartiallyBound),
            Binding(3),
        ];
        Assert.Throws<System.ArgumentException>(
            () => Device.ValidateVariableDescriptorCountOrdering(bindings));
    }
}
