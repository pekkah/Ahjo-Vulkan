/*
 * ahjo_ngx - the Ahjo.Vulkan shim over NVIDIA's NGX (DLSS) Vulkan C API.
 *
 * This file has two jobs, and they pull in the same direction:
 *
 *   1. It is the shim's own header, declaring the seven ahjo_ngx_* functions
 *      native/ngx/src/ahjo_ngx.cpp defines.
 *   2. It is the parse root of tools/generate-ngx.rsp. ClangSharp reads this
 *      file, follows the two includes below, and emits
 *      src/Ahjo.Vulkan.Ngx.Native/Generated/ from what it finds.
 *
 * INCLUDE ORDER MATTERS. The NGX headers do not include Vulkan themselves -
 * nvsdk_ngx_vk.h:69-77 includes only the three nvsdk_ngx_* headers - yet its
 * signatures name VkInstance, VkDevice, VkExtensionProperties and friends. So
 * <vulkan/vulkan.h> has to come first, here, for both the compile and the
 * parse. Do not reorder these two lines.
 *
 * WHY A SHIM AT ALL. The NGX SDK ships a static client library, not a DLL, so
 * something has to link it into a shared library before .NET can P/Invoke
 * anything. That shared library exports 27 symbols and no more (see
 * ahjo_ngx.def / ahjo_ngx.map): 20 re-exported verbatim from NVIDIA's archive,
 * plus the 7 declared below.
 *
 * THE wchar_t BOUNDARY. Four of the five entry points a DLSS integration must
 * call before it can render take wchar_t, transitively through
 * NVSDK_NGX_FeatureDiscoveryInfo and NVSDK_NGX_FeatureCommonInfo. wchar_t is
 * 2 bytes on Windows and 4 on Linux, while every rsp in this repo parses at a
 * fixed --target=x86_64-unknown-linux-gnu so that generated output is a
 * function of the version pin and not of the host. Binding those structs
 * would therefore put a UTF-32 pointer in the public surface that Windows
 * reads as UTF-16 - a silent, Windows-only encoding bug.
 *
 * So the wide-string structs are --exclude'd from the binding surface
 * entirely and AhjoNgxInitInfo below is their UTF-8 mirror. It is declared in
 * OUR header, so the generator emits it from this one definition and there is
 * no cross-language layout agreement to maintain. The shim widens on the way
 * in and narrows on the way out; nothing wide ever reaches managed code.
 *
 * No __declspec(dllexport) appears anywhere in this shim. ahjo_ngx.def is the
 * single Windows export list, and ahjo_ngx.map its Linux counterpart. The
 * seven declarations below do carry AHJO_NGX_API, but on MSVC that expands to
 * nothing — it exists for GCC/Clang, where a version script alone cannot
 * export a symbol this TU compiled as hidden. See the macro's own comment.
 */

#ifndef AHJO_NGX_H
#define AHJO_NGX_H

#include <vulkan/vulkan.h>
#include "nvsdk_ngx_vk.h"

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Export marker for the seven ahjo_ngx_* entry points.
 *
 * On GCC/Clang this is REQUIRED and is not belt-and-braces. This translation
 * unit is compiled -fvisibility=hidden so that nothing of ours leaks into the
 * shared object's dynamic symbol table by accident, and a version script can
 * only RESTRICT visibility — it cannot promote a symbol that was already
 * hidden at compile time. Without this attribute our definitions are STB_LOCAL
 * in the object file, ahjo_ngx.map's `global:` entries cannot resurrect them,
 * and libahjo_ngx.so exports NVIDIA's 20 re-exported symbols (whose objects
 * were compiled without -fvisibility=hidden) while silently dropping all seven
 * of ours. That is a Linux-only EntryPointNotFoundException at the first
 * P/Invoke; issue #216's CI caught it, a Windows-only local build did not.
 *
 * On MSVC this expands to nothing on purpose. ahjo_ngx.def is the single
 * Windows export list — see the header comment there — and adding
 * __declspec(dllexport) here would create a second, competing one.
 */
#if defined(_MSC_VER)
#define AHJO_NGX_API
#elif defined(__GNUC__) || defined(__clang__)
#define AHJO_NGX_API __attribute__((visibility("default")))
#else
#define AHJO_NGX_API
#endif

/*
 * UTF-8 mirror of the identity, path and logging data that
 * NVSDK_NGX_VULKAN_Init_with_ProjectID + NVSDK_NGX_FeatureCommonInfo take on
 * one side and NVSDK_NGX_FeatureDiscoveryInfo takes on the other. One struct
 * serves init and all three discovery calls, because those two carry exactly
 * the same identity, path and logging data.
 *
 * SDKVersion is deliberately absent: the shim stamps NVSDK_NGX_Version_API
 * itself, so the pinned API version is a property of the compiled shim rather
 * than something managed code can get wrong.
 *
 * StructSize is checked on entry by every shim function that takes one; a
 * mismatch returns NVSDK_NGX_Result_FAIL_InvalidParameter. That is what a
 * stale ahjo_ngx.dll on a consumer's search path looks like from managed
 * code, instead of a memory-corrupting read past the end.
 *
 * Measured: sizeof(AhjoNgxInitInfo) == 80 on both win-x64 and linux-x64.
 * AHJO_NGX_LAYOUT_INIT_INFO_SIZE below is how the managed side checks that.
 */
typedef struct AhjoNgxInitInfo
{
    unsigned int                          StructSize;      /* = sizeof(AhjoNgxInitInfo) */
    NVSDK_NGX_Application_Identifier_Type IdentifierType;
    unsigned long long                    ApplicationId;
    const char*                           ProjectId;             /* UTF-8 */
    NVSDK_NGX_EngineType                  EngineType;
    const char*                           EngineVersion;         /* UTF-8 */
    const char*                           ApplicationDataPath;   /* UTF-8 -> widened natively */
    const char* const*                    FeatureSearchPaths;    /* UTF-8 -> widened natively */
    unsigned int                          FeatureSearchPathCount;
    NVSDK_NGX_AppLogCallback              LogCallback;           /* may be NULL */
    NVSDK_NGX_Logging_Level               MinimumLoggingLevel;
    unsigned char                         DisableOtherLoggingSinks;
} AhjoNgxInitInfo;

/*
 * Everything ahjo_ngx_layout can be asked about.
 *
 * Sizes alone cannot verify these layouts. NVSDK_NGX_Resource_VK ends with a
 * 4-byte enum and a 1-byte bool inside 8 bytes of tail, so swapping Type and
 * ReadWrite leaves sizeof at 56 and changes the meaning of every DLSS
 * resource binding. Offsets are load-bearing, not a nicety - hence one id per
 * field rather than one id per struct.
 *
 * AHJO_NGX_LAYOUT_COUNT is generated too, so the managed suite can walk
 * 0 .. COUNT-1 and fail when a native id is added without managed coverage.
 * Append new ids immediately before it; never renumber an existing one.
 */
typedef enum AhjoNgxLayoutId
{
    AHJO_NGX_LAYOUT_RESOURCE_VK_SIZE = 0,
    AHJO_NGX_LAYOUT_RESOURCE_VK_ALIGN,
    AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_RESOURCE,
    AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_TYPE,
    AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_READWRITE,

    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_SIZE,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_ALIGN,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE_VIEW,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_SUBRESOURCE_RANGE,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_FORMAT,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_WIDTH,
    AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_HEIGHT,

    AHJO_NGX_LAYOUT_BUFFER_INFO_VK_SIZE,
    AHJO_NGX_LAYOUT_BUFFER_INFO_VK_ALIGN,
    AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_BUFFER,
    AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_SIZE_IN_BYTES,

    AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_SIZE,
    AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_ALIGN,
    AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_FEATURE_SUPPORTED,
    AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_HW_ARCHITECTURE,
    AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_OS_VERSION,

    AHJO_NGX_LAYOUT_INIT_INFO_SIZE,

    AHJO_NGX_LAYOUT_COUNT
} AhjoNgxLayoutId;

/*
 * The pinned NGX API version the shim was compiled against
 * (NVSDK_NGX_VERSION_API_MACRO, nvsdk_ngx_defs.h:56 - 0x15 at v310.7.0).
 * The managed suite compares it against NVSDK_NGX_Version_API from the
 * committed bindings, which is what proves the shim binary and the generated
 * C# came from the same pinned header.
 */
AHJO_NGX_API unsigned int ahjo_ngx_version_api(void);

/* Returns the queried value, or 0xFFFFFFFF for an unrecognised id. */
AHJO_NGX_API unsigned int ahjo_ngx_layout(AhjoNgxLayoutId id);

/*
 * UTF-8 replacement for GetNGXResultAsString, which returns const wchar_t*.
 * Writes a NUL-terminated UTF-8 string into buffer and returns the byte count
 * INCLUDING the terminator. If buffer is NULL or bufferSize is too small,
 * nothing is written and the required size is returned, so a managed caller
 * can stackalloc and allocate nothing on the common path.
 */
AHJO_NGX_API unsigned int ahjo_ngx_result_to_utf8(NVSDK_NGX_Result result, char* buffer, unsigned int bufferSize);

/*
 * UTF-8 front for NVSDK_NGX_VULKAN_Init / NVSDK_NGX_VULKAN_Init_with_ProjectID.
 * info->IdentifierType selects which one is called; the shim supplies
 * NVSDK_NGX_Version_API as the SDK version either way.
 *
 * The widened strings this builds are retained for the process lifetime - see
 * ahjo_ngx.cpp and the issue #216 spec, D8.
 */
AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_init_utf8(const AhjoNgxInitInfo* info,
                                           VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice device,
                                           PFN_vkGetInstanceProcAddr getInstanceProcAddr,
                                           PFN_vkGetDeviceProcAddr getDeviceProcAddr);

/*
 * The three discovery calls. Each builds a scoped NVSDK_NGX_FeatureDiscoveryInfo
 * from info and releases its widened storage before returning: NGX consumes
 * the struct within the call, and the extension arrays it returns are
 * NGX-owned rather than ours.
 */
AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_requirements_utf8(
                                           VkInstance instance, VkPhysicalDevice physicalDevice,
                                           NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
                                           NVSDK_NGX_FeatureRequirement* outRequirement);

AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8(
                                           NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
                                           unsigned int* outExtensionCount,
                                           VkExtensionProperties** outExtensionProperties);

AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8(
                                           VkInstance instance, VkPhysicalDevice physicalDevice,
                                           NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
                                           unsigned int* outExtensionCount,
                                           VkExtensionProperties** outExtensionProperties);

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* AHJO_NGX_H */
