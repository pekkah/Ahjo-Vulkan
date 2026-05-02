// Implementation translation unit for VulkanMemoryAllocator.
//
// VMA is a C++ header-only library. Compiling exactly one source file with
// VMA_IMPLEMENTATION defined materializes its symbols into a translation
// unit; we wrap that in a SHARED library and expose its C ABI to the .NET
// side via DllImport (see src/Ahjo.Vulkan.Vma.Native).
//
// VMA_STATIC_VULKAN_FUNCTIONS = 0
// VMA_DYNAMIC_VULKAN_FUNCTIONS = 1
//   Avoids a link-time dependency on a specific vulkan-1 import library
//   and matches how Ahjo.Vulkan.Native resolves the loader at runtime: the
//   wrapper supplies vkGetInstanceProcAddr / vkGetDeviceProcAddr in
//   VmaVulkanFunctions, and VMA loads everything else via those.

#define VMA_IMPLEMENTATION
#define VMA_STATIC_VULKAN_FUNCTIONS 0
#define VMA_DYNAMIC_VULKAN_FUNCTIONS 1

#include "vk_mem_alloc.h"
