#pragma once
#include "defs.h"

#ifdef __cplusplus
extern "C"
{
#endif

uint64_t jbc_krw_kcall(uint64_t fn, ...);
uintptr_t jbc_krw_get_td(void);

typedef enum KmemKind { USERSPACE, KERNEL_HEAP, KERNEL_TEXT } KmemKind;

int jbc_krw_memcpy(uintptr_t dst, uintptr_t src, size_t sz, KmemKind kind);
uint64_t jbc_krw_read64(uintptr_t p, KmemKind kind);
int jbc_krw_write64(uintptr_t p, KmemKind kind, uintptr_t val);

#ifdef __cplusplus
}
#endif
