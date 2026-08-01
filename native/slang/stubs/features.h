/*
 * Minimal features.h shim for ClangSharp code generation over slang.h.
 *
 * slang.h:501 does `#include <features.h> // for __GLIBC__ define` inside
 * its SLANG_LINUX_FAMILY branch, which is the branch we take: the parse
 * target is pinned to x86_64-unknown-linux-gnu so the generated output is
 * host-independent (see tools/generate-slang.rsp). glibc's real
 * features.h is a system header we deliberately do not depend on, and the
 * only thing slang.h reads out of it is whether __GLIBC__ is defined —
 * which selects SLANG_HAS_BACKTRACE, a macro with no effect on any
 * declaration ClangSharp emits.
 *
 * This exists so the generated bindings are a function of SlangVersion
 * alone and not of who regenerated them. Not for compilation.
 */
#pragma once

#define __GLIBC__ 2
