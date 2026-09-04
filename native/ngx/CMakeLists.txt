cmake_minimum_required(VERSION 3.21)

# MSVC_RUNTIME_LIBRARY below only takes effect under CMP0091 NEW; with the old
# behaviour the /MT selection is silently ignored and the shim links the
# dynamic CRT against a static-CRT archive. Must be set before project().
# (cmake_minimum_required 3.21 already implies NEW — this is belt and braces,
# and it documents that the property is load-bearing.)
cmake_policy(SET CMP0091 NEW)

project(ahjo_ngx LANGUAGES CXX)

# The MSBuild caller always passes -DCMAKE_BUILD_TYPE=Release. Multi-config
# generators (Visual Studio) ignore it and take the config from
# `cmake --build --config`, so reading it here serves two purposes: it gives
# single-config generators (Ninja, Unix Makefiles — i.e. the linux-x64 lane)
# a sane default, and it stops CMake reporting
# "Manually-specified variables were not used by the project: CMAKE_BUILD_TYPE"
# on Windows, which MSBuild surfaces as a build warning.
get_property(_ahjo_ngx_multi_config GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
if(NOT _ahjo_ngx_multi_config AND NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE "Release" CACHE STRING "Build type" FORCE)
endif()

# C++17, for the same shape of reason native/vma/CMakeLists.txt gives, though
# a different mechanism: src/ahjo_ngx.cpp selects its wchar_t width branch
# with `if constexpr`, which at C++14 is a syntax error rather than a silent
# miscompile. The matching #error in the translation unit is the backstop.
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Inputs configured by the MSBuild caller (see Ahjo.Vulkan.Ngx.Native.csproj):
#   AHJO_NGX_INCLUDE_DIR    — native/ngx/include      (the pinned NGX headers)
#   AHJO_NGX_LIB_DIR        — native/ngx/staged/<rid> (the static client library)
#   AHJO_VULKAN_HEADERS_DIR — native/include          (vulkan/vulkan.h)
if(NOT DEFINED AHJO_NGX_INCLUDE_DIR)
    message(FATAL_ERROR "AHJO_NGX_INCLUDE_DIR is required (path to native/ngx/include).")
endif()
if(NOT DEFINED AHJO_NGX_LIB_DIR)
    message(FATAL_ERROR "AHJO_NGX_LIB_DIR is required (path to native/ngx/staged/<rid>).")
endif()
if(NOT DEFINED AHJO_VULKAN_HEADERS_DIR)
    message(FATAL_ERROR "AHJO_VULKAN_HEADERS_DIR is required (path to Vulkan-Headers include/).")
endif()

# Locate the static client library explicitly rather than letting the linker
# search: the SDK is fetched, not vendored, so "absent" is the normal state of
# a fresh clone and it deserves a message that says what to run.
if(MSVC)
    set(_ahjo_ngx_static "${AHJO_NGX_LIB_DIR}/nvsdk_ngx_s.lib")
else()
    set(_ahjo_ngx_static "${AHJO_NGX_LIB_DIR}/libnvsdk_ngx.a")
endif()

if(NOT EXISTS "${_ahjo_ngx_static}")
    message(FATAL_ERROR
        "NGX static client library not found at ${_ahjo_ngx_static}.\n"
        "Stage the pinned NVIDIA DLSS SDK first:\n"
        "    ./tools/setup-ngx.ps1 -SkipFeatureDll")
endif()

add_library(ahjo_ngx SHARED src/ahjo_ngx.cpp)

# CMake's platform defaults already produce the names the C# side expects:
# ahjo_ngx.dll on Windows (no prefix), libahjo_ngx.so on Linux. Don't override
# PREFIX — setting it to "" would strip the "lib" on POSIX.
set_target_properties(ahjo_ngx PROPERTIES
    OUTPUT_NAME "ahjo_ngx"
    POSITION_INDEPENDENT_CODE ON
)

target_include_directories(ahjo_ngx PRIVATE
    "${AHJO_NGX_INCLUDE_DIR}"
    "${AHJO_VULKAN_HEADERS_DIR}"
)

if(MSVC)
    # The Windows archive is the static-CRT RELEASE build — the object names
    # embedded in it read _out/wddm_amd64_release_static_release_vs2015/…, and
    # its 113 undefined symbols are kernel32/advapi32/user32 plus the static
    # CRT. Linking /MD against it produces CRT-mismatch link errors, so this
    # line is a hard requirement rather than a preference.
    set_property(TARGET ahjo_ngx PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded")

    # The single Windows export list. Naming a symbol there also makes the
    # linker treat it as a root, which is what pulls the corresponding object
    # out of the static archive.
    target_link_options(ahjo_ngx PRIVATE "/DEF:${CMAKE_CURRENT_SOURCE_DIR}/src/ahjo_ngx.def")

    # Measured on nvsdk_ngx_s.lib: zero d3d*, dxgi*, cuda* or nvapi* imports.
    # These three system libraries are the whole of its Windows dependency
    # surface.
    target_link_libraries(ahjo_ngx PRIVATE "${_ahjo_ngx_static}" kernel32 user32 advapi32)

    # /W4 on our own translation unit. The NGX headers carry a few upstream
    # diagnostics of their own; upstream warnings are not our build breaks, so
    # they are silenced individually rather than by lowering the level.
    target_compile_options(ahjo_ngx PRIVATE /W4 /wd4100 /wd4127 /wd4324)
else()
    # Hide everything from our own translation unit by default. The seven
    # ahjo_ngx_* entry points opt back IN individually via AHJO_NGX_API in
    # src/ahjo_ngx.h, which expands to
    # __attribute__((visibility("default"))) here.
    #
    # That attribute is REQUIRED, not redundant with the version script below.
    # A version script can only RESTRICT what is already exported; it cannot
    # promote a symbol the compiler made STB_LOCAL. Without it this flag hides
    # our seven definitions, ahjo_ngx.map's `global:` list cannot resurrect
    # them, and libahjo_ngx.so ships with NVIDIA's 20 re-exported symbols
    # present (their objects were compiled without -fvisibility=hidden) and all
    # seven of ours missing — a Linux-only EntryPointNotFoundException on the
    # first P/Invoke. Windows is unaffected because the .def drives exports
    # there. Do not "simplify" by dropping either the flag or the attribute.
    target_compile_options(ahjo_ngx PRIVATE -fvisibility=hidden -Wall -Wextra -Wno-unused-parameter)

    # --whole-archive is required: without it the linker only pulls objects
    # that resolve an existing undefined symbol, and the 20 verbatim exports
    # have no caller inside this shared object. It is safe here because the
    # archive's measured undefined set is libstdc++, dl*, pthread and libc —
    # no D3D, CUDA or NvAPI import to drag in.
    target_link_libraries(ahjo_ngx PRIVATE
        -Wl,--whole-archive "${_ahjo_ngx_static}" -Wl,--no-whole-archive
        stdc++ dl pthread
    )

    target_link_options(ahjo_ngx PRIVATE
        "-Wl,--version-script=${CMAKE_CURRENT_SOURCE_DIR}/src/ahjo_ngx.map"
    )
endif()
