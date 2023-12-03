using Avalonia.Collections;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
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
            DefaultUpscaleSlots = ReadAnimeJaNaiConf(new ConfigParser(DEFAULT_PROFILES_CONF));
            UpscaleSlots = ReadAnimeJaNaiConf(Path.GetFullPath(@".\animejanai.conf"));

            this.WhenAnyValue(
                x => x.EnableLogging,
                x => x.TensorRtSelected,
                x => x.DirectMlSelected,
                x => x.NcnnSelected).Subscribe(x =>
                {
                    WriteAnimeJaNaiConf();
                });
        }

        private string[] _commonResolutions = [
            "0x0",
            "640x360",
            "640x480",
            "720x480",
            "768x576",
            "960x540",
            "1024x576",
            "1280x720",
            "1440x1080",
            "1920x1080"];

        public string[] CommonResolutions
        {
            get => _commonResolutions;
            set => this.RaiseAndSetIfChanged(ref _commonResolutions, value);
        }

        private static readonly string DEFAULT_PROFILES_CONF = @"[slot_1]
profile_name=Quality
chain_1_min_resolution=0x0
chain_1_max_resolution=1280x720
chain_1_min_fps=0
chain_1_max_fps=31
chain_1_model_1_resize_height_before_upscale=0
chain_1_model_1_resize_factor_before_upscale=100
chain_1_model_1_name=2x_AnimeJaNai_SD_V1betaRC9_Compact
chain_1_rife=no
chain_2_min_resolution=1280x720
chain_2_max_resolution=1920x1080
chain_2_min_fps=0
chain_2_max_fps=31
chain_2_model_1_resize_height_before_upscale=0
chain_2_model_1_resize_factor_before_upscale=100
chain_2_model_1_name=2x_AnimeJaNai_HD_V3_Compact
chain_2_rife=no
chain_3_min_resolution=0x0
chain_3_max_resolution=1920x1080
chain_3_min_fps=0
chain_3_max_fps=61
chain_3_model_1_resize_height_before_upscale=0
chain_3_model_1_resize_factor_before_upscale=100
chain_3_model_1_name=2x_AnimeJaNai_HD_V3_SuperUltraCompact
chain_3_rife=no
[slot_2]
profile_name=Balanced
chain_1_min_resolution=0x0
chain_1_max_resolution=1280x720
chain_1_min_fps=0
chain_1_max_fps=31
chain_1_model_1_resize_height_before_upscale=0
chain_1_model_1_resize_factor_before_upscale=100
chain_1_model_1_name=2x_AnimeJaNai_SD_V1betaRC9_Compact
chain_1_rife=no
chain_2_min_resolution=1280x720
chain_2_max_resolution=1920x1080
chain_2_min_fps=0
chain_2_max_fps=31
chain_2_model_1_resize_height_before_upscale=0
chain_2_model_1_resize_factor_before_upscale=100
chain_2_model_1_name=2x_AnimeJaNai_HD_V3_UltraCompact
chain_2_rife=no
[slot_3]
profile_name=Performance
chain_1_min_resolution=0x0
chain_1_max_resolution=1280x720
chain_1_min_fps=0
chain_1_max_fps=31
chain_1_model_1_resize_height_before_upscale=0
chain_1_model_1_resize_factor_before_upscale=100
chain_1_model_1_name=2x_AnimeJaNai_SD_V1betaRC9_Compact
chain_1_rife=no
chain_2_min_resolution=1280x720
chain_2_max_resolution=1920x1080
chain_2_min_fps=0
chain_2_max_fps=31
chain_2_model_1_resize_height_before_upscale=0
chain_2_model_1_resize_factor_before_upscale=100
chain_2_model_1_name=2x_AnimeJaNai_HD_V3_SuperUltraCompact
chain_2_rife=no";

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
            set
            {
                this.RaiseAndSetIfChanged(ref _showDefaultProfiles, value);
                this.RaisePropertyChanged(nameof(DefaultProfile1Active));
                this.RaisePropertyChanged(nameof(DefaultProfile2Active));
                this.RaisePropertyChanged(nameof(DefaultProfile3Active));
                this.RaisePropertyChanged(nameof(CurrentSlot));
            }
        }

        public bool DefaultProfile1Active => ShowDefaultProfiles && SelectedSlotNumber == 1.ToString();
        public bool DefaultProfile2Active => ShowDefaultProfiles && SelectedSlotNumber == 2.ToString();
        public bool DefaultProfile3Active => ShowDefaultProfiles && SelectedSlotNumber == 3.ToString();

        private bool _showCustomProfiles = true; // TODO 
        [DataMember] public bool ShowCustomProfiles
        {
            get => _showCustomProfiles;
            set {
                this.RaiseAndSetIfChanged(ref _showCustomProfiles, value);
                this.RaisePropertyChanged(nameof(CurrentSlot));
            }
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
                this.RaisePropertyChanged(nameof(DefaultProfile1Active));
                this.RaisePropertyChanged(nameof(DefaultProfile2Active));
                this.RaisePropertyChanged(nameof(DefaultProfile3Active));
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
            get => ShowCustomProfiles ? UpscaleSlots.Where(slot => slot.SlotNumber == SelectedSlotNumber).FirstOrDefault() : 
                ShowDefaultProfiles ? DefaultUpscaleSlots.Where(slot => slot.SlotNumber == SelectedSlotNumber).FirstOrDefault() : null;
        }

        public void AddChain()
        {
            CurrentSlot.Chains.Add(new UpscaleChain { Vm = this });
            UpdateChainHeaders();
        }

        public void DeleteChain(UpscaleChain chain)
        {
            try
            {
                CurrentSlot.Chains.Remove(chain);
            }
            catch (ArgumentOutOfRangeException)
            {

            }

            UpdateChainHeaders();
        }

        public void UpdateChainHeaders()
        {
            for (var i = 0; i < CurrentSlot.Chains.Count; i++)
            {
                CurrentSlot.Chains[i].ChainNumber = (i + 1).ToString();
            }
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

        private AvaloniaList<UpscaleSlot> _defaultUpscaleSlots = [];
        [DataMember]
        public AvaloniaList<UpscaleSlot> DefaultUpscaleSlots
        {
            get => _defaultUpscaleSlots;
            set => this.RaiseAndSetIfChanged(ref _defaultUpscaleSlots, value);
        }

        private AvaloniaList<UpscaleSlot> _upscaleSlots = [];
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
            var modelsPath = Path.GetFullPath(@"./onnx"); 
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

        public Backend SelectedBackend => TensorRtSelected ? Backend.TensorRT : DirectMlSelected ? Backend.DirectML : NcnnSelected ? Backend.NCNN : Backend.TensorRT;


        public void Validate()
        {
            Console.WriteLine("OK");
        }

        public AvaloniaList<UpscaleSlot> ReadAnimeJaNaiConf(string fullPath)
        {
            return ReadAnimeJaNaiConf(new ConfigParser(fullPath));
        }

        public AvaloniaList<UpscaleSlot> ReadAnimeJaNaiConf(ConfigParser parser)
        {
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
                                Vm = this,
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
                                    Vm = this,
                                    AllModels = GetAllModels(),
                                    ModelNumber = currentModelNumber,
                                    Name = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_name", "0x0"),
                                    ResizeFactorBeforeUpscale = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_resize_factor_before_upscale", 100.ToString()),
                                    ResizeHeightBeforeUpscale = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_resize_height_before_upscale", 0.ToString())
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
                        Vm = this,
                        SlotNumber = currentSlotNumber,
                        ProfileName = parser.GetValue(section.SectionName, "profile_name", "New Profile"),
                        Chains = new AvaloniaList<UpscaleChain>(chains.Values.ToList())
                    };
                }
            }

            return new AvaloniaList<UpscaleSlot>(slots.Values);
        }

        public void ReadAnimeJaNaiConfToCurrentSlot(string fullPath)
        {
            ReadAnimeJaNaiConfToCurrentSlot(new ConfigParser(fullPath));
        }

        public void ReadAnimeJaNaiConfToCurrentSlot(ConfigParser parser)
        {
            var sectionKey = "slot";

            foreach (var section in parser.Sections)
            {
                if (section.SectionName != sectionKey)
                {
                    return; // Invalid
                }

                var currentSlotNumber = Regex.Match(section.SectionName, @"slot_(\d+)").Groups[1].Value;

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
                            Vm = this,
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
                                Vm = this,
                                AllModels = GetAllModels(),
                                ModelNumber = currentModelNumber,
                                Name = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_name", "0x0"),
                                ResizeFactorBeforeUpscale = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_resize_factor_before_upscale", 100.ToString()),
                                ResizeHeightBeforeUpscale = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_model_{currentModelNumber}_resize_height_before_upscale", 0.ToString())
                            };
                        }
                    }
                }

                foreach (var currentChainNumber in models.Keys)
                {
                    chains[currentChainNumber].Models = new AvaloniaList<UpscaleModel>(models[currentChainNumber].Values.ToList());
                }

                CurrentSlot.Vm = this;
                CurrentSlot.ProfileName = parser.GetValue(sectionKey, "profile_name", "New Profile");
                CurrentSlot.Chains = new AvaloniaList<UpscaleChain>(chains.Values.ToList());
            }
        }

        static bool ParseBool(string value)
        {
            return value?.ToLower() == "yes";
        }

        public void WriteAnimeJaNaiConf()
        {
            WriteAnimeJaNaiConf(Path.GetFullPath(@".\animejanai-test.conf"));
        }

        public void WriteAnimeJaNaiConf(string fullPath)
        {
            var parser = new ConfigParser();

            parser.SetValue("global", "backend", SelectedBackend.ToString());
            parser.SetValue("global", "logging", EnableLogging ? "yes" : "no");

            foreach (var profile in UpscaleSlots)
            {
                var section = $"slot_{profile.SlotNumber}";
                parser.SetValue(section, "profile_name", profile.ProfileName);

                foreach (var chain in profile.Chains)
                {
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_min_resolution", chain.MinResolution);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_max_resolution", chain.MaxResolution);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_min_fps", chain.MinFps);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_max_fps", chain.MaxFps);

                    foreach (var model in chain.Models)
                    {
                        parser.SetValue(section, $"chain_{chain.ChainNumber}_model_{model.ModelNumber}_resize_height_before_upscale", model.ResizeHeightBeforeUpscale);
                        parser.SetValue(section, $"chain_{chain.ChainNumber}_model_{model.ModelNumber}_resize_factor_before_upscale", model.ResizeFactorBeforeUpscale);
                        parser.SetValue(section, $"chain_{chain.ChainNumber}_model_{model.ModelNumber}_name", model.Name);
                    }

                    parser.SetValue(section, $"chain_{chain.ChainNumber}_rife", chain.EnableRife ? "yes" : "no");
                }
            }

            parser.Save(fullPath);
        }

        public void WriteAnimeJaNaiCurrentProfileConf(string fullPath)
        {
            var parser = new ConfigParser();
            var section = "slot";

            parser.SetValue(section, "profile_name", CurrentSlot.ProfileName);
            foreach (var chain in CurrentSlot.Chains)
            {
                parser.SetValue(section, $"chain_{chain.ChainNumber}_min_resolution", chain.MinResolution);
                parser.SetValue(section, $"chain_{chain.ChainNumber}_max_resolution", chain.MaxResolution);
                parser.SetValue(section, $"chain_{chain.ChainNumber}_min_fps", chain.MinFps);
                parser.SetValue(section, $"chain_{chain.ChainNumber}_max_fps", chain.MaxFps);

                foreach (var model in chain.Models)
                {
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_model_{model.ModelNumber}_resize_height_before_upscale", model.ResizeHeightBeforeUpscale);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_model_{model.ModelNumber}_resize_factor_before_upscale", model.ResizeFactorBeforeUpscale);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_model_{model.ModelNumber}_name", model.Name);
                }

                parser.SetValue(section, $"chain_{chain.ChainNumber}_rife", chain.EnableRife ? "yes" : "no");
            }

            parser.Save(fullPath);
        }
    }

    [DataContract]
    public class UpscaleSlot: ReactiveObject
    {
        public UpscaleSlot()
        {
            this.WhenAnyValue(
                x => x.SlotNumber,
                x => x.ProfileName,
                x => x.Chains.Count
            ).Subscribe(x =>
            {
                Vm?.WriteAnimeJaNaiConf();
            });

            this.WhenAnyValue(x => x.Vm).Subscribe(x =>
            {
                sub?.Dispose();
                sub = Vm.WhenAnyValue(x => x.SelectedSlotNumber, x => x.ShowCustomProfiles).Subscribe(x =>
                {
                    this.RaisePropertyChanged(nameof(ActiveSlot));
                });
            });
        }

        private IDisposable? sub;

        private MainWindowViewModel? _vm;
        public MainWindowViewModel? Vm 
        { 
            get => _vm; 
            set => this.RaiseAndSetIfChanged(ref _vm, value); 
        }

        private string _slotNumber = string.Empty;
        [DataMember]
        public string SlotNumber
        {
            get => _slotNumber;
            set => this.RaiseAndSetIfChanged(ref _slotNumber, value);
        }

        public bool ActiveSlot => (Vm?.ShowCustomProfiles ?? false) && SlotNumber == Vm?.SelectedSlotNumber;

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
        public UpscaleChain()
        {
            this.WhenAnyValue(
                x => x.ChainNumber,
                x => x.MinResolution,
                x => x.MaxResolution,
                x => x.MinFps,
                x => x.MaxFps,
                x => x.Models.Count
            ).Subscribe(x =>
            {
                Vm?.WriteAnimeJaNaiConf();
            });
        }

        public MainWindowViewModel? Vm { get; set; }

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
                Vm = Vm,
                AllModels = MainWindowViewModel.GetAllModels(),
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
        public UpscaleModel()
        {
            this.WhenAnyValue(
                x => x.ModelNumber,
                x => x.ResizeHeightBeforeUpscale,
                x => x.ResizeFactorBeforeUpscale,
                x => x.Name
            ).Subscribe(x =>
            {
                Vm?.WriteAnimeJaNaiConf();
            });
        }

        public MainWindowViewModel? Vm { get; set; }

        private AvaloniaList<string> _allModels = [];
        [DataMember]
        public AvaloniaList<string> AllModels
        {
            get => _allModels;
            set => this.RaiseAndSetIfChanged(ref _allModels, value);
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
            set
            {
                this.RaiseAndSetIfChanged(ref _resizeHeightBeforeUpscale, value);
                this.RaisePropertyChanged(nameof(ResizeFactorBeforeUpscaleIsEnabled));
            }
        }

        private string _resizeFactorBeforeUpscale = 100.ToString();
        [DataMember]
        public string ResizeFactorBeforeUpscale
        {
            get => _resizeFactorBeforeUpscale;
            set => this.RaiseAndSetIfChanged(ref _resizeFactorBeforeUpscale, value);
        }

        public bool ResizeFactorBeforeUpscaleIsEnabled => ResizeHeightBeforeUpscale == 0.ToString();

        private string _name = string.Empty;
        [DataMember]
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }
    }

    public enum Backend
    {
        TensorRT,
        DirectML,
        NCNN
    }
}