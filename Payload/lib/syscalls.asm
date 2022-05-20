section .text
use64

section .text.nosys exec
global nosys
nosys:
mov rax, 0
mov r10, rcx
syscall
jc set_err
ret

section .text.sys_exit exec
global sys_exit
sys_exit:
mov rax, 1
mov r10, rcx
syscall
jc set_err
ret

section .text.fork exec
global fork
fork:
mov rax, 2
mov r10, rcx
syscall
jc set_err
ret

section .text.read exec
global read
read:
mov rax, 3
mov r10, rcx
syscall
jc set_err
ret

section .text.write exec
global write
write:
mov rax, 4
mov r10, rcx
syscall
jc set_err
ret

section .text.open exec
global open
open:
mov rax, 5
mov r10, rcx
syscall
jc set_err
ret

section .text.close exec
global close
close:
mov rax, 6
mov r10, rcx
syscall
jc set_err
ret

section .text.wait4 exec
global wait4
wait4:
mov rax, 7
mov r10, rcx
syscall
jc set_err
ret

section .text.link exec
global link
link:
mov rax, 9
mov r10, rcx
syscall
jc set_err
ret

section .text.unlink exec
global unlink
unlink:
mov rax, 10
mov r10, rcx
syscall
jc set_err
ret

section .text.kexec exec
global kexec
kexec:
mov rax, 11
mov r10, rcx
syscall
jc set_err
ret

section .text.chdir exec
global chdir
chdir:
mov rax, 12
mov r10, rcx
syscall
jc set_err
ret

section .text.fchdir exec
global fchdir
fchdir:
mov rax, 13
mov r10, rcx
syscall
jc set_err
ret

section .text.mknod exec
global mknod
mknod:
mov rax, 14
mov r10, rcx
syscall
jc set_err
ret

section .text.chmod exec
global chmod
chmod:
mov rax, 15
mov r10, rcx
syscall
jc set_err
ret

section .text.chown exec
global chown
chown:
mov rax, 16
mov r10, rcx
syscall
jc set_err
ret

section .text.obreak exec
global obreak
obreak:
mov rax, 17
mov r10, rcx
syscall
jc set_err
ret

section .text.getpid exec
global getpid
getpid:
mov rax, 20
mov r10, rcx
syscall
jc set_err
ret

section .text.mount exec
global mount
mount:
mov rax, 21
mov r10, rcx
syscall
jc set_err
ret

section .text.unmount exec
global unmount
unmount:
mov rax, 22
mov r10, rcx
syscall
jc set_err
ret

section .text.setuid exec
global setuid
setuid:
mov rax, 23
mov r10, rcx
syscall
jc set_err
ret

section .text.getuid exec
global getuid
getuid:
mov rax, 24
mov r10, rcx
syscall
jc set_err
ret

section .text.geteuid exec
global geteuid
geteuid:
mov rax, 25
mov r10, rcx
syscall
jc set_err
ret

section .text.ptrace exec
global ptrace
ptrace:
mov rax, 26
mov r10, rcx
syscall
jc set_err
ret

section .text.recvmsg exec
global recvmsg
recvmsg:
mov rax, 27
mov r10, rcx
syscall
jc set_err
ret

section .text.sendmsg exec
global sendmsg
sendmsg:
mov rax, 28
mov r10, rcx
syscall
jc set_err
ret

section .text.recvfrom exec
global recvfrom
recvfrom:
mov rax, 29
mov r10, rcx
syscall
jc set_err
ret

section .text.accept exec
global accept
accept:
mov rax, 30
mov r10, rcx
syscall
jc set_err
ret

section .text.getpeername exec
global getpeername
getpeername:
mov rax, 31
mov r10, rcx
syscall
jc set_err
ret

section .text.getsockname exec
global getsockname
getsockname:
mov rax, 32
mov r10, rcx
syscall
jc set_err
ret

section .text.access exec
global access
access:
mov rax, 33
mov r10, rcx
syscall
jc set_err
ret

section .text.chflags exec
global chflags
chflags:
mov rax, 34
mov r10, rcx
syscall
jc set_err
ret

section .text.fchflags exec
global fchflags
fchflags:
mov rax, 35
mov r10, rcx
syscall
jc set_err
ret

section .text.sync exec
global sync
sync:
mov rax, 36
mov r10, rcx
syscall
jc set_err
ret

section .text.kill exec
global kill
kill:
mov rax, 37
mov r10, rcx
syscall
jc set_err
ret

section .text.getppid exec
global getppid
getppid:
mov rax, 39
mov r10, rcx
syscall
jc set_err
ret

section .text.dup exec
global dup
dup:
mov rax, 41
mov r10, rcx
syscall
jc set_err
ret

section .text.pipe exec
global pipe
pipe:
mov rax, 42
mov r10, rcx
syscall
jc set_err
ret

section .text.getegid exec
global getegid
getegid:
mov rax, 43
mov r10, rcx
syscall
jc set_err
ret

section .text.profil exec
global profil
profil:
mov rax, 44
mov r10, rcx
syscall
jc set_err
ret

section .text.ktrace exec
global ktrace
ktrace:
mov rax, 45
mov r10, rcx
syscall
jc set_err
ret

section .text.getgid exec
global getgid
getgid:
mov rax, 47
mov r10, rcx
syscall
jc set_err
ret

section .text.getlogin exec
global getlogin
getlogin:
mov rax, 49
mov r10, rcx
syscall
jc set_err
ret

section .text.setlogin exec
global setlogin
setlogin:
mov rax, 50
mov r10, rcx
syscall
jc set_err
ret

section .text.acct exec
global acct
acct:
mov rax, 51
mov r10, rcx
syscall
jc set_err
ret

section .text.sigaltstack exec
global sigaltstack
sigaltstack:
mov rax, 53
mov r10, rcx
syscall
jc set_err
ret

section .text.ioctl exec
global ioctl
ioctl:
mov rax, 54
mov r10, rcx
syscall
jc set_err
ret

section .text.reboot exec
global reboot
reboot:
mov rax, 55
mov r10, rcx
syscall
jc set_err
ret

section .text.revoke exec
global revoke
revoke:
mov rax, 56
mov r10, rcx
syscall
jc set_err
ret

section .text.symlink exec
global symlink
symlink:
mov rax, 57
mov r10, rcx
syscall
jc set_err
ret

section .text.readlink exec
global readlink
readlink:
mov rax, 58
mov r10, rcx
syscall
jc set_err
ret

section .text.execve exec
global execve
execve:
mov rax, 59
mov r10, rcx
syscall
jc set_err
ret

section .text.umask exec
global umask
umask:
mov rax, 60
mov r10, rcx
syscall
jc set_err
ret

section .text.chroot exec
global chroot
chroot:
mov rax, 61
mov r10, rcx
syscall
jc set_err
ret

section .text.msync exec
global msync
msync:
mov rax, 65
mov r10, rcx
syscall
jc set_err
ret

section .text.vfork exec
global vfork
vfork:
mov rax, 66
mov r10, rcx
syscall
jc set_err
ret

section .text.sbrk exec
global sbrk
sbrk:
mov rax, 69
mov r10, rcx
syscall
jc set_err
ret

section .text.sstk exec
global sstk
sstk:
mov rax, 70
mov r10, rcx
syscall
jc set_err
ret

section .text.ovadvise exec
global ovadvise
ovadvise:
mov rax, 72
mov r10, rcx
syscall
jc set_err
ret

section .text.munmap exec
global munmap
munmap:
mov rax, 73
mov r10, rcx
syscall
jc set_err
ret

section .text.mprotect exec
global mprotect
mprotect:
mov rax, 74
mov r10, rcx
syscall
jc set_err
ret

section .text.madvise exec
global madvise
madvise:
mov rax, 75
mov r10, rcx
syscall
jc set_err
ret

section .text.mincore exec
global mincore
mincore:
mov rax, 78
mov r10, rcx
syscall
jc set_err
ret

section .text.getgroups exec
global getgroups
getgroups:
mov rax, 79
mov r10, rcx
syscall
jc set_err
ret

section .text.setgroups exec
global setgroups
setgroups:
mov rax, 80
mov r10, rcx
syscall
jc set_err
ret

section .text.getpgrp exec
global getpgrp
getpgrp:
mov rax, 81
mov r10, rcx
syscall
jc set_err
ret

section .text.setpgid exec
global setpgid
setpgid:
mov rax, 82
mov r10, rcx
syscall
jc set_err
ret

section .text.setitimer exec
global setitimer
setitimer:
mov rax, 83
mov r10, rcx
syscall
jc set_err
ret

section .text.swapon exec
global swapon
swapon:
mov rax, 85
mov r10, rcx
syscall
jc set_err
ret

section .text.getitimer exec
global getitimer
getitimer:
mov rax, 86
mov r10, rcx
syscall
jc set_err
ret

section .text.getdtablesize exec
global getdtablesize
getdtablesize:
mov rax, 89
mov r10, rcx
syscall
jc set_err
ret

section .text.dup2 exec
global dup2
dup2:
mov rax, 90
mov r10, rcx
syscall
jc set_err
ret

section .text.fcntl exec
global fcntl
fcntl:
mov rax, 92
mov r10, rcx
syscall
jc set_err
ret

section .text.select exec
global select
select:
mov rax, 93
mov r10, rcx
syscall
jc set_err
ret

section .text.fsync exec
global fsync
fsync:
mov rax, 95
mov r10, rcx
syscall
jc set_err
ret

section .text.setpriority exec
global setpriority
setpriority:
mov rax, 96
mov r10, rcx
syscall
jc set_err
ret

section .text.socket exec
global socket
socket:
mov rax, 97
mov r10, rcx
syscall
jc set_err
ret

section .text.connect exec
global connect
connect:
mov rax, 98
mov r10, rcx
syscall
jc set_err
ret

section .text.netcontrol exec
global netcontrol
netcontrol:
mov rax, 99
mov r10, rcx
syscall
jc set_err
ret

section .text.getpriority exec
global getpriority
getpriority:
mov rax, 100
mov r10, rcx
syscall
jc set_err
ret

section .text.netabort exec
global netabort
netabort:
mov rax, 101
mov r10, rcx
syscall
jc set_err
ret

section .text.netgetsockinfo exec
global netgetsockinfo
netgetsockinfo:
mov rax, 102
mov r10, rcx
syscall
jc set_err
ret

section .text.bind exec
global bind
bind:
mov rax, 104
mov r10, rcx
syscall
jc set_err
ret

section .text.setsockopt exec
global setsockopt
setsockopt:
mov rax, 105
mov r10, rcx
syscall
jc set_err
ret

section .text.listen exec
global listen
listen:
mov rax, 106
mov r10, rcx
syscall
jc set_err
ret

section .text.socketex exec
global socketex
socketex:
mov rax, 113
mov r10, rcx
syscall
jc set_err
ret

section .text.socketclose exec
global socketclose
socketclose:
mov rax, 114
mov r10, rcx
syscall
jc set_err
ret

section .text.gettimeofday exec
global gettimeofday
gettimeofday:
mov rax, 116
mov r10, rcx
syscall
jc set_err
ret

section .text.getrusage exec
global getrusage
getrusage:
mov rax, 117
mov r10, rcx
syscall
jc set_err
ret

section .text.getsockopt exec
global getsockopt
getsockopt:
mov rax, 118
mov r10, rcx
syscall
jc set_err
ret

section .text.readv exec
global readv
readv:
mov rax, 120
mov r10, rcx
syscall
jc set_err
ret

section .text.writev exec
global writev
writev:
mov rax, 121
mov r10, rcx
syscall
jc set_err
ret

section .text.settimeofday exec
global settimeofday
settimeofday:
mov rax, 122
mov r10, rcx
syscall
jc set_err
ret

section .text.fchown exec
global fchown
fchown:
mov rax, 123
mov r10, rcx
syscall
jc set_err
ret

section .text.fchmod exec
global fchmod
fchmod:
mov rax, 124
mov r10, rcx
syscall
jc set_err
ret

section .text.netgetiflist exec
global netgetiflist
netgetiflist:
mov rax, 125
mov r10, rcx
syscall
jc set_err
ret

section .text.setreuid exec
global setreuid
setreuid:
mov rax, 126
mov r10, rcx
syscall
jc set_err
ret

section .text.setregid exec
global setregid
setregid:
mov rax, 127
mov r10, rcx
syscall
jc set_err
ret

section .text.rename exec
global rename
rename:
mov rax, 128
mov r10, rcx
syscall
jc set_err
ret

section .text.flock exec
global flock
flock:
mov rax, 131
mov r10, rcx
syscall
jc set_err
ret

section .text.mkfifo exec
global mkfifo
mkfifo:
mov rax, 132
mov r10, rcx
syscall
jc set_err
ret

section .text.sendto exec
global sendto
sendto:
mov rax, 133
mov r10, rcx
syscall
jc set_err
ret

section .text.shutdown exec
global shutdown
shutdown:
mov rax, 134
mov r10, rcx
syscall
jc set_err
ret

section .text.socketpair exec
global socketpair
socketpair:
mov rax, 135
mov r10, rcx
syscall
jc set_err
ret

section .text.mkdir exec
global mkdir
mkdir:
mov rax, 136
mov r10, rcx
syscall
jc set_err
ret

section .text.rmdir exec
global rmdir
rmdir:
mov rax, 137
mov r10, rcx
syscall
jc set_err
ret

section .text.utimes exec
global utimes
utimes:
mov rax, 138
mov r10, rcx
syscall
jc set_err
ret

section .text.adjtime exec
global adjtime
adjtime:
mov rax, 140
mov r10, rcx
syscall
jc set_err
ret

section .text.kqueueex exec
global kqueueex
kqueueex:
mov rax, 141
mov r10, rcx
syscall
jc set_err
ret

section .text.setsid exec
global setsid
setsid:
mov rax, 147
mov r10, rcx
syscall
jc set_err
ret

section .text.quotactl exec
global quotactl
quotactl:
mov rax, 148
mov r10, rcx
syscall
jc set_err
ret

section .text.lgetfh exec
global lgetfh
lgetfh:
mov rax, 160
mov r10, rcx
syscall
jc set_err
ret

section .text.getfh exec
global getfh
getfh:
mov rax, 161
mov r10, rcx
syscall
jc set_err
ret

section .text.sysarch exec
global sysarch
sysarch:
mov rax, 165
mov r10, rcx
syscall
jc set_err
ret

section .text.rtprio exec
global rtprio
rtprio:
mov rax, 166
mov r10, rcx
syscall
jc set_err
ret

section .text.freebsd6_pread exec
global freebsd6_pread
freebsd6_pread:
mov rax, 173
mov r10, rcx
syscall
jc set_err
ret

section .text.freebsd6_pwrite exec
global freebsd6_pwrite
freebsd6_pwrite:
mov rax, 174
mov r10, rcx
syscall
jc set_err
ret

section .text.setfib exec
global setfib
setfib:
mov rax, 175
mov r10, rcx
syscall
jc set_err
ret

section .text.ntp_adjtime exec
global ntp_adjtime
ntp_adjtime:
mov rax, 176
mov r10, rcx
syscall
jc set_err
ret

section .text.setgid exec
global setgid
setgid:
mov rax, 181
mov r10, rcx
syscall
jc set_err
ret

section .text.setegid exec
global setegid
setegid:
mov rax, 182
mov r10, rcx
syscall
jc set_err
ret

section .text.seteuid exec
global seteuid
seteuid:
mov rax, 183
mov r10, rcx
syscall
jc set_err
ret

section .text.stat exec
global stat
stat:
mov rax, 188
mov r10, rcx
syscall
jc set_err
ret

section .text.fstat exec
global fstat
fstat:
mov rax, 189
mov r10, rcx
syscall
jc set_err
ret

section .text.lstat exec
global lstat
lstat:
mov rax, 190
mov r10, rcx
syscall
jc set_err
ret

section .text.pathconf exec
global pathconf
pathconf:
mov rax, 191
mov r10, rcx
syscall
jc set_err
ret

section .text.fpathconf exec
global fpathconf
fpathconf:
mov rax, 192
mov r10, rcx
syscall
jc set_err
ret

section .text.getrlimit exec
global getrlimit
getrlimit:
mov rax, 194
mov r10, rcx
syscall
jc set_err
ret

section .text.setrlimit exec
global setrlimit
setrlimit:
mov rax, 195
mov r10, rcx
syscall
jc set_err
ret

section .text.getdirentries exec
global getdirentries
getdirentries:
mov rax, 196
mov r10, rcx
syscall
jc set_err
ret

section .text.freebsd6_mmap exec
global freebsd6_mmap
freebsd6_mmap:
mov rax, 197
mov r10, rcx
syscall
jc set_err
ret

section .text.freebsd6_lseek exec
global freebsd6_lseek
freebsd6_lseek:
mov rax, 199
mov r10, rcx
syscall
jc set_err
ret

section .text.freebsd6_truncate exec
global freebsd6_truncate
freebsd6_truncate:
mov rax, 200
mov r10, rcx
syscall
jc set_err
ret

section .text.freebsd6_ftruncate exec
global freebsd6_ftruncate
freebsd6_ftruncate:
mov rax, 201
mov r10, rcx
syscall
jc set_err
ret

section .text.__sysctl exec
global __sysctl
__sysctl:
mov rax, 202
mov r10, rcx
syscall
jc set_err
ret

section .text.mlock exec
global mlock
mlock:
mov rax, 203
mov r10, rcx
syscall
jc set_err
ret

section .text.munlock exec
global munlock
munlock:
mov rax, 204
mov r10, rcx
syscall
jc set_err
ret

section .text.undelete exec
global undelete
undelete:
mov rax, 205
mov r10, rcx
syscall
jc set_err
ret

section .text.futimes exec
global futimes
futimes:
mov rax, 206
mov r10, rcx
syscall
jc set_err
ret

section .text.getpgid exec
global getpgid
getpgid:
mov rax, 207
mov r10, rcx
syscall
jc set_err
ret

section .text.poll exec
global poll
poll:
mov rax, 209
mov r10, rcx
syscall
jc set_err
ret

section .text.clock_gettime exec
global clock_gettime
clock_gettime:
mov rax, 232
mov r10, rcx
syscall
jc set_err
ret

section .text.clock_settime exec
global clock_settime
clock_settime:
mov rax, 233
mov r10, rcx
syscall
jc set_err
ret

section .text.clock_getres exec
global clock_getres
clock_getres:
mov rax, 234
mov r10, rcx
syscall
jc set_err
ret

section .text.ktimer_create exec
global ktimer_create
ktimer_create:
mov rax, 235
mov r10, rcx
syscall
jc set_err
ret

section .text.ktimer_delete exec
global ktimer_delete
ktimer_delete:
mov rax, 236
mov r10, rcx
syscall
jc set_err
ret

section .text.ktimer_settime exec
global ktimer_settime
ktimer_settime:
mov rax, 237
mov r10, rcx
syscall
jc set_err
ret

section .text.ktimer_gettime exec
global ktimer_gettime
ktimer_gettime:
mov rax, 238
mov r10, rcx
syscall
jc set_err
ret

section .text.ktimer_getoverrun exec
global ktimer_getoverrun
ktimer_getoverrun:
mov rax, 239
mov r10, rcx
syscall
jc set_err
ret

section .text.nanosleep exec
global nanosleep
nanosleep:
mov rax, 240
mov r10, rcx
syscall
jc set_err
ret

section .text.clock_getcpuclockid2 exec
global clock_getcpuclockid2
clock_getcpuclockid2:
mov rax, 247
mov r10, rcx
syscall
jc set_err
ret

section .text.ntp_gettime exec
global ntp_gettime
ntp_gettime:
mov rax, 248
mov r10, rcx
syscall
jc set_err
ret

section .text.minherit exec
global minherit
minherit:
mov rax, 250
mov r10, rcx
syscall
jc set_err
ret

section .text.rfork exec
global rfork
rfork:
mov rax, 251
mov r10, rcx
syscall
jc set_err
ret

section .text.openbsd_poll exec
global openbsd_poll
openbsd_poll:
mov rax, 252
mov r10, rcx
syscall
jc set_err
ret

section .text.issetugid exec
global issetugid
issetugid:
mov rax, 253
mov r10, rcx
syscall
jc set_err
ret

section .text.lchown exec
global lchown
lchown:
mov rax, 254
mov r10, rcx
syscall
jc set_err
ret

section .text.getdents exec
global getdents
getdents:
mov rax, 272
mov r10, rcx
syscall
jc set_err
ret

section .text.lchmod exec
global lchmod
lchmod:
mov rax, 274
mov r10, rcx
syscall
jc set_err
ret

section .text.lutimes exec
global lutimes
lutimes:
mov rax, 276
mov r10, rcx
syscall
jc set_err
ret

section .text.nstat exec
global nstat
nstat:
mov rax, 278
mov r10, rcx
syscall
jc set_err
ret

section .text.nfstat exec
global nfstat
nfstat:
mov rax, 279
mov r10, rcx
syscall
jc set_err
ret

section .text.nlstat exec
global nlstat
nlstat:
mov rax, 280
mov r10, rcx
syscall
jc set_err
ret

section .text.preadv exec
global preadv
preadv:
mov rax, 289
mov r10, rcx
syscall
jc set_err
ret

section .text.pwritev exec
global pwritev
pwritev:
mov rax, 290
mov r10, rcx
syscall
jc set_err
ret

section .text.fhopen exec
global fhopen
fhopen:
mov rax, 298
mov r10, rcx
syscall
jc set_err
ret

section .text.fhstat exec
global fhstat
fhstat:
mov rax, 299
mov r10, rcx
syscall
jc set_err
ret

section .text.modnext exec
global modnext
modnext:
mov rax, 300
mov r10, rcx
syscall
jc set_err
ret

section .text.modstat exec
global modstat
modstat:
mov rax, 301
mov r10, rcx
syscall
jc set_err
ret

section .text.modfnext exec
global modfnext
modfnext:
mov rax, 302
mov r10, rcx
syscall
jc set_err
ret

section .text.modfind exec
global modfind
modfind:
mov rax, 303
mov r10, rcx
syscall
jc set_err
ret

section .text.kldload exec
global kldload
kldload:
mov rax, 304
mov r10, rcx
syscall
jc set_err
ret

section .text.kldunload exec
global kldunload
kldunload:
mov rax, 305
mov r10, rcx
syscall
jc set_err
ret

section .text.kldfind exec
global kldfind
kldfind:
mov rax, 306
mov r10, rcx
syscall
jc set_err
ret

section .text.kldnext exec
global kldnext
kldnext:
mov rax, 307
mov r10, rcx
syscall
jc set_err
ret

section .text.kldstat exec
global kldstat
kldstat:
mov rax, 308
mov r10, rcx
syscall
jc set_err
ret

section .text.kldfirstmod exec
global kldfirstmod
kldfirstmod:
mov rax, 309
mov r10, rcx
syscall
jc set_err
ret

section .text.getsid exec
global getsid
getsid:
mov rax, 310
mov r10, rcx
syscall
jc set_err
ret

section .text.setresuid exec
global setresuid
setresuid:
mov rax, 311
mov r10, rcx
syscall
jc set_err
ret

section .text.setresgid exec
global setresgid
setresgid:
mov rax, 312
mov r10, rcx
syscall
jc set_err
ret

section .text.yield exec
global yield
yield:
mov rax, 321
mov r10, rcx
syscall
jc set_err
ret

section .text.mlockall exec
global mlockall
mlockall:
mov rax, 324
mov r10, rcx
syscall
jc set_err
ret

section .text.munlockall exec
global munlockall
munlockall:
mov rax, 325
mov r10, rcx
syscall
jc set_err
ret

section .text.__getcwd exec
global __getcwd
__getcwd:
mov rax, 326
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_setparam exec
global sched_setparam
sched_setparam:
mov rax, 327
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_getparam exec
global sched_getparam
sched_getparam:
mov rax, 328
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_setscheduler exec
global sched_setscheduler
sched_setscheduler:
mov rax, 329
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_getscheduler exec
global sched_getscheduler
sched_getscheduler:
mov rax, 330
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_yield exec
global sched_yield
sched_yield:
mov rax, 331
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_get_priority_max exec
global sched_get_priority_max
sched_get_priority_max:
mov rax, 332
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_get_priority_min exec
global sched_get_priority_min
sched_get_priority_min:
mov rax, 333
mov r10, rcx
syscall
jc set_err
ret

section .text.sched_rr_get_interval exec
global sched_rr_get_interval
sched_rr_get_interval:
mov rax, 334
mov r10, rcx
syscall
jc set_err
ret

section .text.utrace exec
global utrace
utrace:
mov rax, 335
mov r10, rcx
syscall
jc set_err
ret

section .text.kldsym exec
global kldsym
kldsym:
mov rax, 337
mov r10, rcx
syscall
jc set_err
ret

section .text.jail exec
global jail
jail:
mov rax, 338
mov r10, rcx
syscall
jc set_err
ret

section .text.sigprocmask exec
global sigprocmask
sigprocmask:
mov rax, 340
mov r10, rcx
syscall
jc set_err
ret

section .text.sigsuspend exec
global sigsuspend
sigsuspend:
mov rax, 341
mov r10, rcx
syscall
jc set_err
ret

section .text.sigpending exec
global sigpending
sigpending:
mov rax, 343
mov r10, rcx
syscall
jc set_err
ret

section .text.sigtimedwait exec
global sigtimedwait
sigtimedwait:
mov rax, 345
mov r10, rcx
syscall
jc set_err
ret

section .text.sigwaitinfo exec
global sigwaitinfo
sigwaitinfo:
mov rax, 346
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_get_file exec
global __acl_get_file
__acl_get_file:
mov rax, 347
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_set_file exec
global __acl_set_file
__acl_set_file:
mov rax, 348
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_get_fd exec
global __acl_get_fd
__acl_get_fd:
mov rax, 349
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_set_fd exec
global __acl_set_fd
__acl_set_fd:
mov rax, 350
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_delete_file exec
global __acl_delete_file
__acl_delete_file:
mov rax, 351
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_delete_fd exec
global __acl_delete_fd
__acl_delete_fd:
mov rax, 352
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_aclcheck_file exec
global __acl_aclcheck_file
__acl_aclcheck_file:
mov rax, 353
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_aclcheck_fd exec
global __acl_aclcheck_fd
__acl_aclcheck_fd:
mov rax, 354
mov r10, rcx
syscall
jc set_err
ret

section .text.extattrctl exec
global extattrctl
extattrctl:
mov rax, 355
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_set_file exec
global extattr_set_file
extattr_set_file:
mov rax, 356
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_get_file exec
global extattr_get_file
extattr_get_file:
mov rax, 357
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_delete_file exec
global extattr_delete_file
extattr_delete_file:
mov rax, 358
mov r10, rcx
syscall
jc set_err
ret

section .text.getresuid exec
global getresuid
getresuid:
mov rax, 360
mov r10, rcx
syscall
jc set_err
ret

section .text.getresgid exec
global getresgid
getresgid:
mov rax, 361
mov r10, rcx
syscall
jc set_err
ret

section .text.kqueue exec
global kqueue
kqueue:
mov rax, 362
mov r10, rcx
syscall
jc set_err
ret

section .text.kevent exec
global kevent
kevent:
mov rax, 363
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_set_fd exec
global extattr_set_fd
extattr_set_fd:
mov rax, 371
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_get_fd exec
global extattr_get_fd
extattr_get_fd:
mov rax, 372
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_delete_fd exec
global extattr_delete_fd
extattr_delete_fd:
mov rax, 373
mov r10, rcx
syscall
jc set_err
ret

section .text.__setugid exec
global __setugid
__setugid:
mov rax, 374
mov r10, rcx
syscall
jc set_err
ret

section .text.eaccess exec
global eaccess
eaccess:
mov rax, 376
mov r10, rcx
syscall
jc set_err
ret

section .text.nmount exec
global nmount
nmount:
mov rax, 378
mov r10, rcx
syscall
jc set_err
ret

section .text.mtypeprotect exec
global mtypeprotect
mtypeprotect:
mov rax, 379
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_get_proc exec
global __mac_get_proc
__mac_get_proc:
mov rax, 384
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_set_proc exec
global __mac_set_proc
__mac_set_proc:
mov rax, 385
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_get_fd exec
global __mac_get_fd
__mac_get_fd:
mov rax, 386
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_get_file exec
global __mac_get_file
__mac_get_file:
mov rax, 387
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_set_fd exec
global __mac_set_fd
__mac_set_fd:
mov rax, 388
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_set_file exec
global __mac_set_file
__mac_set_file:
mov rax, 389
mov r10, rcx
syscall
jc set_err
ret

section .text.kenv exec
global kenv
kenv:
mov rax, 390
mov r10, rcx
syscall
jc set_err
ret

section .text.lchflags exec
global lchflags
lchflags:
mov rax, 391
mov r10, rcx
syscall
jc set_err
ret

section .text.uuidgen exec
global uuidgen
uuidgen:
mov rax, 392
mov r10, rcx
syscall
jc set_err
ret

section .text.sendfile exec
global sendfile
sendfile:
mov rax, 393
mov r10, rcx
syscall
jc set_err
ret

section .text.mac_syscall exec
global mac_syscall
mac_syscall:
mov rax, 394
mov r10, rcx
syscall
jc set_err
ret

section .text.getfsstat exec
global getfsstat
getfsstat:
mov rax, 395
mov r10, rcx
syscall
jc set_err
ret

section .text.statfs exec
global statfs
statfs:
mov rax, 396
mov r10, rcx
syscall
jc set_err
ret

section .text.fstatfs exec
global fstatfs
fstatfs:
mov rax, 397
mov r10, rcx
syscall
jc set_err
ret

section .text.fhstatfs exec
global fhstatfs
fhstatfs:
mov rax, 398
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_get_pid exec
global __mac_get_pid
__mac_get_pid:
mov rax, 409
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_get_link exec
global __mac_get_link
__mac_get_link:
mov rax, 410
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_set_link exec
global __mac_set_link
__mac_set_link:
mov rax, 411
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_set_link exec
global extattr_set_link
extattr_set_link:
mov rax, 412
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_get_link exec
global extattr_get_link
extattr_get_link:
mov rax, 413
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_delete_link exec
global extattr_delete_link
extattr_delete_link:
mov rax, 414
mov r10, rcx
syscall
jc set_err
ret

section .text.__mac_execve exec
global __mac_execve
__mac_execve:
mov rax, 415
mov r10, rcx
syscall
jc set_err
ret

section .text.sigaction exec
global sigaction
sigaction:
mov rax, 416
mov r10, rcx
syscall
jc set_err
ret

section .text.sigreturn exec
global sigreturn
sigreturn:
mov rax, 417
mov r10, rcx
syscall
jc set_err
ret

section .text.getcontext exec
global getcontext
getcontext:
mov rax, 421
mov r10, rcx
syscall
jc set_err
ret

section .text.setcontext exec
global setcontext
setcontext:
mov rax, 422
mov r10, rcx
syscall
jc set_err
ret

section .text.swapcontext exec
global swapcontext
swapcontext:
mov rax, 423
mov r10, rcx
syscall
jc set_err
ret

section .text.swapoff exec
global swapoff
swapoff:
mov rax, 424
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_get_link exec
global __acl_get_link
__acl_get_link:
mov rax, 425
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_set_link exec
global __acl_set_link
__acl_set_link:
mov rax, 426
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_delete_link exec
global __acl_delete_link
__acl_delete_link:
mov rax, 427
mov r10, rcx
syscall
jc set_err
ret

section .text.__acl_aclcheck_link exec
global __acl_aclcheck_link
__acl_aclcheck_link:
mov rax, 428
mov r10, rcx
syscall
jc set_err
ret

section .text.sigwait exec
global sigwait
sigwait:
mov rax, 429
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_create exec
global thr_create
thr_create:
mov rax, 430
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_exit exec
global thr_exit
thr_exit:
mov rax, 431
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_self exec
global thr_self
thr_self:
mov rax, 432
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_kill exec
global thr_kill
thr_kill:
mov rax, 433
mov r10, rcx
syscall
jc set_err
ret

section .text._umtx_lock exec
global _umtx_lock
_umtx_lock:
mov rax, 434
mov r10, rcx
syscall
jc set_err
ret

section .text._umtx_unlock exec
global _umtx_unlock
_umtx_unlock:
mov rax, 435
mov r10, rcx
syscall
jc set_err
ret

section .text.jail_attach exec
global jail_attach
jail_attach:
mov rax, 436
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_list_fd exec
global extattr_list_fd
extattr_list_fd:
mov rax, 437
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_list_file exec
global extattr_list_file
extattr_list_file:
mov rax, 438
mov r10, rcx
syscall
jc set_err
ret

section .text.extattr_list_link exec
global extattr_list_link
extattr_list_link:
mov rax, 439
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_suspend exec
global thr_suspend
thr_suspend:
mov rax, 442
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_wake exec
global thr_wake
thr_wake:
mov rax, 443
mov r10, rcx
syscall
jc set_err
ret

section .text.kldunloadf exec
global kldunloadf
kldunloadf:
mov rax, 444
mov r10, rcx
syscall
jc set_err
ret

section .text.audit exec
global audit
audit:
mov rax, 445
mov r10, rcx
syscall
jc set_err
ret

section .text.auditon exec
global auditon
auditon:
mov rax, 446
mov r10, rcx
syscall
jc set_err
ret

section .text.getauid exec
global getauid
getauid:
mov rax, 447
mov r10, rcx
syscall
jc set_err
ret

section .text.setauid exec
global setauid
setauid:
mov rax, 448
mov r10, rcx
syscall
jc set_err
ret

section .text.getaudit exec
global getaudit
getaudit:
mov rax, 449
mov r10, rcx
syscall
jc set_err
ret

section .text.setaudit exec
global setaudit
setaudit:
mov rax, 450
mov r10, rcx
syscall
jc set_err
ret

section .text.getaudit_addr exec
global getaudit_addr
getaudit_addr:
mov rax, 451
mov r10, rcx
syscall
jc set_err
ret

section .text.setaudit_addr exec
global setaudit_addr
setaudit_addr:
mov rax, 452
mov r10, rcx
syscall
jc set_err
ret

section .text.auditctl exec
global auditctl
auditctl:
mov rax, 453
mov r10, rcx
syscall
jc set_err
ret

section .text._umtx_op exec
global _umtx_op
_umtx_op:
mov rax, 454
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_new exec
global thr_new
thr_new:
mov rax, 455
mov r10, rcx
syscall
jc set_err
ret

section .text.sigqueue exec
global sigqueue
sigqueue:
mov rax, 456
mov r10, rcx
syscall
jc set_err
ret

section .text.abort2 exec
global abort2
abort2:
mov rax, 463
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_set_name exec
global thr_set_name
thr_set_name:
mov rax, 464
mov r10, rcx
syscall
jc set_err
ret

section .text.rtprio_thread exec
global rtprio_thread
rtprio_thread:
mov rax, 466
mov r10, rcx
syscall
jc set_err
ret

section .text.sctp_peeloff exec
global sctp_peeloff
sctp_peeloff:
mov rax, 471
mov r10, rcx
syscall
jc set_err
ret

section .text.sctp_generic_sendmsg exec
global sctp_generic_sendmsg
sctp_generic_sendmsg:
mov rax, 472
mov r10, rcx
syscall
jc set_err
ret

section .text.sctp_generic_sendmsg_iov exec
global sctp_generic_sendmsg_iov
sctp_generic_sendmsg_iov:
mov rax, 473
mov r10, rcx
syscall
jc set_err
ret

section .text.sctp_generic_recvmsg exec
global sctp_generic_recvmsg
sctp_generic_recvmsg:
mov rax, 474
mov r10, rcx
syscall
jc set_err
ret

section .text.pread exec
global pread
pread:
mov rax, 475
mov r10, rcx
syscall
jc set_err
ret

section .text.pwrite exec
global pwrite
pwrite:
mov rax, 476
mov r10, rcx
syscall
jc set_err
ret

section .text.mmap exec
global mmap
mmap:
mov rax, 477
mov r10, rcx
syscall
jc set_err
ret

section .text.lseek exec
global lseek
lseek:
mov rax, 478
mov r10, rcx
syscall
jc set_err
ret

section .text.truncate exec
global truncate
truncate:
mov rax, 479
mov r10, rcx
syscall
jc set_err
ret

section .text.ftruncate exec
global ftruncate
ftruncate:
mov rax, 480
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_kill2 exec
global thr_kill2
thr_kill2:
mov rax, 481
mov r10, rcx
syscall
jc set_err
ret

section .text.shm_open exec
global shm_open
shm_open:
mov rax, 482
mov r10, rcx
syscall
jc set_err
ret

section .text.shm_unlink exec
global shm_unlink
shm_unlink:
mov rax, 483
mov r10, rcx
syscall
jc set_err
ret

section .text.cpuset exec
global cpuset
cpuset:
mov rax, 484
mov r10, rcx
syscall
jc set_err
ret

section .text.cpuset_setid exec
global cpuset_setid
cpuset_setid:
mov rax, 485
mov r10, rcx
syscall
jc set_err
ret

section .text.cpuset_getid exec
global cpuset_getid
cpuset_getid:
mov rax, 486
mov r10, rcx
syscall
jc set_err
ret

section .text.cpuset_getaffinity exec
global cpuset_getaffinity
cpuset_getaffinity:
mov rax, 487
mov r10, rcx
syscall
jc set_err
ret

section .text.cpuset_setaffinity exec
global cpuset_setaffinity
cpuset_setaffinity:
mov rax, 488
mov r10, rcx
syscall
jc set_err
ret

section .text.faccessat exec
global faccessat
faccessat:
mov rax, 489
mov r10, rcx
syscall
jc set_err
ret

section .text.fchmodat exec
global fchmodat
fchmodat:
mov rax, 490
mov r10, rcx
syscall
jc set_err
ret

section .text.fchownat exec
global fchownat
fchownat:
mov rax, 491
mov r10, rcx
syscall
jc set_err
ret

section .text.fexecve exec
global fexecve
fexecve:
mov rax, 492
mov r10, rcx
syscall
jc set_err
ret

section .text.fstatat exec
global fstatat
fstatat:
mov rax, 493
mov r10, rcx
syscall
jc set_err
ret

section .text.futimesat exec
global futimesat
futimesat:
mov rax, 494
mov r10, rcx
syscall
jc set_err
ret

section .text.linkat exec
global linkat
linkat:
mov rax, 495
mov r10, rcx
syscall
jc set_err
ret

section .text.mkdirat exec
global mkdirat
mkdirat:
mov rax, 496
mov r10, rcx
syscall
jc set_err
ret

section .text.mkfifoat exec
global mkfifoat
mkfifoat:
mov rax, 497
mov r10, rcx
syscall
jc set_err
ret

section .text.mknodat exec
global mknodat
mknodat:
mov rax, 498
mov r10, rcx
syscall
jc set_err
ret

section .text.openat exec
global openat
openat:
mov rax, 499
mov r10, rcx
syscall
jc set_err
ret

section .text.readlinkat exec
global readlinkat
readlinkat:
mov rax, 500
mov r10, rcx
syscall
jc set_err
ret

section .text.renameat exec
global renameat
renameat:
mov rax, 501
mov r10, rcx
syscall
jc set_err
ret

section .text.symlinkat exec
global symlinkat
symlinkat:
mov rax, 502
mov r10, rcx
syscall
jc set_err
ret

section .text.unlinkat exec
global unlinkat
unlinkat:
mov rax, 503
mov r10, rcx
syscall
jc set_err
ret

section .text.posix_openpt exec
global posix_openpt
posix_openpt:
mov rax, 504
mov r10, rcx
syscall
jc set_err
ret

section .text.jail_get exec
global jail_get
jail_get:
mov rax, 506
mov r10, rcx
syscall
jc set_err
ret

section .text.jail_set exec
global jail_set
jail_set:
mov rax, 507
mov r10, rcx
syscall
jc set_err
ret

section .text.jail_remove exec
global jail_remove
jail_remove:
mov rax, 508
mov r10, rcx
syscall
jc set_err
ret

section .text.closefrom exec
global closefrom
closefrom:
mov rax, 509
mov r10, rcx
syscall
jc set_err
ret

section .text.lpathconf exec
global lpathconf
lpathconf:
mov rax, 513
mov r10, rcx
syscall
jc set_err
ret

section .text.cap_new exec
global cap_new
cap_new:
mov rax, 514
mov r10, rcx
syscall
jc set_err
ret

section .text.cap_getrights exec
global cap_getrights
cap_getrights:
mov rax, 515
mov r10, rcx
syscall
jc set_err
ret

section .text.cap_enter exec
global cap_enter
cap_enter:
mov rax, 516
mov r10, rcx
syscall
jc set_err
ret

section .text.cap_getmode exec
global cap_getmode
cap_getmode:
mov rax, 517
mov r10, rcx
syscall
jc set_err
ret

section .text.pdfork exec
global pdfork
pdfork:
mov rax, 518
mov r10, rcx
syscall
jc set_err
ret

section .text.pdkill exec
global pdkill
pdkill:
mov rax, 519
mov r10, rcx
syscall
jc set_err
ret

section .text.pdgetpid exec
global pdgetpid
pdgetpid:
mov rax, 520
mov r10, rcx
syscall
jc set_err
ret

section .text.pselect exec
global pselect
pselect:
mov rax, 522
mov r10, rcx
syscall
jc set_err
ret

section .text.getloginclass exec
global getloginclass
getloginclass:
mov rax, 523
mov r10, rcx
syscall
jc set_err
ret

section .text.setloginclass exec
global setloginclass
setloginclass:
mov rax, 524
mov r10, rcx
syscall
jc set_err
ret

section .text.rctl_get_racct exec
global rctl_get_racct
rctl_get_racct:
mov rax, 525
mov r10, rcx
syscall
jc set_err
ret

section .text.rctl_get_rules exec
global rctl_get_rules
rctl_get_rules:
mov rax, 526
mov r10, rcx
syscall
jc set_err
ret

section .text.rctl_get_limits exec
global rctl_get_limits
rctl_get_limits:
mov rax, 527
mov r10, rcx
syscall
jc set_err
ret

section .text.rctl_add_rule exec
global rctl_add_rule
rctl_add_rule:
mov rax, 528
mov r10, rcx
syscall
jc set_err
ret

section .text.rctl_remove_rule exec
global rctl_remove_rule
rctl_remove_rule:
mov rax, 529
mov r10, rcx
syscall
jc set_err
ret

section .text.posix_fallocate exec
global posix_fallocate
posix_fallocate:
mov rax, 530
mov r10, rcx
syscall
jc set_err
ret

section .text.posix_fadvise exec
global posix_fadvise
posix_fadvise:
mov rax, 531
mov r10, rcx
syscall
jc set_err
ret

section .text.regmgr_call exec
global regmgr_call
regmgr_call:
mov rax, 532
mov r10, rcx
syscall
jc set_err
ret

section .text.jitshm_create exec
global jitshm_create
jitshm_create:
mov rax, 533
mov r10, rcx
syscall
jc set_err
ret

section .text.jitshm_alias exec
global jitshm_alias
jitshm_alias:
mov rax, 534
mov r10, rcx
syscall
jc set_err
ret

section .text.dl_get_list exec
global dl_get_list
dl_get_list:
mov rax, 535
mov r10, rcx
syscall
jc set_err
ret

section .text.dl_get_info exec
global dl_get_info
dl_get_info:
mov rax, 536
mov r10, rcx
syscall
jc set_err
ret

section .text.dl_notify_event exec
global dl_notify_event
dl_notify_event:
mov rax, 537
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_create exec
global evf_create
evf_create:
mov rax, 538
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_delete exec
global evf_delete
evf_delete:
mov rax, 539
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_open exec
global evf_open
evf_open:
mov rax, 540
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_close exec
global evf_close
evf_close:
mov rax, 541
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_wait exec
global evf_wait
evf_wait:
mov rax, 542
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_trywait exec
global evf_trywait
evf_trywait:
mov rax, 543
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_set exec
global evf_set
evf_set:
mov rax, 544
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_clear exec
global evf_clear
evf_clear:
mov rax, 545
mov r10, rcx
syscall
jc set_err
ret

section .text.evf_cancel exec
global evf_cancel
evf_cancel:
mov rax, 546
mov r10, rcx
syscall
jc set_err
ret

section .text.query_memory_protection exec
global query_memory_protection
query_memory_protection:
mov rax, 547
mov r10, rcx
syscall
jc set_err
ret

section .text.batch_map exec
global batch_map
batch_map:
mov rax, 548
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_create exec
global osem_create
osem_create:
mov rax, 549
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_delete exec
global osem_delete
osem_delete:
mov rax, 550
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_open exec
global osem_open
osem_open:
mov rax, 551
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_close exec
global osem_close
osem_close:
mov rax, 552
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_wait exec
global osem_wait
osem_wait:
mov rax, 553
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_trywait exec
global osem_trywait
osem_trywait:
mov rax, 554
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_post exec
global osem_post
osem_post:
mov rax, 555
mov r10, rcx
syscall
jc set_err
ret

section .text.osem_cancel exec
global osem_cancel
osem_cancel:
mov rax, 556
mov r10, rcx
syscall
jc set_err
ret

section .text.namedobj_create exec
global namedobj_create
namedobj_create:
mov rax, 557
mov r10, rcx
syscall
jc set_err
ret

section .text.namedobj_delete exec
global namedobj_delete
namedobj_delete:
mov rax, 558
mov r10, rcx
syscall
jc set_err
ret

section .text.set_vm_container exec
global set_vm_container
set_vm_container:
mov rax, 559
mov r10, rcx
syscall
jc set_err
ret

section .text.debug_init exec
global debug_init
debug_init:
mov rax, 560
mov r10, rcx
syscall
jc set_err
ret

section .text.suspend_process exec
global suspend_process
suspend_process:
mov rax, 561
mov r10, rcx
syscall
jc set_err
ret

section .text.resume_process exec
global resume_process
resume_process:
mov rax, 562
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_enable exec
global opmc_enable
opmc_enable:
mov rax, 563
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_disable exec
global opmc_disable
opmc_disable:
mov rax, 564
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_set_ctl exec
global opmc_set_ctl
opmc_set_ctl:
mov rax, 565
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_set_ctr exec
global opmc_set_ctr
opmc_set_ctr:
mov rax, 566
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_get_ctr exec
global opmc_get_ctr
opmc_get_ctr:
mov rax, 567
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_create exec
global budget_create
budget_create:
mov rax, 568
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_delete exec
global budget_delete
budget_delete:
mov rax, 569
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_get exec
global budget_get
budget_get:
mov rax, 570
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_set exec
global budget_set
budget_set:
mov rax, 571
mov r10, rcx
syscall
jc set_err
ret

section .text.virtual_query exec
global virtual_query
virtual_query:
mov rax, 572
mov r10, rcx
syscall
jc set_err
ret

section .text.mdbg_call exec
global mdbg_call
mdbg_call:
mov rax, 573
mov r10, rcx
syscall
jc set_err
ret

section .text.sblock_create exec
global sblock_create
sblock_create:
mov rax, 574
mov r10, rcx
syscall
jc set_err
ret

section .text.sblock_delete exec
global sblock_delete
sblock_delete:
mov rax, 575
mov r10, rcx
syscall
jc set_err
ret

section .text.sblock_enter exec
global sblock_enter
sblock_enter:
mov rax, 576
mov r10, rcx
syscall
jc set_err
ret

section .text.sblock_exit exec
global sblock_exit
sblock_exit:
mov rax, 577
mov r10, rcx
syscall
jc set_err
ret

section .text.sblock_xenter exec
global sblock_xenter
sblock_xenter:
mov rax, 578
mov r10, rcx
syscall
jc set_err
ret

section .text.sblock_xexit exec
global sblock_xexit
sblock_xexit:
mov rax, 579
mov r10, rcx
syscall
jc set_err
ret

section .text.eport_create exec
global eport_create
eport_create:
mov rax, 580
mov r10, rcx
syscall
jc set_err
ret

section .text.eport_delete exec
global eport_delete
eport_delete:
mov rax, 581
mov r10, rcx
syscall
jc set_err
ret

section .text.eport_trigger exec
global eport_trigger
eport_trigger:
mov rax, 582
mov r10, rcx
syscall
jc set_err
ret

section .text.eport_open exec
global eport_open
eport_open:
mov rax, 583
mov r10, rcx
syscall
jc set_err
ret

section .text.eport_close exec
global eport_close
eport_close:
mov rax, 584
mov r10, rcx
syscall
jc set_err
ret

section .text.is_in_sandbox exec
global is_in_sandbox
is_in_sandbox:
mov rax, 585
mov r10, rcx
syscall
jc set_err
ret

section .text.dmem_container exec
global dmem_container
dmem_container:
mov rax, 586
mov r10, rcx
syscall
jc set_err
ret

section .text.get_authinfo exec
global get_authinfo
get_authinfo:
mov rax, 587
mov r10, rcx
syscall
jc set_err
ret

section .text.mname exec
global mname
mname:
mov rax, 588
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_dlopen exec
global dynlib_dlopen
dynlib_dlopen:
mov rax, 589
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_dlclose exec
global dynlib_dlclose
dynlib_dlclose:
mov rax, 590
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_dlsym exec
global dynlib_dlsym
dynlib_dlsym:
mov rax, 591
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_list exec
global dynlib_get_list
dynlib_get_list:
mov rax, 592
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_info exec
global dynlib_get_info
dynlib_get_info:
mov rax, 593
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_load_prx exec
global dynlib_load_prx
dynlib_load_prx:
mov rax, 594
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_unload_prx exec
global dynlib_unload_prx
dynlib_unload_prx:
mov rax, 595
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_do_copy_relocations exec
global dynlib_do_copy_relocations
dynlib_do_copy_relocations:
mov rax, 596
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_prepare_dlclose exec
global dynlib_prepare_dlclose
dynlib_prepare_dlclose:
mov rax, 597
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_proc_param exec
global dynlib_get_proc_param
dynlib_get_proc_param:
mov rax, 598
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_process_needed_and_relocate exec
global dynlib_process_needed_and_relocate
dynlib_process_needed_and_relocate:
mov rax, 599
mov r10, rcx
syscall
jc set_err
ret

section .text.sandbox_path exec
global sandbox_path
sandbox_path:
mov rax, 600
mov r10, rcx
syscall
jc set_err
ret

section .text.mdbg_service exec
global mdbg_service
mdbg_service:
mov rax, 601
mov r10, rcx
syscall
jc set_err
ret

section .text.randomized_path exec
global randomized_path
randomized_path:
mov rax, 602
mov r10, rcx
syscall
jc set_err
ret

section .text.rdup exec
global rdup
rdup:
mov rax, 603
mov r10, rcx
syscall
jc set_err
ret

section .text.dl_get_metadata exec
global dl_get_metadata
dl_get_metadata:
mov rax, 604
mov r10, rcx
syscall
jc set_err
ret

section .text.workaround8849 exec
global workaround8849
workaround8849:
mov rax, 605
mov r10, rcx
syscall
jc set_err
ret

section .text.is_development_mode exec
global is_development_mode
is_development_mode:
mov rax, 606
mov r10, rcx
syscall
jc set_err
ret

section .text.get_self_auth_info exec
global get_self_auth_info
get_self_auth_info:
mov rax, 607
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_info_ex exec
global dynlib_get_info_ex
dynlib_get_info_ex:
mov rax, 608
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_getid exec
global budget_getid
budget_getid:
mov rax, 609
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_get_ptype exec
global budget_get_ptype
budget_get_ptype:
mov rax, 610
mov r10, rcx
syscall
jc set_err
ret

section .text.get_paging_stats_of_all_threads exec
global get_paging_stats_of_all_threads
get_paging_stats_of_all_threads:
mov rax, 611
mov r10, rcx
syscall
jc set_err
ret

section .text.get_proc_type_info exec
global get_proc_type_info
get_proc_type_info:
mov rax, 612
mov r10, rcx
syscall
jc set_err
ret

section .text.get_resident_count exec
global get_resident_count
get_resident_count:
mov rax, 613
mov r10, rcx
syscall
jc set_err
ret

section .text.prepare_to_suspend_process exec
global prepare_to_suspend_process
prepare_to_suspend_process:
mov rax, 614
mov r10, rcx
syscall
jc set_err
ret

section .text.get_resident_fmem_count exec
global get_resident_fmem_count
get_resident_fmem_count:
mov rax, 615
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_get_name exec
global thr_get_name
thr_get_name:
mov rax, 616
mov r10, rcx
syscall
jc set_err
ret

section .text.set_gpo exec
global set_gpo
set_gpo:
mov rax, 617
mov r10, rcx
syscall
jc set_err
ret

section .text.get_paging_stats_of_all_objects exec
global get_paging_stats_of_all_objects
get_paging_stats_of_all_objects:
mov rax, 618
mov r10, rcx
syscall
jc set_err
ret

section .text.test_debug_rwmem exec
global test_debug_rwmem
test_debug_rwmem:
mov rax, 619
mov r10, rcx
syscall
jc set_err
ret

section .text.free_stack exec
global free_stack
free_stack:
mov rax, 620
mov r10, rcx
syscall
jc set_err
ret

section .text.suspend_system exec
global suspend_system
suspend_system:
mov rax, 621
mov r10, rcx
syscall
jc set_err
ret

section .text.ipmimgr_call exec
global ipmimgr_call
ipmimgr_call:
mov rax, 622
mov r10, rcx
syscall
jc set_err
ret

section .text.get_gpo exec
global get_gpo
get_gpo:
mov rax, 623
mov r10, rcx
syscall
jc set_err
ret

section .text.get_vm_map_timestamp exec
global get_vm_map_timestamp
get_vm_map_timestamp:
mov rax, 624
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_set_hw exec
global opmc_set_hw
opmc_set_hw:
mov rax, 625
mov r10, rcx
syscall
jc set_err
ret

section .text.opmc_get_hw exec
global opmc_get_hw
opmc_get_hw:
mov rax, 626
mov r10, rcx
syscall
jc set_err
ret

section .text.get_cpu_usage_all exec
global get_cpu_usage_all
get_cpu_usage_all:
mov rax, 627
mov r10, rcx
syscall
jc set_err
ret

section .text.mmap_dmem exec
global mmap_dmem
mmap_dmem:
mov rax, 628
mov r10, rcx
syscall
jc set_err
ret

section .text.physhm_open exec
global physhm_open
physhm_open:
mov rax, 629
mov r10, rcx
syscall
jc set_err
ret

section .text.physhm_unlink exec
global physhm_unlink
physhm_unlink:
mov rax, 630
mov r10, rcx
syscall
jc set_err
ret

section .text.resume_internal_hdd exec
global resume_internal_hdd
resume_internal_hdd:
mov rax, 631
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_suspend_ucontext exec
global thr_suspend_ucontext
thr_suspend_ucontext:
mov rax, 632
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_resume_ucontext exec
global thr_resume_ucontext
thr_resume_ucontext:
mov rax, 633
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_get_ucontext exec
global thr_get_ucontext
thr_get_ucontext:
mov rax, 634
mov r10, rcx
syscall
jc set_err
ret

section .text.thr_set_ucontext exec
global thr_set_ucontext
thr_set_ucontext:
mov rax, 635
mov r10, rcx
syscall
jc set_err
ret

section .text.set_timezone_info exec
global set_timezone_info
set_timezone_info:
mov rax, 636
mov r10, rcx
syscall
jc set_err
ret

section .text.set_phys_fmem_limit exec
global set_phys_fmem_limit
set_phys_fmem_limit:
mov rax, 637
mov r10, rcx
syscall
jc set_err
ret

section .text.utc_to_localtime exec
global utc_to_localtime
utc_to_localtime:
mov rax, 638
mov r10, rcx
syscall
jc set_err
ret

section .text.localtime_to_utc exec
global localtime_to_utc
localtime_to_utc:
mov rax, 639
mov r10, rcx
syscall
jc set_err
ret

section .text.set_uevt exec
global set_uevt
set_uevt:
mov rax, 640
mov r10, rcx
syscall
jc set_err
ret

section .text.get_cpu_usage_proc exec
global get_cpu_usage_proc
get_cpu_usage_proc:
mov rax, 641
mov r10, rcx
syscall
jc set_err
ret

section .text.get_map_statistics exec
global get_map_statistics
get_map_statistics:
mov rax, 642
mov r10, rcx
syscall
jc set_err
ret

section .text.set_chicken_switches exec
global set_chicken_switches
set_chicken_switches:
mov rax, 643
mov r10, rcx
syscall
jc set_err
ret

section .text.extend_page_table_pool exec
global extend_page_table_pool
extend_page_table_pool:
mov rax, 644
mov r10, rcx
syscall
jc set_err
ret

section .text.extend_page_table_pool2 exec
global extend_page_table_pool2
extend_page_table_pool2:
mov rax, 645
mov r10, rcx
syscall
jc set_err
ret

section .text.get_kernel_mem_statistics exec
global get_kernel_mem_statistics
get_kernel_mem_statistics:
mov rax, 646
mov r10, rcx
syscall
jc set_err
ret

section .text.get_sdk_compiled_version exec
global get_sdk_compiled_version
get_sdk_compiled_version:
mov rax, 647
mov r10, rcx
syscall
jc set_err
ret

section .text.app_state_change exec
global app_state_change
app_state_change:
mov rax, 648
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_obj_member exec
global dynlib_get_obj_member
dynlib_get_obj_member:
mov rax, 649
mov r10, rcx
syscall
jc set_err
ret

section .text.budget_get_ptype_of_budget exec
global budget_get_ptype_of_budget
budget_get_ptype_of_budget:
mov rax, 650
mov r10, rcx
syscall
jc set_err
ret

section .text.prepare_to_resume_process exec
global prepare_to_resume_process
prepare_to_resume_process:
mov rax, 651
mov r10, rcx
syscall
jc set_err
ret

section .text.process_terminate exec
global process_terminate
process_terminate:
mov rax, 652
mov r10, rcx
syscall
jc set_err
ret

section .text.blockpool_open exec
global blockpool_open
blockpool_open:
mov rax, 653
mov r10, rcx
syscall
jc set_err
ret

section .text.blockpool_map exec
global blockpool_map
blockpool_map:
mov rax, 654
mov r10, rcx
syscall
jc set_err
ret

section .text.blockpool_unmap exec
global blockpool_unmap
blockpool_unmap:
mov rax, 655
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_info_for_libdbg exec
global dynlib_get_info_for_libdbg
dynlib_get_info_for_libdbg:
mov rax, 656
mov r10, rcx
syscall
jc set_err
ret

section .text.blockpool_batch exec
global blockpool_batch
blockpool_batch:
mov rax, 657
mov r10, rcx
syscall
jc set_err
ret

section .text.fdatasync exec
global fdatasync
fdatasync:
mov rax, 658
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_list2 exec
global dynlib_get_list2
dynlib_get_list2:
mov rax, 659
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_info2 exec
global dynlib_get_info2
dynlib_get_info2:
mov rax, 660
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_submit exec
global aio_submit
aio_submit:
mov rax, 661
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_multi_delete exec
global aio_multi_delete
aio_multi_delete:
mov rax, 662
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_multi_wait exec
global aio_multi_wait
aio_multi_wait:
mov rax, 663
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_multi_poll exec
global aio_multi_poll
aio_multi_poll:
mov rax, 664
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_get_data exec
global aio_get_data
aio_get_data:
mov rax, 665
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_multi_cancel exec
global aio_multi_cancel
aio_multi_cancel:
mov rax, 666
mov r10, rcx
syscall
jc set_err
ret

section .text.get_bio_usage_all exec
global get_bio_usage_all
get_bio_usage_all:
mov rax, 667
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_create exec
global aio_create
aio_create:
mov rax, 668
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_submit_cmd exec
global aio_submit_cmd
aio_submit_cmd:
mov rax, 669
mov r10, rcx
syscall
jc set_err
ret

section .text.aio_init exec
global aio_init
aio_init:
mov rax, 670
mov r10, rcx
syscall
jc set_err
ret

section .text.get_page_table_stats exec
global get_page_table_stats
get_page_table_stats:
mov rax, 671
mov r10, rcx
syscall
jc set_err
ret

section .text.dynlib_get_list_for_libdbg exec
global dynlib_get_list_for_libdbg
dynlib_get_list_for_libdbg:
mov rax, 672
mov r10, rcx
syscall
jc set_err
ret

section .text.blockpool_move exec
global blockpool_move
blockpool_move:
mov rax, 673
mov r10, rcx
syscall
jc set_err
ret

section .text.virtual_query_all exec
global virtual_query_all
virtual_query_all:
mov rax, 674
mov r10, rcx
syscall
jc set_err
ret

section .text.reserve_2mb_page exec
global reserve_2mb_page
reserve_2mb_page:
mov rax, 675
mov r10, rcx
syscall
jc set_err
ret

section .text.cpumode_yield exec
global cpumode_yield
cpumode_yield:
mov rax, 676
mov r10, rcx
syscall
jc set_err
ret

section .text.get_phys_page_size exec
global get_phys_page_size
get_phys_page_size:
mov rax, 677
mov r10, rcx
syscall
jc set_err
ret

section .text.set_err exec
set_err:
mov [rel errno], eax
xor rax, rax
dec rax
ret

section .bss
global errno
errno resw 1
