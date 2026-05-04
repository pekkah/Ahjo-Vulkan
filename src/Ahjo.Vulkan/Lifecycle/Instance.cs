using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Owner of a <c>VkInstance</c>. <c>sealed class</c> rather than the wider
/// struct-handle convention because <see cref="Instance"/> is created once
/// per process, never copied, never on a hot path, and benefits from a
/// finalizer that backstops a missed <c>Dispose</c>. See the design spec at
/// <c>docs/superpowers/specs/2026-05-04-issue-06-instance-creation-design.md</c>
/// for the rationale.
/// </summary>
public sealed unsafe class Instance : IDisposable
{
    internal VkInstance_T*               Handle;
    internal VkDebugUtilsMessengerEXT_T* Messenger;
    private  bool                        _disposed;

    private Instance(VkInstance_T* handle, VkDebugUtilsMessengerEXT_T* messenger)
    {
        Handle = handle;
        Messenger = messenger;
    }

    public static Instance Create(scoped in InstanceDescription desc)
    {
        uint apiVersion = desc.ApiVersion.Packed != 0 ? desc.ApiVersion.Packed : VulkanVersion.V1_4.Packed;

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = desc.ApplicationName.Ptr,
            applicationVersion = desc.ApplicationVersion,
            pEngineName = desc.EngineName.Ptr,
            engineVersion = desc.EngineVersion,
            apiVersion = apiVersion,
        };

        Span<nint> layerPtrs = stackalloc nint[Math.Max(1, desc.Layers.Length)];
        for (int i = 0; i < desc.Layers.Length; i++) layerPtrs[i] = (nint)desc.Layers[i].Ptr;

        Span<nint> extPtrs = stackalloc nint[Math.Max(1, desc.Extensions.Length)];
        for (int i = 0; i < desc.Extensions.Length; i++) extPtrs[i] = (nint)desc.Extensions[i].Ptr;

        var ci = new VkInstanceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo,
            enabledLayerCount = (uint)desc.Layers.Length,
            ppEnabledLayerNames = desc.Layers.Length > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(layerPtrs))
                : null,
            enabledExtensionCount = (uint)desc.Extensions.Length,
            ppEnabledExtensionNames = desc.Extensions.Length > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
                : null,
        };

        VkInstance_T* raw = null;
        Vk.vkCreateInstance(&ci, null, &raw).ThrowIfFailed();

        return new Instance(raw, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Messenger != null)
        {
            // wired in Task 12
        }
        if (Handle != null) Vk.vkDestroyInstance(Handle, null);
        GC.SuppressFinalize(this);
    }

    ~Instance()
    {
        Debug.Fail("Instance was not disposed.");
        Dispose();
    }
}
