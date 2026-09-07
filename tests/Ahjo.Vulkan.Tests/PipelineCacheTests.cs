using System.IO;
using System.Threading;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class PipelineCacheTests
{
    [Fact]
    public void Default_PipelineCache_IsNull_DisposeNoOp()
    {
        PipelineCache c = default;
        Assert.True(c.IsNull);
        c.Dispose();
    }

    [Fact]
    public void CreatePipelineCache_Empty_HandleNonNull()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var cache    = device.CreatePipelineCache();
        Assert.False(cache.IsNull);
    }

    [Fact]
    public void SaveLoadRoundTrip_PreservesCacheBytes()
    {
        // The driver controls cache-data format; the wrapper only
        // promises that what it wrote is what gets read back. Exercise
        // a Save → reload → second Save sequence and check the bytes
        // match. (A pristine empty cache has a stable 32-byte header
        // even with no pipelines compiled — the driver still emits the
        // VkPipelineCacheHeaderVersionOne prefix. That's enough to
        // catch a regression in WriteAtomic / Save buffer sizing.)
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        string path1 = Path.Combine(Path.GetTempPath(), $"ahjo-cache-{Guid.NewGuid():N}.bin");
        string path2 = Path.Combine(Path.GetTempPath(), $"ahjo-cache-{Guid.NewGuid():N}.bin");
        try
        {
            byte[] firstSave;
            using (var cache = device.CreatePipelineCache())
            {
                cache.Save(path1);
                firstSave = File.ReadAllBytes(path1);
            }

            // Reload through LoadOrCreate so we exercise the full path
            // (header validate → pInitialData feed → vkCreatePipelineCache).
            byte[] secondSave;
            using (var cache = device.LoadOrCreatePipelineCache(path1))
            {
                Assert.False(cache.IsNull);
                cache.Save(path2);
                secondSave = File.ReadAllBytes(path2);
            }

            Assert.Equal(firstSave, secondSave);
        }
        finally
        {
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    [Fact]
    public void LoadOrCreate_HeaderMismatch_DiscardsAndCreatesEmpty()
    {
        // Stamp a structurally-valid but wrong-vendor header and confirm
        // LoadOrCreate falls back to an empty cache instead of feeding
        // the bogus bytes to vkCreatePipelineCache (which on some drivers
        // returns ERROR_INITIALIZATION_FAILED rather than just an empty
        // cache). The wrapper's Console.Error log fires too — not asserted
        // here since it would race against test-runner stderr capture.
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        string path = Path.Combine(Path.GetTempPath(), $"ahjo-cache-{Guid.NewGuid():N}.bin");
        try
        {
            byte[] bogus = new byte[32];
            // headerSize = 32, headerVersion = ONE (1), vendorID = 0xDEADBEEF, deviceID = 0xCAFEBABE
            BitConverter.TryWriteBytes(bogus.AsSpan(0,  4), 32u);
            BitConverter.TryWriteBytes(bogus.AsSpan(4,  4), 1u);
            BitConverter.TryWriteBytes(bogus.AsSpan(8,  4), 0xDEADBEEFu);
            BitConverter.TryWriteBytes(bogus.AsSpan(12, 4), 0xCAFEBABEu);
            // pipelineCacheUUID[16] left zero.
            File.WriteAllBytes(path, bogus);

            using var cache = device.LoadOrCreatePipelineCache(path);
            Assert.False(cache.IsNull);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LoadOrCreate_MissingFile_CreatesEmpty()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        string path = Path.Combine(Path.GetTempPath(), $"ahjo-cache-missing-{Guid.NewGuid():N}.bin");
        Assert.False(File.Exists(path));

        using var cache = device.LoadOrCreatePipelineCache(path);
        Assert.False(cache.IsNull);
    }

    [Fact]
    public void Merge_EmptySources_NoOp()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var dst      = device.CreatePipelineCache();

        dst.Merge(ReadOnlySpan<PipelineCache>.Empty);
        Assert.False(dst.IsNull);
    }

    [Fact]
    public void Merge_TwoEmptySources_Succeeds()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        using var dst      = device.CreatePipelineCache();
        using var srcA     = device.CreatePipelineCache();
        using var srcB     = device.CreatePipelineCache();

        Span<PipelineCache> sources = stackalloc PipelineCache[2];
        sources[0] = srcA;
        sources[1] = srcB;
        dst.Merge(sources);
    }

    [Fact]
    public void WriteAtomic_ConcurrentSaversOfOnePath_AllCompleteAndFileReadable()
    {
        // #230: the atomic write used a fixed `path + ".tmp"` sibling, so two
        // savers of one cache path opened the same FileShare.None temp and the
        // second threw IOException. Per-writer temp names let concurrent
        // writers serialize on the rename (last-writer-wins) instead. This is
        // the wrapper's half and needs no Vulkan device — it drives the atomic
        // write directly.
        string path = Path.Combine(Path.GetTempPath(), $"ahjo-cache-{Guid.NewGuid():N}.bin");
        string dir  = Path.GetDirectoryName(path)!;
        string name = Path.GetFileName(path);
        try
        {
            const int writers = 8;
            using var start = new Barrier(writers);
            var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

            var threads = new Thread[writers];
            for (int i = 0; i < writers; i++)
            {
                int payload = i; // distinct content per writer.
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        byte[] bytes = new byte[64];
                        Array.Fill(bytes, (byte)payload);
                        start.SignalAndWait();
                        for (int rep = 0; rep < 50; rep++)
                            PipelineCache.WriteAtomic(path, bytes);
                    }
                    catch (Exception e)
                    {
                        errors.Enqueue(e);
                    }
                });
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            Assert.Empty(errors);

            // The file exists, is readable, and holds one writer's payload
            // whole (last-writer-wins, never a torn blend of two payloads).
            Assert.True(File.Exists(path));
            byte[] final = File.ReadAllBytes(path);
            Assert.Equal(64, final.Length);
            byte first = final[0];
            Assert.All(final, b => Assert.Equal(first, b));

            // No temp siblings left behind by any writer.
            Assert.Empty(Directory.GetFiles(dir, name + ".*.tmp"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var stray in Directory.GetFiles(dir, name + ".*.tmp"))
                File.Delete(stray);
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
