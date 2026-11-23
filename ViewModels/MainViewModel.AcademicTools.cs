using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using OptikFormApp.Models;
using OptikFormApp.Services;

namespace OptikFormApp.ViewModels
{
    public partial class MainViewModel
    {
        private readonly ObsExportService _obsExportService = new();
        private readonly RosterMatchService _rosterMatchService = new();

        // 1. Öğretim Elemanı Profili & Tek Sınav Harf Notu Görünürlüğü
        private string _instructorName = "";
        public string InstructorName
        {
            get => _instructorName;
            set { _instructorName = value; OnPropertyChanged(); SaveSettings(); }
        }

        private string _departmentName = "Ağlasun Meslek Yüksekokulu";
        public string DepartmentName
        {
            get => _departmentName;
            set { _departmentName = value; OnPropertyChanged(); SaveSettings(); }
        }

        private bool _showLetterGradeColumn = false;
        public bool ShowLetterGradeColumn
        {
            get => _showLetterGradeColumn;
            set { _showLetterGradeColumn = value; OnPropertyChanged(); SaveSettings(); }
        }

        // 2. MAKÜ OBS Hazır Aktarım Modalı
        private bool _isObsExportOpen;
        public bool IsObsExportOpen { get => _isObsExportOpen; set { _isObsExportOpen = value; OnPropertyChanged(); } }

        private int _obsRoundingModeIndex = 0;
        public int ObsRoundingModeIndex
        {
            get => _obsRoundingModeIndex;
            set
            {
                _obsRoundingModeIndex = value;
                OnPropertyChanged();
                RefreshObsPreview();
            }
        }

        public ObservableCollection<ObsGradeRow> ObsPreviewRows { get; } = new();
        public ICommand OpenObsExportCommand { get; private set; } = null!;
        public ICommand CloseObsExportCommand { get; private set; } = null!;
        public ICommand CopyObsScoresCommand { get; private set; } = null!;
        public ICommand CopyObsIdAndScoresCommand { get; private set; } = null!;
        public ICommand ExportObsExcelCommand { get; private set; } = null!;
        public ICommand ExportObsCsvCommand { get; private set; } = null!;

        // 3. Sınıf Listesi Eşleştirme (Yoklama & Yanlış Numara Tespiti)
        private bool _isRosterMatchOpen;
        public bool IsRosterMatchOpen { get => _isRosterMatchOpen; set { _isRosterMatchOpen = value; OnPropertyChanged(); } }

        private string _rosterFileName = "Henüz sınıf listesi seçilmedi (.xlsx veya .csv)";
        public string RosterFileName { get => _rosterFileName; set { _rosterFileName = value; OnPropertyChanged(); } }

        private string _rosterMatchSummary = "";
        public string RosterMatchSummary { get => _rosterMatchSummary; set { _rosterMatchSummary = value; OnPropertyChanged(); } }

        private int _rosterMatchedCount;
        public int RosterMatchedCount { get => _rosterMatchedCount; set { _rosterMatchedCount = value; OnPropertyChanged(); } }

        private int _rosterAbsentCount;
        public int RosterAbsentCount { get => _rosterAbsentCount; set { _rosterAbsentCount = value; OnPropertyChanged(); } }

        private int _rosterMismatchCount;
        public int RosterMismatchCount { get => _rosterMismatchCount; set { _rosterMismatchCount = value; OnPropertyChanged(); } }

        private List<RosterEntry> _lastLoadedRoster = new();
        public ObservableCollection<RosterEntry> AbsentStudents { get; } = new();
        public ObservableCollection<SuspectedMismatchItem> SuspectedMismatches { get; } = new();
        public ObservableCollection<StudentResult> UnlistedOpticalStudents { get; } = new();

        public ICommand OpenRosterMatchCommand { get; private set; } = null!;
        public ICommand CloseRosterMatchCommand { get; private set; } = null!;
        public ICommand LoadRosterFileCommand { get; private set; } = null!;
        public ICommand ApplySuspectedFixCommand { get; private set; } = null!;
        public ICommand ExportAbsentStudentsCommand { get; private set; } = null!;

        // 4. Dinamik Optik Form Şablonu Ayarlayıcı
        private bool _isOpticalFormatOpen;
        public bool IsOpticalFormatOpen { get => _isOpticalFormatOpen; set { _isOpticalFormatOpen = value; OnPropertyChanged(); } }

        private int _opticalNameStart = 0;
        public int OpticalNameStart { get => _opticalNameStart; set { _opticalNameStart = Math.Max(0, value); OnPropertyChanged(); UpdateOpticalPreview(); SaveSettings(); } }

        private int _opticalNameLength = 22;
        public int OpticalNameLength { get => _opticalNameLength; set { _opticalNameLength = Math.Max(1, value); OnPropertyChanged(); UpdateOpticalPreview(); SaveSettings(); } }

        private int _opticalIdStart = 22;
        public int OpticalIdStart { get => _opticalIdStart; set { _opticalIdStart = Math.Max(0, value); OnPropertyChanged(); UpdateOpticalPreview(); SaveSettings(); } }

        private int _opticalIdLength = 10;
        public int OpticalIdLength { get => _opticalIdLength; set { _opticalIdLength = Math.Max(1, value); OnPropertyChanged(); UpdateOpticalPreview(); SaveSettings(); } }

        private int _opticalBookletStart = 32;
        public int OpticalBookletStart { get => _opticalBookletStart; set { _opticalBookletStart = Math.Max(0, value); OnPropertyChanged(); UpdateOpticalPreview(); SaveSettings(); } }

        private int _opticalAnswersStart = 33;
        public int OpticalAnswersStart { get => _opticalAnswersStart; set { _opticalAnswersStart = Math.Max(0, value); OnPropertyChanged(); UpdateOpticalPreview(); SaveSettings(); } }

        private string _sampleOpticalLine = "AHMET YILMAZ          2024123456AABCDEABCDEABCDEABCDE";
        public string SampleOpticalLine { get => _sampleOpticalLine; set { _sampleOpticalLine = value; OnPropertyChanged(); UpdateOpticalPreview(); } }

        private string _previewFullName = "";
        public string PreviewFullName { get => _previewFullName; set { _previewFullName = value; OnPropertyChanged(); } }

        private string _previewStudentId = "";
        public string PreviewStudentId { get => _previewStudentId; set { _previewStudentId = value; OnPropertyChanged(); } }

        private string _previewBooklet = "";
        public string PreviewBooklet { get => _previewBooklet; set { _previewBooklet = value; OnPropertyChanged(); } }

        private string _previewAnswers = "";
        public string PreviewAnswers { get => _previewAnswers; set { _previewAnswers = value; OnPropertyChanged(); } }

        public ICommand OpenOpticalFormatCommand { get; private set; } = null!;
        public ICommand CloseOpticalFormatCommand { get; private set; } = null!;
        public ICommand ResetOpticalFormatCommand { get; private set; } = null!;
        public ICommand LoadSampleOpticalLineCommand { get; private set; } = null!;

        // 5. Otomatik Güncelleme Bildirimi Bandı (HasUpdateAvailable & _versionService MainViewModel.cs'de tanımlı)
        private string _latestVersionTag = "";
        public string LatestVersionTag { get => _latestVersionTag; set { _latestVersionTag = value; OnPropertyChanged(); } }

        private string _updateReleaseUrl = "";
        public string UpdateReleaseUrl { get => _updateReleaseUrl; set { _updateReleaseUrl = value; OnPropertyChanged(); } }

        public ICommand OpenUpdateUrlCommand { get; private set; } = null!;
        public ICommand DismissUpdateBannerCommand { get; private set; } = null!;

        public OpticalFormatConfig GetCurrentOpticalFormat() => new()
        {
            NameStart = OpticalNameStart,
            NameLength = OpticalNameLength,
            IdStart = OpticalIdStart,
            IdLength = OpticalIdLength,
            BookletStart = OpticalBookletStart,
            AnswersStart = OpticalAnswersStart
        };

        private void LoadAcademicToolsSettings(AppSettings saved)
        {
            _instructorName = saved.InstructorName ?? "";
            _departmentName = string.IsNullOrWhiteSpace(saved.DepartmentName) ? "Ağlasun Meslek Yüksekokulu" : saved.DepartmentName;
            _showLetterGradeColumn = saved.ShowLetterGradeColumn;

            _opticalNameStart = saved.OpticalNameStart;
            _opticalNameLength = saved.OpticalNameLength > 0 ? saved.OpticalNameLength : 22;
            _opticalIdStart = saved.OpticalIdStart;
            _opticalIdLength = saved.OpticalIdLength > 0 ? saved.OpticalIdLength : 10;
            _opticalBookletStart = saved.OpticalBookletStart;
            _opticalAnswersStart = saved.OpticalAnswersStart;
            UpdateOpticalPreview();
        }

        private void SaveAcademicToolsSettings(AppSettings settings)
        {
            settings.InstructorName = _instructorName;
            settings.DepartmentName = _departmentName;
            settings.ShowLetterGradeColumn = _showLetterGradeColumn;
            settings.OpticalNameStart = _opticalNameStart;
            settings.OpticalNameLength = _opticalNameLength;
            settings.OpticalIdStart = _opticalIdStart;
            settings.OpticalIdLength = _opticalIdLength;
            settings.OpticalBookletStart = _opticalBookletStart;
            settings.OpticalAnswersStart = _opticalAnswersStart;
        }

        private void InitializeAcademicToolsCommands()
        {
            // OBS Export
            OpenObsExportCommand = new RelayCommand(_ =>
            {
                if (Students.Count == 0)
                {
                    ShowToastWarning("OBS aktarımı için önce optik veri yükleyip hesaplama yapın.");
                    return;
                }
                RefreshObsPreview();
                IsObsExportOpen = true;
            });
            CloseObsExportCommand = new RelayCommand(_ => IsObsExportOpen = false);

            CopyObsScoresCommand = new RelayCommand(_ =>
            {
                var txt = _obsExportService.BuildClipboardText(ObsPreviewRows.ToList(), scoresOnly: true);
                if (!string.IsNullOrEmpty(txt))
                {
                    Clipboard.SetText(txt);
                    ShowToastSuccess($"{ObsPreviewRows.Count} öğrencinin OBS not sütunu panoya kopyalandı.");
                }
            });

            CopyObsIdAndScoresCommand = new RelayCommand(_ =>
            {
                var txt = _obsExportService.BuildClipboardText(ObsPreviewRows.ToList(), scoresOnly: false);
                if (!string.IsNullOrEmpty(txt))
                {
                    Clipboard.SetText(txt);
                    ShowToastSuccess($"{ObsPreviewRows.Count} öğrencinin No + Not listesi panoya kopyalandı.");
                }
            });

            ExportObsExcelCommand = new RelayCommand(_ =>
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Dosyası|*.xlsx",
                    FileName = $"{SelectedCourse?.Code ?? "OBS"}_NotListesi_{DateTime.Now:yyyyMMdd}.xlsx"
                };
                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        _obsExportService.ExportToExcel(ObsPreviewRows.ToList(), sfd.FileName, SelectedCourse?.Name ?? "OBS Not Listesi");
                        ShowToastSuccess($"OBS Excel dosyası kaydedildi: {Path.GetFileName(sfd.FileName)}");
                    }
                    catch (Exception ex) { ShowToastError($"OBS Excel hatası: {ex.Message}"); }
                }
            });

            ExportObsCsvCommand = new RelayCommand(_ =>
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV Dosyası|*.csv",
                    FileName = $"{SelectedCourse?.Code ?? "OBS"}_NotListesi_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        _obsExportService.ExportToCsv(ObsPreviewRows.ToList(), sfd.FileName);
                        ShowToastSuccess($"OBS CSV dosyası kaydedildi: {Path.GetFileName(sfd.FileName)}");
                    }
                    catch (Exception ex) { ShowToastError($"OBS CSV hatası: {ex.Message}"); }
                }
            });

            // Roster Match
            OpenRosterMatchCommand = new RelayCommand(_ =>
            {
                if (Students.Count == 0)
                {
                    ShowToastWarning("Sınıf listesi eşleştirmek için önce optik veri yükleyin.");
                    return;
                }
                IsRosterMatchOpen = true;
            });
            CloseRosterMatchCommand = new RelayCommand(_ => IsRosterMatchOpen = false);

            LoadRosterFileCommand = new RelayCommand(_ =>
            {
                var ofd = new OpenFileDialog
                {
                    Filter = "Sınıf Listesi (*.xlsx;*.csv)|*.xlsx;*.csv|Tüm Dosyalar|*.*",
                    Title = "OBS Sınıf Listesini Seçin"
                };
                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        _lastLoadedRoster = _rosterMatchService.LoadRosterFromFile(ofd.FileName);
                        RosterFileName = Path.GetFileName(ofd.FileName);
                        RunRosterMatch();
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"Sınıf listesi okunamadı: {ex.Message}");
                    }
                }
            });

            ApplySuspectedFixCommand = new RelayCommand(param =>
            {
                if (param is SuspectedMismatchItem item && item.OpticalStudent != null)
                {
                    item.OpticalStudent.StudentId = item.SuggestedRosterId;
                    item.OpticalStudent.FullName = item.SuggestedRosterName;
                    HasUnsavedData = true;
                    StudentsView.Refresh();
                    RunRosterMatch();
                    ShowToastSuccess($"Numara düzeltildi: {item.SuggestedRosterId} — {item.SuggestedRosterName}");
                }
            });

            ExportAbsentStudentsCommand = new RelayCommand(_ =>
            {
                if (AbsentStudents.Count == 0)
                {
                    ShowToastInfo("Sınava girmeyen öğrenci bulunmuyor.");
                    return;
                }
                var sfd = new SaveFileDialog
                {
                    Filter = "CSV Dosyası|*.csv",
                    FileName = $"{SelectedCourse?.Code ?? "Sinav"}_Girmeyenler_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        _rosterMatchService.ExportAbsentToCsv(AbsentStudents.ToList(), sfd.FileName);
                        ShowToastSuccess($"Sınava girmeyen {AbsentStudents.Count} öğrenci dışa aktarıldı.");
                    }
                    catch (Exception ex) { ShowToastError($"Dışa aktarma hatası: {ex.Message}"); }
                }
            });

            // Optical Format Designer
            OpenOpticalFormatCommand = new RelayCommand(_ =>
            {
                UpdateOpticalPreview();
                IsOpticalFormatOpen = true;
            });
            CloseOpticalFormatCommand = new RelayCommand(_ => IsOpticalFormatOpen = false);

            ResetOpticalFormatCommand = new RelayCommand(_ =>
            {
                OpticalNameStart = 0;
                OpticalNameLength = 22;
                OpticalIdStart = 22;
                OpticalIdLength = 10;
                OpticalBookletStart = 32;
                OpticalAnswersStart = 33;
                ShowToastInfo("Varsayılan Ağlasun MYO optik formatına sıfırlandı.");
            });

            LoadSampleOpticalLineCommand = new RelayCommand(_ =>
            {
                var ofd = new OpenFileDialog
                {
                    Filter = "Optik Dosya (*.txt;*.dat)|*.txt;*.dat|Tüm Dosyalar|*.*",
                    Title = "Örnek Optik Dosya Seçin"
                };
                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                        var firstNonEmpty = File.ReadLines(ofd.FileName, System.Text.Encoding.GetEncoding(1254))
                            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                        if (!string.IsNullOrEmpty(firstNonEmpty))
                        {
                            SampleOpticalLine = firstNonEmpty;
                            ShowToastSuccess("Örnek satır yüklendi ve ayrıştırıldı.");
                        }
                    }
                    catch (Exception ex) { ShowToastError($"Örnek dosya okunamadı: {ex.Message}"); }
                }
            });

            // Auto Update Banner
            OpenUpdateUrlCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(UpdateReleaseUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = UpdateReleaseUrl, UseShellExecute = true });
                    }
                    catch { }
                }
            });
            DismissUpdateBannerCommand = new RelayCommand(_ => HasUpdateAvailable = false);

            // Sessiz arka plan sürüm kontrolü
            _ = CheckForUpdatesSilentlyAsync();
        }

        private void RefreshObsPreview()
        {
            ObsPreviewRows.Clear();
            var mode = (ObsRoundingMode)Math.Clamp(ObsRoundingModeIndex, 0, 2);
            foreach (var r in _obsExportService.BuildObsRows(Students, mode))
                ObsPreviewRows.Add(r);
        }

        private void RunRosterMatch()
        {
            var res = _rosterMatchService.Match(_lastLoadedRoster, Students.ToList());
            RosterMatchedCount = res.MatchedCount;
            RosterAbsentCount = res.AbsentStudents.Count;
            RosterMismatchCount = res.SuspectedMismatches.Count;

            AbsentStudents.Clear();
            foreach (var a in res.AbsentStudents) AbsentStudents.Add(a);

            SuspectedMismatches.Clear();
            foreach (var m in res.SuspectedMismatches) SuspectedMismatches.Add(m);

            UnlistedOpticalStudents.Clear();
            foreach (var u in res.UnlistedOpticalStudents) UnlistedOpticalStudents.Add(u);

            RosterMatchSummary = $"Listede {res.TotalRosterCount} öğrenci • {res.MatchedCount} Eşleşti • {res.AbsentStudents.Count} Girmedi (G) • {res.SuspectedMismatches.Count} Yanlış Kodlama Şüphesi";
        }

        private void UpdateOpticalPreview()
        {
            var (name, id, booklet, answers) = _parserService.ParseLineFields(SampleOpticalLine ?? "", GetCurrentOpticalFormat());
            PreviewFullName = string.IsNullOrEmpty(name) ? "(Boş)" : name;
            PreviewStudentId = string.IsNullOrEmpty(id) ? "(Boş)" : id;
            PreviewBooklet = string.IsNullOrEmpty(booklet) ? "(Boş)" : booklet;
            PreviewAnswers = string.IsNullOrEmpty(answers) ? "(Boş)" : (answers.Length > 40 ? answers.Substring(0, 40) + "..." : answers);
        }

        private async Task CheckForUpdatesSilentlyAsync()
        {
            try
            {
                await Task.Delay(2500);
                var result = await _versionService.CheckForUpdateAsync();
                if (result.HasUpdate && !string.IsNullOrWhiteSpace(result.LatestVersion))
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        LatestVersionTag = result.LatestVersion;
                        UpdateReleaseUrl = result.ReleaseUrl;
                        HasUpdateAvailable = true;
                    });
                }
            }
            catch
            {
                // Çevrimdışı ortamda sessizce yoksay
            }
        }
    }
}
