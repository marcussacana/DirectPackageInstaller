#include <stddef.h>
#include <sys/mman.h>
#include "ps4-libjbc/jailbreak.h"
#include "struct.h"

asm("clear_stack:\nmov $0x800,%ecx\nmovabs $0xdead000000000000,%rax\n.L1:\npush %rax\nloop .L1\nadd $0x4000,%rsp\nret");
void clear_stack(void);

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

    void* usrsrv = dlopen("/system/common/lib/libSceUserService.sprx", 0);
    int(*sceUserServiceInitialize)(OrbisUserServiceInitializeParams* user_id) = dlsym(usrsrv, "sceUserServiceInitialize");
    int(*sceUserServiceGetForegroundUser)(int* user_id) = dlsym(usrsrv, "sceUserServiceGetForegroundUser");
    int(*sceUserServiceTerminate)(void) = dlsym(usrsrv, "sceUserServiceTerminate");
    
    struct OrbisUserServiceInitializeParams init_params = {
    	.priority = ORBIS_KERNEL_PRIO_FIFO_NORMAL
    };
    
    int uid = 0;
    sceUserServiceInitialize(&init_params);
    sceUserServiceGetForegroundUser(&uid);
    sceUserServiceTerminate();
    
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

    void* handle = dlopen("/system/common/lib/libSceSysUtil.sprx", 0);
    int(*sceSysUtilSendSystemNotificationWithText)(int, const char*) = dlsym(handle, "sceSysUtilSendSystemNotificationWithText");
   
    if (get_pkg_info(&bgft_params) == -1){
    	sceSysUtilSendSystemNotificationWithText(222, "DPI: GET INFO ERROR"); 
    	return -1;
    }
    
    int rv;
    
    void* bgft = dlopen("/system/common/lib/libSceBgft.sprx", 0);
    
    int(*sceBgftInitialize)(struct bgft_init_params*) = dlsym(bgft, "sceBgftServiceIntInit");
    int(*sceBgftFinalize)(void) = dlsym(bgft, "sceBgftFinalize");
    
    int(*sceBgftDownloadRegisterTask)(struct bgft_download_param*, int*) = dlsym(bgft, "sceBgftServiceDownloadRegisterTask");
    int(*sceBgftDebugDownloadRegisterTask)(struct bgft_download_param*, int*) = dlsym(bgft, "sceBgftServiceIntDebugDownloadRegisterPkg");
    int(*sceBgftDownloadStartTask)(int) = dlsym(bgft, "sceBgftServiceIntDownloadStartTask");
    
    void* aiu = dlopen("/system/common/lib/libSceAppInstUtil.sprx", 0);
    int(*sceAppInstUtilInitialize)(void) = dlsym(aiu, "sceAppInstUtilInitialize");
    
    rv = sceAppInstUtilInitialize();
    
    struct bgft_init_params ip = {
        .mem = mmap(NULL, 0x100000, PROT_READ|PROT_WRITE, MAP_PRIVATE|MAP_ANONYMOUS, -1, 0),
        .size = 0x100000,
    };
    
    rv = sceBgftInitialize(&ip);
 
    int task = BGFT_INVALID_TASK_ID;
    rv = sceBgftDownloadRegisterTask(&bgft_params, &task);
    
    if(rv != 0x80990088 && task != BGFT_INVALID_TASK_ID){ 
    	rv = sceBgftDownloadStartTask(task);
    	sceBgftFinalize();
    	return 0;
    }
    
    rv = sceBgftDebugDownloadRegisterTask(&bgft_params, &task);
    	
    if(rv != 0x80990088 && task != BGFT_INVALID_TASK_ID){ 
    	rv = sceBgftDownloadStartTask(task);
    	sceBgftFinalize();
    	return 0;
    }
   
    if (rv == 0x80990088){
       sceSysUtilSendSystemNotificationWithText(222, "DPI: Package Already Installed!");
       sceBgftFinalize();
       return 0; 
    }
    
    	
    void* libc = dlopen("/system/common/lib/libc.sprx", 0);
    int (*sprintf)(char* dst, const char *fmt, ...) = dlsym(libc, "sprintf");
    char str[0x100] = "\x00";
    sprintf(str,"DPI: Error 0x%x", rv);
    sceSysUtilSendSystemNotificationWithText(222, str); 

    sceBgftFinalize();
    return -1;
}
