#include <stdarg.h>
#include <stdbool.h>
#include "kernelrw.h"

static int k_kcall(void* td, uint64_t** uap)
{
    uint64_t* args = uap[1];
    args[0] = ((uint64_t(*)(uint64_t, uint64_t, uint64_t, uint64_t, uint64_t, uint64_t))args[0])(args[1], args[2], args[3], args[4], args[5], args[6]);
    return 0;
}

asm("kexec:\nmov $11, %rax\nmov %rcx, %r10\nsyscall\nret");
void kexec(void*, void*);

uint64_t jbc_krw_kcall(uint64_t fn, ...)
{
    va_list v;
    va_start(v, fn);
    uint64_t uap[7] = {fn};
    for(int i = 1; i <= 6; i++)
        uap[i] = va_arg(v, uint64_t);
    kexec(k_kcall, uap);
    return uap[0];
}

asm("k_get_td:\nmov %gs:0, %rax\nret");
extern char k_get_td[];

uintptr_t jbc_krw_get_td(void)
{
    return jbc_krw_kcall((uintptr_t)k_get_td);
}

static int have_mira = -1;
static int mira_socket[2];

static int do_check_mira(void)
{
    if(socketpair(AF_UNIX, SOCK_STREAM, 0, mira_socket))
        return 0;
    if(write(mira_socket[1], (void*)jbc_krw_get_td(), 1) == 1)
    {
        char c;
        read(mira_socket[0], &c, 1);
        return 1;
    }
    return 0;
}

static inline bool check_mira(void)
{
    if(have_mira < 0)
        have_mira = do_check_mira();
    return (bool)have_mira;
}

static inline bool check_ptr(uintptr_t p, KmemKind kind)
{
    if(kind == USERSPACE)
        return p < 0x800000000000;
    else if(kind == KERNEL_HEAP)
        return p >= 0xffff800000000000 && p < 0xffffffff00000000;
    else if(kind == KERNEL_TEXT)
        return p >= 0xffffffff00000000 && p < 0xfffffffffffff000;
    else
        return false;
}

static int kcpy_mira(uintptr_t dst, uintptr_t src, size_t sz)
{
    while(sz > 0)
    {
        size_t chk = (sz > 64 ? 64 : sz);
        if(write(mira_socket[1], (void*)src, chk) != chk)
            return -1;
        if(read(mira_socket[0], (void*)dst, chk) != chk)
            return -1;
        dst += chk;
        src += chk;
        sz -= chk;
    }
    return 0;
}

asm("k_kcpy:\nmov %rdx, %rcx\nrep movsb\nret");
extern char k_kcpy[];

int jbc_krw_memcpy(uintptr_t dst, uintptr_t src, size_t sz, KmemKind kind)
{
    if(sz == 0)
        return 0;
    bool u1 = check_ptr(dst, USERSPACE) && check_ptr(dst+sz-1, USERSPACE);
    bool ok1 = check_ptr(dst, kind) && check_ptr(dst+sz-1, kind);
    bool u2 = check_ptr(src, USERSPACE) && check_ptr(src+sz-1, USERSPACE);
    bool ok2 = check_ptr(src, kind) && check_ptr(src+sz-1, kind);
    if(!((u1 || ok1) && (u2 || ok2)))
        return -1;
    if(u1 && u2)
        return -1;
    if(check_mira())
        return kcpy_mira(dst, src, sz);
    jbc_krw_kcall((uintptr_t)k_kcpy, dst, src, sz);
    return 0;
}

uint64_t jbc_krw_read64(uintptr_t p, KmemKind kind)
{
    uint64_t ans;
    if(jbc_krw_memcpy((uintptr_t)&ans, p, sizeof(ans), kind))
        return -1;
    return ans;
}

int jbc_krw_write64(uintptr_t p, KmemKind kind, uintptr_t val)
{
    return jbc_krw_memcpy(p, (uintptr_t)&val, sizeof(val), kind);
}
