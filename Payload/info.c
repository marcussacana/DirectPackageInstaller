#include <sys/types.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <unistd.h>
#include <fcntl.h>
#include "struct.h"

struct
{
	uint32_t addr;
	uint16_t port;

} __attribute__((packed)) volatile Connection = { 0xb4b4b4b4, 0xb4b4 };

uint32_t get_uint32(int sock) {
	char dword[4];
	read(sock, dword, sizeof(dword));
	return *(uint32_t*)dword;
}

uint64_t get_uint64(int sock) {
	char qword[8];
	read(sock, qword, sizeof(qword));
	return *(uint64_t*)qword;
}

int strlen(char* buffer) {
	int Len = 0;
	while (buffer[Len] != 0)
		Len++;
	return Len;
}

int get_string(int sock, char* Buffer, int MAX_SIZE) {

	uint32_t str_len = get_uint32(sock);

	Buffer[0] = 0;
	if (str_len > MAX_SIZE)
		return -1;

	read(sock, Buffer, str_len);
	return 0;
}

void append_string(char* buffer, char* new_content) {
	int ori_len = strlen(buffer);
	char* pNew = (char*)(buffer + ori_len);

	int nLen = strlen(new_content);
	for (int i = 0; i < nLen; i++)
		pNew[i] = new_content[i];
}

void get_file_path(char* path, char* fname) {
	path[0] = 0;

	char dir[16] = "/user/data/tmp_";

	append_string(path, dir);
	append_string(path, fname);
}

int get_file(int sock, char* out_path, char* name) {
	out_path[0] = 0;
	int len = get_uint32(sock);
	if (len == 0)
		return 0;

	get_file_path(out_path, name);

	int hFile = open(out_path, O_WRONLY | O_CREAT | O_TRUNC, 0777);

	char buffer[4096];

	int buffer_size = sizeof(buffer);

	ssize_t readed = -1;
	while ((readed = read(sock, buffer, (len < buffer_size) ? len : buffer_size)) > 0) {
		len -= readed;
		char* pBuff = buffer;
		while (readed > 0) {
			ssize_t written = write(hFile, pBuff, readed);
			if (written <= 0)
				return 1;

			pBuff += written;
			readed -= written;
		}

		if (len <= 0)
			break;
	}

	close(hFile);
	return 1;
}


int get_pkg_info(struct bgft_download_param* params)
{
	char url[0x800] = "\x0";
	char name[0x259] = "\x0";
	char id[0x30] = "\x0";
	char icon_name[100] = "\x0";
	char icon_path[0x800] = "\x0";
	char pkg_type[0x10] = "\x0";


	struct sockaddr_in conn_info = {
		.sin_family = AF_INET,
		.sin_addr = {.s_addr = Connection.addr},
		.sin_port = Connection.port,
	};

	int sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
	if (sock < 0)
		return -1;

	if (connect(sock, (struct sockaddr*)&conn_info, sizeof(conn_info)) < 0) {
		close(sock);
		return -1;
	}


	//Finish the payload service if requested by the client
	if (get_uint32(sock) == 0) {
		close(sock);
		return 1;
	}

	//Order: URL, Name, ID, size, [Icon Len, Icon Data]

	if (get_string(sock, url, sizeof(url)) == -1)
		return -1;

	if (get_string(sock, name, sizeof(name)) == -1)
		return -1;

	if (get_string(sock, id, sizeof(id)) == -1)
		return -1;

	if (get_string(sock, pkg_type, sizeof(pkg_type)) == -1)
		return -1;


	uint64_t size = get_uint64(sock);

	char icon_ext[5] = ".png";
	append_string(icon_name, id);
	append_string(icon_name, icon_ext);

	if (get_file(sock, icon_path, icon_name) == 0)
		icon_path[0] == 0x00;

	close(sock);

	params->id = id;
	params->content_url = url;
	params->content_name = name;
	params->package_size = size;
	params->icon_path = icon_path;
	params->package_type = pkg_type;

	return 0;
}