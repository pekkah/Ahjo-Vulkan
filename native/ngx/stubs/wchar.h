/*
 * Minimal wchar.h shim for ClangSharp code generation over the NGX headers.
 *
 * nvsdk_ngx_vk.h:76 and nvsdk_ngx_defs.h:20 `#include <wchar.h>` in C mode
 * for one reason only: so that `wchar_t` resolves as a type name. Nothing
 * from <wchar.h>'s function surface is used.
 *
 * __WCHAR_TYPE__ is clang's own predefine for the *target*, so this typedef
 * follows --target rather than the host's toolchain — 4 bytes under
 * x86_64-unknown-linux-gnu, which is the fixed parse target every rsp in
 * this repo uses. That is deliberate: generated output must be a function
 * of the version pin, not of whose C toolchain was installed.
 *
 * Nothing wide is allowed to reach the generated tree in any case — the
 * wchar_t-bearing NGX structs are --exclude'd and the shim exposes UTF-8
 * mirrors instead (see tools/generate-ngx.rsp and the issue #216 spec, D2).
 * This stub exists so the parse succeeds, not so wchar_t can be bound.
 *
 * Not for compilation.
 */
#pragma once

typedef __WCHAR_TYPE__ wchar_t;
