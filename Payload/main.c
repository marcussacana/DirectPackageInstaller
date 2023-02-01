#include <stddef.h>
#include <sys/mman.h>
#include "ps4-libjbc/jailbreak.h"
#include "struct.h"

asm("clear_stack:\nmov $0x800,%ecx\nxor %rax, %rax\n.L1:\npush %rax\nloop .L1\nadd $0x4000,%rsp\nret");
void clear_stack(void);

void int32ToHex(int32_t Value, char* Hex)
{
	Hex[0] = '0';
	Hex[1] = 'x';

	for (int i = 7, x = 2; i >= 0; i--, x++) {
		int BytePart = (Value >> (4 * i)) & 0x0F;
		if (BytePart < 10)
			Hex[x] = '0' + BytePart;
		else
			Hex[x] = 'A' + (BytePart - 10);
	}

	Hex[10] = 0;
}

void concat(const char* StringA, const char* StringB, char* Output) {
	int Offset = 0;
	for (int i = 0; StringA[i] != 0; i++) {
		Output[Offset++] = StringA[i];
	}
	for (int i = 0; StringB[i] != 0; i++) {
		Output[Offset++] = StringB[i];
	}

	Output[Offset] = 0;
}


int main()
{
	struct jbc_cred cred;
	jbc_get_cred(&cred);
	jbc_jailbreak_cred(&cred);

	cred.jdir = 0;
	cred.sceProcType = 0x3800000000000010;
	cred.sonyCred = 0x40001c0000000000;
	cred.sceProcCap = 0x900000000000ff00;
	jbc_set_cred(&cred);

	clear_stack();


	void* libSceSysUtil = dlopen("/system/common/lib/libSceSysUtil.sprx", 0);
	int(*sceSysUtilSendSystemNotificationWithText)(int, const char*) = dlsym(libSceSysUtil, "sceSysUtilSendSystemNotificationWithText");

	void* usrsrv = dlopen("/system/common/lib/libSceUserService.sprx", 0);
	int(*sceUserServiceInitialize)(OrbisUserServiceInitializeParams * user_id) = dlsym(usrsrv, "sceUserServiceInitialize");
	int(*sceUserServiceGetForegroundUser)(int* user_id) = dlsym(usrsrv, "sceUserServiceGetForegroundUser");
	int(*sceUserServiceTerminate)(void) = dlsym(usrsrv, "sceUserServiceTerminate");


	//void* libc = dlopen("/system/common/lib/libc.sprx", 0);
	//int (*sprintf)(char* dst, const char* fmt, ...) = dlsym(libc, "sprintf");

	//sceSysUtilSendSystemNotificationWithText(222, "libc OK");

	void* bgft = dlopen("/system/common/lib/libSceBgft.sprx", 0);

	int(*sceBgftInitialize)(struct bgft_init_params*) = dlsym(bgft, "sceBgftServiceIntInit");

	int(*sceBgftDownloadRegisterTask)(struct bgft_download_param*, int*) = dlsym(bgft, "sceBgftServiceDownloadRegisterTask");
	int(*sceBgftDebugDownloadRegisterTask)(struct bgft_download_param*, int*) = dlsym(bgft, "sceBgftServiceIntDebugDownloadRegisterPkg");
	int(*sceBgftDownloadStartTask)(int) = dlsym(bgft, "sceBgftServiceIntDownloadStartTask");
	int(*sceBgftFinalize)(void) = dlsym(bgft, "sceBgftFinalize");

	clear_stack();

	struct OrbisUserServiceInitializeParams init_params = {
		.priority = ORBIS_KERNEL_PRIO_FIFO_NORMAL
	};

	int uid = 0;
	sceUserServiceInitialize(&init_params);
	sceUserServiceGetForegroundUser(&uid);
	sceUserServiceTerminate();

	void* libSceAppInstUtil = dlopen("/system/common/lib/libSceAppInstUtil.sprx", 0);
	int(*sceAppInstUtilInitialize)(void) = dlsym(libSceAppInstUtil, "sceAppInstUtilInitialize");

	int rv;

	rv = sceAppInstUtilInitialize();

	if (rv) {
		char err[0x200] = "\x0";
		char errCode[0x20] = "\x0";
		int32ToHex(rv, (char*)&errCode);
		concat("DPI: App Inst Util Error ", errCode, (char*)err);
		sceSysUtilSendSystemNotificationWithText(222, err);
		return -1;
	}

	struct bgft_init_params ip = {
		.mem = mmap(NULL, 0x100000, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0),
		.size = 0x100000,
	};

	rv = sceBgftInitialize(&ip);

	if (rv && rv != 0x80990001) {
		char err[0x200] = "\x0";
		char errCode[0x20] = "\x0";
		int32ToHex(rv, (char*)&errCode);
		concat("DPI: BGFT Init Failed ", errCode, (char*)err);
		sceSysUtilSendSystemNotificationWithText(222, err);
		return -1;
	}

	struct bgft_download_param bgft_params = {
		.user_id = uid,
		.entitlement_type = 5,
		.id = "",
		.content_url = "",
		//.content_ex_url = "",
		.content_name = "",
		.icon_path = "",
		//.sku_id = "",
		//.release_date = "",
		.package_type = "PS4GD",
		.package_sub_type = "",
		.playgo_scenario_id = "0",
		.option = BGFT_TASK_OPTION_DISABLE_CDN_QUERY_PARAM
	};

	while (1) {

		rv = get_pkg_info(&bgft_params);

		if (rv == -1) {
			sceSysUtilSendSystemNotificationWithText(222, "DPI: GET INFO ERROR");
			return -1;
		}

		if (rv == 1) {
			break;
		}

		int task = BGFT_INVALID_TASK_ID;
		rv = sceBgftDownloadRegisterTask(&bgft_params, &task);

		if (rv != 0x80990088 && task != BGFT_INVALID_TASK_ID) {
			rv = sceBgftDownloadStartTask(task);
			continue;
		}

		rv = sceBgftDebugDownloadRegisterTask(&bgft_params, &task);

		if (rv != 0x80990088 && task != BGFT_INVALID_TASK_ID) {
			rv = sceBgftDownloadStartTask(task);
			continue;
		}

		if (rv == 0x80990088) {
			sceSysUtilSendSystemNotificationWithText(222, "DPI: Package Already Installed!");
			continue;
		}

		if (rv == 0x80990039 || rv == 0x80A30026) {
			sceSysUtilSendSystemNotificationWithText(222, "DPI: Insufficient storage space.\nPlease free up space on your hard drive.");
			continue;
		}
		
		if (rv == 0x80990085) {
			sceSysUtilSendSystemNotificationWithText(222, "DPI: Insufficient storage space.\nPlease free up non fragmented space on your hard drive.");
			continue;
		}

		char err[0x200] = "\x0";
		char errCode[0x20] = "\x0";
		int32ToHex(rv, (char*)&errCode);
		concat("DPI: BGFT Error ", errCode, (char*)err);

		sceSysUtilSendSystemNotificationWithText(222, err);
		//sceBgftFinalize();
		return -1;
	}

	sceSysUtilSendSystemNotificationWithText(222, "DirectPackageInstaller Exited");
	//sceBgftFinalize();
	return 0;
}
