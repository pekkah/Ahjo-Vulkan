cmake_minimum_required(VERSION 3.21)
project(ahjo_vma LANGUAGES CXX)

# VMA requires C++14 (uses constexpr-if conditionals); engine code can move
# higher independently. Pin here so consumer toolchain defaults don't matter.
set(CMAKE_CXX_STANDARD 14)
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

# Strip the lib prefix on POSIX so the produced filenames are predictable
# from the C# side: vma.dll / libvma.so / libvma.dylib.
set_target_properties(vma PROPERTIES
    OUTPUT_NAME "vma"
    PREFIX ""
    POSITION_INDEPENDENT_CODE ON
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
