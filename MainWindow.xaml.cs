using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OptikFormApp.ViewModels;

namespace OptikFormApp;

public partial class MainWindow : Window
{
    private Border? _dropOverlay;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.HasUnsavedData && vm.Students.Count > 0)
        {
            var result = MessageBox.Show(
                "Kaydedilmemiş sınav değerlendirme verileri bulunuyor.\nKaydetmeden uygulamadan çıkmak istediğinize emin misiniz?",
                "Kaydedilmemiş Veri Uyarısı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }
    }

    private static bool IsSupportedOpticalFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".txt" || ext == ".dat";
    }

    // Drag-Drop Event Handlers (.txt ve .dat destekli)
    private void MainWindow_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Any(IsSupportedOpticalFile))
            {
                e.Effects = DragDropEffects.Copy;
                ShowDropOverlay();
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void MainWindow_DragLeave(object sender, DragEventArgs e)
    {
        HideDropOverlay();
        e.Handled = true;
    }

    private async void MainWindow_Drop(object sender, DragEventArgs e)
    {
        HideDropOverlay();

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var optFile = files?.FirstOrDefault(IsSupportedOpticalFile);

            if (optFile != null && DataContext is MainViewModel vm)
            {
                await vm.LoadDroppedFileAsync(optFile);
            }
        }
        e.Handled = true;
    }

    private void ShowDropOverlay()
    {
        if (_dropOverlay != null) return;

        _dropOverlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(232, 15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(16),
            Margin = new Thickness(24),
            Opacity = 0,
            IsHitTestVisible = false,
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 72,
                        Height = 72,
                        CornerRadius = new CornerRadius(18),
                        Background = new SolidColorBrush(Color.FromArgb(45, 59, 130, 246)),
                        Margin = new Thickness(0, 0, 0, 16),
                        Child = new TextBlock
                        {
                            Text = "\uE8E5",
                            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                            FontSize = 32,
                            Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = "Optik Veri Dosyasını Buraya Bırakın",
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Desteklenen biçimler: .TXT ve .DAT optik okuyucu çıktıları",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0)
                    }
                }
            }
        };
        Panel.SetZIndex(_dropOverlay, 3000);

        if (Content is Grid mainGrid)
        {
            mainGrid.Children.Add(_dropOverlay);
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160)
            };
            _dropOverlay.BeginAnimation(OpacityProperty, fadeIn);
        }
    }

    private void HideDropOverlay()
    {
        if (_dropOverlay == null) return;

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(120)
        };

        fadeOut.Completed += (_, _) =>
        {
            if (_dropOverlay != null && Content is Grid mainGrid)
            {
                mainGrid.Children.Remove(_dropOverlay);
                _dropOverlay = null;
            }
        };

        _dropOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }
}
