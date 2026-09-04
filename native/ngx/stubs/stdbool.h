/*
 * Minimal stdbool.h shim for ClangSharp code generation over the NGX headers.
 *
 * nvsdk_ngx_defs_vk.h:18 includes it in C mode so that `bool` resolves for
 * NVSDK_NGX_Resource_VK.ReadWrite; nvsdk_ngx_defs.h does the same for
 * NVSDK_NGX_LoggingInfo.DisableOtherLoggingSinks. C99's stdbool.h is
 * exactly these three macros. Not for compilation.
 */
#pragma once

#define bool  _Bool
#define true  1
#define false 0

#define __bool_true_false_are_defined 1
