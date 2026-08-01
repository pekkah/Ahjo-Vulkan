using Ahjo.Vulkan.Testing;

using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// The one place this suite touches Vulkan: the SPIR-V Slang produced has to
/// be something <c>Device.CreateShaderModule</c> accepts, or the whole package
/// is a very elaborate way to produce bytes nobody can use.
/// </summary>
public sealed class SlangShaderModuleTests
{
    [Fact]
    public void Spirv_FeedsCreateShaderModule()
    {
        TestGate.RequireDriver();

        using var compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(default);
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "shaderModule",
            Source = ShaderFixtures.VertexAndFragment,
        });

        using var instance = Instance.Create(default);

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

        using Device device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        // No new API on Device: the existing ReadOnlySpan<uint> overload is
        // the whole integration point, exactly as it is for SpirvBlob.Words.
        using ShaderModule vertex = device.CreateShaderModule(program.Spirv(0));
        using ShaderModule fragment = device.CreateShaderModule(program.Spirv(1));

        Assert.False(vertex.IsNull);
        Assert.False(fragment.IsNull);
    }
}
