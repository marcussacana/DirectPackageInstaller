using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using LibOrbisPkg.PKG;
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
                
                if (Locator.IsValidPS4IP(value))
                    PCIP = Locator.FindLocalIP(value);
                
                this.RaisePropertyChanged();
            }
        }
        
        
        private string _PCIP = null;
        public string PCIP
        {
            get => _PCIP;
            set
            {
                _PCIP = value;
                this.RaisePropertyChanged();
            }
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
            set
            {
                _AllDebirdApiKey = value;
                this.RaisePropertyChanged();
            }
        }

        private bool _ProxyMode = false;
        public bool ProxyMode { get => _ProxyMode;
            set
            {
                _ProxyMode = value;
            
                this.RaisePropertyChanged();
            }
        }

        private bool _SegmentedMode = false;
        public bool SegmentedMode { get => _SegmentedMode;
            set
            {
                _SegmentedMode = value;
                
                this.RaisePropertyChanged();
            }
        }
        
        private bool _UseAllDebird = false;
        public bool UseAllDebird { get => _UseAllDebird;
            set
            {
                _UseAllDebird = value;
                
                this.RaisePropertyChanged();
            }
        }
    }

    public class PkgParamInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}