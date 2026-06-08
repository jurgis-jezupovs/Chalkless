using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Chalkless.Controls;
using System.IO;
using System.Linq;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace Chalkless.Views;

public partial class MainWindow : Window
{
    private WindowState _previousWindowState = WindowState.Normal;
    private string? _currentFilePath;

    public MainWindow()
    {
        InitializeComponent();

        KeyDown += (s, e) =>
        {
            // Don't process shortcuts if a TextBox has focus (user is typing)
            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            if (focusedElement is TextBox)
            {
                return;
            }
            
            if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                OnSaveClick(null, null!);
                e.Handled = true;
            }
            else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                OnLoadClick(null, null!);
                e.Handled = true;
            }
            else if (e.Key == Key.H && !InkCanvas.IsPanMode)
            {
                InkCanvas.IsPanMode = true;
                e.Handled = true;
            }
            else if (e.Key == Key.F || e.Key == Key.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
            {
                ExitFullscreen();
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.D)
            {
                InkCanvas.IsEraserMode = true;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                InkCanvas.IsSelectMode = true;
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.D1 || e.Key == Key.NumPad1)
            {
                InkCanvas.InkColor = Colors.White;
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.D2 || e.Key == Key.NumPad2)
            {
                InkCanvas.InkColor = Colors.Orange;
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.D3 || e.Key == Key.NumPad3)
            {
                InkCanvas.InkColor = Colors.DodgerBlue;
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.D4 || e.Key == Key.NumPad4)
            {
                InkCanvas.InkColor = Colors.Green;
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.D5 || e.Key == Key.NumPad5)
            {
                InkCanvas.InkColor = Colors.IndianRed;
                InkCanvas.IsEraserMode = false;
                InkCanvas.IsPanMode = false;
                InkCanvas.IsSelectMode = false;
                e.Handled = true;
            }
            else if (e.Key == Key.M)
            {
                TopMenu.IsVisible = !TopMenu.IsVisible;
                e.Handled = true;
            }
        };

        KeyUp += (s, e) =>
        {
            if (e.Key == Key.H && InkCanvas.IsPanMode)
            {
                InkCanvas.IsPanMode = false;
                e.Handled = true;
            }
        };
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            ExitFullscreen();
        }
        else
        {
            _previousWindowState = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    private void ExitFullscreen()
    {
        WindowState = _previousWindowState != WindowState.FullScreen 
            ? _previousWindowState 
            : WindowState.Maximized;
    }

    private async void OnClearClick(object? sender, RoutedEventArgs e)
    {
        var result = await MessageBoxManager
            .GetMessageBoxStandard( new MessageBoxStandardParams
            {
                ContentTitle = "Clear confirmation",
                ContentMessage = "Do you really want to clear the canvas?",
                ButtonDefinitions = ButtonEnum.YesNo,
                Width = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            })
            .ShowWindowDialogAsync(this);

        if (result == ButtonResult.Yes)
        {
            InkCanvas.Clear();
        }
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        InkCanvas.Undo();
    }

    private void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        InkCanvas.Redo();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = StorageProvider;
        
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Chalk Drawing",
            SuggestedFileName = "drawing.chalk",
            DefaultExtension = "chalk",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Chalk Drawing")
                {
                    Patterns = new[] { "*.chalk" }
                }
            }
        });

        if (file != null)
        {
            try
            {
                var path = file.Path.LocalPath;
                InkCanvas.SaveToFile(path);
                _currentFilePath = path;
                Title = $"Chalkless - {Path.GetFileName(path)}";
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
            }
        }
    }

    private async void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = StorageProvider;
        
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Chalk Drawing",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Chalk Drawing")
                {
                    Patterns = new[] { "*.chalk" }
                }
            }
        });

        if (files.Count > 0)
        {
            try
            {
                var path = files[0].Path.LocalPath;
                InkCanvas.LoadFromFile(path);
                _currentFilePath = path;
                Title = $"Chalkless - {Path.GetFileName(path)}";
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load error: {ex.Message}");
            }
        }
    }
}