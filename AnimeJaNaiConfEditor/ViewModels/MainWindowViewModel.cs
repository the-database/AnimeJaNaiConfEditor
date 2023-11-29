using Avalonia.Collections;
using DynamicData;
using ReactiveUI;
using Salaros.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeJaNaiConfEditor.ViewModels
{
    [DataContract]
    public class MainWindowViewModel : ViewModelBase
    {
        

        public MainWindowViewModel()
        {
            //this.WhenAnyValue(
                
            //    x => x.SelectedTabIndex, x => x.OverwriteExistingVideos).Subscribe(x =>
            //{
            //    Validate();
            //});
        }

        private CancellationTokenSource? _cancellationTokenSource;
        private Process? _runningProcess = null;

        public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);


        //private int _selectedTabIndex;
        //[DataMember]
        //public int SelectedTabIndex
        //{
        //    get => _selectedTabIndex;
        //    set
        //    {
        //        if (_selectedTabIndex != value)
        //        {
        //            this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
        //        }
        //    }
        //}

        private string _validationText = string.Empty;
        public string ValidationText
        {
            get => _validationText;
            set
            {
                this.RaiseAndSetIfChanged(ref _validationText, value);
                this.RaisePropertyChanged(nameof(LeftStatus));
            }
        }

        private bool _tensorRtSelected = true;
        [DataMember]
        public bool TensorRtSelected
        {
            get => _tensorRtSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref _tensorRtSelected, value);
            }
        }

        private bool _directMlSelected = false;
        [DataMember]
        public bool DirectMlSelected
        {
            get => _directMlSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref _directMlSelected, value);
            }
        }

        private bool _ncnnSelected = false;
        [DataMember]
        public bool NcnnSelected
        {
            get => _ncnnSelected;
            set
            {
                this.RaiseAndSetIfChanged(ref _ncnnSelected, value);
            }
        }

        private AvaloniaList<UpscaleModel> _upscaleSettings = new();
        [DataMember]
        public AvaloniaList<UpscaleModel> UpscaleSettings
        {
            get => _upscaleSettings;
            set => this.RaiseAndSetIfChanged(ref _upscaleSettings, value);
        }

        private bool _enableRife = false;
        [DataMember]
        public bool EnableRife
        {
            get => _enableRife;
            set => this.RaiseAndSetIfChanged(ref _enableRife, value);
        }

        private bool _showAdvancedSettings = false;
        [DataMember]
        public bool ShowAdvancedSettings
        {
            get => _showAdvancedSettings;
            set => this.RaiseAndSetIfChanged(ref _showAdvancedSettings, value);
        }

        private bool _showConsole = false;
        public bool ShowConsole
        {
            get => _showConsole;
            set => this.RaiseAndSetIfChanged(ref _showConsole, value);
        }

        private string _inputStatusText = string.Empty;
        public string InputStatusText
        {
            get => _inputStatusText;
            set
            {
                this.RaiseAndSetIfChanged(ref _inputStatusText, value);
                this.RaisePropertyChanged(nameof(LeftStatus));
            }
        }

        public string LeftStatus => !Valid ? ValidationText.Replace("\n", " ") : $"{InputStatusText} selected for upscaling.";

        private bool _valid = false;
        [IgnoreDataMember]
        public bool Valid
        {
            get => _valid;
            set
            {
                this.RaiseAndSetIfChanged(ref _valid, value);
                this.RaisePropertyChanged(nameof(LeftStatus));
            }
        }

        private bool _upscaling = false;
        [IgnoreDataMember] 
        public bool Upscaling
        {
            get => _upscaling;
            set 
            {
                this.RaiseAndSetIfChanged(ref _upscaling, value);
                this.RaisePropertyChanged(nameof(LeftStatus));
            }
        }

        public void AddModel()
        {
            UpscaleSettings.Add(new UpscaleModel());
            UpdateModelHeaders();
        }

        public void DeleteModel(UpscaleModel model)
        {
            try
            {
                UpscaleSettings.Remove(model);
            }
            catch (ArgumentOutOfRangeException)
            {
                
            }

            UpdateModelHeaders();
        }

        private void UpdateModelHeaders()
        {
            for (var i = 0; i < UpscaleSettings.Count; i++)
            {
                UpscaleSettings[i].ModelHeader = $"Model {i + 1}";
            }
        }

        public void SetTensorRtSelected()
        {
            TensorRtSelected = true;
            DirectMlSelected = false;
            NcnnSelected = false;
        }

        public void SetDirectMlSelected()
        {
            DirectMlSelected = true;
            TensorRtSelected = false;
            NcnnSelected = false;
        }

        public void SetNcnnSelected()
        {
            NcnnSelected = true;
            TensorRtSelected = false;
            DirectMlSelected = false;
        }


        public void Validate()
        {
            
        }

        public void SetupAnimeJaNaiConfSlot1()
        {
            var confPath = Path.GetFullPath(@".\mpv-upscale-2x_animejanai\portable_config\shaders\animejanai_v2.conf");
            var backend = DirectMlSelected ? "DirectML" : NcnnSelected ? "NCNN" : "TensorRT";
            HashSet<string> filesNeedingEngine = new();
            var configText = new StringBuilder($@"[global]
logging=yes
backend={backend}
[slot_1]
");

            for (var i = 0; i < UpscaleSettings.Count; i++)
            {
                var targetCopyPath = @$".\mpv-upscale-2x_animejanai\vapoursynth64\plugins\models\animejanai\{Path.GetFileName(UpscaleSettings[i].OnnxModelPath)}";

                if (Path.GetFullPath(targetCopyPath) != Path.GetFullPath(UpscaleSettings[i].OnnxModelPath))
                {
                    File.Copy(UpscaleSettings[i].OnnxModelPath, targetCopyPath, true);
                }

                configText.AppendLine(@$"chain_1_model_{i + 1}_resize_height_before_upscale={UpscaleSettings[i].ResizeHeightBeforeUpscale}
chain_1_model_{i + 1}_resize_factor_before_upscale={UpscaleSettings[i].ResizeFactorBeforeUpscale}
chain_1_model_{i + 1}_name={Path.GetFileNameWithoutExtension(UpscaleSettings[i].OnnxModelPath)}");
            }

            var rife = EnableRife ? "yes" : "no";
            configText.AppendLine($"chain_1_rife={rife}");

            File.WriteAllText(confPath, configText.ToString());
        }
    }

    [DataContract]
    public class UpscaleModel : ReactiveObject
    {
        private string _modelHeader = string.Empty;
        [DataMember]
        public string ModelHeader
        {
            get => _modelHeader;
            set => this.RaiseAndSetIfChanged(ref _modelHeader, value);
        }

        private string _resizeHeightBeforeUpscale = 0.ToString();
        [DataMember]
        public string ResizeHeightBeforeUpscale
        {
            get => _resizeHeightBeforeUpscale; 
            set => this.RaiseAndSetIfChanged(ref _resizeHeightBeforeUpscale, value);
        }

        private string _resizeFactorBeforeUpscale = 1.0.ToString();
        [DataMember]
        public string ResizeFactorBeforeUpscale
        {
            get => _resizeFactorBeforeUpscale;
            set => this.RaiseAndSetIfChanged(ref _resizeFactorBeforeUpscale, value);
        }

        private string _onnxModelPath = string.Empty;
        [DataMember]
        public string OnnxModelPath
        {
            get => _onnxModelPath;
            set => this.RaiseAndSetIfChanged(ref _onnxModelPath, value);
        }
    }
}