// Minimal repro: spReflectionTypeLayout_getBindingRangeImageFormat crashes on a
// SLANG_BINDING_TYPE_EXISTENTIAL_VALUE binding range (Slang v2026.14.1).
//
// Compiled as C++ only because slang.h is not C-clean; every Slang call below is
// a plain C entry point from slang-deprecated.h.
//
//   Windows: cl /nologo /EHsc /std:c++17 /I <slang>/include repro_min.cpp \
//                /link <slang>/lib/slang.lib
//   Linux:   g++ -std=c++17 -I <slang>/include repro_min.cpp \
//                -L <slang>/lib -lslang -o repro_min
#include <slang.h>
#include <stdio.h>

static const char* kSource =
    "interface ISurface { float4 shade(float3 n); };\n"
    "struct Glossy : ISurface { float4 tint; float4 shade(float3 n) { return tint; } };\n"
    "ISurface makeGlossy() { Glossy g; g.tint = float4(1.0); return g; }\n"
    "ParameterBlock<ISurface> gSurface;\n"
    "[shader(\"fragment\")]\n"
    "float4 fragmentMain(float3 n : NORMAL) : SV_Target\n"
    "{ return gSurface.shade(n) + makeGlossy().shade(n); }\n";

int main(void)
{
    setvbuf(stdout, NULL, _IONBF, 0);

    SlangSession* session = spCreateSession(NULL);
    SlangCompileRequest* request = spCreateCompileRequest(session);
    int target = spAddCodeGenTarget(request, SLANG_SPIRV);
    spSetTargetProfile(request, target, spFindProfile(session, "spirv_1_5"));
    int tu = spAddTranslationUnit(request, SLANG_SOURCE_LANGUAGE_SLANG, "surface");
    spAddTranslationUnitSourceString(request, tu, "surface.slang", kSource);
    printf("spCompile -> 0x%08X\n%s", (unsigned)spCompile(request), spGetDiagnosticOutput(request));

    // Global scope -> the ParameterBlock binding range -> the block's element scope.
    SlangReflectionTypeLayout* global =
        spReflection_getGlobalParamsTypeLayout(spGetReflection(request));
    SlangReflectionTypeLayout* element = spReflectionTypeLayout_GetElementTypeLayout(
        spReflectionTypeLayout_getBindingRangeLeafTypeLayout(global, 0));

    // The element scope of ParameterBlock<ISurface> reports one binding range,
    // of type SLANG_BINDING_TYPE_EXISTENTIAL_VALUE, whose leaf variable is null.
    printf("element scope kind          = %d (SLANG_TYPE_KIND_INTERFACE = %d)\n",
           (int)spReflectionTypeLayout_getKind(element), (int)SLANG_TYPE_KIND_INTERFACE);
    printf("binding range count         = %d\n",
           (int)spReflectionTypeLayout_getBindingRangeCount(element));
    printf("getBindingRangeType(0)      = %d (SLANG_BINDING_TYPE_EXISTENTIAL_VALUE = %d)\n",
           (int)spReflectionTypeLayout_getBindingRangeType(element, 0),
           (int)SLANG_BINDING_TYPE_EXISTENTIAL_VALUE);
    printf("getBindingRangeBindingCount = %d\n",
           (int)spReflectionTypeLayout_getBindingRangeBindingCount(element, 0));
    printf("getBindingRangeLeafVariable = %p   <-- null\n",
           (void*)spReflectionTypeLayout_getBindingRangeLeafVariable(element, 0));
    printf("getBindingRangeLeafTypeLayout = %p\n",
           (void*)spReflectionTypeLayout_getBindingRangeLeafTypeLayout(element, 0));

    printf("calling getBindingRangeImageFormat ...\n");
    SlangImageFormat format = spReflectionTypeLayout_getBindingRangeImageFormat(element, 0);
    printf("returned %d (not reached on v2026.14.1)\n", (int)format);
    return 0;
}
