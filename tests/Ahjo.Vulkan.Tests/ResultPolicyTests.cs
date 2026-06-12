using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Locks down the <see cref="VkResult"/> error policy: success path is zero
/// allocation, failure path produces a <see cref="VulkanException"/> with the
/// calling function name in the message, and catastrophic codes route to
/// pre-allocated cached instances.
/// </summary>
public sealed class ResultPolicyTests
{
    [Fact]
    public void IsSuccess_TrueOnlyForVkSuccess()
    {
        Assert.True(VkResult.VK_SUCCESS.IsSuccess());
        Assert.False(VkResult.VK_INCOMPLETE.IsSuccess());
        Assert.False(VkResult.VK_ERROR_DEVICE_LOST.IsSuccess());
    }

    [Fact]
    public void ThrowIfFailed_SuccessIsZeroAllocation()
    {
        // Warm up the JIT so we don't measure first-call codegen + tier-up
        // allocations from the runtime itself.
        for (var i = 0; i < 1_000; i++)
        {
            VkResult.VK_SUCCESS.ThrowIfFailed();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000_000; i++)
        {
            VkResult.VK_SUCCESS.ThrowIfFailed();
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void ThrowIfFailed_OnFailure_IncludesFunctionAndCode()
    {
        var ex = Assert.Throws<VulkanException>(() => CallSiteBoundary());

        Assert.Equal(VkResult.VK_ERROR_INITIALIZATION_FAILED, ex.Result);
        Assert.Equal(nameof(CallSiteBoundary), ex.Function);
        Assert.Contains(nameof(CallSiteBoundary), ex.Message);
        Assert.Contains("VK_ERROR_INITIALIZATION_FAILED", ex.Message);
    }

    [Fact]
    public void ThrowIfFailed_DeviceLost_ReusesCachedInstance()
    {
        var first = Capture(VkResult.VK_ERROR_DEVICE_LOST);
        var second = Capture(VkResult.VK_ERROR_DEVICE_LOST);

        Assert.Same(first, second);
        Assert.Equal(VkResult.VK_ERROR_DEVICE_LOST, first.Result);
    }

    [Fact]
    public void ThrowIfFailed_OutOfHostMemory_ReusesCachedInstance()
    {
        var first = Capture(VkResult.VK_ERROR_OUT_OF_HOST_MEMORY);
        var second = Capture(VkResult.VK_ERROR_OUT_OF_HOST_MEMORY);

        Assert.Same(first, second);
    }

    [Fact]
    public void ThrowIfFailed_NonCachedCode_AllocatesPerCall()
    {
        var first = Capture(VkResult.VK_ERROR_INITIALIZATION_FAILED);
        var second = Capture(VkResult.VK_ERROR_INITIALIZATION_FAILED);

        Assert.NotSame(first, second);
    }

    [Theory]
    [InlineData(VkResult.VK_SUCCESS)]
    [InlineData(VkResult.VK_INCOMPLETE)]
    [InlineData(VkResult.VK_SUBOPTIMAL_KHR)]
    [InlineData(VkResult.VK_TIMEOUT)]
    [InlineData(VkResult.VK_NOT_READY)]
    [InlineData(VkResult.VK_EVENT_SET)]
    public void ThrowIfErrored_NonErrorCodesPassThrough(VkResult result)
    {
        // Every non-error (non-negative) VkResult is a legal success/partial
        // outcome and must not throw — that's the whole point of the helper
        // for the count→fill idiom (issue #97).
        result.ThrowIfErrored();
    }

    [Theory]
    [InlineData(VkResult.VK_ERROR_OUT_OF_HOST_MEMORY)]
    [InlineData(VkResult.VK_ERROR_DEVICE_LOST)]
    [InlineData(VkResult.VK_ERROR_INITIALIZATION_FAILED)]
    [InlineData(VkResult.VK_ERROR_SURFACE_LOST_KHR)]
    public void ThrowIfErrored_ErrorCodesThrow(VkResult result)
    {
        var ex = Assert.Throws<VulkanException>(() => result.ThrowIfErrored());
        Assert.Equal(result, ex.Result);
    }

    [Fact]
    public void ThrowIfErrored_NonErrorIsZeroAllocation()
    {
        for (var i = 0; i < 1_000; i++)
        {
            VkResult.VK_INCOMPLETE.ThrowIfErrored();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000_000; i++)
        {
            VkResult.VK_INCOMPLETE.ThrowIfErrored();
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private static void CallSiteBoundary()
    {
        VkResult.VK_ERROR_INITIALIZATION_FAILED.ThrowIfFailed();
    }

    private static VulkanException Capture(VkResult result)
    {
        try
        {
            result.ThrowIfFailed();
        }
        catch (VulkanException ex)
        {
            return ex;
        }
        throw new InvalidOperationException("ThrowIfFailed did not throw");
    }
}
