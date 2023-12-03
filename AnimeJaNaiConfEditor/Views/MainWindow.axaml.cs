using AnimeJaNaiConfEditor.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Material.Icons.Avalonia;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AnimeJaNaiConfEditor.Views
{
    public partial class MainWindow : AppWindow
    {
        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            Closing += MainWindow_Closing;
            Opened += MainWindow_Opened;
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {

            }
        }

        private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {

            }
        }

        private async void ImportFullConfButtonClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // Get top level from the current control. Alternatively, you can use Window reference instead.
                var topLevel = GetTopLevel(this);

                // Start async operation to open the dialog.
                var storageProvider = topLevel.StorageProvider;

                var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Profile Conf File",
                    AllowMultiple = false,
                    FileTypeFilter = new FilePickerFileType[] { new("AnimeJaNai Conf File") { Patterns = new[] { "*.conf" }, MimeTypes = new[] { "*/*" } }, FilePickerFileTypes.All },
                    SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(vm.BackupPath),
                });

                if (files.Count >= 1)
                {

                    var inPath = files[0].TryGetLocalPath();

                    if (inPath != null)
                    {
                        var td = new TaskDialog
                        {
                            Title = "Confirm Full Conf Import",
                            ShowProgressBar = false,
                            Content = "The following full conf file will be imported. All configuration settings will be backed up and then all configuration settings for ALL PROFILES will be replaced with the imported conf file.\n\n" +
    inPath,
                            Buttons =
            {
                TaskDialogButton.OKButton,
                TaskDialogButton.CancelButton
            }
                        };


                        td.Closing += async (s, e) =>
                        {
                            if ((TaskDialogStandardResult)e.Result == TaskDialogStandardResult.OK)
                            {
                                var deferral = e.GetDeferral();

                                td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
                                td.ShowProgressBar = true;
                                int value = 0;


                                await Task.Run(() =>
                                {
                                    vm.CheckAndDoBackup();
                                    vm.AnimeJaNaiConf = vm.ReadAnimeJaNaiConf(inPath);
                                });

                                deferral.Complete();
                            }
                        };

                        td.XamlRoot = VisualRoot as Visual;
                        _ = await td.ShowAsync();
                    }

                }
            }
        }

        private async void ImportCurrentProfileConfButtonClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // Get top level from the current control. Alternatively, you can use Window reference instead.
                var topLevel = GetTopLevel(this);

                // Start async operation to open the dialog.
                var storageProvider = topLevel.StorageProvider;

                var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Full Conf File",
                    AllowMultiple = false,
                    FileTypeFilter = new FilePickerFileType[] { new("AnimeJaNai Profile Conf File") { Patterns = new[] { "*.pconf" }, MimeTypes = new[] { "*/*" } }, FilePickerFileTypes.All },
                    SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(vm.BackupPath),
                });

                if (files.Count >= 1)
                {

                    var inPath = files[0].TryGetLocalPath();

                    if (inPath != null)
                    {
                        if (vm.CurrentSlot.Chains.Count == 0)
                        {
                            // blank slot, no need to prompt before importing and no need to do backup
                            vm.ReadAnimeJaNaiConfToCurrentSlot(inPath, true);
                        }
                        else
                        {
                            var td = new TaskDialog
                            {
                                Title = "Confirm Profile Conf Import",
                                ShowProgressBar = false,
                                Content = $"The following profile conf file will be imported to the current profile {vm.CurrentSlot.ProfileName}. All configuration settings will be backed up and then all configuration settings for the current profile {vm.CurrentSlot.ProfileName} will be overwritten.\n\n" +
                                inPath,
                                Buttons =
                            {
                                TaskDialogButton.OKButton,
                                TaskDialogButton.CancelButton
                            }
                            };


                            td.Closing += async (s, e) =>
                            {
                                if ((TaskDialogStandardResult)e.Result == TaskDialogStandardResult.OK)
                                {
                                    var deferral = e.GetDeferral();

                                    td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
                                    td.ShowProgressBar = true;

                                    await Task.Run(() =>
                                    {
                                        vm.CheckAndDoBackup();
                                        vm.ReadAnimeJaNaiConfToCurrentSlot(inPath, true);
                                    });

                                    deferral.Complete();
                                }
                            };

                            td.XamlRoot = VisualRoot as Visual;
                            _ = await td.ShowAsync();
                        }
                    }

                } 
            }
        }

        private async void CloneSelectedProfileToCurrentProfile(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.SelectedProfileToClone != null)
                {
                    if (vm.CurrentSlot.Chains.Count == 0)
                    {
                        // Current slot is blank - no need to ask user for confirmation before cloning and no need to do backup
                        vm.ReadAnimeJaNaiConfToCurrentSlot(
                            vm.ParsedAnimeJaNaiProfileConf(vm.SelectedProfileToClone),
                            true);
                    }
                    else
                    {
                        var td = new TaskDialog
                        {
                            Title = "Confirm Profile Conf Import",
                            ShowProgressBar = false,
                            Content = $"The profile {vm.SelectedProfileToClone.ProfileName} will be cloned to the current profile {vm.CurrentSlot.ProfileName}. All configuration settings will be backed up and then all configuration settings for the current profile {vm.CurrentSlot.ProfileName} will be overwritten.",
                            Buttons =
                        {
                            TaskDialogButton.OKButton,
                            TaskDialogButton.CancelButton
                        }
                        };


                        td.Closing += async (s, e) =>
                        {
                            if ((TaskDialogStandardResult)e.Result == TaskDialogStandardResult.OK)
                            {
                                var deferral = e.GetDeferral();

                                td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
                                td.ShowProgressBar = true;
                                int value = 0;


                                await Task.Run(() =>
                                {
                                    vm.CheckAndDoBackup();
                                    vm.ReadAnimeJaNaiConfToCurrentSlot(
                                        vm.ParsedAnimeJaNaiProfileConf(vm.SelectedProfileToClone),
                                        true);
                                });

                                deferral.Complete();
                            }
                        };

                        td.XamlRoot = VisualRoot as Visual;
                        _ = await td.ShowAsync();
                    }
                }
            }
        }

        private async void ExportFullConfButtonClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // Get top level from the current control. Alternatively, you can use Window reference instead.
                var topLevel = GetTopLevel(this);

                var storageProvider = topLevel.StorageProvider;

                // Start async operation to open the dialog.
                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export Full Conf File",
                    DefaultExtension = "conf",
                    FileTypeChoices = new FilePickerFileType[]
                    {
                    new("AnimeJaNai Conf File (*.conf)") { Patterns = new[] { "*.conf" } },
                    },
                    SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(vm.BackupPath),
                });

                if (file is not null)
                {

                    //vm.OutputFilePath = file.TryGetLocalPath() ?? "";

                    var outPath = file.TryGetLocalPath();

                    if (outPath != null)
                    {
                        vm.WriteAnimeJaNaiConf(outPath);
                    }

                }
            }
        }

        private async void ExportCurrentProfileConfButtonClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // Get top level from the current control. Alternatively, you can use Window reference instead.
                var topLevel = GetTopLevel(this);

                var storageProvider = topLevel.StorageProvider;

                // Start async operation to open the dialog.
                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export Current Profile Conf File",
                    DefaultExtension = "conf",
                    FileTypeChoices = new FilePickerFileType[]
                    {
                    new("AnimeJaNai Profile Conf File (*.pconf)") { Patterns = new[] { "*.pconf" } },
                    },
                    SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(vm.BackupPath),
                });

                if (file is not null)
                {
                    var outPath = file.TryGetLocalPath();

                    if (outPath != null)
                    {
                        vm.WriteAnimeJaNaiCurrentProfileConf(outPath);
                    }
                }
            }
        }
    }
}