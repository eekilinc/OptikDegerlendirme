using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OptikFormApp.Models;

namespace OptikFormApp.ViewModels
{
    public partial class MainViewModel
    {
        private async Task InitializeAsync()
        {
            await _dbService.InitializeDatabaseAsync();
            await LoadCoursesAsync();
        }

        private async Task LoadCoursesAsync()
        {
            var list = await _dbService.GetCoursesAsync();
            System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                Courses.Clear();
                foreach (var c in list) Courses.Add(c);
            });
        }

        private async Task LoadExamsForCourseAsync(int courseId)
        {
            var list = await _dbService.GetExamsForCourseAsync(courseId);
            System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                CourseExams.Clear();
                foreach (var e in list) CourseExams.Add(e);
                if (SelectedExam == null) Students.Clear();
            });
        }

        private async Task LoadExamDataAsync(ExamEntry exam)
        {
            try
            {
                IsBusy = true;
                BusyMessage = $"{exam.Title} yükleniyor...";
                AddToLog($"{exam.Title} sınav verileri yükleniyor...");
                
                var results = await _dbService.GetResultsForExamAsync(exam.Id);
                
                System.Windows.Application.Current?.Dispatcher.Invoke(() => {
                    Students.Clear();
                    foreach (var r in results) Students.Add(r);

                    if (!string.IsNullOrEmpty(exam.ConfigJson))
                    {
                        var config = JsonSerializer.Deserialize<ExamConfigData>(exam.ConfigJson);
                        if (config != null)
                        {
                            AnswerKeys.Clear();
                            foreach (var k in config.AnswerKeys) AnswerKeys.Add(k);
                            QuestionSettings.Clear();
                            foreach (var s in config.QuestionSettings) QuestionSettings.Add(s);
                            LearningOutcomes.Clear();
                            foreach (var o in config.LearningOutcomes) LearningOutcomes.Add(o);
                            
                            SchoolName = config.SchoolName ?? "Okul Adı";
                            NetCoefficient = config.NetCoefficient != 0 ? config.NetCoefficient : 1.0;
                            BaseScore = config.BaseScore;
                            WrongDeductionFactor = (config.WrongDeductionFactor != 0 || exam.ConfigJson.Contains("WrongDeductionFactor")) ? config.WrongDeductionFactor : 0.25;
                        }
                    }
                });

                await EvaluateAsync();
                HasUnsavedData = false;
                AddToLog($"{exam.Title} başarıyla yüklendi.", LogLevel.Success);
            }
            catch (Exception ex)
            {
                AddToLog($"Sınav yükleme hatası: {ex.Message}", LogLevel.Error);
                ShowAlert("Hata", "Sınav verileri yüklenirken bir sorun oluştu.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SaveCurrentExamAsync(string title, Course courseToSave, ExamEntry? existingExam = null)
        {
            AddToLog($"DEBUG: SaveCurrentExamAsync started - Students.Count: {Students.Count}, CourseToSave: {courseToSave?.Name}", LogLevel.Info);
            
            if (courseToSave == null)
            {
                ShowAlert("Hata", "Lütfen önce bir ders seçin.");
                return;
            }
            try
            {
                var config = new ExamConfigData
                {
                    AnswerKeys = new List<AnswerKeyModel>(AnswerKeys),
                    QuestionSettings = new List<QuestionSetting>(QuestionSettings),
                    LearningOutcomes = new List<LearningOutcome>(LearningOutcomes),
                    SchoolName = SchoolName,
                    NetCoefficient = NetCoefficient,
                    BaseScore = BaseScore,
                    WrongDeductionFactor = WrongDeductionFactor
                };

                if (existingExam != null && existingExam.Id > 0)
                {
                    // Update existing exam
                    existingExam.Title = title;
                    existingExam.ConfigJson = JsonSerializer.Serialize(config);
                    await _dbService.UpdateExamAsync(existingExam, new List<StudentResult>(Students));
                    AddToLog($"'{title}' sınavı güncellendi.", LogLevel.Success);
                }
                else
                {
                    // Insert new exam
                    var exam = new ExamEntry
                    {
                        CourseId = courseToSave.Id,
                        Title = title,
                        Date = DateTime.Now,
                        ConfigJson = JsonSerializer.Serialize(config)
                    };
                    await _dbService.SaveExamAsync(exam, new List<StudentResult>(Students));
                    AddToLog($"'{title}' sınavı veritabanına kaydedildi.", LogLevel.Success);
                }
                await LoadExamsForCourseAsync(courseToSave.Id);
                HasUnsavedData = false;
            }
            catch (Exception ex)
            {
                AddToLog($"DEBUG: Kaydetme hatası: {ex.Message}\nStack: {ex.StackTrace}", LogLevel.Error);
                AddToLog($"Kaydetme hatası: {ex.Message}", LogLevel.Error);
                ShowAlert("Hata", $"Kayıt sırasında hata oluştu: {ex.Message}");
            }
        }

        private async Task AutoSaveSelectedExamAsync()
        {
            if (SelectedExam == null || SelectedExam.Id <= 0 || SelectedCourse == null) return;
            try
            {
                var config = new ExamConfigData
                {
                    AnswerKeys = new List<AnswerKeyModel>(AnswerKeys),
                    QuestionSettings = new List<QuestionSetting>(QuestionSettings),
                    LearningOutcomes = new List<LearningOutcome>(LearningOutcomes),
                    SchoolName = SchoolName,
                    NetCoefficient = NetCoefficient,
                    BaseScore = BaseScore,
                    WrongDeductionFactor = WrongDeductionFactor
                };
                SelectedExam.ConfigJson = JsonSerializer.Serialize(config);
                await _dbService.UpdateExamAsync(SelectedExam, new List<StudentResult>(Students));
                AddToLog($"'{SelectedExam.Title}' değişiklikler otomatik kaydedildi.", LogLevel.Success);
            }
            catch (Exception ex)
            {
                AddToLog($"Otomatik kaydetme hatası: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task ExecuteDeleteCourseAsync(Course c, List<ExamEntry> exams)
        {
            if (SelectedExam != null && exams.Any(e => e.Id == SelectedExam.Id))
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
                StatusMessage = "Ders ve sınavlar silindi. Yeni veri yükleyin.";
                AnswerKeys.Add(new AnswerKeyModel { BookletName = "A", Answers = "" });
            }
            
            await _dbService.DeleteCourseAsync(c.Id);
            await LoadCoursesAsync();
            
            if (SelectedCourse?.Id == c.Id) {
                SelectedCourse = null;
                CourseExams.Clear();
            }
            
            AddToLog($"'{c.Name}' dersi ve {exams.Count} sınavı silindi.", LogLevel.Warning);
        }
    }
}
