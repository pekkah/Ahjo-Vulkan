cmake_minimum_required(VERSION 3.21)
project(ahjo_vma LANGUAGES CXX)

# C++17 is a hard requirement on POSIX, not a preference — do not lower it.
#
# VMA picks its aligned-allocation backend by preprocessor branch
# (vk_mem_alloc.h, vma_aligned_alloc): _aligned_malloc on _WIN32,
# posix_memalign on Apple/Android/older-glibc, plain C11 aligned_alloc when
# __cplusplus >= 201703L — and, failing all of those, a stub that trips
# VMA_ASSERT and returns null. Release builds define NDEBUG, which compiles
# VMA_ASSERT to nothing, so the stub degrades to "silently return null" and
# VMA segfaults dereferencing it on the first allocation.
#
# On glibc >= 2.16 (Ubuntu 24.04 et al) _GLIBCXX_HAVE_ALIGNED_ALLOC is
# defined, which disqualifies the posix_memalign branch — so at C++14 a
# Linux Release build lands on the stub and every vmaCreateAllocator call
# SIGSEGVs. Windows never sees this: the _WIN32 branch wins before the
# standard-version check, which is why issue #144 was linux-x64-only.
# native/vma/src/vma.cpp carries a matching #error as the backstop.
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Inputs are configured by the MSBuild caller (see Ahjo.Vulkan.Vma.Native.csproj):
#   AHJO_VMA_HEADERS_DIR  — directory containing vk_mem_alloc.h (VMA tag checkout)
#   AHJO_VULKAN_HEADERS_DIR — directory containing vulkan/vulkan.h (Vulkan-Headers tag checkout)
if(NOT DEFINED AHJO_VMA_HEADERS_DIR)
    message(FATAL_ERROR "AHJO_VMA_HEADERS_DIR is required (path to VMA include/).")
endif()
if(NOT DEFINED AHJO_VULKAN_HEADERS_DIR)
    message(FATAL_ERROR "AHJO_VULKAN_HEADERS_DIR is required (path to Vulkan-Headers include/).")
endif()

add_library(vma SHARED src/vma.cpp)

target_include_directories(vma PRIVATE
    "${AHJO_VMA_HEADERS_DIR}"
    "${AHJO_VULKAN_HEADERS_DIR}"
)

# CMake's platform defaults already produce the names the C# side expects:
# vma.dll on Windows (no prefix), libvma.so on Linux, libvma.dylib on macOS.
# Don't override PREFIX — setting it to "" would strip the "lib" on POSIX.
set_target_properties(vma PROPERTIES
    OUTPUT_NAME "vma"
    POSITION_INDEPENDENT_CODE ON
    # VMA's headers declare its C API as extern "C" but never tag it with
    # __declspec(dllexport). Without this, the resulting DLL on Windows
    # has zero exported symbols and DllImport fails with EntryPointNotFoundException.
    WINDOWS_EXPORT_ALL_SYMBOLS ON
)

if(MSVC)
    # VMA's headers carry a few intentional const-discard / unreferenced-formal
    # diagnostics on MSVC. Don't let upstream warnings block our build.
    target_compile_options(vma PRIVATE /W3 /wd4100 /wd4127 /wd4189 /wd4324)
else()
    target_compile_options(vma PRIVATE -Wall -Wno-unused-parameter -Wno-unused-variable -Wno-missing-field-initializers)
endif()

if(APPLE)
    # rpath so the dylib can be loaded from a sibling directory of the
    # consumer assembly without DYLD_LIBRARY_PATH gymnastics.
    set_target_properties(vma PROPERTIES
        BUILD_WITH_INSTALL_RPATH TRUE
        INSTALL_RPATH "@loader_path"
    )
endif()
