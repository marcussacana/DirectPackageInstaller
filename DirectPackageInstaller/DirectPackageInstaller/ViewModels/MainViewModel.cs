using System.Collections.Generic;
using System.Net;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels
{
    public class MainViewModel : ReactiveObject
    {

        private string _PS4IP = null;
        public string PS4IP
        {
            get => _PS4IP;
            set
            {
                _PS4IP = value;
                
                if (IPAddress.TryParse(value, out _))
                    PCIP = IPHelper.FindLocalIP(value) ?? PCIP;
                
                this.RaisePropertyChanged();
            }
        }
        
        
        private string _PCIP = null;
        public string PCIP
        {
            get => _PCIP;
            set => this.RaiseAndSetIfChanged(ref _PCIP, value);
        }

        private string _CurrentURL = null;
        public string CurrentURL
        {
            get => _CurrentURL;
            set
            {
                _CurrentURL = value;
                this.RaisePropertyChanged();
            }
        }

        private string _allDebridApiKey = null;
        public string AllDebridApiKey
        {
            get => _allDebridApiKey;
            set => this.RaiseAndSetIfChanged(ref _allDebridApiKey, value);
        }

        private string _realDebridApiKey = null;
        public string RealDebridApiKey
        {
            get => _realDebridApiKey;
            set => this.RaiseAndSetIfChanged(ref _realDebridApiKey, value);
        }

        private bool _ProxyMode = false;
        public bool ProxyMode 
        {
            get => _ProxyMode;
            set => this.RaiseAndSetIfChanged(ref _ProxyMode, value);
        }

        private bool _SegmentedMode = false;
        public bool SegmentedMode 
        {
            get => _SegmentedMode;
            set => this.RaiseAndSetIfChanged(ref _SegmentedMode, value);
        }
        
        private bool _useAllDebrid = false;
        public bool UseAllDebrid 
        {
            get => _useAllDebrid;
            set => this.RaiseAndSetIfChanged(ref _useAllDebrid, value);
        }
        
        private bool _useRealDebrid = false;
        public bool UseRealDebrid 
        {
            get => _useRealDebrid;
            set => this.RaiseAndSetIfChanged(ref _useRealDebrid, value);
        }


        private bool _CNLService = false;
        public bool CNLService 
        {
            get => _CNLService;
            set => this.RaiseAndSetIfChanged(ref _CNLService, value);
        }

        private List<PkgParamInfo> _PKGParams = new List<PkgParamInfo>();
        public List<PkgParamInfo> PKGParams
        {
            get => _PKGParams;
            set => this.RaiseAndSetIfChanged(ref _PKGParams, value);
        }
    }

    public class PkgParamInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}