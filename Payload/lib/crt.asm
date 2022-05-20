use64

global _start
section .text.startup ; needed for correct linkage with -O2
_start:
extern main
jmp main
