using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        private string _AllDebirdApiKey = null;
        public string AllDebirdApiKey
        {
            get => _AllDebirdApiKey;
            set => this.RaiseAndSetIfChanged(ref _AllDebirdApiKey, value);
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
        
        private bool _UseAllDebird = false;
        public bool UseAllDebird 
        {
            get => _UseAllDebird;
            set => this.RaiseAndSetIfChanged(ref _UseAllDebird, value);
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