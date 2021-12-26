using System.Diagnostics;

namespace DirectPackageInstaller
{
    public class MemoryInfo
    {
        static PerformanceCounter Counter = null;

        public static ulong GetAvaiablePhysicalMemory()
        {
            if (Counter == null) {
                string Cat = Program.IsUnix ? "Mono Memory" : "Memory";
                string Con = Program.IsUnix ? "Available Physical Memory" : "Available MBytes";

                Counter = new PerformanceCounter(Cat, Con);
            }

            var Value = unchecked((ulong)Counter.RawValue);

            if (Program.IsUnix)
                return Value;

            return Value * 1024 * 1024;
        }
    }
}
