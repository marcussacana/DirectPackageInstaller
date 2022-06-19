using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectPackageInstaller
{
    public struct Settings
    {
        public string AllDebridApiKey;
        public string PS4IP;
        public string PCIP;

        public bool UseAllDebrid;
        public bool SearchPS4;
        public bool ProxyDownload;
        public bool SegmentedDownload;
        public bool SkipUpdateCheck;

        public bool EnableCNL;

        public bool ShowError;
    }
}
