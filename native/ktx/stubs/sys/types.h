/*
 * Minimal sys/types.h shim for ClangSharp code generation over ktx.h.
 *
 * ktx.h includes <stdio.h> and <sys/types.h> and then uses size_t and
 * off_t without including <stddef.h> — on a real POSIX host both arrive
 * through these two headers. The generator parses against a fixed
 * x86_64-unknown-linux-gnu target (see tools/generate-ktx.rsp), so the
 * LP64 widths below are the only ones that can be correct here.
 * Not for compilation.
 */
#pragma once

#ifndef _KTX_STUB_SIZE_T
#define _KTX_STUB_SIZE_T
typedef unsigned long size_t;
#endif

#ifndef _KTX_STUB_SSIZE_T
#define _KTX_STUB_SSIZE_T
typedef long ssize_t;
#endif

#ifndef _KTX_STUB_OFF_T
#define _KTX_STUB_OFF_T
typedef long off_t;
#endif
