using Avalonia.Collections;
using ReactiveUI;
using Salaros.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AnimeJaNaiConfEditor.ViewModels
{
    [DataContract]
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            DefaultUpscaleSlots = ReadAnimeJaNaiConf(new ConfigParser(DEFAULT_PROFILES_CONF)).UpscaleSlots;
            DefaultUpscaleSlots[0].MpvProfileName = "upscale-on-quality";
            DefaultUpscaleSlots[1].MpvProfileName = "upscale-on-balanced";
            DefaultUpscaleSlots[2].MpvProfileName = "upscale-on-performance";
            DefaultUpscaleSlots[0].DescriptionText = "Minimum Suggested GPU: NVIDIA RTX 4090";
            DefaultUpscaleSlots[1].DescriptionText = "Minimum Suggested GPU: NVIDIA RTX 3080";
            DefaultUpscaleSlots[2].DescriptionText = "Minimum Suggested GPU: NVIDIA RTX 3060";
            AnimeJaNaiConf = ReadAnimeJaNaiConf(AnimeJaNaiConfPath, true);


            for (var i = 0; i < AnimeJaNaiConf.UpscaleSlots.Count; i++)
            {
                AnimeJaNaiConf.UpscaleSlots[i].MpvProfileName = $"upscale-on-{i + 1}";
            }

            CheckAndDoBackup();

            SelectedMpvProfile = ReadCurrentProfileFromMpvConf();

            InitializeSelectedSlot();
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
chain_1_min_resolution=1280x720
chain_1_max_resolution=1920x1080
chain_1_min_fps=0
chain_1_max_fps=31
chain_1_model_1_resize_height_before_upscale=0
chain_1_model_1_resize_factor_before_upscale=100
chain_1_model_1_name=2x_AnimeJaNai_HD_V3_Compact
chain_1_rife=no
chain_2_min_resolution=0x0
chain_2_max_resolution=1280x720
chain_2_min_fps=0
chain_2_max_fps=31
chain_2_model_1_resize_height_before_upscale=0
chain_2_model_1_resize_factor_before_upscale=100
chain_2_model_1_name=2x_AnimeJaNai_SD_V1beta34_Compact
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
chain_1_min_resolution=1280x720
chain_1_max_resolution=1920x1080
chain_1_min_fps=0
chain_1_max_fps=31
chain_1_model_1_resize_height_before_upscale=0
chain_1_model_1_resize_factor_before_upscale=100
chain_1_model_1_name=2x_AnimeJaNai_HD_V3_UltraCompact
chain_1_rife=no
chain_2_min_resolution=0x0
chain_2_max_resolution=1280x720
chain_2_min_fps=0
chain_2_max_fps=31
chain_2_model_1_resize_height_before_upscale=0
chain_2_model_1_resize_factor_before_upscale=100
chain_2_model_1_name=2x_AnimeJaNai_SD_V1beta34_Compact
chain_2_rife=no
[slot_3]
profile_name=Performance
chain_1_min_resolution=1280x720
chain_1_max_resolution=1920x1080
chain_1_min_fps=0
chain_1_max_fps=31
chain_1_model_1_resize_height_before_upscale=0
chain_1_model_1_resize_factor_before_upscale=100
chain_1_model_1_name=2x_AnimeJaNai_HD_V3_SuperUltraCompact
chain_1_rife=no
chain_2_min_resolution=0x0
chain_2_max_resolution=1280x720
chain_2_min_fps=0
chain_2_max_fps=31
chain_2_model_1_resize_height_before_upscale=0
chain_2_model_1_resize_factor_before_upscale=100
chain_2_model_1_name=2x_AnimeJaNai_SD_V1beta34_Compact
chain_2_rife=no";

        public string ExePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        private static readonly string BACKUP_PATH_RELATIVE = "./backups";
        public string BackupPath => Path.GetFullPath(Path.Combine(ExePath, BACKUP_PATH_RELATIVE));

        public string MpvConfPath => Path.GetFullPath(Path.Combine(ExePath, "../portable_config/mpv.conf"));

        public string AnimeJaNaiConfPath => Path.GetFullPath(Path.Combine(ExePath, "./animejanai.conf"));

        public string OnnxPath => Path.GetFullPath(Path.Combine(ExePath, "./onnx"));

        private bool _showGlobalSettings = false; // TODO
        [DataMember]
        public bool ShowGlobalSettings
        {
            get => _showGlobalSettings;
            set => this.RaiseAndSetIfChanged(ref _showGlobalSettings, value);
        }

        private bool _showDefaultProfiles;
        [DataMember]
        public bool ShowDefaultProfiles
        {
            get => _showDefaultProfiles;
            set
            {
                this.RaiseAndSetIfChanged(ref _showDefaultProfiles, value);
                this.RaisePropertyChanged(nameof(CurrentSlot));
            }
        }

        private bool _showCustomProfiles = true; // TODO 
        [DataMember]
        public bool ShowCustomProfiles
        {
            get => _showCustomProfiles;
            set
            {
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
            }
        }

        private string? _selectedMpvProfile;
        [DataMember]
        public string? SelectedMpvProfile
        {
            get => _selectedMpvProfile;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedMpvProfile, value);
                this.RaisePropertyChanged(nameof(MpvConfDetected));
            }
        }

        public bool MpvConfDetected => File.Exists(MpvConfPath);


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

        private AnimeJaNaiConf _animeJaNaiConf;
        public AnimeJaNaiConf AnimeJaNaiConf
        {
            get => _animeJaNaiConf;
            set => this.RaiseAndSetIfChanged(ref _animeJaNaiConf, value);
        }

        public UpscaleSlot CurrentSlot
        {
            get => ShowCustomProfiles ? AnimeJaNaiConf.UpscaleSlots.Where(slot => slot.SlotNumber == SelectedSlotNumber).FirstOrDefault() :
                ShowDefaultProfiles ? DefaultUpscaleSlots.Where(slot => slot.SlotNumber == SelectedSlotNumber).FirstOrDefault() : null;
        }

        private UpscaleSlot? _selectedProfileToClone;
        public UpscaleSlot? SelectedProfileToClone
        {
            get => _selectedProfileToClone;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedProfileToClone, value);
                this.RaisePropertyChanged(nameof(CanCloneProfile));
            }
        }

        public bool CanCloneProfile => true; //SelectedProfileToClone != null;

        public void AddChain()
        {
            CurrentSlot.Chains.Add(new UpscaleChain(true) { Vm = this });
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

        private AvaloniaList<UpscaleSlot> _defaultUpscaleSlots = [];
        [DataMember]
        public AvaloniaList<UpscaleSlot> DefaultUpscaleSlots
        {
            get => _defaultUpscaleSlots;
            set
            {
                this.RaiseAndSetIfChanged(ref _defaultUpscaleSlots, value);
                this.RaisePropertyChanged(nameof(AllSlots));
            }
        }

        public AvaloniaList<UpscaleSlot> AllSlots => new(DefaultUpscaleSlots.Concat(AnimeJaNaiConf?.UpscaleSlots?.Where(x => x.ShowSlot && !x.ActiveSlot)));

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

        public AvaloniaList<string> GetAllModels()
        {
            return new AvaloniaList<string>(Directory.GetFiles(OnnxPath).Where(filename => Path.GetExtension(filename).Equals(".onnx", StringComparison.CurrentCultureIgnoreCase))
                .Select(filename => Path.GetFileNameWithoutExtension(filename))
                .Order().ToList());
        }


        public void Validate()
        {
            Console.WriteLine("OK");
        }

        private static List<string>? _rifeModels = null;

        public static List<string> RifeModels
        {
            get
            {
                if (_rifeModels == null)
                {
                    var models = new List<string>();
                    var modelsPath = @"..\vs-plugins\models\rife";

                    if (!Directory.Exists(modelsPath))
                    {
                        return [];
                    }

                    var files = Directory.GetFiles(modelsPath, searchPattern: "*.onnx");

                    foreach (var file in files)
                    {
                        Debug.WriteLine(file);
                        var m = Regex.Match(Path.GetFileName(file), @"rife_v(\d+)\.(\d+)(_lite)?(_ensemble)?.onnx");
                        if (m.Success)
                        {
                            var model = m.Groups[1].Value + m.Groups[2].Value;
                            if (file.Contains("_lite"))
                            {
                                model += "1";
                            }

                            if (!models.Contains(model))
                            {
                                models.Add(model);
                            }
                        }
                    }

                    models.Sort(delegate (string m1, string m2)
                    {
                        var m1i = decimal.Parse(m1[..Math.Min(3, m1.Length)]);
                        var m2i = decimal.Parse(m2[..Math.Min(3, m2.Length)]);

                        if (m1.Length > 3)
                        {
                            m1i += .1m;
                        }

                        if (m2.Length > 3)
                        {
                            m2i += .1m;
                        }

                        return m2i.CompareTo(m1i);
                    });

                    _rifeModels = [.. models.Select(m => RifeValueToLabel(m))];
                }

                return _rifeModels;
            }
        }

        public static string RifeLabelToValue(string rifeLabel)
        {
            var m = Regex.Match(rifeLabel, @"RIFE (\d+)\.(\d+)( Lite)?");
            if (m.Success)
            {
                var value = $"{m.Groups[1].Value}{m.Groups[2].Value}";
                if (m.Groups.Count > 2)
                {
                    value += "1";
                }
                return value;
            }
            else if (string.IsNullOrEmpty(rifeLabel))
            {
                return rifeLabel;
            }

            throw new ArgumentException(rifeLabel);
        }

        public static string RifeValueToLabel(string rifeValue)
        {
            string dec;

            if (string.IsNullOrEmpty(rifeValue))
            {
                return rifeValue;
            }

            if (rifeValue.Length == 2)
            {
                dec = rifeValue[1].ToString();
            }
            else
            {
                dec = rifeValue.Substring(1, 2);
            }

            var modelName = $"RIFE {rifeValue[0]}.{dec}";

            if (rifeValue.Length >= 4 && rifeValue.EndsWith('1'))
            {
                modelName += " Lite";
            }

            return modelName;
        }

        public AnimeJaNaiConf ReadAnimeJaNaiConf(string fullPath, bool autoSave = false)
        {
            return ReadAnimeJaNaiConf(new ConfigParser(fullPath), autoSave);
        }

        public AnimeJaNaiConf ReadAnimeJaNaiConf(ConfigParser parser, bool autoSave = false)
        {
            var animeJaNaiConf = new AnimeJaNaiConf(autoSave) { Vm = this };
            var slots = new Dictionary<string, UpscaleSlot>();

            animeJaNaiConf.EnableLogging = ParseBool(parser.GetValue("global", "logging", "no"));
            if (Enum.TryParse(parser.GetValue("global", "backend", "TensorRT"), out Backend backend))
            {
                switch (backend)
                {
                    case Backend.DirectML:
                        animeJaNaiConf.SetDirectMlSelected();
                        break;
                    case Backend.NCNN:
                        animeJaNaiConf.SetNcnnSelected();
                        break;
                    case Backend.TensorRT:
                    default:
                        animeJaNaiConf.SetTensorRtSelected();
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

                            chains[currentChainNumber] = new UpscaleChain(autoSave)
                            {
                                Vm = this,
                                ChainNumber = currentChainNumber,
                                MinResolution = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_min_resolution"),
                                MaxResolution = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_max_resolution"),
                                MinFps = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_min_fps"),
                                MaxFps = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_max_fps"),
                                EnableRife = ParseBool(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife")),
                                //RifeModel = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_model"),
                                RifeEnsemble = ParseBool(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_ensemble")),
                            };

                            if (int.TryParse(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_factor_numerator"), out var numerator))
                            {
                                chains[currentChainNumber].RifeFactorNumerator = numerator;
                            }

                            if (int.TryParse(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_factor_denominator"), out var denominator))
                            {
                                chains[currentChainNumber].RifeFactorDenominator = denominator;
                            }

                            if (decimal.TryParse(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_scene_detect_threshold"), out var scene_detect_threshold))
                            {
                                chains[currentChainNumber].RifeSceneDetectThreshold = scene_detect_threshold;
                            }

                            var rifeModel = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_model");

                            if (rifeModel != null)
                            {
                                chains[currentChainNumber].RifeModel = RifeValueToLabel(rifeModel);
                            }

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

                                models[currentChainNumber][currentModelNumber] = new UpscaleModel(autoSave)
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

                    slots[currentSlotNumber] = new UpscaleSlot(autoSave)
                    {
                        Vm = this,
                        SlotNumber = currentSlotNumber,
                        ProfileName = parser.GetValue(section.SectionName, "profile_name", "New Profile"),
                        Chains = new AvaloniaList<UpscaleChain>(chains.Values.ToList())
                    };
                }
            }

            animeJaNaiConf.UpscaleSlots = new AvaloniaList<UpscaleSlot>(slots.Values);

            return animeJaNaiConf;
        }

        public void ReadAnimeJaNaiConfToCurrentSlot(string fullPath, bool autoSave = false)
        {
            ReadAnimeJaNaiConfToCurrentSlot(new ConfigParser(fullPath), autoSave);
        }

        public void ReadAnimeJaNaiConfToCurrentSlot(ConfigParser parser, bool autoSave = false)
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

                        chains[currentChainNumber] = new UpscaleChain(autoSave)
                        {
                            Vm = this,
                            ChainNumber = currentChainNumber,
                            MinResolution = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_min_resolution"),
                            MaxResolution = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_max_resolution"),
                            MinFps = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_min_fps"),
                            MaxFps = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_max_fps"),
                            EnableRife = ParseBool(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife")),
                            RifeEnsemble = ParseBool(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_ensemble")),
                        };

                        if (int.TryParse(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_factor_numerator"), out var numerator))
                        {
                            chains[currentChainNumber].RifeFactorNumerator = numerator;
                        }

                        if (int.TryParse(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_factor_denominator"), out var denominator))
                        {
                            chains[currentChainNumber].RifeFactorDenominator = denominator;
                        }

                        if (decimal.TryParse(parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_scene_detect_threshold"), out var scene_detect_threshold))
                        {
                            chains[currentChainNumber].RifeSceneDetectThreshold = scene_detect_threshold;
                        }

                        var rifeModel = parser.GetValue(section.SectionName, $"chain_{currentChainNumber}_rife_model");

                        if (rifeModel != null)
                        {
                            chains[currentChainNumber].RifeModel = RifeValueToLabel(rifeModel);
                        }

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

                            models[currentChainNumber][currentModelNumber] = new UpscaleModel(true)
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
            WriteAnimeJaNaiConf(AnimeJaNaiConfPath);
        }

        public void WriteAnimeJaNaiConf(string fullPath)
        {
            var parser = ParsedAnimeJaNaiConf(AnimeJaNaiConf);
            parser?.Save(fullPath);
        }

        public static ConfigParser? ParsedAnimeJaNaiConf(AnimeJaNaiConf conf)
        {
            if (conf is null)
            {
                return null;
            }

            var parser = new ConfigParser();

            parser.SetValue("global", "backend", conf.SelectedBackend.ToString());
            parser.SetValue("global", "logging", conf.EnableLogging ? "yes" : "no");

            foreach (var profile in conf.UpscaleSlots)
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
                    if (chain.RifeModel == null)
                    {
                        chain.RifeModel = chain.RifeModelList.FirstOrDefault("");
                    }

                    var rifeModel = RifeLabelToValue(chain.RifeModel);

                    parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_model", rifeModel);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_factor_numerator", chain.RifeFactorNumerator ?? 1);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_factor_denominator", chain.RifeFactorDenominator ?? 1);
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_scene_detect_threshold", $"{chain.RifeSceneDetectThreshold ?? 0.015M}");
                    parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_ensemble", chain.RifeEnsemble ? "yes" : "no");
                }
            }

            return parser;
        }

        public void WriteAnimeJaNaiCurrentProfileConf(string fullPath)
        {
            var parser = ParsedAnimeJaNaiProfileConf(CurrentSlot);
            parser.Save(fullPath);
        }

        public ConfigParser ParsedAnimeJaNaiProfileConf(UpscaleSlot slot)
        {
            var parser = new ConfigParser();
            var section = "slot";

            parser.SetValue(section, "profile_name", slot.ProfileName);
            foreach (var chain in slot.Chains)
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
                parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_factor_numerator", chain.RifeFactorNumerator ?? 0);
                parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_factor_denominator", chain.RifeFactorDenominator ?? 1);
                parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_model", RifeLabelToValue(chain.RifeModel));
                parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_ensemble", chain.RifeEnsemble);
                parser.SetValue(section, $"chain_{chain.ChainNumber}_rife_scene_detect_threshold", $"{chain.RifeSceneDetectThreshold ?? 0.015M}");
            }

            return parser;
        }

        public void CheckAndDoBackup()
        {
            Task.Run(() =>
            {
                if (!Path.Exists(BackupPath))
                {
                    Directory.CreateDirectory(BackupPath);
                }

                var files = Directory.EnumerateFiles(BackupPath)
                            .Where(f => Path.GetFileName(f).StartsWith("autobackup_"))
                            .OrderByDescending(f => f)
                            .ToList();

                var currentConfStr = ParsedAnimeJaNaiConf(AnimeJaNaiConf)?.ToString();

                if (files.Count >= 10)
                {
                    // Delete oldest backup
                    File.Delete(files.Last());
                }

                if (files.Count > 0)
                {
                    var backupConf = ReadAnimeJaNaiConf(files.First());
                    if (currentConfStr == ParsedAnimeJaNaiConf(backupConf)?.ToString())
                    {
                        // Backup already exists - no need to create another backup
                        return;
                    }
                }

                WriteAnimeJaNaiConf(Path.Join(BackupPath, $"autobackup_{DateTime.Now:yyyyMMdd-HHmmss}.conf"));
            });
        }

#pragma warning disable CA1822 // Mark members as static
        public async void LaunchBenchmark()
#pragma warning restore CA1822 // Mark members as static
        {
            await Task.Run(async () =>
            {
                using var process = new Process();

                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = @"/C .\benchmarks\animejanai_benchmark_all.bat";

                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardError = false;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.WorkingDirectory = ExePath;

                process.Start();
                await process.WaitForExitAsync();
            });
        }

#pragma warning disable CA1822 // Mark members as static
        public async void OpenModelsDirectory()
#pragma warning restore CA1822 // Mark members as static
        {
            await Task.Run(() =>
            {
                Process.Start("explorer.exe", OnnxPath);
            });
        }

        public void SelectDefaultProfile()
        {
            if (SelectedMpvProfile == CurrentSlot.MpvProfileName)
            {
                SelectedMpvProfile = null;
                WriteCurrentProfileToMpvConf(true);
            }
            else
            {
                SelectedMpvProfile = CurrentSlot.MpvProfileName;
                WriteCurrentProfileToMpvConf();
            }
        }

        private string? ReadCurrentProfileFromMpvConf()
        {
            if (!MpvConfDetected)
            {
                // can't find mpv.conf - ignore and return
                return null;
            }

            var confText = File.ReadAllText(MpvConfPath);

            string pattern = @"\[default\]\s*profile=(.+)$";

            Match match = Regex.Match(confText, pattern, RegexOptions.Multiline);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            else
            {
                return null;
            }
        }

        private void WriteCurrentProfileToMpvConf(bool upscaleOff = false)
        {
            if (!MpvConfDetected)
            {
                // can't find mpv.conf - ignore and return
                return;
            }

            var confText = File.ReadAllText(MpvConfPath);
            var pattern = @"\[default\]\s*profile=(.+)$";
            var replacementProfile = upscaleOff ? "upscale-off" : CurrentSlot.MpvProfileName;

            var result = Regex.Replace(confText, pattern, $"[default]\nprofile={replacementProfile}", RegexOptions.Multiline);

            File.WriteAllText(MpvConfPath, result);
        }

        private void InitializeSelectedSlot()
        {
            if (SelectedMpvProfile == null || SelectedMpvProfile == "upscale-off")
            {
                return;
            }

            var defaultSlot = DefaultUpscaleSlots.Where(x => x.MpvProfileName == SelectedMpvProfile).FirstOrDefault();

            if (defaultSlot != null)
            {
                HandleShowDefaultProfile(defaultSlot.SlotNumber);
                return;
            }

            var customSlot = AnimeJaNaiConf.UpscaleSlots.Where(x => x.MpvProfileName == SelectedMpvProfile).FirstOrDefault();

            if (customSlot != null)
            {
                HandleShowCustomProfile(customSlot.SlotNumber);
            }
        }
    }

    [DataContract]
    public class AnimeJaNaiConf : ReactiveObject
    {
        public AnimeJaNaiConf(bool autoSave = false)
        {
            Debug.WriteLine($"autoSave? {autoSave}");
            if (autoSave)
            {
                this.WhenAnyValue(
                    x => x.EnableLogging,
                    x => x.TensorRtSelected,
                    x => x.DirectMlSelected,
                    x => x.NcnnSelected).Subscribe(x =>
                    {
                        Vm?.WriteAnimeJaNaiConf();
                    });
            }
        }

        private MainWindowViewModel? _vm;
        public MainWindowViewModel? Vm
        {
            get => _vm;
            set => this.RaiseAndSetIfChanged(ref _vm, value);
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

        private AvaloniaList<UpscaleSlot> _upscaleSlots = [];
        [DataMember]
        public AvaloniaList<UpscaleSlot> UpscaleSlots
        {
            get => _upscaleSlots;
            set
            {
                this.RaiseAndSetIfChanged(ref _upscaleSlots, value);
                Vm?.RaisePropertyChanged("AllSlots");
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

        public Backend SelectedBackend => TensorRtSelected ? Backend.TensorRT : DirectMlSelected ? Backend.DirectML : NcnnSelected ? Backend.NCNN : Backend.TensorRT;
    }

    [DataContract]
    public class UpscaleSlot : ReactiveObject
    {
        public UpscaleSlot(bool autoSave = false)
        {
            if (autoSave)
            {
                this.WhenAnyValue(
                    x => x.SlotNumber,
                    x => x.ProfileName,
                    x => x.Chains.Count
                ).Subscribe(x =>
                {
                    Vm?.WriteAnimeJaNaiConf();
                });
            }

            this.WhenAnyValue(x => x.Vm).Subscribe(x =>
            {
                sub?.Dispose();
                sub = Vm.WhenAnyValue(
                    x => x.SelectedSlotNumber,
                    x => x.ShowCustomProfiles,
                    x => x.ShowDefaultProfiles
                    ).Subscribe(x =>
                {
                    this.RaisePropertyChanged(nameof(ActiveSlot));
                    Vm?.RaisePropertyChanged("AllSlots");
                });

                sub2?.Dispose();
                sub2 = Vm.WhenAnyValue(x => x.SelectedMpvProfile).Subscribe(x =>
                {
                    this.RaisePropertyChanged(nameof(IsSelectedMpvProfile));
                });
            });
        }

        private IDisposable? sub;
        private IDisposable? sub2;

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

        public bool ActiveSlot => ((Vm?.ShowCustomProfiles ?? false) && IsCustomSlot || (Vm?.ShowDefaultProfiles ?? false) && !IsCustomSlot) && SlotNumber == Vm?.SelectedSlotNumber;

        public bool IsCustomSlot => MpvProfileName?.Any(char.IsDigit) ?? false;

        public string SlotIcon => $"Number{SlotNumber}Circle";

        public bool ShowSlot => int.Parse(_slotNumber) < 10;

        private string _profileName = string.Empty;
        [DataMember]
        public string ProfileName
        {
            get => _profileName;
            set => this.RaiseAndSetIfChanged(ref _profileName, value);
        }

        private string _descriptionText = string.Empty;
        [DataMember]
        public string DescriptionText
        {
            get => _descriptionText;
            set => this.RaiseAndSetIfChanged(ref _descriptionText, value);
        }

        private string _mpvProfileName;
        [DataMember]
        public string MpvProfileName
        {
            get => _mpvProfileName;
            set => this.RaiseAndSetIfChanged(ref _mpvProfileName, value);
        }

        public bool IsSelectedMpvProfile => MpvProfileName == Vm?.SelectedMpvProfile;

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
        public UpscaleChain(bool autoSave = false)
        {
            AutoSave = autoSave;

            if (autoSave)
            {
                var g1 = this.WhenAnyValue(
                    x => x.ChainNumber,
                    x => x.MinResolution,
                    x => x.MaxResolution,
                    x => x.MinFps,
                    x => x.MaxFps,
                    x => x.Models.Count,
                    x => x.EnableRife
                );

                var g2 = this.WhenAnyValue(
                    x => x.RifeFactorNumerator,
                    x => x.RifeFactorDenominator,
                    x => x.RifeModel,
                    x => x.RifeSceneDetectThreshold,
                    x => x.RifeEnsemble
                );

                g1.CombineLatest(g2).Subscribe(x =>
                {
                    Vm?.WriteAnimeJaNaiConf();
                });
            }
        }

        public bool AutoSave { get; set; }

        public MainWindowViewModel? Vm { get; set; }

        private string _chainNumber = string.Empty;
        [DataMember]
        public string ChainNumber
        {
            get => _chainNumber;
            set => this.RaiseAndSetIfChanged(ref _chainNumber, value);
        }

        private string _minResolution = "0x0";
        [DataMember]
        public string MinResolution
        {
            get => _minResolution;
            set => this.RaiseAndSetIfChanged(ref _minResolution, value);
        }

        private string _maxResolution = "0x0";
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

        private List<string> _rifeModelList = new(MainWindowViewModel.RifeModels);

        public List<string> RifeModelList
        {
            get => _rifeModelList;
            set => this.RaiseAndSetIfChanged(ref _rifeModelList, value);
        }

        private string _rifeModel = "RIFE 4.14";
        [DataMember]
        public string RifeModel
        {
            get => _rifeModel;
            set => this.RaiseAndSetIfChanged(ref _rifeModel, value);
        }

        private bool _rifeEnsemble = false;
        [DataMember]
        public bool RifeEnsemble
        {
            get => _rifeEnsemble;
            set => this.RaiseAndSetIfChanged(ref _rifeEnsemble, value);
        }

        private int? _rifeFactorNumerator = 2;
        [DataMember]
        public int? RifeFactorNumerator
        {
            get => _rifeFactorNumerator ?? 2;
            set => this.RaiseAndSetIfChanged(ref _rifeFactorNumerator, value ?? 2);
        }

        private int? _rifeFactorDenominator = 1;
        [DataMember]
        public int? RifeFactorDenominator
        {
            get => _rifeFactorDenominator ?? 1;
            set => this.RaiseAndSetIfChanged(ref _rifeFactorDenominator, value ?? 1);
        }

        private decimal? _rifeSceneDetectThreshold = 0.150M;
        [DataMember]
        public decimal? RifeSceneDetectThreshold
        {
            get => _rifeSceneDetectThreshold;
            set => this.RaiseAndSetIfChanged(ref _rifeSceneDetectThreshold, value ?? 0.150M);
        }

        public void AddModel()
        {
            Models.Add(new UpscaleModel(AutoSave)
            {
                Vm = Vm,
                AllModels = Vm?.GetAllModels(),
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
        public UpscaleModel(bool autoSave = false)
        {
            if (autoSave)
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