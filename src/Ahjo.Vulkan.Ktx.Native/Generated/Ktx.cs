using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Ktx.Native;

public static unsafe partial class Ktx
{
    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture_CreateFromStdioStream([NativeTypeName("FILE *")] void* stdioStream, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture_CreateFromNamedFile([NativeTypeName("const char *const")] sbyte* filename, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture_CreateFromMemory([NativeTypeName("const ktx_uint8_t *")] byte* bytes, [NativeTypeName("ktx_size_t")] nuint size, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture_CreateFromStream(ktxStream* stream, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_uint8_t *")]
    public static extern byte* ktxTexture_GetData(ktxTexture* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_uint32_t")]
    public static extern uint ktxTexture_GetRowPitch(ktxTexture* This, [NativeTypeName("ktx_uint32_t")] uint level);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_uint32_t")]
    public static extern uint ktxTexture_GetElementSize(ktxTexture* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_size_t")]
    public static extern nuint ktxTexture_GetDataSize(ktxTexture* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture_IterateLevelFaces(ktxTexture* This, [NativeTypeName("PFNKTXITERCB")] delegate* unmanaged[Cdecl]<int, int, int, int, int, ulong, void*, void*, ktx_error_code_e> iterCb, void* userdata);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_Create([NativeTypeName("const ktxTextureCreateInfo *const")] ktxTextureCreateInfo* createInfo, ktxTextureCreateStorageEnum storageAllocation, ktxTexture1** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_CreateFromStdioStream([NativeTypeName("FILE *")] void* stdioStream, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture1** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_CreateFromNamedFile([NativeTypeName("const char *const")] sbyte* filename, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture1** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_CreateFromMemory([NativeTypeName("const ktx_uint8_t *")] byte* bytes, [NativeTypeName("ktx_size_t")] nuint size, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture1** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_CreateFromStream(ktxStream* stream, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture1** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxTexture1_Destroy(ktxTexture1* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_bool_t")]
    public static extern bool ktxTexture1_NeedsTranscoding(ktxTexture1* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_LoadImageData(ktxTexture1* This, [NativeTypeName("ktx_uint8_t *")] byte* pBuffer, [NativeTypeName("ktx_size_t")] nuint bufSize);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteToStdioStream(ktxTexture1* This, [NativeTypeName("FILE *")] void* dstsstr);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteToNamedFile(ktxTexture1* This, [NativeTypeName("const char *const")] sbyte* dstname);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteToMemory(ktxTexture1* This, [NativeTypeName("ktx_uint8_t **")] byte** bytes, [NativeTypeName("ktx_size_t *")] nuint* size);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteToStream(ktxTexture1* This, ktxStream* dststr);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteKTX2ToStdioStream(ktxTexture1* This, [NativeTypeName("FILE *")] void* dstsstr);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteKTX2ToNamedFile(ktxTexture1* This, [NativeTypeName("const char *const")] sbyte* dstname);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteKTX2ToMemory(ktxTexture1* This, [NativeTypeName("ktx_uint8_t **")] byte** bytes, [NativeTypeName("ktx_size_t *")] nuint* size);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture1_WriteKTX2ToStream(ktxTexture1* This, ktxStream* dststr);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_Create([NativeTypeName("const ktxTextureCreateInfo *const")] ktxTextureCreateInfo* createInfo, ktxTextureCreateStorageEnum storageAllocation, ktxTexture2** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CreateCopy(ktxTexture2* orig, ktxTexture2** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CreateFromStdioStream([NativeTypeName("FILE *")] void* stdioStream, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture2** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CreateFromNamedFile([NativeTypeName("const char *const")] sbyte* filename, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture2** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CreateFromMemory([NativeTypeName("const ktx_uint8_t *")] byte* bytes, [NativeTypeName("ktx_size_t")] nuint size, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture2** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CreateFromStream(ktxStream* stream, [NativeTypeName("ktxTextureCreateFlags")] uint createFlags, ktxTexture2** newTex);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxTexture2_Destroy(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CompressBasis(ktxTexture2* This, [NativeTypeName("ktx_uint32_t")] uint quality);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_DeflateZstd(ktxTexture2* This, [NativeTypeName("ktx_uint32_t")] uint level);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_DeflateZLIB(ktxTexture2* This, [NativeTypeName("ktx_uint32_t")] uint level);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxTexture2_GetComponentInfo(ktxTexture2* This, [NativeTypeName("ktx_uint32_t *")] uint* numComponents, [NativeTypeName("ktx_uint32_t *")] uint* componentByteLength);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_GetImageOffset(ktxTexture2* This, [NativeTypeName("ktx_uint32_t")] uint level, [NativeTypeName("ktx_uint32_t")] uint layer, [NativeTypeName("ktx_uint32_t")] uint faceSlice, [NativeTypeName("ktx_size_t *")] nuint* pOffset);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_uint32_t")]
    public static extern uint ktxTexture2_GetNumComponents(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("khr_df_transfer_e")]
    public static extern _khr_df_transfer_e ktxTexture2_GetTransferFunction_e(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("khr_df_transfer_e")]
    public static extern _khr_df_transfer_e ktxTexture2_GetOETF_e(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_uint32_t")]
    public static extern uint ktxTexture2_GetOETF(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("khr_df_model_e")]
    public static extern _khr_df_model_e ktxTexture2_GetColorModel_e(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_bool_t")]
    public static extern bool ktxTexture2_GetPremultipliedAlpha(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("khr_df_primaries_e")]
    public static extern _khr_df_primaries_e ktxTexture2_GetPrimaries_e(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktx_bool_t")]
    public static extern bool ktxTexture2_NeedsTranscoding(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_SetTransferFunction(ktxTexture2* This, [NativeTypeName("khr_df_transfer_e")] _khr_df_transfer_e tf);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_SetOETF(ktxTexture2* This, [NativeTypeName("khr_df_transfer_e")] _khr_df_transfer_e oetf);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_SetPrimaries(ktxTexture2* This, [NativeTypeName("khr_df_primaries_e")] _khr_df_primaries_e primaries);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_LoadImageData(ktxTexture2* This, [NativeTypeName("ktx_uint8_t *")] byte* pBuffer, [NativeTypeName("ktx_size_t")] nuint bufSize);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_LoadDeflatedImageData(ktxTexture2* This, [NativeTypeName("ktx_uint8_t *")] byte* pBuffer, [NativeTypeName("ktx_size_t")] nuint bufSize);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_WriteToStdioStream(ktxTexture2* This, [NativeTypeName("FILE *")] void* dstsstr);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_WriteToNamedFile(ktxTexture2* This, [NativeTypeName("const char *const")] sbyte* dstname);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_WriteToMemory(ktxTexture2* This, [NativeTypeName("ktx_uint8_t **")] byte** bytes, [NativeTypeName("ktx_size_t *")] nuint* size);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_WriteToStream(ktxTexture2* This, ktxStream* dststr);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CompressAstcEx(ktxTexture2* This, ktxAstcParams* @params);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CompressAstc(ktxTexture2* This, [NativeTypeName("ktx_uint32_t")] uint quality);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_DecodeAstc(ktxTexture2* This);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_CompressBasisEx(ktxTexture2* This, ktxBasisParams* @params);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxTexture2_TranscodeBasis(ktxTexture2* This, ktx_transcode_fmt_e fmt, [NativeTypeName("ktx_transcode_flags")] uint transcodeFlags);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* ktxErrorString(ktx_error_code_e error);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* ktxSupercompressionSchemeString(ktxSupercmpScheme scheme);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* ktxTranscodeFormatString(ktx_transcode_fmt_e format);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_Create([NativeTypeName("ktxHashList **")] ktxKVListEntry*** ppHl);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_CreateCopy([NativeTypeName("ktxHashList **")] ktxKVListEntry*** ppHl, [NativeTypeName("ktxHashList")] ktxKVListEntry* orig);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxHashList_Construct([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHl);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxHashList_ConstructCopy([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHl, [NativeTypeName("ktxHashList")] ktxKVListEntry* orig);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxHashList_Destroy([NativeTypeName("ktxHashList *")] ktxKVListEntry** head);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void ktxHashList_Destruct([NativeTypeName("ktxHashList *")] ktxKVListEntry** head);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_AddKVPair([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("const char *")] sbyte* key, [NativeTypeName("unsigned int")] uint valueLen, [NativeTypeName("const void *")] void* value);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_DeleteEntry([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("ktxHashListEntry *")] ktxKVListEntry* pEntry);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_DeleteKVPair([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("const char *")] sbyte* key);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_FindEntry([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("const char *")] sbyte* key, [NativeTypeName("ktxHashListEntry **")] ktxKVListEntry** ppEntry);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_FindValue([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("const char *")] sbyte* key, [NativeTypeName("unsigned int *")] uint* pValueLen, void** pValue);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    [return: NativeTypeName("ktxHashListEntry *")]
    public static extern ktxKVListEntry* ktxHashList_Next([NativeTypeName("ktxHashListEntry *")] ktxKVListEntry* entry);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_Sort([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_Serialize([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("unsigned int *")] uint* kvdLen, [NativeTypeName("unsigned char **")] byte** kvd);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashList_Deserialize([NativeTypeName("ktxHashList *")] ktxKVListEntry** pHead, [NativeTypeName("unsigned int")] uint kvdLen, void* kvd);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashListEntry_GetKey([NativeTypeName("ktxHashListEntry *")] ktxKVListEntry* This, [NativeTypeName("unsigned int *")] uint* pKeyLen, [NativeTypeName("char **")] sbyte** ppKey);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxHashListEntry_GetValue([NativeTypeName("ktxHashListEntry *")] ktxKVListEntry* This, [NativeTypeName("unsigned int *")] uint* pValueLen, void** ppValue);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintInfoForStdioStream([NativeTypeName("FILE *")] void* stdioStream);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintInfoForNamedFile([NativeTypeName("const char *const")] sbyte* filename);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintInfoForMemory([NativeTypeName("const ktx_uint8_t *")] byte* bytes, [NativeTypeName("ktx_size_t")] nuint size);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX1InfoTextForStream(ktxStream* stream);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoTextForMemory([NativeTypeName("const ktx_uint8_t *")] byte* bytes, [NativeTypeName("ktx_size_t")] nuint size);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoTextForNamedFile([NativeTypeName("const char *const")] sbyte* filename);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoTextForStdioStream([NativeTypeName("FILE *")] void* stdioStream);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoTextForStream(ktxStream* stream);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoJSONForMemory([NativeTypeName("const ktx_uint8_t *")] byte* bytes, [NativeTypeName("ktx_size_t")] nuint size, [NativeTypeName("ktx_uint32_t")] uint base_indent, [NativeTypeName("ktx_uint32_t")] uint indent_width, [NativeTypeName("_Bool")] bool minified);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoJSONForNamedFile([NativeTypeName("const char *const")] sbyte* filename, [NativeTypeName("ktx_uint32_t")] uint base_indent, [NativeTypeName("ktx_uint32_t")] uint indent_width, [NativeTypeName("_Bool")] bool minified);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoJSONForStdioStream([NativeTypeName("FILE *")] void* stdioStream, [NativeTypeName("ktx_uint32_t")] uint base_indent, [NativeTypeName("ktx_uint32_t")] uint indent_width, [NativeTypeName("_Bool")] bool minified);

    [DllImport("ktx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern ktx_error_code_e ktxPrintKTX2InfoJSONForStream(ktxStream* stream, [NativeTypeName("ktx_uint32_t")] uint base_indent, [NativeTypeName("ktx_uint32_t")] uint indent_width, [NativeTypeName("_Bool")] bool minified);
}
