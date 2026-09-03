// Implementation of the ahjo_ngx shim. See ahjo_ngx.h for what this library
// is and why it exists.
//
// Two responsibilities live here and nowhere else:
//
//   1. Widening UTF-8 to wchar_t on the way into NGX and narrowing wchar_t to
//      UTF-8 on the way out, so that nothing wide reaches managed code.
//   2. Owning the lifetime of the widened strings, which differs between the
//      init path and the discovery path (see AhjoRetainForever below).
//
// The other 20 exports are re-exported verbatim from NVIDIA's static client
// library by the linker; there is no wrapper code for them, deliberately.
// NVSDK_NGX_VULKAN_EvaluateFeature_C in particular is on the per-frame path
// and must not acquire a shim frame.

// Guard the C++ standard level before anything else, the way
// native/vma/src/vma.cpp does. if constexpr in the wchar_t conversion helpers
// below needs C++17, and a compiler that silently fell back to C++14 would
// take the ill-formed branch instead of discarding it. MSVC only reports
// __cplusplus accurately with /Zc:__cplusplus, hence the _MSVC_LANG arm.
#if !(__cplusplus >= 201703L || _MSVC_LANG >= 201703L)
#error "ahjo_ngx must be compiled as C++17 or later; see CMAKE_CXX_STANDARD in native/ngx/CMakeLists.txt."
#endif

#include "ahjo_ngx.h"

#include <cstddef>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <new>
#include <type_traits>
#include <vector>

namespace
{

// ---------------------------------------------------------------------------
// UTF-8 <-> wchar_t, hand-rolled
// ---------------------------------------------------------------------------
//
// Not mbstowcs: that depends on the process locale, and a library must not
// call setlocale. Not MultiByteToWideChar: Windows-only, and this same
// translation unit builds for linux-x64.
//
// The branch is on sizeof(wchar_t), not on _WIN32. Those coincide today
// (2 bytes on Windows, 4 on Linux) but the property that actually matters is
// the width, and writing the condition as the thing that matters is what
// keeps a future target from silently taking the wrong arm.
//
// Malformed UTF-8 is replaced with U+FFFD rather than rejected. A path this
// deep in is not the place to invent an error code: the caller's string came
// from a .NET string, so it is well-formed by construction, and the failure
// mode worth designing for is "someone passed raw bytes", where a visible
// replacement character in an NGX log line beats a spurious
// FAIL_InvalidParameter from a function whose job is not validation.

constexpr unsigned int kReplacementChar = 0xFFFDu;

// Decodes one code point from utf8 starting at *index, advancing *index past
// it. Returns U+FFFD and advances by one byte on any malformed sequence.
unsigned int AhjoDecodeUtf8(const char* utf8, std::size_t length, std::size_t* index)
{
    const unsigned char* bytes = reinterpret_cast<const unsigned char*>(utf8);
    const std::size_t i = *index;
    const unsigned char lead = bytes[i];

    unsigned int codePoint;
    std::size_t extra;
    unsigned int lowerBound;

    if (lead < 0x80u)
    {
        *index = i + 1;
        return lead;
    }
    else if ((lead & 0xE0u) == 0xC0u) { codePoint = lead & 0x1Fu; extra = 1; lowerBound = 0x80u; }
    else if ((lead & 0xF0u) == 0xE0u) { codePoint = lead & 0x0Fu; extra = 2; lowerBound = 0x800u; }
    else if ((lead & 0xF8u) == 0xF0u) { codePoint = lead & 0x07u; extra = 3; lowerBound = 0x10000u; }
    else { *index = i + 1; return kReplacementChar; }

    if (i + extra >= length)
    {
        *index = i + 1;
        return kReplacementChar;
    }

    for (std::size_t k = 1; k <= extra; ++k)
    {
        const unsigned char continuation = bytes[i + k];
        if ((continuation & 0xC0u) != 0x80u)
        {
            *index = i + 1;
            return kReplacementChar;
        }
        codePoint = (codePoint << 6) | (continuation & 0x3Fu);
    }

    // Overlong encodings, surrogates and out-of-range values are all
    // malformed; consuming the whole sequence is right because it *was* a
    // well-formed sequence shape, just an illegal value.
    if (codePoint < lowerBound || codePoint > 0x10FFFFu || (codePoint >= 0xD800u && codePoint <= 0xDFFFu))
    {
        *index = i + extra + 1;
        return kReplacementChar;
    }

    *index = i + extra + 1;
    return codePoint;
}

// Number of wchar_t units (excluding the terminator) that utf8 needs.
std::size_t AhjoWideLengthOf(const char* utf8)
{
    const std::size_t length = std::strlen(utf8);
    std::size_t index = 0;
    std::size_t units = 0;

    while (index < length)
    {
        const unsigned int codePoint = AhjoDecodeUtf8(utf8, length, &index);
        if constexpr (sizeof(wchar_t) == 2)
        {
            units += (codePoint > 0xFFFFu) ? 2 : 1;   // UTF-16 surrogate pair
        }
        else
        {
            units += 1;                                // UTF-32
        }
    }

    return units;
}

// Widens utf8 into a freshly allocated, NUL-terminated buffer. Returns null
// only when utf8 is null or the allocation failed; the caller decides whether
// that is fatal. Free with AhjoFreeWide.
wchar_t* AhjoUtf8ToWide(const char* utf8)
{
    if (utf8 == nullptr)
    {
        return nullptr;
    }

    const std::size_t length = std::strlen(utf8);
    const std::size_t units = AhjoWideLengthOf(utf8);

    wchar_t* wide = static_cast<wchar_t*>(std::malloc((units + 1) * sizeof(wchar_t)));
    if (wide == nullptr)
    {
        return nullptr;
    }

    std::size_t index = 0;
    std::size_t out = 0;
    while (index < length)
    {
        const unsigned int codePoint = AhjoDecodeUtf8(utf8, length, &index);
        if constexpr (sizeof(wchar_t) == 2)
        {
            if (codePoint > 0xFFFFu)
            {
                const unsigned int adjusted = codePoint - 0x10000u;
                wide[out++] = static_cast<wchar_t>(0xD800u + (adjusted >> 10));
                wide[out++] = static_cast<wchar_t>(0xDC00u + (adjusted & 0x3FFu));
            }
            else
            {
                wide[out++] = static_cast<wchar_t>(codePoint);
            }
        }
        else
        {
            wide[out++] = static_cast<wchar_t>(codePoint);
        }
    }

    wide[out] = static_cast<wchar_t>(0);
    return wide;
}

void AhjoFreeWide(wchar_t* wide)
{
    std::free(wide);
}

// UTF-8 byte length of one code point, excluding any terminator.
unsigned int AhjoUtf8LengthOf(unsigned int codePoint)
{
    if (codePoint < 0x80u)     { return 1; }
    if (codePoint < 0x800u)    { return 2; }
    if (codePoint < 0x10000u)  { return 3; }
    return 4;
}

// Appends one code point to buffer at *out. Assumes the caller has already
// sized the buffer with AhjoUtf8LengthOf.
void AhjoAppendUtf8(char* buffer, unsigned int* out, unsigned int codePoint)
{
    unsigned char* bytes = reinterpret_cast<unsigned char*>(buffer);
    unsigned int i = *out;

    if (codePoint < 0x80u)
    {
        bytes[i++] = static_cast<unsigned char>(codePoint);
    }
    else if (codePoint < 0x800u)
    {
        bytes[i++] = static_cast<unsigned char>(0xC0u | (codePoint >> 6));
        bytes[i++] = static_cast<unsigned char>(0x80u | (codePoint & 0x3Fu));
    }
    else if (codePoint < 0x10000u)
    {
        bytes[i++] = static_cast<unsigned char>(0xE0u | (codePoint >> 12));
        bytes[i++] = static_cast<unsigned char>(0x80u | ((codePoint >> 6) & 0x3Fu));
        bytes[i++] = static_cast<unsigned char>(0x80u | (codePoint & 0x3Fu));
    }
    else
    {
        bytes[i++] = static_cast<unsigned char>(0xF0u | (codePoint >> 18));
        bytes[i++] = static_cast<unsigned char>(0x80u | ((codePoint >> 12) & 0x3Fu));
        bytes[i++] = static_cast<unsigned char>(0x80u | ((codePoint >> 6) & 0x3Fu));
        bytes[i++] = static_cast<unsigned char>(0x80u | (codePoint & 0x3Fu));
    }

    *out = i;
}

// Reads one code point from wide starting at *index, advancing *index. On a
// 2-byte wchar_t this joins surrogate pairs; a lone or malformed surrogate
// becomes U+FFFD. On a 4-byte wchar_t it is a straight read with the same
// range validation.
unsigned int AhjoDecodeWide(const wchar_t* wide, std::size_t* index)
{
    const std::size_t i = *index;
    const unsigned int unit = static_cast<unsigned int>(wide[i]) &
                              (sizeof(wchar_t) == 2 ? 0xFFFFu : 0xFFFFFFFFu);

    if constexpr (sizeof(wchar_t) == 2)
    {
        if (unit >= 0xD800u && unit <= 0xDBFFu)
        {
            const unsigned int low = static_cast<unsigned int>(wide[i + 1]) & 0xFFFFu;
            if (low >= 0xDC00u && low <= 0xDFFFu)
            {
                *index = i + 2;
                return 0x10000u + ((unit - 0xD800u) << 10) + (low - 0xDC00u);
            }

            *index = i + 1;
            return kReplacementChar;
        }

        if (unit >= 0xDC00u && unit <= 0xDFFFu)
        {
            *index = i + 1;
            return kReplacementChar;
        }

        *index = i + 1;
        return unit;
    }
    else
    {
        *index = i + 1;
        if (unit > 0x10FFFFu || (unit >= 0xD800u && unit <= 0xDFFFu))
        {
            return kReplacementChar;
        }
        return unit;
    }
}

// Narrows wide into buffer as NUL-terminated UTF-8 and returns the byte count
// INCLUDING the terminator. When buffer is null or bufferSize is smaller than
// the required size, nothing is written and the required size is returned.
unsigned int AhjoWideToUtf8(const wchar_t* wide, char* buffer, unsigned int bufferSize)
{
    if (wide == nullptr)
    {
        // A one-byte empty string is still a valid answer; reporting 0 would
        // be indistinguishable from an error at the managed call site.
        if (buffer != nullptr && bufferSize >= 1)
        {
            buffer[0] = '\0';
        }
        return 1;
    }

    unsigned int required = 1;   // the terminator
    for (std::size_t index = 0; wide[index] != 0; )
    {
        required += AhjoUtf8LengthOf(AhjoDecodeWide(wide, &index));
    }

    if (buffer == nullptr || bufferSize < required)
    {
        return required;
    }

    unsigned int out = 0;
    for (std::size_t index = 0; wide[index] != 0; )
    {
        AhjoAppendUtf8(buffer, &out, AhjoDecodeWide(wide, &index));
    }
    buffer[out] = '\0';

    return required;
}

// ---------------------------------------------------------------------------
// Init-path retention (spec D8)
// ---------------------------------------------------------------------------
//
// NVSDK_NGX_FeatureCommonInfo carries
// `NVSDK_NGX_FeatureCommonInfo_Internal* InternalData; // Used internally by
// NGX` (nvsdk_ngx_defs.h:405), and the feature-DLL search paths it holds are
// consulted when the feature DLL is loaded - at CreateFeature1 time, long
// after Init returns. Nothing in the SDK documents whether NGX deep-copies
// them.
//
// So the init path allocates EVERY string it hands to NGX on the heap and
// never frees any of them: the widened application data path, the widened
// search-path array and its elements, the NVSDK_NGX_FeatureCommonInfo, and —
// via AhjoRetainedCopyUtf8 — copies of the narrow ProjectId and EngineVersion.
// That last pair matters as much as the rest: they arrive as caller-owned
// pointers valid only for the duration of the P/Invoke, and
// NVSDK_NGX_ProjectIdDescription is documented as identity used for
// over-the-air updates and app-specific customisation, so "read after Init
// returns" is the expected case rather than a hypothetical one. Forwarding
// them verbatim would be the same lifetime bug as forwarding a stack buffer,
// merely without an encoding conversion to make it conspicuous.
//
// The cost is a few hundred bytes per Init call, at setup time; the repo's
// zero-allocation invariant is a per-frame constraint and is not engaged here.
// Retaining is the only construction that is correct without knowing NGX's
// internal copy semantics.
//
// THE DISCOVERY PATH DELIBERATELY DOES NOT DO THIS, and the asymmetry is
// argued rather than incidental. NVSDK_NGX_FeatureDiscoveryInfo is consumed
// entirely within the GetFeature* call: NGX reads the identity and paths to
// decide what to answer, and the extension array it returns points at
// NGX-owned storage, not at ours. Nothing of ours can outlive the call, so
// AhjoDiscoveryStorage frees on return — without which a settings screen that
// re-queries the optimal modes would leak on every query.
//
// This is the one place the design infers rather than measures. If a
// driver-side fault is ever traced to a discovery call, promoting that path to
// the same retention is the fix, and this paragraph is what to re-examine
// first.

std::mutex& AhjoRetentionMutex()
{
    static std::mutex mutex;
    return mutex;
}

std::vector<void*>& AhjoRetainedAllocations()
{
    static std::vector<void*> retained;
    return retained;
}

// noexcept is load-bearing, not decoration.
//
// Every caller of this function is reached from an extern "C" entry point that
// .NET P/Invokes. An exception escaping into managed frames is undefined, and
// MSVC's /EHsc — what CMake gives us here — additionally lets the compiler
// assume extern "C" functions do not throw, so the careful
// NVSDK_NGX_Result_Fail guards in ahjo_ngx_vulkan_init_utf8 would simply never
// run: the caller would get a process abort instead of a result code.
//
// The throwing operation is the vector's geometric regrow. Under real memory
// pressure the caller's std::malloc can succeed while this push_back throws
// std::bad_alloc, which is precisely the case the OOM guards claim to cover.
//
// Swallowing is correct here because this vector is WRITE-ONLY bookkeeping:
// nothing in this translation unit ever reads AhjoRetainedAllocations(). Its
// only purpose is to keep deliberately-leaked pointers reachable for a leak
// checker and to document the D8 retention decision in code. Losing one record
// costs nothing; throwing costs the process.
void AhjoRetainForever(void* allocation) noexcept
{
    if (allocation == nullptr)
    {
        return;
    }

    try
    {
        std::lock_guard<std::mutex> lock(AhjoRetentionMutex());
        AhjoRetainedAllocations().push_back(allocation);
    }
    catch (const std::bad_alloc&)
    {
        // The named case: the regrow failed. The pointer stays alive and
        // leaked, exactly as intended — only the record of it is lost.
    }
    catch (...)
    {
        // std::lock_guard's lock() can throw std::system_error. Without this
        // arm, noexcept would turn that into a terminate — reintroducing the
        // failure mode this function exists to remove.
    }
}

// Copies a NUL-terminated UTF-8 string onto the heap and retains it forever.
//
// The narrow strings need this as much as the widened ones do. AhjoNgxInitInfo
// carries ProjectId and EngineVersion as caller-owned `const char*`, and a
// managed caller's pointer is valid only for the duration of the P/Invoke —
// after that the GC may move or collect the buffer behind it. Handing those
// pointers to NGX verbatim would be exactly the lifetime bug the widening path
// is careful to avoid, just without the encoding conversion to make it
// visible.
//
// Returns null only when utf8 is null or the allocation failed.
const char* AhjoRetainedCopyUtf8(const char* utf8)
{
    if (utf8 == nullptr)
    {
        return nullptr;
    }

    const std::size_t bytes = std::strlen(utf8) + 1;

    char* copy = static_cast<char*>(std::malloc(bytes));
    if (copy == nullptr)
    {
        return nullptr;
    }

    std::memcpy(copy, utf8, bytes);
    AhjoRetainForever(copy);

    return copy;
}

// ---------------------------------------------------------------------------
// Scoped storage for the discovery path
// ---------------------------------------------------------------------------
//
// Small path counts stay on the stack; anything above the threshold falls
// back to the heap. Eight is chosen to cover the shapes a real integration
// uses (an application directory, a couple of DLC or mod roots) without a
// malloc on the common path.

constexpr unsigned int kInlineSearchPaths = 8;

struct AhjoDiscoveryStorage
{
    wchar_t*  ApplicationDataPath = nullptr;
    wchar_t*  InlinePaths[kInlineSearchPaths] = {};
    wchar_t** Paths = nullptr;
    wchar_t** HeapPaths = nullptr;
    unsigned int PathCount = 0;

    AhjoDiscoveryStorage() = default;
    AhjoDiscoveryStorage(const AhjoDiscoveryStorage&) = delete;
    AhjoDiscoveryStorage& operator=(const AhjoDiscoveryStorage&) = delete;

    ~AhjoDiscoveryStorage()
    {
        AhjoFreeWide(ApplicationDataPath);
        for (unsigned int i = 0; i < PathCount; ++i)
        {
            AhjoFreeWide(Paths[i]);
        }
        std::free(HeapPaths);
    }
};

// Widens info's search-path array into storage. Returns false only on
// allocation failure.
bool AhjoWidenSearchPaths(const AhjoNgxInitInfo* info, AhjoDiscoveryStorage& storage)
{
    const unsigned int count = (info->FeatureSearchPaths == nullptr) ? 0u : info->FeatureSearchPathCount;
    if (count == 0)
    {
        storage.Paths = storage.InlinePaths;
        return true;
    }

    if (count <= kInlineSearchPaths)
    {
        storage.Paths = storage.InlinePaths;
    }
    else
    {
        storage.HeapPaths = static_cast<wchar_t**>(std::malloc(count * sizeof(wchar_t*)));
        if (storage.HeapPaths == nullptr)
        {
            storage.Paths = storage.InlinePaths;
            return false;
        }
        storage.Paths = storage.HeapPaths;
    }

    for (unsigned int i = 0; i < count; ++i)
    {
        wchar_t* widened = AhjoUtf8ToWide(info->FeatureSearchPaths[i]);
        if (widened == nullptr)
        {
            return false;   // ~AhjoDiscoveryStorage releases what was widened so far
        }

        storage.Paths[i] = widened;
        storage.PathCount = i + 1;
    }

    return true;
}

// Fills a caller-owned NVSDK_NGX_FeatureCommonInfo from info, pointing its
// path list at storage. The struct is only valid while storage lives.
void AhjoFillCommonInfo(const AhjoNgxInitInfo* info,
                        const AhjoDiscoveryStorage& storage,
                        NVSDK_NGX_FeatureCommonInfo& commonInfo)
{
    std::memset(&commonInfo, 0, sizeof(commonInfo));

    commonInfo.PathListInfo.Path = (storage.PathCount == 0)
        ? nullptr
        : const_cast<wchar_t const* const*>(storage.Paths);
    commonInfo.PathListInfo.Length = storage.PathCount;

    commonInfo.LoggingInfo.LoggingCallback = info->LogCallback;
    commonInfo.LoggingInfo.MinimumLoggingLevel = info->MinimumLoggingLevel;
    commonInfo.LoggingInfo.DisableOtherLoggingSinks = (info->DisableOtherLoggingSinks != 0);
}

// Fills a caller-owned NVSDK_NGX_FeatureDiscoveryInfo from info. SDKVersion is
// stamped by the shim, never by the caller (spec D2).
void AhjoFillDiscoveryInfo(const AhjoNgxInitInfo* info,
                           NVSDK_NGX_Feature featureId,
                           const AhjoDiscoveryStorage& storage,
                           const NVSDK_NGX_FeatureCommonInfo* commonInfo,
                           NVSDK_NGX_FeatureDiscoveryInfo& discoveryInfo)
{
    std::memset(&discoveryInfo, 0, sizeof(discoveryInfo));

    discoveryInfo.SDKVersion = NVSDK_NGX_Version_API;
    discoveryInfo.FeatureID = featureId;
    discoveryInfo.Identifier.IdentifierType = info->IdentifierType;

    if (info->IdentifierType == NVSDK_NGX_Application_Identifier_Type_Project_Id)
    {
        discoveryInfo.Identifier.v.ProjectDesc.ProjectId = info->ProjectId;
        discoveryInfo.Identifier.v.ProjectDesc.EngineType = info->EngineType;
        discoveryInfo.Identifier.v.ProjectDesc.EngineVersion = info->EngineVersion;
    }
    else
    {
        discoveryInfo.Identifier.v.ApplicationId = info->ApplicationId;
    }

    discoveryInfo.ApplicationDataPath = storage.ApplicationDataPath;
    discoveryInfo.FeatureInfo = commonInfo;
}

// The StructSize guard every entry point that takes an AhjoNgxInitInfo runs
// first. A stale ahjo_ngx on a consumer's search path is exactly what this
// catches, and it must be caught before any field past StructSize is read.
bool AhjoInitInfoIsValid(const AhjoNgxInitInfo* info)
{
    return info != nullptr && info->StructSize == static_cast<unsigned int>(sizeof(AhjoNgxInitInfo));
}

// Builds the scoped discovery storage + the two NGX structs. Returns
// NVSDK_NGX_Result_Success when the caller may proceed.
NVSDK_NGX_Result AhjoBuildDiscovery(const AhjoNgxInitInfo* info,
                                    NVSDK_NGX_Feature featureId,
                                    AhjoDiscoveryStorage& storage,
                                    NVSDK_NGX_FeatureCommonInfo& commonInfo,
                                    NVSDK_NGX_FeatureDiscoveryInfo& discoveryInfo)
{
    if (!AhjoInitInfoIsValid(info))
    {
        return NVSDK_NGX_Result_FAIL_InvalidParameter;
    }

    if (info->ApplicationDataPath != nullptr)
    {
        storage.ApplicationDataPath = AhjoUtf8ToWide(info->ApplicationDataPath);
        if (storage.ApplicationDataPath == nullptr)
        {
            return NVSDK_NGX_Result_Fail;
        }
    }

    if (!AhjoWidenSearchPaths(info, storage))
    {
        return NVSDK_NGX_Result_Fail;
    }

    AhjoFillCommonInfo(info, storage, commonInfo);
    AhjoFillDiscoveryInfo(info, featureId, storage, &commonInfo, discoveryInfo);

    return NVSDK_NGX_Result_Success;
}

} // namespace

// ---------------------------------------------------------------------------
// Exports
// ---------------------------------------------------------------------------

extern "C" {

AHJO_NGX_API unsigned int ahjo_ngx_version_api(void)
{
    return static_cast<unsigned int>(NVSDK_NGX_VERSION_API_MACRO);
}

AHJO_NGX_API unsigned int ahjo_ngx_layout(AhjoNgxLayoutId id)
{
    switch (id)
    {
        case AHJO_NGX_LAYOUT_RESOURCE_VK_SIZE:
            return static_cast<unsigned int>(sizeof(NVSDK_NGX_Resource_VK));
        case AHJO_NGX_LAYOUT_RESOURCE_VK_ALIGN:
            return static_cast<unsigned int>(alignof(NVSDK_NGX_Resource_VK));
        case AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_RESOURCE:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_Resource_VK, Resource));
        case AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_TYPE:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_Resource_VK, Type));
        case AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_READWRITE:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_Resource_VK, ReadWrite));

        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_SIZE:
            return static_cast<unsigned int>(sizeof(NVSDK_NGX_ImageViewInfo_VK));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_ALIGN:
            return static_cast<unsigned int>(alignof(NVSDK_NGX_ImageViewInfo_VK));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE_VIEW:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_ImageViewInfo_VK, ImageView));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_ImageViewInfo_VK, Image));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_SUBRESOURCE_RANGE:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_ImageViewInfo_VK, SubresourceRange));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_FORMAT:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_ImageViewInfo_VK, Format));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_WIDTH:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_ImageViewInfo_VK, Width));
        case AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_HEIGHT:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_ImageViewInfo_VK, Height));

        case AHJO_NGX_LAYOUT_BUFFER_INFO_VK_SIZE:
            return static_cast<unsigned int>(sizeof(NVSDK_NGX_BufferInfo_VK));
        case AHJO_NGX_LAYOUT_BUFFER_INFO_VK_ALIGN:
            return static_cast<unsigned int>(alignof(NVSDK_NGX_BufferInfo_VK));
        case AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_BUFFER:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_BufferInfo_VK, Buffer));
        case AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_SIZE_IN_BYTES:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_BufferInfo_VK, SizeInBytes));

        case AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_SIZE:
            return static_cast<unsigned int>(sizeof(NVSDK_NGX_FeatureRequirement));
        case AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_ALIGN:
            return static_cast<unsigned int>(alignof(NVSDK_NGX_FeatureRequirement));
        case AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_FEATURE_SUPPORTED:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_FeatureRequirement, FeatureSupported));
        case AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_HW_ARCHITECTURE:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_FeatureRequirement, MinHWArchitecture));
        case AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_OS_VERSION:
            return static_cast<unsigned int>(offsetof(NVSDK_NGX_FeatureRequirement, MinOSVersion));

        case AHJO_NGX_LAYOUT_INIT_INFO_SIZE:
            return static_cast<unsigned int>(sizeof(AhjoNgxInitInfo));

        // AHJO_NGX_LAYOUT_COUNT is a bound, not a queryable value, so it
        // shares the sentinel with every unrecognised id. The managed suite
        // walks 0 .. COUNT-1 and requires each one to answer.
        case AHJO_NGX_LAYOUT_COUNT:
        default:
            return 0xFFFFFFFFu;
    }
}

AHJO_NGX_API unsigned int ahjo_ngx_result_to_utf8(NVSDK_NGX_Result result, char* buffer, unsigned int bufferSize)
{
    return AhjoWideToUtf8(GetNGXResultAsString(result), buffer, bufferSize);
}

AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_init_utf8(const AhjoNgxInitInfo* info,
                                           VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice device,
                                           PFN_vkGetInstanceProcAddr getInstanceProcAddr,
                                           PFN_vkGetDeviceProcAddr getDeviceProcAddr)
{
    if (!AhjoInitInfoIsValid(info))
    {
        return NVSDK_NGX_Result_FAIL_InvalidParameter;
    }

    // Everything allocated below outlives this call by construction (D8), so
    // it is retained rather than scoped. Failure paths still retain what was
    // already allocated: the alternative is freeing a pointer NGX may have
    // captured, and a few hundred leaked bytes on an init that failed is the
    // cheaper mistake.
    wchar_t* applicationDataPath = nullptr;
    if (info->ApplicationDataPath != nullptr)
    {
        applicationDataPath = AhjoUtf8ToWide(info->ApplicationDataPath);
        if (applicationDataPath == nullptr)
        {
            return NVSDK_NGX_Result_Fail;
        }
        AhjoRetainForever(applicationDataPath);
    }

    const unsigned int pathCount = (info->FeatureSearchPaths == nullptr) ? 0u : info->FeatureSearchPathCount;
    wchar_t** paths = nullptr;
    if (pathCount != 0)
    {
        paths = static_cast<wchar_t**>(std::malloc(pathCount * sizeof(wchar_t*)));
        if (paths == nullptr)
        {
            return NVSDK_NGX_Result_Fail;
        }
        AhjoRetainForever(paths);

        for (unsigned int i = 0; i < pathCount; ++i)
        {
            paths[i] = AhjoUtf8ToWide(info->FeatureSearchPaths[i]);
            if (paths[i] == nullptr)
            {
                return NVSDK_NGX_Result_Fail;
            }
            AhjoRetainForever(paths[i]);
        }
    }

    NVSDK_NGX_FeatureCommonInfo* commonInfo =
        static_cast<NVSDK_NGX_FeatureCommonInfo*>(std::malloc(sizeof(NVSDK_NGX_FeatureCommonInfo)));
    if (commonInfo == nullptr)
    {
        return NVSDK_NGX_Result_Fail;
    }
    AhjoRetainForever(commonInfo);

    std::memset(commonInfo, 0, sizeof(*commonInfo));
    commonInfo->PathListInfo.Path = (pathCount == 0) ? nullptr : const_cast<wchar_t const* const*>(paths);
    commonInfo->PathListInfo.Length = pathCount;
    commonInfo->LoggingInfo.LoggingCallback = info->LogCallback;
    commonInfo->LoggingInfo.MinimumLoggingLevel = info->MinimumLoggingLevel;
    commonInfo->LoggingInfo.DisableOtherLoggingSinks = (info->DisableOtherLoggingSinks != 0);

    if (info->IdentifierType == NVSDK_NGX_Application_Identifier_Type_Project_Id)
    {
        // Copied and retained for the same reason as everything above, not
        // forwarded verbatim: these are caller-owned pointers valid only for
        // the duration of this call, and NVSDK_NGX_ProjectIdDescription is
        // documented as the identity NGX uses for over-the-air updates and
        // app-specific customisation — i.e. plausibly read long after Init
        // returns.
        const char* projectId = AhjoRetainedCopyUtf8(info->ProjectId);
        const char* engineVersion = AhjoRetainedCopyUtf8(info->EngineVersion);

        if ((info->ProjectId != nullptr && projectId == nullptr) ||
            (info->EngineVersion != nullptr && engineVersion == nullptr))
        {
            return NVSDK_NGX_Result_Fail;
        }

        return NVSDK_NGX_VULKAN_Init_with_ProjectID(projectId,
                                                    info->EngineType,
                                                    engineVersion,
                                                    applicationDataPath,
                                                    instance, physicalDevice, device,
                                                    getInstanceProcAddr, getDeviceProcAddr,
                                                    commonInfo,
                                                    NVSDK_NGX_Version_API);
    }

    return NVSDK_NGX_VULKAN_Init(info->ApplicationId,
                                 applicationDataPath,
                                 instance, physicalDevice, device,
                                 getInstanceProcAddr, getDeviceProcAddr,
                                 commonInfo,
                                 NVSDK_NGX_Version_API);
}

AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_requirements_utf8(
    VkInstance instance, VkPhysicalDevice physicalDevice,
    NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
    NVSDK_NGX_FeatureRequirement* outRequirement)
{
    AhjoDiscoveryStorage storage;
    NVSDK_NGX_FeatureCommonInfo commonInfo;
    NVSDK_NGX_FeatureDiscoveryInfo discoveryInfo;

    const NVSDK_NGX_Result prepared =
        AhjoBuildDiscovery(info, featureId, storage, commonInfo, discoveryInfo);
    if (prepared != NVSDK_NGX_Result_Success)
    {
        return prepared;
    }

    return NVSDK_NGX_VULKAN_GetFeatureRequirements(instance, physicalDevice, &discoveryInfo, outRequirement);
}

AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8(
    NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
    unsigned int* outExtensionCount,
    VkExtensionProperties** outExtensionProperties)
{
    AhjoDiscoveryStorage storage;
    NVSDK_NGX_FeatureCommonInfo commonInfo;
    NVSDK_NGX_FeatureDiscoveryInfo discoveryInfo;

    const NVSDK_NGX_Result prepared =
        AhjoBuildDiscovery(info, featureId, storage, commonInfo, discoveryInfo);
    if (prepared != NVSDK_NGX_Result_Success)
    {
        return prepared;
    }

    return NVSDK_NGX_VULKAN_GetFeatureInstanceExtensionRequirements(
        &discoveryInfo, outExtensionCount, outExtensionProperties);
}

AHJO_NGX_API NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8(
    VkInstance instance, VkPhysicalDevice physicalDevice,
    NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
    unsigned int* outExtensionCount,
    VkExtensionProperties** outExtensionProperties)
{
    AhjoDiscoveryStorage storage;
    NVSDK_NGX_FeatureCommonInfo commonInfo;
    NVSDK_NGX_FeatureDiscoveryInfo discoveryInfo;

    const NVSDK_NGX_Result prepared =
        AhjoBuildDiscovery(info, featureId, storage, commonInfo, discoveryInfo);
    if (prepared != NVSDK_NGX_Result_Success)
    {
        return prepared;
    }

    return NVSDK_NGX_VULKAN_GetFeatureDeviceExtensionRequirements(
        instance, physicalDevice, &discoveryInfo, outExtensionCount, outExtensionProperties);
}

} // extern "C"
