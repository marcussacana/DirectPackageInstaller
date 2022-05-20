#pragma once
#include <stdint.h>
#include "defs.h"

enum { CWD_KEEP, CWD_ROOT, CWD_RESET };

void jbc_run_as_root(void(*fn)(void* arg), void* arg, int cwd_mode);
int jbc_mount_in_sandbox(const char* system_path, const char* mnt_name);
int jbc_unmount_in_sandbox(const char* mnt_name);
