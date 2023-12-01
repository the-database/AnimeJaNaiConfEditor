using Avalonia.Collections;
using Avalonia.Controls;
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
using System.Text.RegularExpressions;
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

            ReadAnimeJaNaiConf();

            //AllModels = GetAllModels();
        }

        private CancellationTokenSource? _cancellationTokenSource;
        private Process? _runningProcess = null;

        public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        private bool _showGlobalSettings = false; // TODO
        [DataMember]
        public bool ShowGlobalSettings
        {
            get => _showGlobalSettings;
            set => this.RaiseAndSetIfChanged(ref _showGlobalSettings, value);
        }

        private bool _showDefaultProfiles;
        [DataMember] public bool ShowDefaultProfiles
        {
            get => _showDefaultProfiles;
            set => this.RaiseAndSetIfChanged(ref _showDefaultProfiles, value);
        }

        private bool _showCustomProfiles = true; // TODO 
        [DataMember] public bool ShowCustomProfiles
        {
            get => _showCustomProfiles;
            set => this.RaiseAndSetIfChanged(ref _showCustomProfiles, value);
        }

        private string _selectedSlotNumber = "1"; // TODO
        [DataMember]
        public string SelectedSlotNumber
        {
            get => _selectedSlotNumber;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSlotNumber, value);
                this.RaisePropertyChanged(nameof(CurrentSlot));
            }
        }

        public void HandleShowGlobalSettings()
        {
            ShowGlobalSettings = true;
            ShowCustomProfiles = false;
            ShowDefaultProfiles = false;
        }

        public void HandleShowDefaultProfile(string slotNumber)
        {
            ShowGlobalSettings = false;
            ShowDefaultProfiles = true;
            ShowCustomProfiles = false;
            SelectedSlotNumber = slotNumber;
        }

        public void HandleShowCustomProfile(string slotNumber)
        {
            ShowGlobalSettings = false;
            ShowCustomProfiles = true;
            ShowDefaultProfiles = false;
            SelectedSlotNumber = slotNumber;
        }

        public UpscaleSlot CurrentSlot
        {
            get => UpscaleSlots.Where(slot => slot.SlotNumber == SelectedSlotNumber).FirstOrDefault();
        }

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

        private bool _enableLogging = false;
        [DataMember]
        public bool EnableLogging
        {
            get => _enableLogging;
            set => this.RaiseAndSetIfChanged(ref _enableLogging, value);
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

        private AvaloniaList<UpscaleSlot> _upscaleSlots = new();
        [DataMember]
        public AvaloniaList<UpscaleSlot> UpscaleSlots
        {
            get => _upscaleSlots;
            set => this.RaiseAndSetIfChanged(ref _upscaleSlots, value);
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

        public static AvaloniaList<string> GetAllModels()
        {
            var modelsPath = @"C:\mpv-upscale-2x_animejanai\vapoursynth64\plugins\models\animejanai"; // TODO
            return new AvaloniaList<string>(Directory.GetFiles(modelsPath).Where(filename => Path.GetExtension(filename).Equals(".onnx", StringComparison.CurrentCultureIgnoreCase))
                .Select(filename => Path.GetFileNameWithoutExtension(filename))
                .Order().ToList());
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
            Console.WriteLine("OK");
        }

        public void ReadAnimeJaNaiConf()
        {
            var parser = new ConfigParser(Path.GetFullPath(@".\animejanai.conf"));
            var slots = new Dictionary<string, UpscaleSlot>();

            EnableLogging = ParseBool(parser.GetValue("global", "logging", "no"));
            if (Enum.TryParse(parser.GetValue("global", "backend", "TensorRT"), out Backend backend))
            {
                switch (backend)
                {
                    case Backend.DirectML:
                        SetDirectMlSelected();
                        break;
                    case Backend.NCNN:
                        SetNcnnSelected();
                        break;
                    case Backend.TensorRT:
                    default:
                        SetTensorRtSelected();
                        break;
                }
            }

            foreach (var section in parser.Sections)
            {
                if (section.SectionName != "global")
                {
                    var currentSlotNumber = Regex.Match(section.SectionName, @"slot_(\d+)").Groups[1].Value;
                    //var currentSlot = new UpscaleSlot();

                    var chains = new Dictionary<string, UpscaleChain>();
                    var models = new Dictionary<string, Dictionary<string, UpscaleModel>>();

                    foreach (var key in section.Keys)
                    {
                        var matchCurrentChainNumber = Regex.Match(key.Name, @"chain_(\d+)_");

                        if (matchCurrentChainNumber.Success)
                        {
                            var currentChainNumber = matchCurrentChainNumber.Groups[1].Value;

                            chains[currentChainNumber] = new UpscaleChain
                            {
                                ChainNumber = currentChainNumber,
                                MinResolution = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_min_resolution"),
                                MaxResolution = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_max_resolution"),
                                MinFps = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_min_fps"),
                                MaxFps = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_max_fps"),
                                EnableRife = ParseBool(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife"))
                            };

                            var matchCurrentModelNumber = Regex.Match(key.Name, @"_model_(\d+)_");

                            if (matchCurrentModelNumber.Success)
                            {
                                var currentModelNumber = matchCurrentModelNumber.Groups[1].Value;

                                if (!models.ContainsKey(currentChainNumber))
                                {
                                    models[currentChainNumber] = [];
                                }

                                if (models[currentChainNumber].ContainsKey(currentModelNumber))
                                {
                                    continue;
                                }

                                models[currentChainNumber][currentModelNumber] = new UpscaleModel
                                {
                                    AllModels = GetAllModels(),
                                    ModelNumber = currentModelNumber,
                                    ChainNumber = currentChainNumber,
                                    Name = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_name", "0x0"),
                                    ResizeFactorBeforeUpscale = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_resize_factor_before_upscale", "1"),
                                    ResizeHeightBeforeUpscale = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_resize_height_before_upscale", "0")
                                };
                            }
                        }
                    }

                    foreach (var currentChainNumber in models.Keys)
                    {
                        chains[currentChainNumber].Models = new AvaloniaList<UpscaleModel>(models[currentChainNumber].Values.ToList());
                    }

                    slots[currentSlotNumber] = new UpscaleSlot
                    {
                        SlotNumber = currentSlotNumber,
                        ProfileName = parser.GetValue(section.SectionName, "profile_name", "New Profile"),
                        Chains = new AvaloniaList<UpscaleChain>(chains.Values.ToList())
                    };
                }
            }

            UpscaleSlots = new AvaloniaList<UpscaleSlot>(slots.Values);
        }

        static bool ParseBool(string value)
        {
            return value?.ToLower() == "yes";
        }

        public void WriteAnimeJaNaiConf()
        {

        }

        public void SetupAnimeJaNaiConfSlot1()
        {
//            var confPath = Path.GetFullPath(@".\mpv-upscale-2x_animejanai\portable_config\shaders\animejanai_v2.conf");
//            var backend = DirectMlSelected ? "DirectML" : NcnnSelected ? "NCNN" : "TensorRT";
//            HashSet<string> filesNeedingEngine = new();
//            var configText = new StringBuilder($@"[global]
//logging=yes
//backend={backend}
//[slot_1]
//");

//            for (var i = 0; i < UpscaleSettings.Count; i++)
//            {
//                var targetCopyPath = @$".\mpv-upscale-2x_animejanai\vapoursynth64\plugins\models\animejanai\{Path.GetFileName(UpscaleSettings[i].OnnxModelPath)}";

//                if (Path.GetFullPath(targetCopyPath) != Path.GetFullPath(UpscaleSettings[i].OnnxModelPath))
//                {
//                    File.Copy(UpscaleSettings[i].OnnxModelPath, targetCopyPath, true);
//                }

//                configText.AppendLine(@$"chain_1_model_{i + 1}_resize_height_before_upscale={UpscaleSettings[i].ResizeHeightBeforeUpscale}
//chain_1_model_{i + 1}_resize_factor_before_upscale={UpscaleSettings[i].ResizeFactorBeforeUpscale}
//chain_1_model_{i + 1}_name={Path.GetFileNameWithoutExtension(UpscaleSettings[i].OnnxModelPath)}");
//            }

//            var rife = EnableRife ? "yes" : "no";
//            configText.AppendLine($"chain_1_rife={rife}");

//            File.WriteAllText(confPath, configText.ToString());
        }
    }

    [DataContract]
    public class UpscaleSlot: ReactiveObject
    {
        private string _slotNumber = string.Empty;
        [DataMember]
        public string SlotNumber
        {
            get => _slotNumber;
            set => this.RaiseAndSetIfChanged(ref _slotNumber, value);
        }

        public string SlotIcon => $"Number{SlotNumber}Circle";

        public bool ShowSlot => int.Parse(_slotNumber) < 10;

        private string _profileName = string.Empty;
        [DataMember]
        public string ProfileName
        {
            get => _profileName;
            set => this.RaiseAndSetIfChanged(ref _profileName, value);
        }

        private AvaloniaList<UpscaleChain> _chains = [];
        [DataMember]
        public AvaloniaList<UpscaleChain> Chains
        {
            get => _chains;
            set => this.RaiseAndSetIfChanged(ref _chains, value);
        }
    }

    [DataContract]
    public class UpscaleChain : ReactiveObject
    {
        private string _chainNumber = string.Empty;
        [DataMember]
        public string ChainNumber
        {
            get => _chainNumber;
            set => this.RaiseAndSetIfChanged(ref _chainNumber, value);
        }

        private string _minResolution = 0.ToString();
        [DataMember]
        public string MinResolution
        {
            get => _minResolution;
            set => this.RaiseAndSetIfChanged(ref _minResolution, value);
        }

        private string _maxResolution = 0.ToString();
        [DataMember] 
        public string MaxResolution
        {
            get => _maxResolution;
            set => this.RaiseAndSetIfChanged(ref _maxResolution, value);
        }

        private string _minFps = 0.ToString();
        [DataMember]
        public string MinFps
        {
            get => _minFps;
            set => this.RaiseAndSetIfChanged(ref _minFps, value);
        }

        private string _maxFps = 0.ToString();
        [DataMember] 
        public string MaxFps
        {
            get => _maxFps;
            set => this.RaiseAndSetIfChanged(ref _maxFps, value);
        }

        private AvaloniaList<UpscaleModel> _models = [];
        [DataMember]
        public AvaloniaList<UpscaleModel> Models
        {
            get => _models;
            set => this.RaiseAndSetIfChanged(ref _models, value);
        }

        private bool _enableRife = false;
        [DataMember]
        public bool EnableRife
        {
            get => _enableRife;
            set => this.RaiseAndSetIfChanged(ref _enableRife, value);
        }

        public void AddModel()
        {
            Models.Add(new UpscaleModel
            {
                AllModels = MainWindowViewModel.GetAllModels(),
                ChainNumber = ChainNumber
            });

            UpdateModelHeaders();
        }

        public void DeleteModel(UpscaleModel model)
        {
            try
            {
                Models.Remove(model);
            }
            catch (ArgumentOutOfRangeException)
            {

            }

            UpdateModelHeaders();
        }

        public void UpdateModelHeaders()
        {
            for (var i = 0; i < Models.Count; i++)
            {
                Models[i].ModelNumber = (i + 1).ToString();
            }
        }

    }

    [DataContract]
    public class UpscaleModel : ReactiveObject
    {
        private AvaloniaList<string> _allModels = [];
        [DataMember]
        public AvaloniaList<string> AllModels
        {
            get => _allModels;
            set => this.RaiseAndSetIfChanged(ref _allModels, value);
        }

        private string _chainNumber = string.Empty;
        [DataMember]
        public string ChainNumber
        {
            get => _chainNumber;
            set => this.RaiseAndSetIfChanged(ref _chainNumber, value);
        }

        private string _modelNumber = string.Empty;
        [DataMember]
        public string ModelNumber
        {
            get => _modelNumber;   
            set => this.RaiseAndSetIfChanged(ref _modelNumber, value);
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

        private string _name = string.Empty;
        [DataMember]
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public void Test(UpscaleModel model)
        {
            Console.WriteLine("Test");
            //ItemsControl.ItemTemplateProperty
        }
    }

    public enum Backend
    {
        TensorRT,
        DirectML,
        NCNN
    }
}