using System.Diagnostics;
using System.IO;
using System.Linq;

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
    }
}
