using System;
using System.ComponentModel;
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
    private double _savedSidebarWidth = 268.0;
    private bool _sidebarAutoCollapsed;
    private bool _sidebarManuallyOverridden;
    private bool _kpiAutoCollapsed;
    private bool _suppressSidebarCallback;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureInitialViewport();

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnMainViewModelPropertyChanged;
            SyncSidebarColumn(vm.IsSidebarCollapsed);
        }
    }

    /// <summary>
    /// Farklı çözünürlük ve Windows DPI ölçeklerinde (%100, %125, %150) pencerenin
    /// görev çubuğunun altına taşmamasını ve küçük ekranlarda tam ekran açılmasını sağlar.
    /// </summary>
    private void ConfigureInitialViewport()
    {
        Rect workArea = SystemParameters.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
            return;

        MinWidth = Math.Min(740, Math.Max(640, workArea.Width * 0.65));
        MinHeight = Math.Min(540, Math.Max(480, workArea.Height * 0.72));

        // 1366x768 veya %150 ölçekli 1080p (1280x720 mantıksal) ekranlarda tam çalışma alanı sağla
        if (workArea.Width <= 1420 || workArea.Height <= 800)
        {
            Width = Math.Min(1240, workArea.Width * 0.96);
            Height = Math.Min(740, workArea.Height * 0.94);
            WindowState = WindowState.Maximized;
        }
        else
        {
            Width = Math.Min(1340, workArea.Width * 0.88);
            Height = Math.Min(840, workArea.Height * 0.88);
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm) return;

        if (e.PropertyName == nameof(MainViewModel.IsSidebarCollapsed))
        {
            SyncSidebarColumn(vm.IsSidebarCollapsed);
        }
    }

    private void SyncSidebarColumn(bool isCollapsed)
    {
        if (SidebarColumn == null) return;

        if (isCollapsed)
        {
            if (SidebarColumn.ActualWidth >= 180)
            {
                _savedSidebarWidth = SidebarColumn.ActualWidth;
            }
            SidebarColumn.MinWidth = 0;
            SidebarColumn.Width = new GridLength(0);
        }
        else
        {
            SidebarColumn.MinWidth = 205;
            SidebarColumn.Width = new GridLength(Math.Clamp(_savedSidebarWidth, 205, 380));
        }
    }

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _sidebarManuallyOverridden = true;
            _sidebarAutoCollapsed = false;
            vm.IsSidebarCollapsed = !vm.IsSidebarCollapsed;
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_suppressSidebarCallback || DataContext is not MainViewModel vm)
            return;

        double w = e.NewSize.Width;
        double h = e.NewSize.Height;

        // 1. Akıllı Responsive Kenar Çubuğu (< 1040px otomatik daralır, >= 1100px geri açılır)
        _suppressSidebarCallback = true;
        try
        {
            if (w < 1040 && !vm.IsSidebarCollapsed && !_sidebarManuallyOverridden)
            {
                _sidebarAutoCollapsed = true;
                vm.IsSidebarCollapsed = true;
            }
            else if (w >= 1100)
            {
                _sidebarManuallyOverridden = false;
                if (_sidebarAutoCollapsed && vm.IsSidebarCollapsed)
                {
                    _sidebarAutoCollapsed = false;
                    vm.IsSidebarCollapsed = false;
                }
            }

            // 2. Düşük dikey çözünürlüklerde (< 670px) KPI kartlarını tek satır kompakt şeride daralt
            if (h < 670 && !vm.IsKpiCollapsed && !_kpiAutoCollapsed)
            {
                _kpiAutoCollapsed = true;
                vm.IsKpiCollapsed = true;
            }
            else if (h >= 720 && _kpiAutoCollapsed && vm.IsKpiCollapsed)
            {
                _kpiAutoCollapsed = false;
                vm.IsKpiCollapsed = false;
            }
        }
        finally
        {
            _suppressSidebarCallback = false;
        }

        // Dar pencerelerde çalışma alanı kenar boşluklarını optimize et
        if (WorkspaceGrid != null)
        {
            WorkspaceGrid.Margin = w < 900
                ? new Thickness(10, 8, 10, 8)
                : new Thickness(14, 12, 16, 10);
        }
    }

    private void TabHeaderScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.ScrollableWidth > 0)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - (e.Delta * 0.5));
            e.Handled = true;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
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
            Margin = new Thickness(20),
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
                        Width = 68,
                        Height = 68,
                        CornerRadius = new CornerRadius(16),
                        Background = new SolidColorBrush(Color.FromArgb(45, 59, 130, 246)),
                        Margin = new Thickness(0, 0, 0, 14),
                        Child = new TextBlock
                        {
                            Text = "\uE8E5",
                            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                            FontSize = 30,
                            Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = "Optik Veri Dosyasını Buraya Bırakın",
                        FontSize = 19,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Desteklenen biçimler: .TXT ve .DAT optik okuyucu çıktıları",
                        FontSize = 12.5,
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
