using Microsoft.VisualStudio.PlatformUI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace NeoDebug.VS
{
    public class LaunchConfigSelectionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public IEnumerable<LaunchConfigViewModel> LaunchConfigs { get; }
        public UiCommand<DialogWindow> OkCommand { get; }
        public UiCommand<DialogWindow> CancelCommand { get; }

        private LaunchConfigViewModel _selectedLaunchConfig;
        public LaunchConfigViewModel SelectedLaunchConfig
        {
            get 
            { 
                return _selectedLaunchConfig; 
            }
            set
            {
                if (value != _selectedLaunchConfig)
                {
                    _selectedLaunchConfig = value;
                    NotifyPropertyChanged(nameof(SelectedLaunchConfig));
                    OkCommand.Refresh();
                }
            }
        }

        public LaunchConfigSelectionViewModel()
        {
            LaunchConfigs = Enumerable.Empty<LaunchConfigViewModel>();
            OkCommand = new UiCommand<DialogWindow>(OnCancel, w => false);
            CancelCommand = new UiCommand<DialogWindow>(OnCancel);
        }

        public LaunchConfigSelectionViewModel(IEnumerable<(string file, JObject config)> launchConfigurations)
        {
            LaunchConfigs = launchConfigurations.Select(t => new LaunchConfigViewModel(t.file, t.config)).ToList();
            OkCommand = new UiCommand<DialogWindow>(OnCommit, w => Validate());
            CancelCommand = new UiCommand<DialogWindow>(OnCancel);
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool Validate()
        {
            return SelectedLaunchConfig != null;
        }

        private void OnCommit(DialogWindow w)
        {
            if (w != null)
            {
                w.DialogResult = true;
                w.Close();
            }
        }

        private void OnCancel(DialogWindow w)
        {
            if (w != null)
            {
                w.DialogResult = false;
                w.Close();
            }
        }
    }

    public class LaunchConfigViewModel
    {
        public LaunchConfigViewModel(string file, JObject config)
        {
            File = file;
            Config = config;
            Name = config.TryGetValue("name", out var nameToken) ? nameToken.Value<string>() : "<unnamed launch configuraiton>";
        }

        public string File { get; }
        public string Name { get; }
        public JObject Config { get; }

        public string Label => $"{Name} ({File})";
    }
}
