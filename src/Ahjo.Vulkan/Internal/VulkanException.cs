using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Thrown by wrapper APIs that contract a single-success result when the
/// Vulkan call returns anything other than <see cref="VkResult.VK_SUCCESS"/>.
/// </summary>
/// <remarks>
/// <para>Hot-path APIs that legitimately return non-success codes
/// (<c>VK_INCOMPLETE</c>, <c>VK_SUBOPTIMAL_KHR</c>, <c>VK_TIMEOUT</c>, etc.)
/// don't throw — they surface the <see cref="VkResult"/> directly so the
/// caller can branch without paying for an exception. See the wrapper's
/// per-function XML docs for which path applies.</para>
/// <para>The message is fixed at construction so cached instances (see
/// <see cref="ResultExtensions"/>) can be re-thrown without rebuilding the
/// string.</para>
/// </remarks>
public sealed class VulkanException : Exception
{
    public VkResult Result { get; }
    public string Function { get; }

    internal VulkanException(VkResult result, string function)
        : base($"{function} failed: {result}")
    {
        Result = result;
        Function = function;
    }
}
