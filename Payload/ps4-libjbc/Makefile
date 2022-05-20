all: jbc.o

jbc.o: *.c *.h
	gcc *.c -nostdlib -nostartfiles -ffreestanding -mno-red-zone -no-pie -Wl,-r -o jbc.o -g

clean:
	rm -f jbc.o
