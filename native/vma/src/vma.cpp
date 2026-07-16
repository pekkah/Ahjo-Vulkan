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

// Guard the C++ standard level before including the header. VMA's
// vma_aligned_alloc falls back to a stub that returns null once none of its
// platform branches match, and NDEBUG compiles out the VMA_ASSERT that would
// otherwise flag it — so a Release build at C++14 produces a library that
// SIGSEGVs on the first vmaCreateAllocator instead of failing to build
// (issue #144, linux-x64). Fail loudly at compile time rather than ship that
// again. MSVC only reports __cplusplus accurately with /Zc:__cplusplus, hence
// the _MSVC_LANG arm — the same idiom VMA's own branches use.
#if !(__cplusplus >= 201703L || _MSVC_LANG >= 201703L)
#error "Ahjo.Vulkan.Vma.Native must be compiled as C++17 or later; see CMAKE_CXX_STANDARD in native/vma/CMakeLists.txt."
#endif

#define VMA_IMPLEMENTATION
#define VMA_STATIC_VULKAN_FUNCTIONS 0
#define VMA_DYNAMIC_VULKAN_FUNCTIONS 1

#include "vk_mem_alloc.h"
