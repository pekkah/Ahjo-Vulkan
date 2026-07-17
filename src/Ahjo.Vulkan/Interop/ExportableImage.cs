using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A device-local <c>VkImage</c> backed by a <b>dedicated, exportable</b>
/// <c>VkDeviceMemory</c> — the render target you hand to another GPU API
/// (an Avalonia compositor, a D3D swapchain) for zero-copy sharing. Created
/// with <c>VkExternalMemoryImageCreateInfo</c> on the image and
/// <c>VkExportMemoryAllocateInfo</c> + <c>VkMemoryDedicatedAllocateInfo</c>
/// on the allocation, so <see cref="ExportOpaqueWin32Handle"/> /
/// <see cref="ExportOpaqueFd"/> return an OS handle to memory that holds
/// exactly this one image at offset 0.
/// </summary>
/// <remarks>
/// <para><b>Not a VMA allocation.</b> Every other image in the wrapper comes
/// from <see cref="Allocator.CreateImage"/> (VMA sub-allocates from shared
/// blocks). Export needs a whole <c>VkDeviceMemory</c> per image at offset 0
/// and VMA cannot inject <c>VkExportMemoryAllocateInfo</c> into a
/// sub-allocation's memory, so this path allocates dedicated memory directly
/// with <c>vkAllocateMemory</c>. <see cref="Dispose"/> therefore calls
/// <c>vkDestroyImage</c> + <c>vkFreeMemory</c>, not <c>vmaDestroyImage</c>.</para>
/// <para><b>Extensions.</b> The owning <see cref="Device"/> must have enabled
/// the handle-type's export extension:
/// <see cref="VulkanExtensions.KhrExternalMemoryWin32"/> for
/// <see cref="ExternalHandleType.OpaqueWin32"/>,
/// <see cref="VulkanExtensions.KhrExternalMemoryFd"/> for
/// <see cref="ExternalHandleType.OpaqueFd"/>.</para>
/// <para><b>Lifetime.</b> <c>default(ExportableImage)</c> is a legal null
/// handle (<see cref="IsNull"/> is <see langword="true"/>,
/// <see cref="Dispose"/> is a no-op). Double-dispose is undefined behavior —
/// the struct can't null its own <c>readonly</c> fields. Each
/// <c>Export*</c> call creates a new OS handle/fd the caller owns and must
/// close (<c>CloseHandle</c> / <c>close</c>) once every importer is done;
/// disposing this <see cref="ExportableImage"/> does not invalidate handles
/// already exported.</para>
/// </remarks>
public readonly unsafe struct ExportableImage : IDisposable
{
    /// <summary>
    /// The wrapped image as a borrowed <see cref="Image"/> (owns no
    /// lifetime) — pass it to <see cref="Image.CreateView"/>, the command
    /// recorder, and dynamic-rendering attachments exactly like any other
    /// image. Its own <see cref="Image.Dispose"/> is a no-op; this
    /// <see cref="ExportableImage"/> owns the real teardown.
    /// </summary>
    public readonly Image Image;

    internal readonly VkDeviceMemory_T* Memory;
    internal readonly VkDevice_T*       DeviceHandle;

    /// <summary>Size in bytes of the dedicated allocation backing the image (its whole <c>VkDeviceMemory</c>).</summary>
    public readonly ulong MemorySize;

    /// <summary>The resolved handle type the memory was made exportable for (never <see cref="ExternalHandleType.Auto"/>).</summary>
    public readonly ExternalHandleType HandleType;

    internal ExportableImage(Image image, VkDeviceMemory_T* memory, VkDevice_T* device, ulong memorySize, ExternalHandleType handleType)
    {
        Image        = image;
        Memory       = memory;
        DeviceHandle = device;
        MemorySize   = memorySize;
        HandleType   = handleType;
    }

    public bool IsNull => Image.Handle == null;

    /// <summary>
    /// Byte offset of the image within its exported <c>VkDeviceMemory</c>.
    /// Always <c>0</c> — the allocation is dedicated to this one image — but
    /// exposed because compositor import APIs (Avalonia's
    /// <c>PlatformGraphicsExternalImageProperties</c>) take an offset.
    /// </summary>
    public ulong MemoryOffset => 0;

    /// <summary>
    /// Creates an exportable image from <paramref name="image"/>.
    /// </summary>
    /// <param name="device">The device that owns the image; must have enabled the handle type's export extension.</param>
    /// <param name="image">Image shape. <see cref="ImageDescription.Usage"/> should include the attachment/transfer bits the render pass needs.</param>
    /// <param name="handleType">Handle flavor to make the memory exportable for; <see cref="ExternalHandleType.Auto"/> picks the platform default.</param>
    public static ExportableImage Create(Device device, in ImageDescription image, ExternalHandleType handleType = ExternalHandleType.Auto)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (image.Width == 0 || image.Height == 0)
            throw new ArgumentException("ExportableImage requires non-zero Width and Height.", nameof(image));

        ExternalHandleType resolved = handleType.Resolve();
        var memoryHandleType = resolved.ToMemoryFlag();

        // VkExternalMemoryImageCreateInfo on the image declares which handle
        // types its memory may be exported as; it must agree with the
        // VkExportMemoryAllocateInfo below.
        var externalImageInfo = new VkExternalMemoryImageCreateInfo
        {
            sType       = VkStructureType.VK_STRUCTURE_TYPE_EXTERNAL_MEMORY_IMAGE_CREATE_INFO,
            handleTypes = (uint)memoryHandleType,
        };

        VkImageCreateInfo ici = default;
        ici.sType                 = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
        ici.pNext                 = &externalImageInfo;
        ici.flags                 = (uint)image.Flags;
        ici.imageType             = image.ImageType;
        ici.format                = image.Format;
        ici.extent                = new VkExtent3D { width = image.Width, height = image.Height, depth = image.Depth };
        ici.mipLevels             = image.MipLevels;
        ici.arrayLayers           = image.ArrayLayers;
        ici.samples               = image.Samples;
        ici.tiling                = image.Tiling;
        ici.usage                 = (uint)image.Usage;
        ici.sharingMode           = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
        ici.queueFamilyIndexCount = 0;
        ici.pQueueFamilyIndices   = null;
        ici.initialLayout         = image.InitialLayout;

        VkImage_T*        rawImage  = null;
        VkDeviceMemory_T* rawMemory = null;
        try
        {
            Vk.vkCreateImage(device.Handle, &ici, null, &rawImage).ThrowIfFailed();

            VkMemoryRequirements req;
            Vk.vkGetImageMemoryRequirements(device.Handle, rawImage, &req);

            uint memoryTypeIndex = FindDeviceLocalMemoryType(device.PhysicalDevice.Handle, req.memoryTypeBits);

            // Export info chained after the dedicated-allocate info; both are
            // valid pNext extensions of VkMemoryAllocateInfo. Dedicated so the
            // exported VkDeviceMemory maps to exactly this image at offset 0.
            var exportInfo = new VkExportMemoryAllocateInfo
            {
                sType       = VkStructureType.VK_STRUCTURE_TYPE_EXPORT_MEMORY_ALLOCATE_INFO,
                handleTypes = (uint)memoryHandleType,
            };
            var dedicatedInfo = new VkMemoryDedicatedAllocateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_DEDICATED_ALLOCATE_INFO,
                pNext = &exportInfo,
                image = rawImage,
            };
            var mai = new VkMemoryAllocateInfo
            {
                sType           = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
                pNext           = &dedicatedInfo,
                allocationSize  = req.size,
                memoryTypeIndex = memoryTypeIndex,
            };
            Vk.vkAllocateMemory(device.Handle, &mai, null, &rawMemory).ThrowIfFailed();
            Vk.vkBindImageMemory(device.Handle, rawImage, rawMemory, 0).ThrowIfFailed();

            // Borrowed Image (no owning Allocator → OwnsHandle false, Dispose a
            // no-op, never tracked by HandleRegistry). It carries the real
            // format/extent so CreateView and the recorder work unchanged; this
            // ExportableImage owns the destroy.
            var borrowed = new Image(
                rawImage, null, default,
                image.Format, image.Width, image.Height, image.Depth,
                image.MipLevels, image.ArrayLayers, image.Usage,
                null);

            return new ExportableImage(borrowed, rawMemory, device.Handle, req.size, resolved);
        }
        catch
        {
            if (rawMemory != null) Vk.vkFreeMemory(device.Handle, rawMemory, null);
            if (rawImage  != null) Vk.vkDestroyImage(device.Handle, rawImage, null);
            throw;
        }
    }

    /// <summary>
    /// Exports the backing memory as a Win32 NT <c>HANDLE</c> via
    /// <c>vkGetMemoryWin32HandleKHR</c>. The caller owns the returned handle
    /// and must <c>CloseHandle</c> it once every importer is finished.
    /// Valid only when <see cref="HandleType"/> is
    /// <see cref="ExternalHandleType.OpaqueWin32"/>.
    /// </summary>
    public nint ExportOpaqueWin32Handle()
    {
        ThrowIfNull();
        if (HandleType != ExternalHandleType.OpaqueWin32)
            throw new InvalidOperationException(
                $"ExportOpaqueWin32Handle requires HandleType OpaqueWin32; this image is {HandleType}.");

        var info = new VkMemoryGetWin32HandleInfoKHR
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_GET_WIN32_HANDLE_INFO_KHR,
            memory     = Memory,
            handleType = VkExternalMemoryHandleTypeFlagBits.VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_WIN32_BIT,
        };
        var vkGetMemoryWin32HandleKHR =
            (delegate* unmanaged[Stdcall]<VkDevice_T*, VkMemoryGetWin32HandleInfoKHR*, nint*, VkResult>)
            DeviceExtensionProcs.Load(DeviceHandle, "vkGetMemoryWin32HandleKHR"u8);
        nint handle = 0;
        vkGetMemoryWin32HandleKHR(DeviceHandle, &info, &handle).ThrowIfFailed();
        return handle;
    }

    /// <summary>
    /// Exports the backing memory as a POSIX file descriptor via
    /// <c>vkGetMemoryFdKHR</c>. The caller owns the returned fd and must
    /// <c>close</c> it once every importer is finished. Valid only when
    /// <see cref="HandleType"/> is <see cref="ExternalHandleType.OpaqueFd"/>.
    /// </summary>
    public int ExportOpaqueFd()
    {
        ThrowIfNull();
        if (HandleType != ExternalHandleType.OpaqueFd)
            throw new InvalidOperationException(
                $"ExportOpaqueFd requires HandleType OpaqueFd; this image is {HandleType}.");

        var info = new VkMemoryGetFdInfoKHR
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_GET_FD_INFO_KHR,
            memory     = Memory,
            handleType = VkExternalMemoryHandleTypeFlagBits.VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_FD_BIT,
        };
        var vkGetMemoryFdKHR =
            (delegate* unmanaged[Stdcall]<VkDevice_T*, VkMemoryGetFdInfoKHR*, int*, VkResult>)
            DeviceExtensionProcs.Load(DeviceHandle, "vkGetMemoryFdKHR"u8);
        int fd = -1;
        vkGetMemoryFdKHR(DeviceHandle, &info, &fd).ThrowIfFailed();
        return fd;
    }

    public void Dispose()
    {
        if (Image.Handle == null) return;
        Vk.vkDestroyImage(DeviceHandle, Image.Handle, null);
        if (Memory != null) Vk.vkFreeMemory(DeviceHandle, Memory, null);
    }

    private void ThrowIfNull()
    {
        if (IsNull)
            throw new InvalidOperationException("ExportableImage is a null handle.");
    }

    // Prefers a DEVICE_LOCAL memory type among the ones the image accepts —
    // exported render targets live on the GPU. vkGetImageMemoryRequirements
    // guarantees at least one bit is set, and the Vulkan spec guarantees at
    // least one DEVICE_LOCAL memory type exists on every conformant device.
    private static uint FindDeviceLocalMemoryType(VkPhysicalDevice_T* physicalDevice, uint typeBits)
    {
        VkPhysicalDeviceMemoryProperties props;
        Vk.vkGetPhysicalDeviceMemoryProperties(physicalDevice, &props);

        VkMemoryType* types = &props.memoryTypes.e0;
        for (uint i = 0; i < props.memoryTypeCount; i++)
        {
            bool accepted     = (typeBits & (1u << (int)i)) != 0;
            bool deviceLocal  = (types[i].propertyFlags & (uint)VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT) != 0;
            if (accepted && deviceLocal)
                return i;
        }

        throw new VulkanException(VkResult.VK_ERROR_FEATURE_NOT_PRESENT,
            "No DEVICE_LOCAL memory type accepts this exportable image.");
    }
}
