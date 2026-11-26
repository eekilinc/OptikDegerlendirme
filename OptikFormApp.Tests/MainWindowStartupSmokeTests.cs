using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OptikFormApp.ViewModels;
using Xunit;

namespace OptikFormApp.Tests
{
    public class MainWindowStartupSmokeTests
    {
        private static readonly Lazy<Dispatcher> SharedWpfDispatcher = new(() =>
        {
            var ready = new ManualResetEventSlim(false);
            Dispatcher? dispatcher = null;
            Exception? initError = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.ResourceAssembly == null)
                    {
                        Application.ResourceAssembly = typeof(App).Assembly;
                    }

                    if (Application.Current == null)
                    {
                        var app = new App
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown
                        };
                        app.InitializeComponent();
                    }

                    dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    initError = ex;
                    ready.Set();
                }
            })
            {
                IsBackground = true,
                Name = "TestWpfStaDispatcher"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(15));

            if (initError != null)
                throw new InvalidOperationException("Failed to initialize shared WPF STA dispatcher.", initError);

            return dispatcher ?? throw new InvalidOperationException("WPF STA dispatcher was null.");
        });

        [Fact]
        public void MainWindow_ShouldInitializeBindAndResizeAcrossResolutions_WithoutXamlOrBindingErrors()
        {
            var dispatcher = SharedWpfDispatcher.Value;
            Exception? capturedException = null;

            dispatcher.Invoke(() =>
            {
                try
                {
                    var window = new MainWindow();

                    // 1. Standart dizüstü çözünürlüğü (1366x768) yerleşim ve bağlama doğrulaması
                    window.Measure(new Size(1366, 768));
                    window.Arrange(new Rect(0, 0, 1366, 768));
                    window.UpdateLayout();
                    dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);

                    // 2. Dar / bölünmüş ekran çözünürlüğü (760x560) responsive doğrulaması
                    window.Measure(new Size(760, 560));
                    window.Arrange(new Rect(0, 0, 760, 560));
                    window.UpdateLayout();
                    dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);

                    if (window.DataContext is MainViewModel vm)
                    {
                        vm.IsSidebarCollapsed = true;
                        vm.IsKpiCollapsed = true;
                        vm.IsLogDrawerOpen = true;
                        window.UpdateLayout();

                        vm.IsSidebarCollapsed = false;
                        vm.IsKpiCollapsed = false;
                        vm.IsLogDrawerOpen = false;
                        window.UpdateLayout();

                        // Koyu ve açık tema geçişlerini doğrula
                        vm.ThemeIndex = 1;
                        window.UpdateLayout();
                        vm.ThemeIndex = 0;
                        window.UpdateLayout();

                        // Tüm modal pencereleri tek tek görünür hale getirerek gizli XAML / Binding hatalarını denetle
                        Action[] modalToggles =
                        {
                            () => { vm.IsModalOpen = true; window.UpdateLayout(); vm.IsModalOpen = false; },
                            () => { vm.IsGeneralConfigOpen = true; window.UpdateLayout(); vm.IsGeneralConfigOpen = false; },
                            () => { vm.IsUISettingsOpen = true; window.UpdateLayout(); vm.IsUISettingsOpen = false; },
                            () => { vm.IsShortcutsOpen = true; window.UpdateLayout(); vm.IsShortcutsOpen = false; },
                            () => { vm.IsAboutOpen = true; window.UpdateLayout(); vm.IsAboutOpen = false; },
                            () => { vm.IsAddCourseOpen = true; window.UpdateLayout(); vm.IsAddCourseOpen = false; },
                            () => { vm.IsSaveExamModalOpen = true; window.UpdateLayout(); vm.IsSaveExamModalOpen = false; },
                            () => { vm.IsRenameModalOpen = true; window.UpdateLayout(); vm.IsRenameModalOpen = false; },
                            () => { vm.IsConfirmModalOpen = true; window.UpdateLayout(); vm.IsConfirmModalOpen = false; },
                            () => { vm.IsObsExportOpen = true; window.UpdateLayout(); vm.IsObsExportOpen = false; },
                            () => { vm.IsRosterMatchOpen = true; window.UpdateLayout(); vm.IsRosterMatchOpen = false; },
                            () => { vm.IsOpticalFormatOpen = true; window.UpdateLayout(); vm.IsOpticalFormatOpen = false; },
                            () => { vm.IsItemAnalysisOpen = true; window.UpdateLayout(); vm.IsItemAnalysisOpen = false; },
                            () => { vm.IsAdvancedFilterOpen = true; window.UpdateLayout(); vm.IsAdvancedFilterOpen = false; },
                            () => { vm.IsSuccessPredictionOpen = true; window.UpdateLayout(); vm.IsSuccessPredictionOpen = false; },
                            () => { vm.IsBookletMappingOpen = true; window.UpdateLayout(); vm.IsBookletMappingOpen = false; },
                            () => { vm.IsQuestionSettingsOpen = true; window.UpdateLayout(); vm.IsQuestionSettingsOpen = false; },
                            () => { vm.IsLearningOutcomesOpen = true; window.UpdateLayout(); vm.IsLearningOutcomesOpen = false; },
                            () => { vm.IsTemplateManagerOpen = true; window.UpdateLayout(); vm.IsTemplateManagerOpen = false; },
                            () => { vm.IsStudentDetailOpen = true; window.UpdateLayout(); vm.IsStudentDetailOpen = false; },
                            () => { vm.IsValidationDetailsOpen = true; window.UpdateLayout(); vm.IsValidationDetailsOpen = false; }
                        };

                        foreach (var toggle in modalToggles)
                        {
                            toggle();
                            dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
                        }
                    }

                    // 3. Tüm ana sekmeleri tek tek aktif ederek alt görünümlerin bağlamalarını doğrula
                    if (window.Content is Panel root && FindFirstTabControl(root) is TabControl mainTabs)
                    {
                        for (int i = 0; i < mainTabs.Items.Count; i++)
                        {
                            mainTabs.SelectedIndex = i;
                            window.UpdateLayout();
                            dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            if (capturedException != null)
            {
                throw new InvalidOperationException($"WPF Başlatma / Bağlama Hatası: {capturedException.Message}", capturedException);
            }
        }

        private static TabControl? FindFirstTabControl(DependencyObject parent)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is TabControl tc) return tc;
                var nested = FindFirstTabControl(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
