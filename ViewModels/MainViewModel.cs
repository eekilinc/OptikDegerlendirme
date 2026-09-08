using System;
using System.Windows;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using OptikFormApp.Models;
using OptikFormApp.Services;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows.Input;

namespace OptikFormApp.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly OpticalParserService _parserService;
        private readonly ExcelExportService _excelService;
        private readonly ValidationService _validationService;
        private readonly PdfReportService _pdfService;
        private readonly AppSettingsService _settingsService;
        private readonly CsvExportService _csvService;
        private readonly NotificationService _notificationService;
        private readonly UndoRedoManager _undoRedoManager;
        private readonly StatisticsReportService _statsReportService;
        private readonly TemplateService _templateService;
        private readonly JsonDataService _jsonDataService;
        private readonly KeyboardShortcutService _shortcutService;
        private readonly ProgressService _progressService;
        private readonly VersionCheckService _versionCheckService;
        private readonly ItemAnalysisService _itemAnalysisService;
        private readonly SuccessPredictionService _successPredictionService;
        private readonly VersionService _versionService;
        private readonly BackupService _backupService;
        private readonly GradingService _gradingService;
        private readonly BookletMappingService _bookletMappingService = new();

        private readonly object _studentsLock = new();
        private readonly object _coursesLock = new();
        private readonly object _examsLock = new();
        private readonly object _statsLock = new();
        private readonly object _validationLock = new();
        private readonly object _logsLock = new();
        private readonly Task _initTask;
        
        private Course? _selectedCourse;
        private ExamEntry? _selectedExam;
        private bool _isAddCourseOpen;
        private string _newCourseCode = "";
        private string _newCourseName = "";
        private bool _isModalOpen;
        private string _statusMessage = "İşlem Bekleniyor. TXT dosyası yükleyin.";
        private bool _isQuestionSettingsOpen;
        private bool _isLearningOutcomesOpen;
        private bool _isAlertOpen;
        private string _alertTitle = string.Empty;
        private string _alertMessage = string.Empty;
        private bool _isAboutOpen;
        private bool _isUISettingsOpen;
        private bool _isRenameModalOpen;
        private bool _isConfirmModalOpen;
        private string _confirmModalTitle = "";
        private string _confirmModalMessage = "";
        private string _confirmButtonText = "Sil";
        private System.Action? _onConfirmAction;
        private bool _isShortcutSettingsOpen;
        private string _renameModalTitle = "";
        private string _renameInput1 = "";
        private string _renameInput2 = "";
        private bool _showRenameInput2;
        private int _renameId;
        private string _renameContext = "";
        private string _schoolName = "Okul Adı";
        private string _defaultExcelPath = "";
        private int _themeIndex = 0;
        private int _layoutIndex = 0;
        private double _fontSize = 14;
        private double _netCoefficient = 1.0;
        private double _baseScore = 0.0;
        private double _wrongDeductionFactor = 0.25;
        private bool _isBusy;
        private string _busyMessage = "";
        private string _newOutcomeName = "";
        private string _newOutcomeRange = "";
        private string _newOutcomeBooklet = "A";
        private string _searchText = "";
        private double? _minScoreFilter;
        private double? _maxScoreFilter;
        private string? _selectedBookletFilter;
        private int? _minCorrectFilter;
        private int? _maxWrongFilter;
        private string _updateStatus = "Güncelleme kontrolü yapılmadı";
        private bool _isCheckingForUpdate;
        private bool _hasUpdateAvailable;
        private string _latestVersion = "";
        private string _selectedSortColumn = "Score";
        private bool _isSortDescending = true;
        private bool _isAdvancedFilterOpen;
        private bool _isItemAnalysisOpen;
        private bool _isSuccessPredictionOpen;
        private bool _isValidationDetailsOpen = false;
        private bool _isSaveExamModalOpen;
        private bool _isDetailedAnswerKeyEditorOpen;
        private bool _isBulkAnswerEntryOpen;
        private AnswerKeyModel? _selectedAnswerKeyForEdit;
        private string _bulkAnswerEntryText = "";
        private string _bulkAnswerPreview = "";
        
        // Inline course management fields
        private bool _isAddCourseInlineOpen = false;
        private string _newCourseCodeInline = "";
        private string _newCourseNameInline = "";
        
        // Separate field for SaveExamModal dropdown (to avoid affecting sidebar)
        private Course? _selectedCourseForSave;
        
        private bool _isGeneralConfigOpen;
        private bool _hasUnsavedData;
        
        private ObservableCollection<ItemAnalysisService.QuestionItemStats> _questionStats = new();
        private ObservableCollection<ItemAnalysisService.AnomalyResult> _anomalies = new();
        private ItemAnalysisService.ReliabilityStats _reliabilityStats = new(0,0,0,0,0,0,0,0);
        
        private ObservableCollection<SuccessPredictionService.PredictionResult> _studentPredictions = new();
        private SuccessPredictionService.ClassPredictionSummary _classPredictionSummary = new();
        private double _passingScore = 50;

        private GradingSettings _gradingSettings = new();
        private ClassGradingStats _classGradingStats = new();

        // Booklet Mapping Modal fields
        private bool _isBookletMappingOpen;
        private AnswerKeyModel? _selectedSourceAnswerKey;
        private string _targetBookletName = "B";
        private string _mappingInputText = "";
        private string _mappingStatusHeader = "Eşleme Bekleniyor";
        private string _mappingPreviewOrErrorMessage = "Lütfen eşleme metnini girin.";
        private bool _isMappingValid;
        private ObservableCollection<ItemAnalysisService.QuestionDistractorStats> _distractorStats = new();

        // Student Detail Modal fields
        private bool _isStudentDetailOpen;
        private StudentResult? _selectedStudentDetail;
        private StudentResult? _originalStudentBeforeEdit;

        public ICollectionView StudentsView { get; }

        public MainViewModel()
        {
            _parserService = new OpticalParserService();
            _excelService = new ExcelExportService();
            _validationService = new ValidationService();
            _pdfService = new PdfReportService();
            _dbService = new DatabaseService();
            _settingsService = new AppSettingsService();
            _csvService = new CsvExportService();
            _notificationService = new NotificationService();
            _undoRedoManager = new UndoRedoManager(50);
            _statsReportService = new StatisticsReportService();
            _templateService = new TemplateService();
            _jsonDataService = new JsonDataService();
            _shortcutService = new KeyboardShortcutService();
            _progressService = ProgressService.Instance;
            _itemAnalysisService = new ItemAnalysisService();
            _successPredictionService = new SuccessPredictionService();
            _versionService = new VersionService();
            _versionCheckService = new VersionCheckService();
            _backupService = new BackupService(_dbService, _settingsService, _notificationService, _jsonDataService);
            _gradingService = new GradingService();

            // Undo/Redo event handlers
            _undoRedoManager.CanUndoChanged += (s, e) => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(UndoDescription)); };
            _undoRedoManager.CanRedoChanged += (s, e) => { OnPropertyChanged(nameof(CanRedo)); OnPropertyChanged(nameof(RedoDescription)); };
            _undoRedoManager.CommandExecuted += (s, desc) => AddToLog($"İşlem: {desc}", LogLevel.Info);

            // Kalıcı ayarları yükle
            var saved = _settingsService.Load();
            _schoolName = saved.SchoolName;
            _defaultExcelPath = saved.DefaultExcelPath;
            _netCoefficient = saved.NetCoefficient;
            _baseScore = saved.BaseScore;
            _wrongDeductionFactor = saved.WrongDeductionFactor;
            _themeIndex = saved.ThemeIndex;
            _layoutIndex = saved.LayoutIndex;
            _fontSize = saved.FontSize > 0 ? saved.FontSize : 14;
            _gradingSettings.Mode = saved.GradingModeIndex == 0 ? GradingMode.RelativeEvaluation : GradingMode.AbsoluteGrading;
            if (saved.RelativeThreshold > 0)
                _gradingSettings.RelativeThreshold = saved.RelativeThreshold;
            _gridRowHeight = _layoutIndex == 0 ? 32 : 50;
            _gridCellPadding = _layoutIndex == 0
                ? new System.Windows.Thickness(10, 0, 10, 0)
                : new System.Windows.Thickness(15, 0, 15, 0);
            
            Students = new ObservableCollection<StudentResult>();
            AnswerKeys = new ObservableCollection<AnswerKeyModel>();
            Statistics = new ObservableCollection<QuestionStatisticItem>();
            ValidationIssues = new ObservableCollection<ValidationIssue>();
            Courses = new ObservableCollection<Course>();
            CourseExams = new ObservableCollection<ExamEntry>();
            
            StudentsView = CollectionViewSource.GetDefaultView(Students);
            StudentsView.Filter = FilterStudents;
            
            // Temayı uygula (saved ayarları yüklendikten sonra)
            ApplyTheme(_themeIndex == 1);
            ApplyFontSize(_fontSize);
            
            _initTask = InitializeAsync();

            AnswerKeys.Add(new AnswerKeyModel { BookletName = "A", Answers = "" });

            LoadTxtCommand = new RelayCommand(async _ => await LoadTxtFileAsync());
            EvaluateCommand = new AsyncRelayCommand(async _ => await EvaluateAsync(), _ => Students.Count > 0);
            
            AddToLog("Uygulama hazır. Lütfen bir optik veri (.txt) dosyası yükleyin.", LogLevel.Info);
            
            AddAnswerKeyCommand = new RelayCommand(_ => AnswerKeys.Add(new AnswerKeyModel { BookletName = "B", Answers = "" }));
            RemoveAnswerKeyCommand = new RelayCommand(param => {
                if (param is AnswerKeyModel model) AnswerKeys.Remove(model);
            });

            OpenModalCommand = new RelayCommand(_ => IsModalOpen = true);
            CloseModalCommand = new AsyncRelayCommand(async _ => { IsModalOpen = false; if (Students.Count > 0) { await EvaluateAsync(); await AutoSaveSelectedExamAsync(); } });
            OpenQuestionSettingsCommand = new RelayCommand(_ => {
                // Preserve existing settings that still apply
                var existingSettings = new Dictionary<(string, int), QuestionSetting>();
                foreach (var s in QuestionSettings)
                    existingSettings[(s.BookletName, s.QuestionNumber)] = s;
                QuestionSettings.Clear();
                foreach (var key in AnswerKeys.Where(k => !string.IsNullOrEmpty(k.Answers)))
                {
                    int maxLen = key.Answers.Length;
                    for (int i = 1; i <= maxLen; i++)
                    {
                        if (existingSettings.TryGetValue((key.BookletName, i), out var existing))
                            QuestionSettings.Add(existing);
                        else
                            QuestionSettings.Add(new QuestionSetting { BookletName = key.BookletName, QuestionNumber = i });
                    }
                }
                IsQuestionSettingsOpen = true;
                AddToLog("Soru ayarları paneli açıldı (Kitapçık bazlı).");
            });
            CloseQuestionSettingsCommand = new AsyncRelayCommand(async _ => { 
                IsQuestionSettingsOpen = false; 
                await EvaluateAsync(); 
                await AutoSaveSelectedExamAsync(); 
            });

            OpenLearningOutcomesCommand = new RelayCommand(_ => IsLearningOutcomesOpen = true);
            CloseLearningOutcomesCommand = new AsyncRelayCommand(async _ => {
                IsLearningOutcomesOpen = false;
                await EvaluateAsync();
                await AutoSaveSelectedExamAsync();
            });
            
            AddLearningOutcomeCommand = new RelayCommand(_ => 
            {
                if (!string.IsNullOrWhiteSpace(NewOutcomeRange) && !string.IsNullOrWhiteSpace(NewOutcomeName))
                {
                    var outcome = new LearningOutcome 
                    { 
                        Name = NewOutcomeName,
                        BookletName = NewOutcomeBooklet,
                        QuestionNumbersRaw = NewOutcomeRange
                    };
                    LearningOutcomes.Add(outcome);
                    
                    // Clear input fields
                    NewOutcomeName = string.Empty;
                    NewOutcomeRange = string.Empty;
                    NewOutcomeBooklet = "A";
                    
                    AddToLog($"Yeni kazanım eklendi: {outcome.Name} ({outcome.BookletName} kitapçığı)", LogLevel.Success);
                    
                    // Recalculate if we have students
                    if (Students.Count > 0)
                    {
                        UpdateOutcomeStats();
                    }
                }
                else
                {
                    ShowAlert("Eksik Bilgi", "Lütfen kazanım adı ve soru numaralarını girin.");
                }
            });
            RemoveOutcomeCommand = new RelayCommand(p => { if (p is LearningOutcome lo) LearningOutcomes.Remove(lo); });

            CloseAlertCommand = new RelayCommand(_ => IsAlertOpen = false);
            OpenAboutCommand = new RelayCommand(_ => IsAboutOpen = true);
            CloseAboutCommand = new RelayCommand(_ => IsAboutOpen = false);
            OpenGitHubCommand = new RelayCommand(_ => {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = "https://github.com/eekilinc",
                    UseShellExecute = true
                });
            });

            // Version Check Command
            CheckForUpdatesCommand = new AsyncRelayCommand(async _ => {
                IsCheckingForUpdate = true;
                UpdateStatus = "Kontrol ediliyor...";
                try
                {
                    var result = await _versionCheckService.CheckForUpdateAsync(CurrentVersion);
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        UpdateStatus = $"Hata: {result.ErrorMessage}";
                        HasUpdateAvailable = false;
                    }
                    else
                    {
                        HasUpdateAvailable = result.HasUpdate;
                        LatestVersion = result.LatestVersion;
                        UpdateStatus = result.StatusText;
                        
                        if (result.HasUpdate)
                        {
                            AddToLog($"Yeni sürüm mevcut: {result.LatestVersion}", LogLevel.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus = "Kontrol hatası";
                    AddToLog($"Güncelleme kontrolü hatası: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    IsCheckingForUpdate = false;
                }
            });

            OpenReleasesPageCommand = new RelayCommand(_ => {
                _versionCheckService.OpenReleasesPage();
            });
            OpenUISettingsCommand = new RelayCommand(_ => IsUISettingsOpen = true);
            CloseUISettingsCommand = new RelayCommand(_ => IsUISettingsOpen = false);
            OpenGeneralConfigCommand = new RelayCommand(_ => IsGeneralConfigOpen = true);
            CloseGeneralConfigCommand = new AsyncRelayCommand(async _ => { 
                IsGeneralConfigOpen = false; 
                await EvaluateAsync();
                await AutoSaveSelectedExamAsync(); 
            });

            SelectFolderCommand = new RelayCommand(_ => {
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Excel Dosyaları İçin Varsayılan Klasörü Seçin" };
                if (dialog.ShowDialog() == true) {
                    DefaultExcelPath = dialog.FolderName;
                }
            });

            AddToLog("Gelişmiş puanlama ve arama modülü aktif.", LogLevel.Info);

            ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());

            ExportExcelCommand = new AsyncRelayCommand(async _ => {
                if (Students.Count == 0) {
                    ShowAlert("Dışa Aktarma Hatası", "Dışa aktarılacak öğrenci kaydı bulunamadı.");
                    return;
                }
                
                string? filePath = null;
                if (!string.IsNullOrWhiteSpace(DefaultExcelPath) && System.IO.Directory.Exists(DefaultExcelPath)) {
                    filePath = System.IO.Path.Combine(DefaultExcelPath, $"OptikSonuclar_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                } else {
                    var sfd = new SaveFileDialog { Filter = "Excel Dosyası|*.xlsx", FileName = $"OptikSonuclar_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx" };
                    if (sfd.ShowDialog() == true) filePath = sfd.FileName;
                }

                if (filePath != null) {
                    IsBusy = true;
                    BusyMessage = "Excel raporu oluşturuluyor...";
                    try {
                        var studentsCopy = new List<StudentResult>(Students);
                        var statsCopy = new List<QuestionStatisticItem>(Statistics);
                        var outcomesCopy = LearningOutcomes.ToList();
                        await Task.Run(() => _excelService.ExportToExcel(studentsCopy, statsCopy, outcomesCopy, filePath));
                        ShowAlert("Başarılı", $"Excel raporu kaydedildi:\n{filePath}");
                        AddToLog($"Excel raporu oluşturuldu: {System.IO.Path.GetFileName(filePath)}", LogLevel.Success);
                    } catch (Exception ex) {
                        ShowAlert("Hata", $"Excel oluşturulurken hata: {ex.Message}");
                    } finally {
                        IsBusy = false;
                    }
                }
            });

            ExportCsvCommand = new AsyncRelayCommand(async _ => {
                if (Students.Count == 0) {
                    ShowAlert("Dışa Aktarma Hatası", "Dışa aktarılacak öğrenci kaydı bulunamadı.");
                    return;
                }
                var sfd = new SaveFileDialog { 
                    Filter = "CSV Dosyası|*.csv", 
                    FileName = $"OptikSonuclar_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "CSV Olarak Kaydet"
                };
                if (sfd.ShowDialog() == true) {
                    IsBusy = true;
                    BusyMessage = "CSV dosyası oluşturuluyor...";
                    try {
                        var studentsCopy = new List<StudentResult>(Students);
                        await Task.Run(() => _csvService.ExportToCsv(studentsCopy, sfd.FileName));
                        StatusMessage = "CSV'ye aktarım tamamlandı.";
                        AddToLog($"CSV raporu oluşturuldu: {System.IO.Path.GetFileName(sfd.FileName)}", LogLevel.Success);
                    } catch (Exception ex) {
                        AddToLog($"CSV hatası: {ex.Message}", LogLevel.Error);
                        ShowAlert("Hata", $"CSV oluşturulurken hata: {ex.Message}");
                    } finally {
                        IsBusy = false;
                    }
                }
            });

            ExportPdfCommand = new AsyncRelayCommand(async _ => {
                if (Students.Count == 0) {
                    ShowToastError("Önce veri yüklemeniz ve puanları hesaplamanız gerekmektedir.");
                    return;
                }
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Öğrenci Karnelerinin Kaydedileceği Klasörü Seçin" };
                if (dialog.ShowDialog() == true) {
                    IsBusy = true;
                    BusyMessage = "PDF karneleri hazırlanıyor... (0%)";
                    
                    var progress = new Progress<PdfReportService.PdfProgressReport>(report => {
                        BusyMessage = $"PDF karneleri hazırlanıyor... ({report.Percentage:F0}%) - {report.CurrentStudentName}";
                    });
                    
                    try {
                        var studentsCopy = new List<StudentResult>(Students);
                        var outcomesCopy = LearningOutcomes.ToList();
                        string folder = dialog.FolderName;
                        
                        await _pdfService.GenerateStudentReportsAsync(studentsCopy, studentsCopy, outcomesCopy, folder, SchoolName, progress);
                        
                        ShowToastSuccess($"{Students.Count} adet öğrenci karnesi PDF olarak kaydedildi.");
                        AddToLog($"{Students.Count} adet öğrenci karnesi PDF olarak dışa aktarıldı.", LogLevel.Success);
                    } catch (Exception ex) {
                        ShowToastError($"PDF oluşturma hatası: {ex.Message}");
                        AddToLog($"PDF hatası: {ex.Message}", LogLevel.Error);
                    } finally {
                        IsBusy = false;
                    }
                }
            });

            ExportSinglePdfCommand = new AsyncRelayCommand(async p => {
                if (p is StudentResult student) {
                    var sfd = new SaveFileDialog { 
                        Filter = "PDF Dosyası|*.pdf", 
                        FileName = $"Karne_{student.StudentId}_{student.FullName.Replace(" ", "_")}.pdf",
                        Title = "Öğrenci Karnesini Kaydet"
                    };
                    if (sfd.ShowDialog() == true) {
                        IsBusy = true;
                        BusyMessage = $"{student.FullName} için karne hazırlanıyor...";
                        try {
                            var allStudentsCopy = new List<StudentResult>(Students);
                            var outcomesCopy = LearningOutcomes.ToList();
                            string dir = System.IO.Path.GetDirectoryName(sfd.FileName) ?? "";
                            await Task.Run(() => _pdfService.GenerateStudentReports(new List<StudentResult> { student }, allStudentsCopy, outcomesCopy, dir, SchoolName));
                            StatusMessage = $"{student.FullName} için karne oluşturuldu.";
                            AddToLog($"{student.FullName} için PDF karne oluşturuldu.", LogLevel.Success);
                        } catch (Exception ex) {
                            AddToLog($"Karne hatası: {ex.Message}", LogLevel.Error);
                            ShowAlert("Hata", $"Karne oluşturulurken hata oluştu: {ex.Message}");
                        } finally {
                            IsBusy = false;
                        }
                    }
                }
            });

            // TAM RAPOR PDF EXPORT
            ExportFullReportCommand = new AsyncRelayCommand(async _ => {
                if (Students.Count == 0) {
                    ShowToastError("Önce veri yüklemeniz ve puanları hesaplamanız gerekmektedir.");
                    return;
                }
                
                var sfd = new SaveFileDialog {
                    Filter = "PDF Dosyası|*.pdf",
                    FileName = $"{SelectedCourse?.Name ?? "Sinav"}_TamRapor_{DateTime.Now:yyyyMMdd}.pdf"
                };
                
                if (sfd.ShowDialog() == true) {
                    try {
                        IsBusy = true;
                        BusyMessage = "Tam rapor PDF hazırlanıyor...";
                        
                        await Task.Run(() => {
                            _pdfService.GenerateFullReport(
                                SelectedCourse?.Name ?? "Sınav Raporu",
                                Students.ToList(),
                                QuestionStats.ToList(),
                                QuestionDifficulties.ToList(),
                                LearningOutcomes,
                                ReliabilityStats,
                                ClassStats,
                                ScoreDistribution,
                                QuestionCorrelations,
                                Percentiles,
                                AnswerKeys,
                                sfd.FileName
                            );
                        });
                        
                        ShowToastSuccess($"Tam rapor kaydedildi: {Path.GetFileName(sfd.FileName)}");
                        AddToLog($"Tam rapor PDF export edildi: {sfd.FileName}", LogLevel.Success);
                        
                        // PDF'i aç
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex) {
                        ShowToastError($"PDF oluşturma hatası: {ex.Message}");
                        AddToLog($"Tam rapor PDF hatası: {ex.Message}", LogLevel.Error);
                    }
                    finally {
                        IsBusy = false;
                    }
                }
            });

            ShowAddCourseCommand = new RelayCommand(_ => IsAddCourseOpen = true);
            CloseAddCourseCommand = new RelayCommand(_ => IsAddCourseOpen = false);
            AddCourseCommand = new AsyncRelayCommand(async _ => {
                if (string.IsNullOrWhiteSpace(NewCourseName)) return;
                var c = new Course { Code = NewCourseCode, Name = NewCourseName };
                await _dbService.SaveCourseAsync(c);
                await LoadCoursesAsync();
                IsAddCourseOpen = false;
                NewCourseCode = ""; NewCourseName = "";
                AddToLog($"'{c.Name}' dersi eklendi.", LogLevel.Success);
            });
            DeleteCourseCommand = new AsyncRelayCommand(async obj => {
                if (obj is Course c) {
                    // Sınav var mı kontrol et
                    var exams = await _dbService.GetExamsForCourseAsync(c.Id);
                    if (exams.Any()) {
                        // Modern onay modalını göster
                        ConfirmModalTitle = "Ders Silme Onayı";
                        ConfirmModalMessage = $"'{c.Name}' dersine ait {exams.Count()} sınav bulunuyor.\n\nDersi silerseniz tüm sınavlar da silinecek.\n\nSilmek istediğinize emin misiniz?";
                        ConfirmButtonText = "Sil";
                        _onConfirmAction = async () => {
                            await ExecuteDeleteCourseAsync(c, exams);
                        };
                        IsConfirmModalOpen = true;
                        return;
                    }
                    
                    // Sınav yoksa direkt sil
                    await ExecuteDeleteCourseAsync(c, exams);
                }
            });
            DeleteExamCommand = new AsyncRelayCommand(async obj => {
                if (obj is ExamEntry e) {
                    await _dbService.DeleteExamAsync(e.Id);
                    
                    // Silinen sınavı CourseExams koleksiyonundan da kaldır
                    var examToRemove = CourseExams.FirstOrDefault(ex => ex.Id == e.Id);
                    if (examToRemove != null)
                        CourseExams.Remove(examToRemove);
                    
                    // Eğer silinen sınav seçiliyse veya başka sınav seçili değilse, tüm verileri temizle
                    if (SelectedExam?.Id == e.Id || SelectedExam == null)
                    {
                        SelectedExam = null;
                        Students.Clear();
                        AnswerKeys.Clear();
                        QuestionSettings.Clear();
                        LearningOutcomes.Clear();
                        Statistics.Clear();
                        ValidationIssues.Clear();
                        AccuracyData.Clear();
                        ScoreDistData.Clear();
                        HasUnsavedData = false;
                        StatusMessage = "Sınav silindi. Yeni veri yükleyin.";
                        
                        // Varsayılan cevap anahtarını geri ekle
                        AnswerKeys.Add(new AnswerKeyModel { BookletName = "A", Answers = "" });
                    }
                    
                    OnPropertyChanged(nameof(CourseExams));
                    AddToLog($"'{e.Title}' sınavı silindi.", LogLevel.Warning);
                }
            });
            SaveExamCommand = new AsyncRelayCommand(async _ => {
                // Yeni modal'ı aç
                IsSaveExamModalOpen = true;
            });

            OpenSaveExamModalCommand = new RelayCommand(_ => {
                IsSaveExamModalOpen = true;
                // Initialize with current selected course to avoid null
                SelectedCourseForSave = SelectedCourse;
            });

            CloseSaveExamModalCommand = new RelayCommand(_ => {
                IsSaveExamModalOpen = false;
            });

            ConfirmSaveExamCommand = new AsyncRelayCommand(async _ => {
                string title = string.IsNullOrWhiteSpace(NewExamName) ? $"{DateTime.Now:dd.MM.yyyy} Sınavı" : NewExamName;
                
                // Use SelectedCourseForSave for saving, fallback to SelectedCourse if null
                var courseToUse = SelectedCourseForSave ?? SelectedCourse;
                if (courseToUse == null)
                {
                    ShowToastError("Lütfen bir ders seçin!");
                    return;
                }
                
                // Pass course directly - DON'T change SelectedCourse to avoid clearing Students
                await SaveCurrentExamAsync(title, courseToUse, SelectedExam);
                
                IsSaveExamModalOpen = false;
                NewExamName = ""; // Reset
                SelectedCourseForSave = null; // Reset
            });

            // Inline Course Management Commands
            ToggleAddCourseInlineCommand = new RelayCommand(_ => {
                IsAddCourseInlineOpen = !IsAddCourseInlineOpen;
                if (!IsAddCourseInlineOpen)
                {
                    NewCourseCodeInline = "";
                    NewCourseNameInline = "";
                }
            });

            AddCourseInlineCommand = new AsyncRelayCommand(async _ => {
                if (string.IsNullOrWhiteSpace(NewCourseNameInline)) {
                    ShowToastError("Ders adı girmelisiniz!");
                    return;
                }
                var c = new Course { Code = NewCourseCodeInline, Name = NewCourseNameInline };
                await _dbService.SaveCourseAsync(c);
                await LoadCoursesAsync();
                IsAddCourseInlineOpen = false;
                NewCourseCodeInline = "";
                NewCourseNameInline = "";
                ShowToastSuccess($"'{c.Name}' dersi eklendi!");
                AddToLog($"Yeni ders eklendi: {c.Name}", LogLevel.Success);
            });

            DeleteSelectedCourseInlineCommand = new AsyncRelayCommand(async _ => {
                var courseToDelete = SelectedCourseForSave ?? SelectedCourse;
                if (courseToDelete == null) {
                    ShowToastError("Silinecek ders seçilmedi!");
                    return;
                }
                
                // Sınav var mı kontrol et
                var exams = await _dbService.GetExamsForCourseAsync(courseToDelete.Id);
                if (exams.Any()) {
                    // Modern onay modalını göster
                    ConfirmModalTitle = "Ders Silme Onayı";
                    ConfirmModalMessage = $"'{courseToDelete.Name}' dersine ait {exams.Count()} sınav bulunuyor.\n\nDersi silerseniz tüm sınavlar da silinecek.\n\nSilmek istediğinize emin misiniz?";
                    ConfirmButtonText = "Sil";
                    _onConfirmAction = async () => {
                        await ExecuteDeleteCourseAsync(courseToDelete, exams);
                        // Only clear selections if deleted course matches current selections
                        if (SelectedCourseForSave?.Id == courseToDelete.Id) SelectedCourseForSave = null;
                    };
                    IsConfirmModalOpen = true;
                    return;
                }
                
                // Sınav yoksa direkt sil
                await ExecuteDeleteCourseAsync(courseToDelete, exams);
                if (SelectedCourseForSave?.Id == courseToDelete.Id) SelectedCourseForSave = null;
            });

            OpenRenameCourseCommand = new RelayCommand(obj => {
                if (obj is Course c) {
                    _renameContext = "Course";
                    _renameId = c.Id;
                    RenameModalTitle = "Dersi Düzenle";
                    RenameInput1 = c.Code;
                    RenameInput2 = c.Name;
                    ShowRenameInput2 = true;
                    IsRenameModalOpen = true;
                }
            });
            OpenRenameExamCommand = new RelayCommand(obj => {
                if (obj is ExamEntry e) {
                    _renameContext = "Exam";
                    _renameId = e.Id;
                    RenameModalTitle = "Sınavı Düzenle";
                    RenameInput1 = e.Title;
                    ShowRenameInput2 = false;
                    IsRenameModalOpen = true;
                }
            });
            CloseRenameModalCommand = new RelayCommand(_ => IsRenameModalOpen = false);
            SaveRenameCommand = new AsyncRelayCommand(async _ => {
                if (_renameContext == "Course") {
                    if (string.IsNullOrWhiteSpace(RenameInput1) || string.IsNullOrWhiteSpace(RenameInput2)) return;
                    await _dbService.RenameCourseAsync(_renameId, RenameInput1, RenameInput2);
                    await LoadCoursesAsync();
                    if (SelectedCourse != null && SelectedCourse.Id == _renameId) {
                        SelectedCourse.Code = RenameInput1;
                        SelectedCourse.Name = RenameInput2;
                        OnPropertyChanged(nameof(SelectedCourse));
                    }
                    AddToLog($"Ders düzenlendi: {RenameInput2}", LogLevel.Success);
                } else if (_renameContext == "Exam") {
                    if (string.IsNullOrWhiteSpace(RenameInput1)) return;
                    await _dbService.RenameExamAsync(_renameId, RenameInput1);
                    if (SelectedCourse != null) await LoadExamsForCourseAsync(SelectedCourse.Id);
                    if (SelectedExam != null && SelectedExam.Id == _renameId) {
                        SelectedExam.Title = RenameInput1;
                        OnPropertyChanged(nameof(SelectedExam));
                    }
                    AddToLog($"Sınav yeniden adlandırıldı: {RenameInput1}", LogLevel.Success);
                }
                IsRenameModalOpen = false;
            });

            // Confirmation Modal Commands
            CancelConfirmCommand = new RelayCommand(_ => {
                IsConfirmModalOpen = false;
                _onConfirmAction = null;
            });
            ConfirmActionCommand = new AsyncRelayCommand(async _ => {
                IsConfirmModalOpen = false;
                if (_onConfirmAction != null)
                {
                    var action = _onConfirmAction;
                    _onConfirmAction = null;
                    action();
                }
            });

            ShowShortcutsCommand = new RelayCommand(_ => IsShortcutsOpen = true);
            CloseShortcutsCommand = new RelayCommand(_ => IsShortcutsOpen = false);

            // Shortcut Settings Commands
            OpenShortcutSettingsCommand = new RelayCommand(_ => IsShortcutSettingsOpen = true);
            CloseShortcutSettingsCommand = new RelayCommand(_ => IsShortcutSettingsOpen = false);
            EditShortcutCommand = new RelayCommand(param => {
                // TODO: Implement shortcut editing dialog
                ShowToastInfo("Kısayol düzenleme yakında gelecek!");
            });
            ResetShortcutsCommand = new RelayCommand(_ => {
                _shortcutService.ResetToDefaults();
                ShowToastSuccess("Kısayollar varsayılana sıfırlandı!");
            });

            ExportGradeListCommand = new RelayCommand(_ =>
            {
                if (Students.Count == 0) {
                    ShowAlert("Dışa Aktarma Hatası", "Dışa aktarılacak öğrenci kaydı bulunamadı.");
                    return;
                }
                var sfd = new SaveFileDialog {
                    Filter = "CSV Dosyası|*.csv",
                    FileName = $"NotListesi_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Title = "Not Listesini Kaydet"
                };
                if (sfd.ShowDialog() == true) {
                    try {
                        _csvService.ExportGradeList(new System.Collections.Generic.List<StudentResult>(Students), sfd.FileName);
                        StatusMessage = "Not listesi CSV olarak aktarıldı.";
                        AddToLog($"Not listesi oluşturuldu: {System.IO.Path.GetFileName(sfd.FileName)}", LogLevel.Success);
                    } catch (Exception ex) {
                        AddToLog($"Not listesi hatası: {ex.Message}", LogLevel.Error);
                        ShowAlert("Hata", $"Not listesi oluşturulurken hata: {ex.Message}");
                    }
                }
            });

            ApplyTheme(false);

            // Toast Notification Commands
            DismissNotificationCommand = new RelayCommand(param => {
                if (param is string notificationId) {
                    _notificationService.Dismiss(notificationId);
                }
            });

            // Progress Commands
            CancelProgressCommand = new RelayCommand(_ => _progressService.CancelOperation());

            // Undo/Redo Commands
            UndoCommand = new RelayCommand(_ => {
                if (_undoRedoManager.CanUndo)
                {
                    _undoRedoManager.Undo();
                    ShowToastInfo("İşlem geri alındı");
                    EvaluateAsync().ConfigureAwait(false);
                }
            }, _ => CanUndo);

            RedoCommand = new RelayCommand(_ => {
                if (_undoRedoManager.CanRedo)
                {
                    _undoRedoManager.Redo();
                    ShowToastInfo("İşlem yenilendi");
                    EvaluateAsync().ConfigureAwait(false);
                }
            }, _ => CanRedo);

            CopyReportCommand = new RelayCommand(_ => {
                if (!string.IsNullOrEmpty(ReportText))
                {
                    System.Windows.Clipboard.SetText(ReportText);
                    ShowToastSuccess("Rapor panoya kopyalandı!");
                }
            });

            OpenTemplateManagerCommand = new RelayCommand(_ => {
                RefreshTemplates();
                IsTemplateManagerOpen = true;
                OnPropertyChanged(nameof(IsTemplateManagerOpen));
            });

            CloseTemplateManagerCommand = new RelayCommand(_ => {
                IsTemplateManagerOpen = false;
                OnPropertyChanged(nameof(IsTemplateManagerOpen));
            });

            SaveAsTemplateCommand = new RelayCommand(_ => {
                if (string.IsNullOrWhiteSpace(NewTemplateName))
                {
                    ShowToastError("Şablon adı girmelisiniz!");
                    return;
                }

                var template = _templateService.CreateFromCurrent(
                    NewTemplateName,
                    NewTemplateDescription,
                    AnswerKeys,
                    QuestionSettings,
                    LearningOutcomes,
                    NetCoefficient,
                    BaseScore,
                    WrongDeductionFactor,
                    SchoolName
                );

                _templateService.SaveTemplate(template);
                RefreshTemplates();
                
                NewTemplateName = "";
                NewTemplateDescription = "";
                OnPropertyChanged(nameof(NewTemplateName));
                OnPropertyChanged(nameof(NewTemplateDescription));
                
                ShowToastSuccess("Şablon başarıyla kaydedildi!");
            });

            LoadTemplateCommand = new RelayCommand(param => {
                if (param is string templateId)
                {
                    var template = _templateService.LoadTemplate(templateId);
                    if (template != null)
                    {
                        AnswerKeys.Clear();
                        foreach (var key in template.AnswerKeys) AnswerKeys.Add(key);

                        QuestionSettings.Clear();
                        foreach (var setting in template.QuestionSettings) QuestionSettings.Add(setting);

                        LearningOutcomes.Clear();
                        foreach (var outcome in template.LearningOutcomes) LearningOutcomes.Add(outcome);

                        NetCoefficient = template.NetCoefficient;
                        BaseScore = template.BaseScore;
                        WrongDeductionFactor = template.WrongDeductionFactor;
                        SchoolName = template.SchoolName;

                        OnPropertyChanged(nameof(NetCoefficient));
                        OnPropertyChanged(nameof(BaseScore));
                        OnPropertyChanged(nameof(WrongDeductionFactor));
                        OnPropertyChanged(nameof(SchoolName));

                        IsTemplateManagerOpen = false;
                        OnPropertyChanged(nameof(IsTemplateManagerOpen));

                        ShowToastSuccess($"'{template.Name}' şablonu yüklendi!");
                        AddToLog($"Şablon yüklendi: {template.Name}", LogLevel.Success);
                    }
                }
            });

            DeleteTemplateCommand = new RelayCommand(param => {
                if (param is string templateId)
                {
                    _templateService.DeleteTemplate(templateId);
                    RefreshTemplates();
                    ShowToastInfo("Şablon silindi.");
                }
            });

            ExportTemplateCommand = new RelayCommand(_ => {
                var dialog = new SaveFileDialog {
                    Filter = "JSON Dosyası|*.json",
                    FileName = $"SınavŞablonu_{DateTime.Now:yyyyMMdd}.json"
                };
                if (dialog.ShowDialog() == true)
                {
                    // Export current settings as template
                    var template = _templateService.CreateFromCurrent(
                        "Dışa Aktarılan Şablon",
                        "",
                        AnswerKeys,
                        QuestionSettings,
                        LearningOutcomes,
                        NetCoefficient,
                        BaseScore,
                        WrongDeductionFactor,
                        SchoolName
                    );
                    _templateService.SaveTemplate(template);
                    _templateService.ExportTemplate(template.Id, dialog.FileName);
                    ShowToastSuccess("Şablon dışa aktarıldı!");
                }
            });

            ImportTemplateCommand = new RelayCommand(_ => {
                var dialog = new OpenFileDialog {
                    Filter = "JSON Dosyası|*.json",
                    Title = "Şablon Dosyası Seçin"
                };
                if (dialog.ShowDialog() == true)
                {
                    var template = _templateService.ImportTemplate(dialog.FileName);
                    if (template != null)
                    {
                        RefreshTemplates();
                        ShowToastSuccess($"'{template.Name}' şablonu içe aktarıldı!");
                    }
                    else
                    {
                        ShowToastError("Şablon içe aktarılamadı!");
                    }
                }
            });

            // JSON Export/Import Commands
            ExportJsonCommand = new AsyncRelayCommand(async _ => {
                if (Students.Count == 0)
                {
                    ShowToastError("Dışa aktarılacak veri yok!");
                    return;
                }

                var dialog = new SaveFileDialog {
                    Filter = "JSON Dosyası|*.json",
                    FileName = $"Sınav_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };
                if (dialog.ShowDialog() == true)
                {
                    IsBusy = true;
                    BusyMessage = "JSON dosyası oluşturuluyor...";
                    try
                    {
                        await _jsonDataService.ExportToJsonAsync(
                            dialog.FileName,
                            SelectedExam?.Title ?? "Dışa Aktarılan Sınav",
                            SelectedCourse?.Name ?? "",
                            SchoolName,
                            NetCoefficient,
                            BaseScore,
                            WrongDeductionFactor,
                            AnswerKeys,
                            QuestionSettings,
                            LearningOutcomes,
                            Students
                        );
                        ShowToastSuccess("Veriler JSON olarak dışa aktarıldı!");
                        AddToLog($"JSON dışa aktarma: {Path.GetFileName(dialog.FileName)}", LogLevel.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"Dışa aktarma hatası: {ex.Message}");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            });

            ImportJsonCommand = new AsyncRelayCommand(async _ => {
                var dialog = new OpenFileDialog {
                    Filter = "JSON Dosyası|*.json",
                    Title = "Sınav Verisi JSON Dosyası Seçin"
                };
                if (dialog.ShowDialog() == true)
                {
                    IsBusy = true;
                    BusyMessage = "JSON dosyası okunuyor...";
                    try
                    {
                        var data = await _jsonDataService.ImportFromJsonAsync(dialog.FileName);
                        if (data == null)
                        {
                            ShowToastError("JSON dosyası okunamadı veya geçersiz format!");
                            return;
                        }

                        // Clear existing data
                        SelectedExam = null;
                        Students.Clear();
                        AnswerKeys.Clear();
                        QuestionSettings.Clear();
                        LearningOutcomes.Clear();

                        // Load new data
                        foreach (var key in data.AnswerKeys) AnswerKeys.Add(key);
                        foreach (var setting in data.QuestionSettings) QuestionSettings.Add(setting);
                        foreach (var outcome in data.LearningOutcomes) LearningOutcomes.Add(outcome);
                        foreach (var student in data.Students) Students.Add(student);

                        NetCoefficient = data.NetCoefficient;
                        BaseScore = data.BaseScore;
                        WrongDeductionFactor = data.WrongDeductionFactor;
                        SchoolName = data.SchoolName;

                        OnPropertyChanged(nameof(NetCoefficient));
                        OnPropertyChanged(nameof(BaseScore));
                        OnPropertyChanged(nameof(WrongDeductionFactor));
                        OnPropertyChanged(nameof(SchoolName));

                        HasUnsavedData = true;
                        ShowToastSuccess($"'{data.ExamTitle}' JSON'dan yüklendi! Öğrenci: {data.Students.Count}");
                        AddToLog($"JSON içe aktarma: {data.ExamTitle} ({data.Students.Count} öğrenci)", LogLevel.Success);

                        await EvaluateAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"İçe aktarma hatası: {ex.Message}");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            });

            // Backup & Restore Commands
            CreateFullBackupCommand = new AsyncRelayCommand(async _ => {
                var sfd = new SaveFileDialog {
                    Filter = "ZIP Dosyası|*.zip",
                    FileName = $"OptikBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                    Title = "Yedek Dosyasını Kaydet"
                };
                if (sfd.ShowDialog() == true)
                {
                    IsBusy = true;
                    BusyMessage = "Tam yedek oluşturuluyor...";
                    try
                    {
                        await _backupService.CreateFullBackupAsync(sfd.FileName);
                        ShowToastSuccess("Yedekleme tamamlandı!");
                        AddToLog($"Tam yedek oluşturuldu: {Path.GetFileName(sfd.FileName)}", LogLevel.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"Yedekleme hatası: {ex.Message}");
                        AddToLog($"Yedekleme hatası: {ex.Message}", LogLevel.Error);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            });

            CreateDatabaseBackupCommand = new AsyncRelayCommand(async _ => {
                var sfd = new SaveFileDialog {
                    Filter = "SQLite Database|*.db",
                    FileName = $"OptikDB_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                    Title = "Veritabanı Yedeğini Kaydet"
                };
                if (sfd.ShowDialog() == true)
                {
                    IsBusy = true;
                    BusyMessage = "Veritabanı yedeği oluşturuluyor...";
                    try
                    {
                        await _backupService.CreateDatabaseBackupAsync(sfd.FileName);
                        ShowToastSuccess("Veritabanı yedeği oluşturuldu!");
                        AddToLog($"Veritabanı yedeği: {Path.GetFileName(sfd.FileName)}", LogLevel.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"Yedekleme hatası: {ex.Message}");
                        AddToLog($"Veritabanı yedekleme hatası: {ex.Message}", LogLevel.Error);
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            });

            ExportSettingsCommand = new AsyncRelayCommand(async _ => {
                var sfd = new SaveFileDialog {
                    Filter = "JSON Dosyası|*.json",
                    FileName = $"OptikSettings_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    Title = "Ayarları Dışa Aktar"
                };
                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        await _backupService.ExportSettingsAsync(sfd.FileName);
                        ShowToastSuccess("Ayarlar dışa aktarıldı!");
                        AddToLog($"Ayarlar dışa aktarıldı: {Path.GetFileName(sfd.FileName)}", LogLevel.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"Aktarma hatası: {ex.Message}");
                    }
                }
            });

            RestoreFullBackupCommand = new AsyncRelayCommand(async _ => {
                var ofd = new OpenFileDialog {
                    Filter = "ZIP Yedek Dosyası|*.zip",
                    Title = "Yedek Dosyası Seçin"
                };
                if (ofd.ShowDialog() == true)
                {
                    ConfirmModalTitle = "Yedek Geri Yükleme";
                    ConfirmModalMessage = "Mevcut verilerin üzerine yazılacak.\n\nDevam etmek istediğinize emin misiniz?";
                    ConfirmButtonText = "Geri Yükle";
                    _onConfirmAction = async () => {
                        IsBusy = true;
                        BusyMessage = "Yedek geri yükleniyor...";
                        try
                        {
                            await _backupService.RestoreFullBackupAsync(ofd.FileName, true);
                            await LoadCoursesAsync();
                            ShowToastSuccess("Yedek geri yüklendi! Lütfen uygulamayı yeniden başlatın.");
                            AddToLog($"Yedek geri yüklendi: {Path.GetFileName(ofd.FileName)}", LogLevel.Success);
                        }
                        catch (Exception ex)
                        {
                            ShowToastError($"Geri yükleme hatası: {ex.Message}");
                            AddToLog($"Geri yükleme hatası: {ex.Message}", LogLevel.Error);
                        }
                        finally
                        {
                            IsBusy = false;
                        }
                    };
                    IsConfirmModalOpen = true;
                }
            });

            RestoreDatabaseCommand = new AsyncRelayCommand(async _ => {
                var ofd = new OpenFileDialog {
                    Filter = "SQLite Database|*.db",
                    Title = "Veritabanı Yedeği Seçin"
                };
                if (ofd.ShowDialog() == true)
                {
                    ConfirmModalTitle = "Veritabanı Geri Yükleme";
                    ConfirmModalMessage = "Mevcut veritabanı değiştirilecek.\n\nDevam etmek istediğinize emin misiniz?";
                    ConfirmButtonText = "Geri Yükle";
                    _onConfirmAction = async () => {
                        IsBusy = true;
                        BusyMessage = "Veritabanı geri yükleniyor...";
                        try
                        {
                            await _backupService.RestoreDatabaseAsync(ofd.FileName);
                            await LoadCoursesAsync();
                            ShowToastSuccess("Veritabanı geri yüklendi!");
                            AddToLog($"Veritabanı geri yüklendi: {Path.GetFileName(ofd.FileName)}", LogLevel.Success);
                        }
                        catch (Exception ex)
                        {
                            ShowToastError($"Geri yükleme hatası: {ex.Message}");
                            AddToLog($"Veritabanı geri yükleme hatası: {ex.Message}", LogLevel.Error);
                        }
                        finally
                        {
                            IsBusy = false;
                        }
                    };
                    IsConfirmModalOpen = true;
                }
            });

            ImportSettingsCommand = new AsyncRelayCommand(async _ => {
                var ofd = new OpenFileDialog {
                    Filter = "JSON Dosyası|*.json",
                    Title = "Ayar Dosyası Seçin"
                };
                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        await _backupService.ImportSettingsAsync(ofd.FileName);
                        // Ayarları yeniden yükle
                        var saved = _settingsService.Load();
                        SchoolName = saved.SchoolName;
                        NetCoefficient = saved.NetCoefficient;
                        BaseScore = saved.BaseScore;
                        WrongDeductionFactor = saved.WrongDeductionFactor;
                        ThemeIndex = saved.ThemeIndex;
                        FontSize = saved.FontSize > 0 ? saved.FontSize : 14;
                        ApplyTheme(ThemeIndex == 1);
                        ApplyFontSize(FontSize);
                        ShowToastSuccess("Ayarlar içe aktarıldı!");
                    }
                    catch (Exception ex)
                    {
                        ShowToastError($"Ayar içe aktarma hatası: {ex.Message}");
                    }
                }
            });

            // Item Analysis Commands
            OpenItemAnalysisCommand = new RelayCommand(_ => {
                IsItemAnalysisOpen = true;
                RunItemAnalysisAsync().ConfigureAwait(false);
            });
            CloseItemAnalysisCommand = new RelayCommand(_ => IsItemAnalysisOpen = false);
            RunItemAnalysisCommand = new AsyncRelayCommand(async _ => await RunItemAnalysisAsync());

            // Advanced Filter Commands
            OpenAdvancedFilterCommand = new RelayCommand(_ => IsAdvancedFilterOpen = true);
            CloseAdvancedFilterCommand = new RelayCommand(_ => IsAdvancedFilterOpen = false);
            ClearFiltersCommand = new RelayCommand(_ => ClearAdvancedFilters());
            ToggleSortDirectionCommand = new RelayCommand(_ => { IsSortDescending = !IsSortDescending; });
            SetQuickFilterCommand = new RelayCommand(p => QuickFilterStatus = p?.ToString() ?? "All");

            // Booklet Mapping Commands
            OpenBookletMappingCommand = new RelayCommand(_ => {
                SelectedSourceAnswerKey = AnswerKeys.FirstOrDefault();
                IsBookletMappingOpen = true;
                UpdateMappingPreview();
            });
            CloseBookletMappingCommand = new RelayCommand(_ => IsBookletMappingOpen = false);
            ApplyBookletMappingCommand = new RelayCommand(_ => {
                if (!IsMappingValid || SelectedSourceAnswerKey == null || string.IsNullOrEmpty(SelectedSourceAnswerKey.Answers))
                {
                    ShowToastWarning("Geçerli bir eşleme tanımlanmadı!");
                    return;
                }

                var validation = _bookletMappingService.ParseAndValidate(MappingInputText, SelectedSourceAnswerKey.Answers.Length);
                if (!validation.IsValid) return;

                string targetAnswers = _bookletMappingService.GenerateTargetAnswers(SelectedSourceAnswerKey.Answers, validation.ValidMapping);
                string tName = string.IsNullOrWhiteSpace(TargetBookletName) ? "B" : TargetBookletName.Trim().ToUpper();

                var existing = AnswerKeys.FirstOrDefault(k => string.Equals(k.BookletName, tName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Answers = targetAnswers;
                }
                else
                {
                    AnswerKeys.Add(new AnswerKeyModel { BookletName = tName, Answers = targetAnswers });
                }

                HasUnsavedData = true;
                IsBookletMappingOpen = false;
                ShowToastSuccess($"{tName} kitapçığı başarıyla türetildi ve eklendi ({targetAnswers.Length} soru).");
                AddToLog($"{tName} kitapçığı eşleme matrisi ile türetildi.", LogLevel.Success);

                if (Students.Count > 0)
                {
                    EvaluateCommand.Execute(null);
                }
            });

            // Success Prediction Commands
            OpenSuccessPredictionCommand = new RelayCommand(_ => {
                IsSuccessPredictionOpen = true;
                RunSuccessPredictionAsync().ConfigureAwait(false);
            });
            CloseSuccessPredictionCommand = new RelayCommand(_ => IsSuccessPredictionOpen = false);
            RunSuccessPredictionCommand = new AsyncRelayCommand(async _ => await RunSuccessPredictionAsync());

            // Student Detail Commands
            OpenStudentDetailCommand = new RelayCommand(obj => {
                if (obj is StudentResult student)
                {
                    // Store reference to original student
                    _originalStudentBeforeEdit = student;
                    
                    // Create editable COPY - changes here won't affect original until saved
                    SelectedStudentDetail = new StudentResult
                    {
                        StudentId = student.StudentId,
                        FullName = student.FullName,
                        BookletType = student.BookletType,
                        RawAnswers = student.RawAnswers,
                        Answers = new List<string>(student.Answers),
                        Score = student.Score,
                        CorrectCount = student.CorrectCount,
                        IncorrectCount = student.IncorrectCount,
                        EmptyCount = student.EmptyCount,
                        NetCount = student.NetCount,
                        Rank = student.Rank,
                        RowNumber = student.RowNumber,
                        QuestionResults = new List<bool>(student.QuestionResults),
                        ColoredAnswers = new ObservableCollection<AnswerItem>(student.ColoredAnswers.Select(a => new AnswerItem { Character = a.Character, State = a.State, QuestionNumber = a.QuestionNumber }))
                    };
                    
                    IsStudentDetailOpen = true;
                    AddToLog($"Öğrenci detayı açıldı: {student.FullName} ({student.StudentId})", LogLevel.Info);
                }
            });
            CloseStudentDetailCommand = new RelayCommand(_ => {
                // Discard changes - just close without applying to original
                IsStudentDetailOpen = false;
                _originalStudentBeforeEdit = null;
                SelectedStudentDetail = null;
            });
            SaveStudentDetailCommand = new AsyncRelayCommand(async _ => {
                if (SelectedStudentDetail != null && _originalStudentBeforeEdit != null)
                {
                    // Apply changes to the ORIGINAL student
                    _originalStudentBeforeEdit.StudentId = SelectedStudentDetail.StudentId;
                    _originalStudentBeforeEdit.FullName = SelectedStudentDetail.FullName;
                    
                    // Add to undo stack
                    var command = new StudentDataChangeCommand(
                        new List<StudentResult>(Students),
                        new List<StudentResult>(Students),
                        $"Öğrenci düzenlendi: {SelectedStudentDetail.FullName}"
                    );
                    _undoRedoManager.ExecuteCommand(command);
                    
                    AddToLog($"Öğrenci bilgileri güncellendi: {SelectedStudentDetail.FullName} ({SelectedStudentDetail.StudentId})", LogLevel.Success);
                    ShowToastSuccess("Öğrenci bilgileri güncellendi!");
                    
                    // Refresh the view
                    StudentsView.Refresh();
                    
                    // Auto-save if exam is selected
                    if (SelectedExam != null)
                    {
                        await AutoSaveSelectedExamAsync();
                    }
                }
                IsStudentDetailOpen = false;
                _originalStudentBeforeEdit = null;
                SelectedStudentDetail = null;
            });

            // Validation Details Commands
            ShowValidationDetailsCommand = new RelayCommand(_ => IsValidationDetailsOpen = true);
            CloseValidationDetailsCommand = new RelayCommand(_ => IsValidationDetailsOpen = false);
            CopyValidationErrorsCommand = new RelayCommand(_ => {
                var errorText = string.Join("\n", ValidationIssues.Select(i => $"{i.Title}: {i.Message} (Etkilenen: {i.AffectedItems})"));
                System.Windows.Clipboard.SetText(errorText);
                ShowToastSuccess("Hatalar panoya kopyalandı!");
            });

            // Detailed Answer Key Editor Commands
            OpenDetailedAnswerKeyEditorCommand = new RelayCommand(_ => {
                // Select first answer key if none selected
                if (SelectedAnswerKeyForEdit == null && AnswerKeys.Count > 0)
                    SelectedAnswerKeyForEdit = AnswerKeys[0];
                
                // Sync details from answers string
                SelectedAnswerKeyForEdit?.SyncDetailsFromAnswers();
                
                IsDetailedAnswerKeyEditorOpen = true;
                AddToLog("Detaylı cevap anahtarı düzenleyici açıldı.");
            });
            
            // Open detailed editor from BookletSettingsModal
            OpenDetailedAnswerKeyEditorFromBookletCommand = new RelayCommand(param => {
                if (param is AnswerKeyModel key)
                {
                    SelectedAnswerKeyForEdit = key;
                    SelectedAnswerKeyForEdit?.SyncDetailsFromAnswers();
                    IsDetailedAnswerKeyEditorOpen = true;
                    AddToLog($"Detaylı düzenleyici açıldı: Kitapçık {key.BookletName}");
                }
            });
            
            CloseDetailedAnswerKeyEditorCommand = new AsyncRelayCommand(async _ => { 
                // Sync answers from details back to string
                SelectedAnswerKeyForEdit?.SyncAnswersFromDetails();
                IsDetailedAnswerKeyEditorOpen = false; 
                if (Students.Count > 0) { 
                    await EvaluateAsync(); 
                    await AutoSaveSelectedExamAsync(); 
                }
            });
            
            // Add question to answer key
            AddQuestionToAnswerKeyCommand = new RelayCommand(_ => {
                if (SelectedAnswerKeyForEdit == null) return;
                
                int nextQuestionNumber = SelectedAnswerKeyForEdit.AnswerDetails.Count + 1;
                SelectedAnswerKeyForEdit.AnswerDetails.Add(new AnswerDetailItem
                {
                    QuestionNumber = nextQuestionNumber,
                    Answer = "A" // Default answer
                });
                
                // Sync to string
                SelectedAnswerKeyForEdit.SyncAnswersFromDetails();
                
                OnPropertyChanged(nameof(SelectedAnswerKeyForEdit));
                AddToLog($"Yeni soru eklendi: Soru {nextQuestionNumber}");
            });
            
            // Remove last question from answer key
            RemoveLastQuestionFromAnswerKeyCommand = new RelayCommand(_ => {
                if (SelectedAnswerKeyForEdit == null || SelectedAnswerKeyForEdit.AnswerDetails.Count == 0) return;
                
                var lastItem = SelectedAnswerKeyForEdit.AnswerDetails.Last();
                SelectedAnswerKeyForEdit.AnswerDetails.Remove(lastItem);
                
                // Re-number questions
                for (int i = 0; i < SelectedAnswerKeyForEdit.AnswerDetails.Count; i++)
                {
                    SelectedAnswerKeyForEdit.AnswerDetails[i].QuestionNumber = i + 1;
                }
                
                // Sync to string
                SelectedAnswerKeyForEdit.SyncAnswersFromDetails();
                
                OnPropertyChanged(nameof(SelectedAnswerKeyForEdit));
                AddToLog($"Son soru silindi: Kalan {SelectedAnswerKeyForEdit.AnswerDetails.Count} soru");
            });
            
            AnalyzeAnswerKeyCommand = new RelayCommand(_ => {
                if (SelectedAnswerKeyForEdit == null || Students.Count == 0) return;
                
                // Calculate statistics for each question
                foreach (var detail in SelectedAnswerKeyForEdit.AnswerDetails)
                {
                    int qIndex = detail.QuestionNumber - 1;
                    int correct = 0, wrong = 0, empty = 0;
                    
                    foreach (var student in Students.Where(s => s.BookletType == SelectedAnswerKeyForEdit.BookletName))
                    {
                        if (qIndex < student.QuestionResults.Count)
                        {
                            if (student.QuestionResults[qIndex]) correct++;
                            else if (qIndex < student.Answers.Count && 
                                     (string.IsNullOrEmpty(student.Answers[qIndex]) || student.Answers[qIndex] == " "))
                                empty++;
                            else
                                wrong++;
                        }
                    }
                    
                    detail.CorrectCount = correct;
                    detail.WrongCount = wrong;
                    detail.EmptyCount = empty;
                    detail.DifficultyIndex = Students.Count > 0 ? (double)correct / Students.Count : 0;
                }
                
                OnPropertyChanged(nameof(SelectedAnswerKeyForEdit));
                OnPropertyChanged(nameof(SelectedAnswerKeyStats));
                AddToLog($"Cevap anahtarı analizi tamamlandı: {SelectedAnswerKeyForEdit.BookletName} kitapçığı");
                ShowToastSuccess("Cevap anahtarı analizi tamamlandı!");
            });
            
            OpenBulkAnswerEntryCommand = new RelayCommand(_ => {
                IsBulkAnswerEntryOpen = true;
            });
            
            CloseBulkAnswerEntryCommand = new RelayCommand(_ => {
                IsBulkAnswerEntryOpen = false;
                BulkAnswerEntryText = "";
                BulkAnswerPreview = "";
            });
            
            ApplyBulkAnswerEntryCommand = new RelayCommand(_ => {
                if (SelectedAnswerKeyForEdit == null || string.IsNullOrWhiteSpace(BulkAnswerEntryText)) return;
                
                var lines = BulkAnswerEntryText.Split('\n', '\r')
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToList();
                
                var parsedAnswers = new Dictionary<int, string>();
                
                foreach (var line in lines)
                {
                    // Parse "1 A" or "5-C" format
                    var parts = line.Split(' ', '-', '.', ')');
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0], out int qNum))
                        {
                            var answer = parts[1].ToUpper().Trim();
                            if (answer.Length > 0 && char.IsLetter(answer[0]))
                            {
                                parsedAnswers[qNum] = answer[0].ToString();
                            }
                        }
                    }
                }
                
                // Apply to answer details
                foreach (var kvp in parsedAnswers)
                {
                    var detail = SelectedAnswerKeyForEdit.AnswerDetails.FirstOrDefault(d => d.QuestionNumber == kvp.Key);
                    if (detail != null)
                    {
                        detail.Answer = kvp.Value;
                    }
                }
                
                // Sync to main answers string
                SelectedAnswerKeyForEdit.SyncAnswersFromDetails();
                
                IsBulkAnswerEntryOpen = false;
                BulkAnswerEntryText = "";
                ShowToastSuccess($"{parsedAnswers.Count} cevap uygulandı!");
                AddToLog($"Toplu cevap girişi: {parsedAnswers.Count} cevap uygulandı.");
            });
        }

        public string SearchText 
        { 
            get => _searchText; 
            set { _searchText = value; OnPropertyChanged(); StudentsView.Refresh(); } 
        }

        // Advanced Filtering Properties
        public bool IsAdvancedFilterOpen { get => _isAdvancedFilterOpen; set { _isAdvancedFilterOpen = value; OnPropertyChanged(); } }
        public double? MinScoreFilter { get => _minScoreFilter; set { _minScoreFilter = value; OnPropertyChanged(); StudentsView.Refresh(); } }
        public double? MaxScoreFilter { get => _maxScoreFilter; set { _maxScoreFilter = value; OnPropertyChanged(); StudentsView.Refresh(); } }
        public string? SelectedBookletFilter { get => _selectedBookletFilter; set { _selectedBookletFilter = value; OnPropertyChanged(); StudentsView.Refresh(); } }
        public int? MinCorrectFilter { get => _minCorrectFilter; set { _minCorrectFilter = value; OnPropertyChanged(); StudentsView.Refresh(); } }
        public int? MaxWrongFilter { get => _maxWrongFilter; set { _maxWrongFilter = value; OnPropertyChanged(); StudentsView.Refresh(); } }
        public string SelectedSortColumn { get => _selectedSortColumn; set { _selectedSortColumn = value; OnPropertyChanged(); ApplySorting(); } }
        public bool IsSortDescending { get => _isSortDescending; set { _isSortDescending = value; OnPropertyChanged(); ApplySorting(); } }
        
        // Available Booklet Types for Filter
        public ObservableCollection<string> AvailableBookletTypes => new ObservableCollection<string>(AnswerKeys.Select(k => k.BookletName).Distinct());
        
        // Active Filter Status
        public bool HasActiveFilters => 
            !string.IsNullOrWhiteSpace(SearchText) ||
            MinScoreFilter.HasValue ||
            MaxScoreFilter.HasValue ||
            !string.IsNullOrEmpty(SelectedBookletFilter) ||
            MinCorrectFilter.HasValue ||
            MaxWrongFilter.HasValue;
        
        public string UpdateStatus { get => _updateStatus; set { _updateStatus = value; OnPropertyChanged(); } }
        public bool IsCheckingForUpdate { get => _isCheckingForUpdate; set { _isCheckingForUpdate = value; OnPropertyChanged(); } }
        public bool HasUpdateAvailable { get => _hasUpdateAvailable; set { _hasUpdateAvailable = value; OnPropertyChanged(); } }
        public string LatestVersion { get => _latestVersion; set { _latestVersion = value; OnPropertyChanged(); } }

        public string ActiveFiltersSummary
        {
            get
            {
                var filters = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(SearchText))
                    filters.Add($"Arama: '{SearchText}'");
                if (MinScoreFilter.HasValue || MaxScoreFilter.HasValue)
                    filters.Add($"Puan: {MinScoreFilter?.ToString() ?? "-∞"} - {MaxScoreFilter?.ToString() ?? "∞"}");
                if (!string.IsNullOrEmpty(SelectedBookletFilter))
                    filters.Add($"Kitapçık: {SelectedBookletFilter}");
                if (MinCorrectFilter.HasValue)
                    filters.Add($"Min Doğru: {MinCorrectFilter}");
                if (MaxWrongFilter.HasValue)
                    filters.Add($"Max Yanlış: {MaxWrongFilter}");
                
                return filters.Count > 0 ? "Aktif Filtreler: " + string.Join(", ", filters) : "Filtre yok";
            }
        }

        public string SchoolName { get => _schoolName; set { _schoolName = value; OnPropertyChanged(); SaveSettings(); } }
        public double NetCoefficient { get => _netCoefficient; set { _netCoefficient = value; OnPropertyChanged(); SaveSettings(); } }
        public double BaseScore { get => _baseScore; set { _baseScore = value; OnPropertyChanged(); SaveSettings(); } }
        public double WrongDeductionFactor { get => _wrongDeductionFactor; set { _wrongDeductionFactor = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeductionRuleIndex)); SaveSettings(); } }
        
        public int DeductionRuleIndex 
        { 
            get => _wrongDeductionFactor switch { 0 => 0, 0.33 => 1, 0.25 => 2, _ => 2 };
            set 
            { 
                WrongDeductionFactor = value switch { 0 => 0, 1 => 0.33, 2 => 0.25, _ => 0.25 };
            } 
        }

        public int ValidationIssuesCount => ValidationIssues.Count;

        public GradingSettings GradingSettings 
        { 
            get => _gradingSettings; 
            set { _gradingSettings = value; OnPropertyChanged(); } 
        }

        public ClassGradingStats ClassGradingStats 
        { 
            get => _classGradingStats; 
            set { _classGradingStats = value; OnPropertyChanged(); } 
        }

        public int GradingModeIndex
        {
            get => _gradingSettings.Mode == GradingMode.RelativeEvaluation ? 0 : 1;
            set
            {
                _gradingSettings.Mode = value == 0 ? GradingMode.RelativeEvaluation : GradingMode.AbsoluteGrading;
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public double RelativeThreshold
        {
            get => _gradingSettings.RelativeThreshold;
            set
            {
                _gradingSettings.RelativeThreshold = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }

        // Collections and Properties
        public ObservableCollection<StudentResult> Students { get; set; }
        public ObservableCollection<AnswerKeyModel> AnswerKeys { get; set; }
        public ObservableCollection<QuestionStatisticItem> Statistics { get; set; }
        public ObservableCollection<ValidationIssue> ValidationIssues { get; set; }
        public ObservableCollection<QuestionSetting> QuestionSettings { get; set; } = new();
        public ObservableCollection<LearningOutcome> LearningOutcomes { get; set; } = new();
        public ObservableCollection<Course> Courses { get; set; }
        public ObservableCollection<ExamEntry> CourseExams { get; set; }
        public ObservableCollection<LogEntry> Logs { get; set; } = new();
        public ObservableCollection<ChartItem> AccuracyData { get; set; } = new();
        public ObservableCollection<ChartItem> ScoreDistData { get; set; } = new();

        public Course? SelectedCourse
        {
            get => _selectedCourse;
            set { _selectedCourse = value; OnPropertyChanged(); if (value != null) _ = LoadExamsForCourseAsync(value.Id); else CourseExams.Clear(); }
        }

        public ExamEntry? SelectedExam
        {
            get => _selectedExam;
            set { _selectedExam = value; OnPropertyChanged(); if (value != null) _ = LoadExamDataAsync(value); }
        }

        public bool IsAddCourseOpen { get => _isAddCourseOpen; set { _isAddCourseOpen = value; OnPropertyChanged(); } }
        public string NewCourseCode { get => _newCourseCode; set { _newCourseCode = value; OnPropertyChanged(); } }
        public string NewCourseName { get => _newCourseName; set { _newCourseName = value; OnPropertyChanged(); } }
        
        public string NewExamName { get => _newExamName; set { _newExamName = value; OnPropertyChanged(); } }
        private string _newExamName = "";

        public bool IsRenameModalOpen { get => _isRenameModalOpen; set { _isRenameModalOpen = value; OnPropertyChanged(); } }
        public bool IsConfirmModalOpen { get => _isConfirmModalOpen; set { _isConfirmModalOpen = value; OnPropertyChanged(); } }
        public string ConfirmModalTitle { get => _confirmModalTitle; set { _confirmModalTitle = value; OnPropertyChanged(); } }
        public string ConfirmModalMessage { get => _confirmModalMessage; set { _confirmModalMessage = value; OnPropertyChanged(); } }
        public string ConfirmButtonText { get => _confirmButtonText; set { _confirmButtonText = value; OnPropertyChanged(); } }
        public string RenameModalTitle { get => _renameModalTitle; set { _renameModalTitle = value; OnPropertyChanged(); } }
        public string RenameInput1 { get => _renameInput1; set { _renameInput1 = value; OnPropertyChanged(); } }
        public string RenameInput2 { get => _renameInput2; set { _renameInput2 = value; OnPropertyChanged(); } }
        public bool ShowRenameInput2 { get => _showRenameInput2; set { _showRenameInput2 = value; OnPropertyChanged(); } }

        public bool IsModalOpen { get => _isModalOpen; set { _isModalOpen = value; OnPropertyChanged(); } }
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }
        public bool IsQuestionSettingsOpen { get => _isQuestionSettingsOpen; set { _isQuestionSettingsOpen = value; OnPropertyChanged(); } }
        public bool IsLearningOutcomesOpen { get => _isLearningOutcomesOpen; set { _isLearningOutcomesOpen = value; OnPropertyChanged(); } }
        public bool IsAlertOpen { get => _isAlertOpen; set { _isAlertOpen = value; OnPropertyChanged(); } }
        public string AlertTitle { get => _alertTitle; set { _alertTitle = value; OnPropertyChanged(); } }
        public string AlertMessage { get => _alertMessage; set { _alertMessage = value; OnPropertyChanged(); } }
        public bool IsAboutOpen { get => _isAboutOpen; set { _isAboutOpen = value; OnPropertyChanged(); } }
        public bool IsShortcutsOpen { get => _isShortcutsOpen; set { _isShortcutsOpen = value; OnPropertyChanged(); } }
        private bool _isShortcutsOpen;
        public bool IsShortcutSettingsOpen { get => _isShortcutSettingsOpen; set { _isShortcutSettingsOpen = value; OnPropertyChanged(); } }
        public ICommand OpenShortcutSettingsCommand { get; set; }
        public ICommand CloseShortcutSettingsCommand { get; set; }
        public ICommand EditShortcutCommand { get; set; }
        public ICommand ResetShortcutsCommand { get; set; }
        public bool IsUISettingsOpen { get => _isUISettingsOpen; set { _isUISettingsOpen = value; OnPropertyChanged(); } }
        public bool IsGeneralConfigOpen { get => _isGeneralConfigOpen; set { _isGeneralConfigOpen = value; OnPropertyChanged(); } }
        public string DefaultExcelPath { get => _defaultExcelPath; set { _defaultExcelPath = value; OnPropertyChanged(); SaveSettings(); } }
        
        public int ThemeIndex { get => _themeIndex; set { _themeIndex = value; OnPropertyChanged(); ApplyTheme(value == 1); OnPropertyChanged(nameof(IsDarkTheme)); SaveSettings(); } }
        public bool IsDarkTheme => _themeIndex == 1;
        public string CurrentVersion => _versionService.CurrentVersion;
        public int LayoutIndex { get => _layoutIndex; set { _layoutIndex = value; OnPropertyChanged(); GridRowHeight = value == 0 ? 32 : 50; GridCellPadding = value == 0 ? new System.Windows.Thickness(10, 0, 10, 0) : new System.Windows.Thickness(15, 0, 15, 0); SaveSettings(); } }
        public double FontSize { get => _fontSize; set { _fontSize = value; OnPropertyChanged(); ApplyFontSize(value); SaveSettings(); } }

        public int GridRowHeight { get => _gridRowHeight; set { _gridRowHeight = value; OnPropertyChanged(); } }
        private int _gridRowHeight = 32;
        public System.Windows.Thickness GridCellPadding { get => _gridCellPadding; set { _gridCellPadding = value; OnPropertyChanged(); } }
        private System.Windows.Thickness _gridCellPadding = new System.Windows.Thickness(10, 0, 10, 0);

        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        public string BusyMessage { get => _busyMessage; set { _busyMessage = value; OnPropertyChanged(); } }

        public string NewOutcomeName { get => _newOutcomeName; set { _newOutcomeName = value; OnPropertyChanged(); } }
        public string NewOutcomeRange { get => _newOutcomeRange; set { _newOutcomeRange = value; OnPropertyChanged(); } }
        public string NewOutcomeBooklet { get => _newOutcomeBooklet; set { _newOutcomeBooklet = value; OnPropertyChanged(); } }

        // Zorluk Seviyesi Analizi için yeni property'ler
        public ObservableCollection<QuestionDifficulty> QuestionDifficulties { get; } = new ObservableCollection<QuestionDifficulty>();
        public double AverageDifficulty { get; set; } = 0;
        public string DifficultyDistribution { get; set; } = "";

        // İstatistiksel Raporlar için yeni property'ler
        public StatisticsReportService.ClassStatistics ClassStats { get; private set; } = new();
        public ObservableCollection<StatisticsReportService.ScoreDistribution> ScoreDistribution { get; } = new();
        public ObservableCollection<StatisticsReportService.QuestionCorrelation> QuestionCorrelations { get; } = new();
        public Dictionary<string, double> Percentiles { get; private set; } = new();
        public string ReportText { get; private set; } = "";
        public ICommand CopyReportCommand { get; set; }

        // Toast Notifications
        public System.Collections.ObjectModel.ObservableCollection<ToastNotificationModel> Notifications => _notificationService.ActiveNotifications;
        public ICommand DismissNotificationCommand { get; set; }

        public bool HasUnsavedData { get => _hasUnsavedData; set { _hasUnsavedData = value; OnPropertyChanged(); OnPropertyChanged(nameof(SaveStatusText)); } }
        public string SaveStatusText => _hasUnsavedData ? "⚠️ Kaydedilmedi" : (Students.Count > 0 ? "✅ Kaydedildi" : "");

        // Undo/Redo Properties
        public bool CanUndo => _undoRedoManager.CanUndo;
        public bool CanRedo => _undoRedoManager.CanRedo;
        public string UndoDescription => _undoRedoManager.CanUndo ? $"Geri Al: {_undoRedoManager.GetUndoDescription()}" : "Geri alınacak işlem yok";
        public string RedoDescription => _undoRedoManager.CanRedo ? $"Yenile: {_undoRedoManager.GetRedoDescription()}" : "Yenilenecek işlem yok";

        // Commands
        public ICommand LoadTxtCommand { get; set; }
        public ICommand EvaluateCommand { get; set; }
        public ICommand ExportExcelCommand { get; set; }
        public ICommand ExportCsvCommand { get; set; }
        public ICommand ExportPdfCommand { get; set; }
        public ICommand ExportSinglePdfCommand { get; set; }
        public ICommand ExportFullReportCommand { get; set; }
        public ICommand AddAnswerKeyCommand { get; set; }
        public ICommand RemoveAnswerKeyCommand { get; set; }
        public ICommand OpenModalCommand { get; set; }
        public ICommand CloseModalCommand { get; set; }
        public ICommand OpenQuestionSettingsCommand { get; set; }
        public ICommand CloseQuestionSettingsCommand { get; set; }
        public ICommand OpenLearningOutcomesCommand { get; set; }
        public ICommand CloseLearningOutcomesCommand { get; set; }
        public ICommand AddLearningOutcomeCommand { get; set; }
        public ICommand AddOutcomeCommand { get; set; } = new RelayCommand(_ => { });
        public ICommand RemoveOutcomeCommand { get; set; }
        public ICommand SelectFolderCommand { get; set; }
        public ICommand CloseAlertCommand { get; set; }
        public ICommand OpenAboutCommand { get; set; }
        public ICommand OpenGitHubCommand { get; set; }
        public ICommand CheckForUpdatesCommand { get; set; }
        public ICommand OpenReleasesPageCommand { get; set; }
        public ICommand CloseAboutCommand { get; set; }
        public ICommand ShowShortcutsCommand { get; set; }
        public ICommand CloseShortcutsCommand { get; set; }
        public ICommand ExportGradeListCommand { get; set; }
        public ICommand OpenUISettingsCommand { get; set; }
        public ICommand CloseUISettingsCommand { get; set; }
        public bool IsSaveExamModalOpen
        {
            get => _isSaveExamModalOpen;
            set
            {
                _isSaveExamModalOpen = value;
                OnPropertyChanged();
            }
        }

        public ICommand OpenSaveExamModalCommand { get; set; }
        public ICommand CloseSaveExamModalCommand { get; set; }
        public ICommand ConfirmSaveExamCommand { get; set; }
        public ICommand OpenGeneralConfigCommand { get; set; }
        public ICommand CloseGeneralConfigCommand { get; set; }
        public ICommand ExitCommand { get; set; }
        public ICommand ShowAddCourseCommand { get; set; }
        public ICommand CloseAddCourseCommand { get; set; }
        public ICommand AddCourseCommand { get; set; }
        public ICommand DeleteCourseCommand { get; set; }
        public ICommand DeleteExamCommand { get; set; }
        public ICommand SaveExamCommand { get; set; }
        public ICommand OpenRenameCourseCommand { get; set; }
        public ICommand OpenRenameExamCommand { get; set; }
        public ICommand CloseRenameModalCommand { get; set; }
        public ICommand SaveRenameCommand { get; set; }
        public ICommand CancelConfirmCommand { get; set; }
        public ICommand ConfirmActionCommand { get; set; }
        public ICommand UndoCommand { get; set; }
        public ICommand RedoCommand { get; set; }

        // Template Manager Properties and Commands
        public bool IsTemplateManagerOpen { get; set; }
        public string NewTemplateName { get; set; } = "";
        public string NewTemplateDescription { get; set; } = "";
        public ObservableCollection<TemplateService.ExamTemplate> Templates { get; } = new();
        public ICommand OpenTemplateManagerCommand { get; set; }
        public ICommand CloseTemplateManagerCommand { get; set; }
        public ICommand SaveAsTemplateCommand { get; set; }
        public ICommand LoadTemplateCommand { get; set; }
        public ICommand DeleteTemplateCommand { get; set; }
        public ICommand ExportTemplateCommand { get; set; }
        public ICommand ImportTemplateCommand { get; set; }

        // JSON Data Import/Export Commands
        public ICommand ExportJsonCommand { get; set; }
        public ICommand ImportJsonCommand { get; set; }

        // Backup & Restore Commands
        public ICommand CreateFullBackupCommand { get; set; }
        public ICommand CreateDatabaseBackupCommand { get; set; }
        public ICommand ExportSettingsCommand { get; set; }
        public ICommand RestoreFullBackupCommand { get; set; }
        public ICommand RestoreDatabaseCommand { get; set; }
        public ICommand ImportSettingsCommand { get; set; }
        
        public bool IsItemAnalysisOpen { get => _isItemAnalysisOpen; set { _isItemAnalysisOpen = value; OnPropertyChanged(); } }
        public ObservableCollection<ItemAnalysisService.QuestionItemStats> QuestionStats { get => _questionStats; set { _questionStats = value; OnPropertyChanged(); } }
        public ObservableCollection<ItemAnalysisService.AnomalyResult> Anomalies { get => _anomalies; set { _anomalies = value; OnPropertyChanged(); } }
        public ItemAnalysisService.ReliabilityStats ReliabilityStats { get => _reliabilityStats; set { _reliabilityStats = value; OnPropertyChanged(); } }
        public ObservableCollection<ItemAnalysisService.QuestionDistractorStats> DistractorStats { get => _distractorStats; set { _distractorStats = value; OnPropertyChanged(); } }
        private ItemAnalysisService.QuestionDistractorStats? _selectedDistractorQuestion;
        public ItemAnalysisService.QuestionDistractorStats? SelectedDistractorQuestion { get => _selectedDistractorQuestion; set { _selectedDistractorQuestion = value; OnPropertyChanged(); } }

        // Booklet Mapping Properties & Commands
        public bool IsBookletMappingOpen { get => _isBookletMappingOpen; set { _isBookletMappingOpen = value; OnPropertyChanged(); } }
        public AnswerKeyModel? SelectedSourceAnswerKey 
        { 
            get => _selectedSourceAnswerKey; 
            set { _selectedSourceAnswerKey = value; OnPropertyChanged(); UpdateMappingPreview(); } 
        }
        public string TargetBookletName 
        { 
            get => _targetBookletName; 
            set { _targetBookletName = value; OnPropertyChanged(); UpdateMappingPreview(); } 
        }
        public string MappingInputText 
        { 
            get => _mappingInputText; 
            set { _mappingInputText = value; OnPropertyChanged(); UpdateMappingPreview(); } 
        }
        public string MappingStatusHeader { get => _mappingStatusHeader; set { _mappingStatusHeader = value; OnPropertyChanged(); } }
        public string MappingPreviewOrErrorMessage { get => _mappingPreviewOrErrorMessage; set { _mappingPreviewOrErrorMessage = value; OnPropertyChanged(); } }
        public bool IsMappingValid { get => _isMappingValid; set { _isMappingValid = value; OnPropertyChanged(); } }

        public ICommand OpenBookletMappingCommand { get; set; }
        public ICommand CloseBookletMappingCommand { get; set; }
        public ICommand ApplyBookletMappingCommand { get; set; }
        
        // Item Analysis Commands
        public ICommand OpenItemAnalysisCommand { get; set; }
        public ICommand CloseItemAnalysisCommand { get; set; }
        public ICommand RunItemAnalysisCommand { get; set; }

        // Advanced Filter Commands
        public ICommand OpenAdvancedFilterCommand { get; set; }
        public ICommand CloseAdvancedFilterCommand { get; set; }
        public ICommand ClearFiltersCommand { get; set; }
        public ICommand ToggleSortDirectionCommand { get; set; }
        public ICommand SetQuickFilterCommand { get; set; }

        // Success Prediction Properties
        public bool IsSuccessPredictionOpen { get => _isSuccessPredictionOpen; set { _isSuccessPredictionOpen = value; OnPropertyChanged(); } }
        public ObservableCollection<SuccessPredictionService.PredictionResult> StudentPredictions { get => _studentPredictions; set { _studentPredictions = value; OnPropertyChanged(); } }
        public SuccessPredictionService.ClassPredictionSummary ClassPredictionSummary { get => _classPredictionSummary; set { _classPredictionSummary = value; OnPropertyChanged(); } }
        public double PassingScore { get => _passingScore; set { _passingScore = value; OnPropertyChanged(); } }
        
        // Success Prediction Commands
        public ICommand OpenSuccessPredictionCommand { get; set; }
        public ICommand CloseSuccessPredictionCommand { get; set; }
        public ICommand RunSuccessPredictionCommand { get; set; }

        // Student Detail Properties
        public bool IsStudentDetailOpen { get => _isStudentDetailOpen; set { _isStudentDetailOpen = value; OnPropertyChanged(); } }
        public StudentResult? SelectedStudentDetail { get => _selectedStudentDetail; set { _selectedStudentDetail = value; OnPropertyChanged(); } }
        
        // Student Detail Commands
        public ICommand OpenStudentDetailCommand { get; set; }
        public ICommand CloseStudentDetailCommand { get; set; }
        public ICommand SaveStudentDetailCommand { get; set; }

        // Validation Details Properties
        public bool IsValidationDetailsOpen { get => _isValidationDetailsOpen; set { _isValidationDetailsOpen = value; OnPropertyChanged(); } }
        
        // Validation Details Commands
        public ICommand ShowValidationDetailsCommand { get; set; }
        public ICommand CloseValidationDetailsCommand { get; set; }
        public ICommand CopyValidationErrorsCommand { get; set; }

        // Detailed Answer Key Editor Properties
        public bool IsDetailedAnswerKeyEditorOpen { get => _isDetailedAnswerKeyEditorOpen; set { _isDetailedAnswerKeyEditorOpen = value; OnPropertyChanged(); } }
        public bool IsBulkAnswerEntryOpen { get => _isBulkAnswerEntryOpen; set { _isBulkAnswerEntryOpen = value; OnPropertyChanged(); } }
        public AnswerKeyModel? SelectedAnswerKeyForEdit { get => _selectedAnswerKeyForEdit; set { _selectedAnswerKeyForEdit = value; OnPropertyChanged(); } }
        public string BulkAnswerEntryText { get => _bulkAnswerEntryText; set { _bulkAnswerEntryText = value; OnPropertyChanged(); UpdateBulkPreview(); } }
        public string BulkAnswerPreview { get => _bulkAnswerPreview; set { _bulkAnswerPreview = value; OnPropertyChanged(); } }
        
        // Inline Course Management Properties
        public bool IsAddCourseInlineOpen { get => _isAddCourseInlineOpen; set { _isAddCourseInlineOpen = value; OnPropertyChanged(); } }
        public string NewCourseCodeInline { get => _newCourseCodeInline; set { _newCourseCodeInline = value; OnPropertyChanged(); } }
        public string NewCourseNameInline { get => _newCourseNameInline; set { _newCourseNameInline = value; OnPropertyChanged(); } }
        
        // Separate property for SaveExamModal dropdown (isolated from sidebar)
        public Course? SelectedCourseForSave { get => _selectedCourseForSave; set { _selectedCourseForSave = value; OnPropertyChanged(); } }
        
        // Selected answer key stats for display
        public string SelectedAnswerKeyStats => SelectedAnswerKeyForEdit?.AnswerDetails?.Count > 0 
            ? $"{SelectedAnswerKeyForEdit.AnswerDetails.Count} Soru"
            : "Henüz cevap yok";

        // Detailed Answer Key Editor Commands
        public ICommand OpenDetailedAnswerKeyEditorCommand { get; set; }
        public ICommand OpenDetailedAnswerKeyEditorFromBookletCommand { get; set; }
        public ICommand CloseDetailedAnswerKeyEditorCommand { get; set; }
        public ICommand AnalyzeAnswerKeyCommand { get; set; }
        public ICommand AddQuestionToAnswerKeyCommand { get; set; }
        public ICommand RemoveLastQuestionFromAnswerKeyCommand { get; set; }
        public ICommand OpenBulkAnswerEntryCommand { get; set; }
        public ICommand CloseBulkAnswerEntryCommand { get; set; }
        public ICommand ApplyBulkAnswerEntryCommand { get; set; }

        // Inline Course Management Commands
        public ICommand ToggleAddCourseInlineCommand { get; set; }
        public ICommand AddCourseInlineCommand { get; set; }
        public ICommand DeleteSelectedCourseInlineCommand { get; set; }

        // Progress Service
        public ProgressService Progress => _progressService;
        public ICommand CancelProgressCommand { get; set; }

        public void Dispose()
        {
            _dbService?.Dispose();
            GC.SuppressFinalize(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
