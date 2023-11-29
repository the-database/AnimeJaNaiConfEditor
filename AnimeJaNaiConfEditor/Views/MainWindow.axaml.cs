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

        private async void OpenOnnxFileButtonClick(object? sender, RoutedEventArgs e)
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var storageProvider = topLevel.StorageProvider;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open ONNX Model File",
                AllowMultiple = false,
                FileTypeFilter = new FilePickerFileType[] { new("ONNX Model File") { Patterns = new[] { "*.onnx" }, MimeTypes = new[] { "*/*" } }, FilePickerFileTypes.All },
                SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(@".\mpv-upscale-2x_animejanai\vapoursynth64\plugins\models\animejanai"))),
            });

            if (files.Count >= 1)
            {
                //// Open reading stream from the first file.
                //await using var stream = await files[0].OpenReadAsync();
                //using var streamReader = new StreamReader(stream);
                //// Reads all the content of file as a text.
                //var fileContent = await streamReader.ReadToEndAsync();
                if (DataContext is MainWindowViewModel vm)
                {
                    // TODO 
                    //vm.InputFilePath = files[0].TryGetLocalPath() ?? "";
                    if (sender is Button button && button.DataContext is UpscaleModel item)
                    {
                        int index = vm.UpscaleSettings.IndexOf(item);
                        // 'index' now contains the index of the clicked item in the ItemsControl
                        // You can use it as needed
                        vm.UpscaleSettings[index].OnnxModelPath = files[0].TryGetLocalPath() ?? string.Empty;
                        vm.Validate();
                    }
                    
                }
            }
        }

        private async Task<bool> ShowConfirmationDialog(string message)
        {
            var dialog = new Window
            {
                Title = "Cancel unfinished upscales?",
                Width = 480,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Icon = Icon,
                CanResize = false,
                ShowInTaskbar = false
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Margin = new Thickness(20),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 380,
            };

            var materialIcon = new MaterialIcon
            {
                Kind = Material.Icons.MaterialIconKind.QuestionMarkCircleOutline,
                Width = 48,
                Height = 48,
            };

            var textPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20),
                Children = { materialIcon, textBlock },
            };

            var yesButton = new Button
            {
                Content = "Yes",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            yesButton.Click += (sender, e) => dialog.Close(true);

            var noButton = new Button
            {
                Content = "No",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalAlignment= VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            noButton.Click += (sender, e) => dialog.Close(false);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { yesButton, noButton },
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 20)
            };

            var mainPanel = new StackPanel
            {
                Children = { textPanel, buttonPanel }
            };

            dialog.Content = mainPanel;
            var result = await dialog.ShowDialog<bool?>(this);

            return result ?? false;
        }
    }
}