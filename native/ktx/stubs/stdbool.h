/*
 * Minimal stdbool.h shim for ClangSharp code generation over ktx.h.
 *
 * ktx.h does `typedef bool ktx_bool_t;` in C, so `bool` has to resolve at
 * parse time. C99's stdbool.h is exactly these three macros. Not for
 * compilation.
 */
#pragma once

#define bool  _Bool
#define true  1
#define false 0

#define __bool_true_false_are_defined 1
