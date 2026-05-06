using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps a <c>VkSwapchainKHR</c> plus its per-image
/// <c>VkImage</c> / <c>VkImageView</c> arrays. Class shape (rather than
/// the wrapper-wide <c>readonly struct</c> handle convention) because
/// resize swaps the underlying handles in-place and the per-image
/// view arrays need explicit lifecycle.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> Construct against a <see cref="Surface"/> +
/// the <see cref="Device"/> that owns the swapchain device-extension.
/// Call <see cref="AcquireNextImage"/> per frame to drive the present
/// loop, <see cref="Recreate"/> on
/// <see cref="AcquireResult.OutOfDate"/> / window resize, and
/// <see cref="Dispose"/> when tearing down (after the device is idle).</para>
/// <para><b>Sharing mode.</b> Always
/// <c>VK_SHARING_MODE_EXCLUSIVE</c>. Multi-queue-family sharing on a
/// swapchain is rare in practice — engines that need it can drive
/// queue-family ownership transfers explicitly via
/// <see cref="ImageBarrier"/>.</para>
/// </remarks>
public sealed unsafe class Swapchain : IDisposable
{
    private readonly Device       _device;
    private readonly Surface      _surface;
    private VkSwapchainKHR_T*     _handle;
    private VkImage_T*[]          _images = [];
    private ImageView[]           _views  = [];
    private VkSurfaceFormatKHR    _format;
    private VkExtent2D            _extent;
    private VkPresentModeKHR      _presentMode;
    private ImageUsage            _imageUsage;
    private bool                  _disposed;

    public VkSwapchainKHR_T*     Handle      => _handle;
    public VkSurfaceFormatKHR    SurfaceFormat => _format;
    public VkFormat              Format      => _format.format;
    public VkColorSpaceKHR       ColorSpace  => _format.colorSpace;
    public VkExtent2D            Extent      => _extent;
    public VkPresentModeKHR      PresentMode => _presentMode;
    public ImageUsage            ImageUsage  => _imageUsage;
    public uint                  ImageCount  => (uint)_images.Length;
    public ReadOnlySpan<ImageView> ImageViews  => _views;

    public Swapchain(Device device, in SwapchainDescription desc)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (desc.Surface.IsNull) throw new ArgumentException("Surface is null.", nameof(desc));

        _device  = device;
        _surface = desc.Surface;
        CreateOrRecreate(in desc, oldSwapchain: null);
    }

    /// <summary>
    /// Tear down the existing swapchain and rebuild against the same
    /// surface. Pass a fresh <see cref="SwapchainDescription"/> with the
    /// new <see cref="SwapchainDescription.Width"/> /
    /// <see cref="SwapchainDescription.Height"/> on resize. The
    /// <see cref="Surface"/> field MUST equal the surface this swapchain
    /// was originally built against.
    /// </summary>
    public void Recreate(in SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (desc.Surface.Handle != _surface.Handle)
            throw new ArgumentException(
                "Recreate must use the same Surface this Swapchain was constructed with.",
                nameof(desc));

        Vk.vkDeviceWaitIdle(_device.Handle).ThrowIfFailed();

        VkSwapchainKHR_T* old = _handle;
        DestroyViews();
        CreateOrRecreate(in desc, oldSwapchain: old);
        if (old != null) Vk.vkDestroySwapchainKHR(_device.Handle, old, null);
    }

    /// <summary>
    /// Block (up to <paramref name="timeout"/>) for the next swapchain
    /// image. The acquired image will be ready for rendering once
    /// <paramref name="signaled"/> fires on the GPU. The CPU side
    /// receives an <see cref="AcquireResult"/> describing whether the
    /// caller can use the image (<see cref="AcquireResult.Success"/> /
    /// <see cref="AcquireResult.Suboptimal"/>) or must
    /// <see cref="Recreate"/> first
    /// (<see cref="AcquireResult.OutOfDate"/>).
    /// </summary>
    public AcquireResult AcquireNextImage(
        in BinarySemaphore  signaled,
        TimeSpan            timeout,
        out uint            imageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        imageIndex = 0;
        VkResult r = Vk.vkAcquireNextImageKHR(
            _device.Handle, _handle, timeout.ToVulkanTimeout(),
            signaled.Handle, fence: null,
            (uint*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref imageIndex));

        return r switch
        {
            VkResult.VK_SUCCESS                  => AcquireResult.Success,
            VkResult.VK_SUBOPTIMAL_KHR           => AcquireResult.Suboptimal,
            VkResult.VK_ERROR_OUT_OF_DATE_KHR    => AcquireResult.OutOfDate,
            VkResult.VK_TIMEOUT                  => AcquireResult.Timeout,
            VkResult.VK_NOT_READY                => AcquireResult.NotReady,
            _                                    => throw new VulkanException(r, "vkAcquireNextImageKHR"),
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DestroyViews();
        if (_handle != null)
        {
            Vk.vkDestroySwapchainKHR(_device.Handle, _handle, null);
            _handle = null;
        }
    }

    private void CreateOrRecreate(in SwapchainDescription desc, VkSwapchainKHR_T* oldSwapchain)
    {
        VkPhysicalDevice_T* gpu = _device.PhysicalDevice.Handle;

        // ---- Caps ----
        VkSurfaceCapabilitiesKHR caps = default;
        Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(gpu, _surface.Handle, &caps).ThrowIfFailed();

        // ---- Format ----
        _format = NegotiateFormat(gpu, in desc);

        // ---- Present mode ----
        _presentMode = NegotiatePresentMode(gpu, in desc);

        // ---- Extent ----
        _extent = caps.currentExtent.width != ~0u
            ? caps.currentExtent
            : new VkExtent2D
            {
                width  = Math.Clamp(desc.Width  == 0 ? 1u : desc.Width,
                                    caps.minImageExtent.width,  caps.maxImageExtent.width),
                height = Math.Clamp(desc.Height == 0 ? 1u : desc.Height,
                                    caps.minImageExtent.height, caps.maxImageExtent.height),
            };

        // ---- Image count ----
        uint requested = desc.PreferredImageCount == 0 ? caps.minImageCount + 1u : desc.PreferredImageCount;
        uint maxClamp  = caps.maxImageCount == 0 ? requested : caps.maxImageCount; // 0 means "no limit"
        uint count     = Math.Clamp(requested, caps.minImageCount, maxClamp);

        // ---- Usage ----
        _imageUsage = desc.ImageUsage == 0 ? ImageUsage.ColorAttachment : desc.ImageUsage;

        var ci = new VkSwapchainCreateInfoKHR
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
            surface          = _surface.Handle,
            minImageCount    = count,
            imageFormat      = _format.format,
            imageColorSpace  = _format.colorSpace,
            imageExtent      = _extent,
            imageArrayLayers = 1,
            imageUsage       = (uint)_imageUsage,
            imageSharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
            preTransform     = caps.currentTransform,
            compositeAlpha   = VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR,
            presentMode      = _presentMode,
            clipped          = 1,
            oldSwapchain     = oldSwapchain,
        };
        VkSwapchainKHR_T* raw = null;
        Vk.vkCreateSwapchainKHR(_device.Handle, &ci, null, &raw).ThrowIfFailed();
        _handle = raw;

        LoadImagesAndViews();
    }

    private void LoadImagesAndViews()
    {
        uint imageCount = 0;
        Vk.vkGetSwapchainImagesKHR(_device.Handle, _handle, &imageCount, null).ThrowIfFailed();

        _images = new VkImage_T*[imageCount];
        _views  = new ImageView[imageCount];

        fixed (VkImage_T** p = _images)
            Vk.vkGetSwapchainImagesKHR(_device.Handle, _handle, &imageCount, p).ThrowIfFailed();

        for (uint i = 0; i < imageCount; i++)
        {
            var ci = new VkImageViewCreateInfo
            {
                sType    = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                image    = _images[i],
                viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
                format   = _format.format,
                subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask     = (uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    baseMipLevel   = 0, levelCount = 1,
                    baseArrayLayer = 0, layerCount = 1,
                },
            };
            VkImageView_T* viewRaw = null;
            Vk.vkCreateImageView(_device.Handle, &ci, null, &viewRaw).ThrowIfFailed();
            _views[i] = new ImageView(viewRaw, _device.Handle);
        }
    }

    private void DestroyViews()
    {
        for (int i = 0; i < _views.Length; i++) _views[i].Dispose();
        _views  = [];
        _images = [];
    }

    private VkSurfaceFormatKHR NegotiateFormat(VkPhysicalDevice_T* gpu, in SwapchainDescription desc)
    {
        uint count = 0;
        Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(gpu, _surface.Handle, &count, null).ThrowIfFailed();
        if (count == 0)
            throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
                "Surface reports zero supported formats.");

        Span<VkSurfaceFormatKHR> formats = count <= 16
            ? stackalloc VkSurfaceFormatKHR[(int)count]
            : new VkSurfaceFormatKHR[count];
        fixed (VkSurfaceFormatKHR* p = formats)
            Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(gpu, _surface.Handle, &count, p).ThrowIfFailed();

        if (desc.PreferredFormat.format != VkFormat.VK_FORMAT_UNDEFINED)
        {
            for (int i = 0; i < formats.Length; i++)
            {
                if (formats[i].format     == desc.PreferredFormat.format &&
                    formats[i].colorSpace == desc.PreferredFormat.colorSpace)
                    return formats[i];
            }
        }
        return formats[0];
    }

    private VkPresentModeKHR NegotiatePresentMode(VkPhysicalDevice_T* gpu, in SwapchainDescription desc)
    {
        // FIFO is required by the spec — if the caller didn't request
        // anything else, ship FIFO without an extra round-trip query.
        if (desc.PreferredPresentMode == default ||
            desc.PreferredPresentMode == VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR)
            return VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;

        uint count = 0;
        Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(gpu, _surface.Handle, &count, null).ThrowIfFailed();
        Span<VkPresentModeKHR> modes = count <= 8
            ? stackalloc VkPresentModeKHR[(int)count]
            : new VkPresentModeKHR[count];
        fixed (VkPresentModeKHR* p = modes)
            Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(gpu, _surface.Handle, &count, p).ThrowIfFailed();

        for (int i = 0; i < modes.Length; i++)
            if (modes[i] == desc.PreferredPresentMode) return desc.PreferredPresentMode;

        return VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
    }
}
