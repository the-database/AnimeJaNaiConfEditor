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
using Material.Icons.Avalonia;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AnimeJaNaiConfEditor.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        private bool _autoScrollConsole = true;
        private bool _userWantsToQuit = false;
        private bool _focusedRecently = false;
        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);
            this.WhenActivated(disposable => { });
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

            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var storageProvider = topLevel.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Full Conf File",
                AllowMultiple = false,
                FileTypeFilter = new FilePickerFileType[] { new("AnimeJaNai Conf File") { Patterns = new[] { "*.conf" }, MimeTypes = new[] { "*/*" } }, FilePickerFileTypes.All },
            });

            if (files.Count >= 1)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var inPath = files[0].TryGetLocalPath();

                    if (inPath != null)
                    {
                        await Task.Run(() =>
                        {
                            vm.UpscaleSlots = vm.ReadAnimeJaNaiConf(inPath);
                        });
                    }                    
                }
            }
        }

        private async void ExportFullConfButtonClick(object? sender, RoutedEventArgs e)
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Full Conf File",
                DefaultExtension = "conf",
                FileTypeChoices = new FilePickerFileType[]
                {
                    new("AnimeJaNai Conf File (*.conf)") { Patterns = new[] { "*.conf" } },
                },
            });

            if (file is not null)
            {
                if (DataContext is MainWindowViewModel vm)
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

        private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
        }
    }
}