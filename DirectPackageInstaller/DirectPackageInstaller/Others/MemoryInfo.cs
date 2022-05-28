using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;

namespace DirectPackageInstaller
{
    public class MemoryInfo
    {
        static PerformanceCounter Counter = null;

        public static ulong GetAvaiablePhysicalMemory()
        {
            ulong Value = 0;
            
            if (App.IsUnix && File.Exists("/proc/meminfo"))
            {
                var Status = File.ReadAllLines("/proc/meminfo");
                var MemAvailable = Status.Where(x => x.StartsWith("MemAvailable")).First();
                MemAvailable = MemAvailable.Substring(MemAvailable.LastIndexOf(':') + 1).Trim();
                if (MemAvailable.Contains(" "))
                {
                    Value = ulong.Parse(MemAvailable.Split(' ').First());
                    var Scale = MemAvailable.Split(' ').Last();
                    switch (Scale.ToLowerInvariant())
                    {
                        case "kb":
                        case "kbyte":
                        case "kbytes":
                            Value *= 1024;
                            break;
                        case "mb":
                        case "mbyte":
                        case "mbytes":
                            Value *= 1024 * 1024;
                            break;
                        case "gb":
                        case "gbyte":
                        case "gbytes":
                            Value *= 1024 * 1024 * 1024;
                            break;
                    }

                    return Value;
                }
            }

            if (App.IsOSX)
            {
	            var Mem = GetMacAvailableMemory();
	            if (Mem < 0)
		            throw new Exception("Failed to get OSX Available Memory");

	            return (ulong)Mem;
            }
            
            if (Counter == null) {
                string Cat = App.IsRunningOnMono ? "Mono Memory" : "Memory";
                string Con = App.IsRunningOnMono  ? "Available Physical Memory" : "Available MBytes";

                Counter = new PerformanceCounter(Cat, Con);
            }

            Value = unchecked((ulong)Counter.RawValue);

            if (App.IsRunningOnMono)
                return Value;

            return Value * 1024 * 1024;
        }

        private static long GetMacAvailableMemory()
        {
	        var SelfHost = mach_host_self();
	        if (host_page_size(SelfHost, out int PageSize) != 0)
		        return -1;
	        
	        var Status = new VmStatistics64();

	        int FieldCount = Marshal.SizeOf(Status) / 4;
	        
	        if (host_statistics64(SelfHost, Flavor.HOST_VM_INFO64, ref Status, ref FieldCount) != 0)
		        return -1;

	        return ((long)Status.free_count + Status.inactive_count) * PageSize;
        }
        public enum Flavor : int
        {
	        // host_statistics()
	        HOST_LOAD_INFO = 1,         /* System loading stats */
	        HOST_VM_INFO = 2,           /* Virtual memory stats */
	        HOST_CPU_LOAD_INFO = 3,     /* CPU load stats */

	        // host_statistics64()
	        HOST_VM_INFO64 = 4,         /* 64-bit virtual memory stats */
	        HOST_EXTMOD_INFO64 = 5,     /* External modification stats */
	        HOST_EXPIRED_TASK_INFO = 6  /* Statistics for expired tasks */
        }
        
        [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
        private static extern int host_page_size(IntPtr host, out int pageSize);

        [DllImport("libc")]
        private static extern IntPtr mach_host_self();
        
        [DllImport("libc")]
        private static extern int host_statistics64(IntPtr host_priv, Flavor host_flavor, ref VmStatistics64 host_info64_out, ref int host_info64_outCnt);
        
		[StructLayout(LayoutKind.Sequential)]
		private struct VmStatistics64
		{
			public int free_count;
			public int active_count; 
			public int inactive_count; 
			public int wire_count;
			public ulong zero_fill_count; 
			public ulong reactivations; 
			public ulong pageins;
			public ulong pageouts; 
			public ulong faults;
			public ulong copy_on_write_faults;
			public ulong cache_lookups;
			public ulong cache_hits;
			public ulong page_purges;

			public  int purgeable_pages_count;

			/*
			 * NB: speculative pages are already accounted for in "free_count",
			 * so "speculative_count" is the number of "free" pages that are
			 * used to hold data that was read speculatively from disk but
			 * haven't actually been used by anyone so far.
			 */
			public int speculative_count;

			/* added for rev1 */
			public ulong decompressions; 
			public ulong compressions; 
			public ulong swapins;
			public ulong swapouts;
			public int compressor_page_count;
			public int throttled_count;

			public int external_page_count;

			public int internal_page_count;

			public ulong total_uncompressed_pages_in_compressor;
		}
    }
}
