/*
 * Minimal inttypes.h shim for ClangSharp code generation over slang.h.
 *
 * slang.h:517 does `#include <inttypes.h>` (guarded only by
 * SLANG_NO_INTTYPES, which we do not define because defining it would
 * change the header's own view of its integer types). libclang needs the
 * include to resolve at parse time; the two typedefs below are all that
 * slang.h actually reaches for from it.
 *
 * This exists so the generated bindings are a function of SlangVersion
 * alone and not of whether the host that regenerated them had a system C
 * toolchain installed. Not for compilation.
 */
#pragma once

#include <stdint.h>

typedef long long          intmax_t;
typedef unsigned long long uintmax_t;
