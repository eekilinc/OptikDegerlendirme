using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using OptikFormApp.Models;

namespace OptikFormApp.ViewModels
{
    public partial class MainViewModel
    {
        private async Task LoadTxtFileAsync()
        {
            var openFileDialog = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt", Title = "Optik Okuyucu Dosyasını Seçin" };
            if (openFileDialog.ShowDialog() == true)
            {
                await LoadDroppedFileAsync(openFileDialog.FileName);
            }
        }

        public async Task LoadDroppedFileAsync(string filePath)
        {
            SelectedExam = null;
            Students.Clear(); AnswerKeys.Clear();
            Statistics.Clear(); ValidationIssues.Clear(); AccuracyData.Clear(); ScoreDistData.Clear();
            
            StatusMessage = "Dosya okunuyor, lütfen bekleyin...";
            ShowToastInfo($"{System.IO.Path.GetFileName(filePath)} dosyası yükleniyor...");
            
            try
            {
                var (students, answerKeys, errors) = await _parserService.ParseFileAsync(filePath);
                bool isCritical = errors.Any(e => e.StartsWith("KRİTİK HATA"));
                
                if (errors.Count > 0)
                {
                    // Hataları log'a ekle
                    foreach (var err in errors) AddToLog(err, LogLevel.Error);
                    
                    // Parse hatalarını ValidationIssues'e de ekle (üst panelde görünsün)
                    foreach (var err in errors.Where(e => !e.StartsWith("KRİTİK HATA")))
                    {
                        var parts = err.Split(':');
                        var title = parts.Length > 1 ? parts[0].Trim() : "Parse Hatası";
                        var message = parts.Length > 1 ? string.Join(":", parts.Skip(1)).Trim() : err;
                        
                        ValidationIssues.Add(new ValidationIssue
                        {
                            Title = title.Length > 50 ? title.Substring(0, 50) + "..." : title,
                            Message = message,
                            Severity = ValidationSeverity.Warning,
                            AffectedItems = err.Contains('[') && err.Contains(']') 
                                ? err.Substring(err.IndexOf('['), err.IndexOf(']') - err.IndexOf('[') + 1)
                                : $"Satır {errors.IndexOf(err) + 1}"
                        });
                    }
                    OnPropertyChanged(nameof(ValidationIssuesCount));
                    
                    if (isCritical)
                    {
                        ShowToastError("Dosya formatı geçersiz. Lütfen optik okuyucu çıktısı (.txt) olduğundan emin olun.");
                        StatusMessage = "Dosya formatı geçersiz.";
                        return;
                    }
                    ShowToastWarning($"{errors.Count} satırda hata var, {students.Count} kayıt yüklendi.");
                }
                
                if (answerKeys.Count > 0)
                {
                    AnswerKeys.Clear();
                    foreach(var ak in answerKeys) AnswerKeys.Add(ak);
                    ShowToastSuccess($"{answerKeys.Count} adet cevap anahtarı yüklendi.");
                }
                
                Students.Clear();
                int rowNum = 1;
                foreach (var student in students) { student.RowNumber = rowNum++; Students.Add(student); }
                
                if (students.Count > 0)
                {
                    HasUnsavedData = true;
                    StatusMessage = $"{students.Count} öğrenci başarıyla yüklendi.";
                    ShowToastSuccess($"{students.Count} öğrenci kaydı başarıyla yüklendi.");
                    bool hasValidKey = false;
                    foreach(var key in AnswerKeys) { if(!string.IsNullOrWhiteSpace(key.Answers)) hasValidKey = true; }
                    if (hasValidKey) await EvaluateAsync();
                }
                else
                {
                    StatusMessage = "Dosyada yüklenebilir öğrenci kaydı bulunamadı.";
                    ShowToastWarning("Yüklenebilir kayıt bulunamadı. Lütfen dosya formatını kontrol edin.");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Hata: {ex.Message}";
                ShowToastError($"Yükleme hatası: {ex.Message}");
            }
        }

        private async Task EvaluateAsync()
        {
            bool hasValidKey = false;
            foreach(var key in AnswerKeys) { if(!string.IsNullOrWhiteSpace(key.Answers)) hasValidKey = true; }
            if (!hasValidKey)
            {
                StatusMessage = "Cevap anahtarı girilmeden hesaplama yapılamaz!";
                AddToLog("Değerlendirme iptal: Cevap anahtarı eksik.", LogLevel.Warning);
                ShowAlert("Eksik Cevap Anahtarı", "Lütfen puanları hesaplamadan veya dosyayı içe aktarmadan önce 'Cevap Anahtarlarını Yönet' bölümünden bir cevap şablonu (Örn: A) doldurunuz.");
                return;
            }

            IsBusy = true;
            BusyMessage = "Puanlar hesaplanıyor...";
            AddToLog("Puanlar hesaplanıyor ve madde analizi yapılıyor...");
            
            try
            {
                var list = Students.Select(s => new StudentResult 
                { 
                    StudentId = s.StudentId, 
                    FullName = s.FullName, 
                    BookletType = s.BookletType, 
                    RawAnswers = s.RawAnswers,
                    Answers = new List<string>(s.Answers)
                }).ToList();

                var keys = new List<AnswerKeyModel>(AnswerKeys);
                var settings = new List<QuestionSetting>(QuestionSettings);
                var factor = WrongDeductionFactor;
                var coeff = NetCoefficient;
                var bScore = BaseScore;

                ClassGradingStats? gradingStats = null;
                await Task.Run(() => 
                {
                    _parserService.EvaluateStudents(list, keys, settings, factor, coeff, bScore);
                    gradingStats = _gradingService.CalculateGrades(list, GradingSettings);
                });

                var statsList = _parserService.CalculateStatistics(list, keys);
                var issues = _validationService.Validate(list, keys);

                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    for (int i = 0; i < Students.Count && i < list.Count; i++)
                    {
                        var original = Students[i];
                        var evaluated = list[i];
                        
                        original.CorrectCount = evaluated.CorrectCount;
                        original.IncorrectCount = evaluated.IncorrectCount;
                        original.EmptyCount = evaluated.EmptyCount;
                        original.NetCount = evaluated.NetCount;
                        original.Rank = evaluated.Rank;
                        original.Score = evaluated.Score;
                        original.LetterGrade = evaluated.LetterGrade;
                        original.GradePoint = evaluated.GradePoint;
                        original.Status = evaluated.Status;
                        original.ZScore = evaluated.ZScore;
                        original.TScore = evaluated.TScore;
                        original.Percentile = evaluated.Percentile;
                        
                        original.QuestionResults = new List<bool>(evaluated.QuestionResults);
                        original.ColoredAnswers.Clear();
                        foreach (var answer in evaluated.ColoredAnswers)
                        {
                            original.ColoredAnswers.Add(answer);
                        }
                    }

                    if (gradingStats != null)
                    {
                        ClassGradingStats = gradingStats;
                    }

                    Statistics.Clear();
                    foreach (var stat in statsList) Statistics.Add(stat);
                    
                    ValidationIssues.Clear();
                    foreach (var issue in issues) ValidationIssues.Add(issue);
                    
                    StudentsView.Refresh();
                    UpdateChartData(statsList, list);
                    UpdateOutcomeStats();
                    UpdateQuestionDifficultyAnalysis();
                    UpdateStatisticsReport();
                    OnPropertyChanged(nameof(ValidationIssuesCount));
                });

                await RunItemAnalysisAsync();

                StatusMessage = "Değerlendirme başarıyla tamamlandı.";
                if (gradingStats != null)
                {
                    AddToLog($"Değerlendirme ve Harf Notları tamamlandı. Düzey: {gradingStats.ClassLevelDescription}, Ort: {gradingStats.MeanScore:F2}, Sapma: {gradingStats.StandardDeviation:F2}, Başarı: %{gradingStats.PassRate:F1}", LogLevel.Success);
                }
                else
                {
                    AddToLog("Değerlendirme başarıyla tamamlandı.", LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Hesaplama sırasında teknik bir sorun oluştu.";
                var detailedError = $"Hata: {ex.Message}\nStackTrace: {ex.StackTrace}";
                if (ex.InnerException != null)
                    detailedError += $"\nInner: {ex.InnerException.Message}";
                AddToLog($"Hesaplama hatası (Teknik Detay): {detailedError}", LogLevel.Error);
                ShowAlert("Hesaplama Hatası", "Puanlar hesaplanırken bir sorun oluştu. Lütfen verilerinizi ve cevap anahtarını kontrol edin.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateOutcomeStats()
        {
            if (Students.Count == 0 || LearningOutcomes.Count == 0) return;

            var groups = LearningOutcomes.GroupBy(o => o.Name).ToList();
            var updatedOutcomes = new List<LearningOutcome>();

            foreach (var group in groups)
            {
                var globalCorrect = 0;
                var globalTotal = 0;

                foreach (var outcome in group)
                {
                    var bookletStudents = Students.Where(s => s.BookletType == outcome.BookletName).ToList();
                    
                    foreach (var student in bookletStudents)
                    {
                        foreach (var qNum in outcome.QuestionNumbers)
                        {
                            if (qNum > 0 && qNum <= student.QuestionResults.Count)
                            {
                                globalTotal++;
                                if (student.QuestionResults[qNum - 1]) globalCorrect++;
                            }
                        }
                    }

                    outcome.TotalQuestions = globalTotal;
                    outcome.SuccessRate = globalTotal > 0 ? (double)globalCorrect / globalTotal * 100 : 0;
                }

                var firstOutcome = group.First();
                firstOutcome.GlobalCorrectCount = globalCorrect;
                firstOutcome.GlobalTotalCount = globalTotal;
                firstOutcome.GlobalSuccessRate = globalTotal > 0 ? (double)globalCorrect / globalTotal * 100 : 0;
                updatedOutcomes.Add(firstOutcome);
            }

            LearningOutcomes.Clear();
            foreach (var outcome in updatedOutcomes)
            {
                LearningOutcomes.Add(outcome);
            }
            
            OnPropertyChanged(nameof(LearningOutcomes));
            AddToLog($"Kazanım odaklı başarı analizi tamamlandı: {LearningOutcomes.Count} kazanımdan {groups.Count()} benzersiz konu hesaplandı.", LogLevel.Success);
        }

        private void UpdateQuestionDifficultyAnalysis()
        {
            if (Students.Count == 0) return;

            QuestionDifficulties.Clear();
            double totalDifficulty = 0;
            int minQuestionCount = Students.Min(s => s.QuestionResults?.Count ?? 0);
            int questionCount = Math.Min(minQuestionCount, 50);

            for (int i = 0; i < questionCount; i++)
            {
                var questionNum = i + 1;
                var correctCount = Students.Count(s => s.QuestionResults != null && i < s.QuestionResults.Count && s.QuestionResults[i]);
                var successRate = Students.Count > 0 ? (double)correctCount / Students.Count * 100 : 0;

                var difficulty = new QuestionDifficulty
                {
                    QuestionNumber = questionNum,
                    SuccessRate = successRate,
                    CorrectAnswers = correctCount,
                    TotalAnswers = Students.Count,
                    DifficultyLevel = GetDifficultyLevel(successRate)
                };

                QuestionDifficulties.Add(difficulty);
                totalDifficulty += successRate;
            }

            AverageDifficulty = questionCount > 0 ? totalDifficulty / questionCount : 0;
            DifficultyDistribution = GetDifficultyDistribution();

            OnPropertyChanged(nameof(QuestionDifficulties));
            OnPropertyChanged(nameof(AverageDifficulty));
            OnPropertyChanged(nameof(DifficultyDistribution));
        }

        private string GetDifficultyLevel(double successRate)
        {
            return successRate switch
            {
                >= 80 => "Kolay",
                >= 60 => "Orta",
                >= 40 => "Zor",
                _ => "Çok Zor"
            };
        }

        private string GetDifficultyDistribution()
        {
            if (QuestionDifficulties.Count == 0) return "Veri yok";

            var easy = QuestionDifficulties.Count(q => q.DifficultyLevel == "Kolay");
            var medium = QuestionDifficulties.Count(q => q.DifficultyLevel == "Orta");
            var hard = QuestionDifficulties.Count(q => q.DifficultyLevel == "Zor");
            var veryHard = QuestionDifficulties.Count(q => q.DifficultyLevel == "Çok Zor");

            return $"Kolay: {easy}, Orta: {medium}, Zor: {hard}, Çok Zor: {veryHard}";
        }

        private void UpdateChartData(System.Collections.Generic.List<QuestionStatisticItem> stats, System.Collections.Generic.List<StudentResult> students)
        {
            AccuracyData.Clear();
            foreach(var s in stats) {
                AccuracyData.Add(new ChartItem { Label = s.QuestionNumber.ToString(), Value = s.CorrectPercent, Tooltip = $"Soru {s.QuestionNumber}: %{s.CorrectPercent} Doğru" });
            }
            ScoreDistData.Clear();
            var bins = new int[11];
            foreach(var s in students) {
                int binIdx = (int)Math.Min(s.Score / 10, 10);
                bins[binIdx]++;
            }
            for(int i = 0; i < bins.Length; i++) {
                ScoreDistData.Add(new ChartItem { Label = (i*10).ToString(), Value = bins[i], Tooltip = $"{i*10}-{(i*10)+10} Puan: {bins[i]} Öğrenci" });
            }
        }

        private void UpdateStatisticsReport()
        {
            if (Students.Count == 0) return;

            var studentsList = Students.ToList();
            double kr20 = ReliabilityStats?.KR20 ?? 0;
            double cronbach = ReliabilityStats?.CronbachAlpha ?? 0;

            ClassStats = _statsReportService.CalculateClassStatistics(studentsList, passingScore: 50, kr20: kr20, cronbachAlpha: cronbach);
            OnPropertyChanged(nameof(ClassStats));

            ScoreDistribution.Clear();
            foreach (var dist in _statsReportService.GetScoreDistribution(studentsList, 10))
                ScoreDistribution.Add(dist);

            Percentiles = _statsReportService.CalculatePercentiles(studentsList);
            OnPropertyChanged(nameof(Percentiles));

            QuestionCorrelations.Clear();
            foreach (var corr in _statsReportService.AnalyzeQuestionCorrelations(studentsList, 50))
                QuestionCorrelations.Add(corr);

            ReportText = _statsReportService.GenerateReportSummary(studentsList, SelectedExam?.Title ?? "Mevcut Sınav");
            OnPropertyChanged(nameof(ReportText));
        }

        private async Task RunItemAnalysisAsync()
        {
            if (Students.Count == 0 || AnswerKeys.Count == 0)
            {
                ShowToastWarning("Analiz için öğrenci ve cevap anahtarı gerekli!");
                return;
            }

            IsBusy = true;
            BusyMessage = "Gelişmiş soru analizi yapılıyor...";
            AddToLog("Madde analizi başlatıldı...", LogLevel.Info);

            try
            {
                var studentsCopy = Students.ToList();
                var answerKey = AnswerKeys.FirstOrDefault()?.Answers?.ToCharArray()?.Select(c => c.ToString()).ToArray() ?? Array.Empty<string>();
                int questionCount = AnswerKeys.FirstOrDefault()?.Answers?.Length ?? 0;

                await Task.Run(() =>
                {
                    var questionStats = _itemAnalysisService.AnalyzeQuestions(studentsCopy, answerKey, questionCount);
                    var reliability = _itemAnalysisService.CalculateReliability(studentsCopy, answerKey, questionCount);
                    var anomalies = _itemAnalysisService.DetectAnomalies(studentsCopy, answerKey, questionCount);
                    var distractors = _itemAnalysisService.AnalyzeDistractors(studentsCopy, answerKey, questionCount);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        QuestionStats.Clear();
                        foreach (var stat in questionStats) QuestionStats.Add(stat);

                        ReliabilityStats = reliability;
                        OnPropertyChanged(nameof(ReliabilityStats));

                        Anomalies.Clear();
                        foreach (var anomaly in anomalies) Anomalies.Add(anomaly);

                        DistractorStats.Clear();
                        foreach (var d in distractors) DistractorStats.Add(d);
                        SelectedDistractorQuestion = DistractorStats.FirstOrDefault();

                        UpdateStatisticsReport();
                    });
                });

                AddToLog($"Madde analizi tamamlandı. KR-20: {ReliabilityStats.KR20:F3}, Cronbach's α: {ReliabilityStats.CronbachAlpha:F3}", LogLevel.Success);
                
                if (Anomalies.Count > 0)
                {
                    ShowToastWarning($"{Anomalies.Count} anomali tespit edildi!");
                }
            }
            catch (Exception ex)
            {
                AddToLog($"Madde analizi hatası: {ex.Message}", LogLevel.Error);
                ShowToastError("Analiz sırasında hata oluştu!");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RunSuccessPredictionAsync()
        {
            if (Students.Count == 0)
            {
                ShowToastWarning("Tahmin için öğrenci verisi gerekli!");
                return;
            }

            IsBusy = true;
            BusyMessage = "Başarı tahmini yapılıyor...";
            AddToLog("Başarı tahmini analizi başlatıldı...", LogLevel.Info);

            try
            {
                var studentsCopy = Students.ToList();

                await Task.Run(() =>
                {
                    var classSummary = _successPredictionService.PredictClassSuccess(studentsCopy, PassingScore);
                    var predictions = studentsCopy
                        .Select(s => _successPredictionService.PredictStudentSuccess(s, studentsCopy, PassingScore))
                        .OrderByDescending(p => p.PredictedScore)
                        .ToList();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StudentPredictions.Clear();
                        foreach (var pred in predictions) StudentPredictions.Add(pred);

                        ClassPredictionSummary = classSummary;
                        OnPropertyChanged(nameof(ClassPredictionSummary));
                    });
                });

                AddToLog($"Başarı tahmini tamamlandı. Geçme oranı: %{ClassPredictionSummary.PassRate * 100:F1}, Yüksek riskli: {ClassPredictionSummary.HighRiskCount}", LogLevel.Success);
                
                if (ClassPredictionSummary.HighRiskCount > 0)
                {
                    ShowToastWarning($"{ClassPredictionSummary.HighRiskCount} öğrenci yüksek riskli!");
                }
            }
            catch (Exception ex)
            {
                AddToLog($"Başarı tahmini hatası: {ex.Message}", LogLevel.Error);
                ShowToastError("Tahmin sırasında hata oluştu!");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateMappingPreview()
        {
            if (SelectedSourceAnswerKey == null || string.IsNullOrWhiteSpace(SelectedSourceAnswerKey.Answers))
            {
                IsMappingValid = false;
                MappingStatusHeader = "Kaynak Kitapçık Seçilmedi";
                MappingPreviewOrErrorMessage = "Lütfen önce geçerli cevap anahtarı bulunan bir kaynak kitapçık (örn: A Kitapçığı) seçin.";
                return;
            }

            int expectedCount = SelectedSourceAnswerKey.Answers.Length;
            string targetName = string.IsNullOrWhiteSpace(TargetBookletName) ? "B" : TargetBookletName.Trim().ToUpper();
            string sourceName = SelectedSourceAnswerKey.BookletName;

            if (string.IsNullOrWhiteSpace(MappingInputText))
            {
                IsMappingValid = false;
                MappingStatusHeader = "Eşleme Bekleniyor";
                MappingPreviewOrErrorMessage = $"Lütfen {expectedCount} soru için eşleme kurallarını girin veya yapıştırın.\n\nÖrnek formatlar:\n- Ok formatı: 1->5, 2->1, 3->4...\n- Satır satır: 1 5 [Enter] 2 1 [Enter] 3 4...\n- Doğrudan sıra: 5, 1, 4, 2, 3...";
                return;
            }

            var validation = _bookletMappingService.ParseAndValidate(MappingInputText, expectedCount);
            if (!validation.IsValid)
            {
                IsMappingValid = false;
                MappingStatusHeader = "⚠️ Hatalı Eşleme";
                MappingPreviewOrErrorMessage = validation.ErrorMessage ?? "Geçersiz eşleme formatı.";
                return;
            }

            try
            {
                string targetAnswers = _bookletMappingService.GenerateTargetAnswers(SelectedSourceAnswerKey.Answers, validation.ValidMapping);
                IsMappingValid = true;
                MappingStatusHeader = $"✅ Başarılı ({validation.ValidMapping.Count} Soru Eşleşti)";

                var previewLines = new System.Text.StringBuilder();
                previewLines.AppendLine($"Kaynak: {sourceName} Kitapçığı ({expectedCount} Soru) -> Hedef: {targetName} Kitapçığı");
                previewLines.AppendLine($"Üretilen Cevaplar: {targetAnswers}");
                previewLines.AppendLine();
                previewLines.AppendLine("Eşleme Önizlemesi:");

                int displayCount = Math.Min(validation.ValidMapping.Count, 15);
                for (int i = 1; i <= displayCount; i++)
                {
                    int srcQ = validation.ValidMapping[i];
                    char ans = targetAnswers[i - 1];
                    previewLines.AppendLine($"  {targetName} Soru {i,2}  <--  {sourceName} Soru {srcQ,2}   (Cevap: {ans})");
                }
                if (validation.ValidMapping.Count > displayCount)
                {
                    previewLines.AppendLine($"  ... ve {validation.ValidMapping.Count - displayCount} soru daha.");
                }

                MappingPreviewOrErrorMessage = previewLines.ToString();
            }
            catch (Exception ex)
            {
                IsMappingValid = false;
                MappingStatusHeader = "⚠️ Oluşturma Hatası";
                MappingPreviewOrErrorMessage = ex.Message;
            }
        }
    }
}
