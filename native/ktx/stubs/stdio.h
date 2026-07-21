/*
 * Minimal stdio.h shim for ClangSharp code generation over ktx.h.
 *
 * ktx.h includes <stdio.h> solely for FILE, used by the stdio-stream
 * entry points (ktxTexture_WriteToStdioStream and friends). libclang only
 * needs the type to exist and be opaque; the generator emits an IntPtr for
 * it either way. Not for compilation.
 */
#pragma once

typedef struct _KtxStubFile FILE;
