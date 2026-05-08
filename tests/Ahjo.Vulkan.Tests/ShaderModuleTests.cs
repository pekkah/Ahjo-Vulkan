using System.IO;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class ShaderModuleTests
{
    [Fact]
    public void Default_ShaderModule_IsNull_DisposeNoOp()
    {
        ShaderModule m = default;
        Assert.True(m.IsNull);
        m.Dispose();
    }

    [Fact]
    public void CreateShaderModule_EmptySpan_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        Assert.Throws<ArgumentException>(() => device.CreateShaderModule(ReadOnlySpan<uint>.Empty));
        Assert.Throws<ArgumentException>(() => device.CreateShaderModule(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void CreateShaderModule_MisalignedByteSpan_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        Assert.Throws<ArgumentException>(() =>
        {
            // ReadOnlySpan<byte> can't cross the lambda boundary; stage the
            // 7-byte (non-multiple-of-4) buffer inside the throwing call.
            ReadOnlySpan<byte> b = stackalloc byte[7];
            device.CreateShaderModule(b);
        });
    }

    [Fact]
    public void SpirvBlob_Load_RoundtripsBytes()
    {
        // A 16-byte placeholder (mimics the SPIR-V header layout — magic +
        // version + generator + bound. Not valid SPIR-V; this test only
        // covers the loader's I/O contract, not vkCreateShaderModule.)
        ReadOnlySpan<uint> raw = [0x07230203u, 0x00010000u, 0u, 1u];

        string path = Path.Combine(Path.GetTempPath(), $"spirv-test-{Guid.NewGuid():N}.spv");
        try
        {
            File.WriteAllBytes(path, System.Runtime.InteropServices.MemoryMarshal.AsBytes(raw).ToArray());

            using var blob = SpirvBlob.Load(path);
            Assert.Equal(4, blob.Words.Length);
            Assert.Equal(0x07230203u, blob.Words[0]);
            Assert.Equal(16, blob.Bytes.Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SpirvBlob_Load_MisalignedFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spirv-test-{Guid.NewGuid():N}.spv");
        try
        {
            File.WriteAllBytes(path, [0x01, 0x02, 0x03]); // 3 bytes — not /4.
            Assert.Throws<ArgumentException>(() => SpirvBlob.Load(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SpirvBlob_Load_EmptyFile_Throws()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spirv-test-{Guid.NewGuid():N}.spv");
        try
        {
            File.WriteAllBytes(path, []);
            Assert.Throws<ArgumentException>(() => SpirvBlob.Load(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CreateShaderModule_FromCompiledTriangleSpv_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        // The csproj's CompileShaders target invokes glslc on every
        // Shaders/*.{vert,frag,comp} input and drops the output as
        // Shaders/<name>.spv next to the test DLL. With ContinueOnError =
        // WarnAndContinue the build still succeeds when glslc isn't on
        // PATH, leaving the .spv missing — skip cleanly here so the rest
        // of the suite still runs on hosts without the Vulkan SDK.
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "triangle.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "triangle.frag.spv");
        Assert.SkipUnless(File.Exists(vertSpv) && File.Exists(fragSpv),
            $"Compiled SPIR-V missing (glslc not on PATH at build time): {vertSpv}, {fragSpv}");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var vertBlob = SpirvBlob.Load(vertSpv);
        using var fragBlob = SpirvBlob.Load(fragSpv);

        // Real driver acceptance — vkCreateShaderModule parses the SPIR-V
        // header (magic 0x07230203, version, generator, bound) and
        // structurally validates the words. Bogus blobs throw.
        using var vert = device.CreateShaderModule(vertBlob.Words);
        using var frag = device.CreateShaderModule(fragBlob.Words);
        Assert.False(vert.IsNull);
        Assert.False(frag.IsNull);
    }

    [Fact]
    public void SpirvBlob_AfterDispose_WordsThrows()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spirv-test-{Guid.NewGuid():N}.spv");
        try
        {
            File.WriteAllBytes(path, [0, 0, 0, 0]);
            var blob = SpirvBlob.Load(path);
            blob.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { _ = blob.Words; });
            Assert.Throws<ObjectDisposedException>(() => { _ = blob.Bytes; });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static Device CreateGraphicsDevice(Instance instance)
    {
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
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
