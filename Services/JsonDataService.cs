using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OptikFormApp.Models;

namespace OptikFormApp.Services
{
    public class JsonDataService
    {
        public class ExamExportData
        {
            public string ExportVersion { get; set; } = "1.0";
            public DateTime ExportDate { get; set; } = DateTime.Now;
            public string Application { get; set; } = "OptikFormApp";

            // Exam Information
            public string ExamTitle { get; set; } = "";
            public string CourseCode { get; set; } = "";
            public string CourseName { get; set; } = "";
            public string SchoolName { get; set; } = "";
            public DateTime ExamDate { get; set; } = DateTime.Now;

            // Configuration
            public double NetCoefficient { get; set; } = 1.0;
            public double BaseScore { get; set; } = 0.0;
            public double WrongDeductionFactor { get; set; } = 0.25;

            // Data
            public List<AnswerKeyModel> AnswerKeys { get; set; } = new();
            public List<QuestionSetting> QuestionSettings { get; set; } = new();
            public List<LearningOutcome> LearningOutcomes { get; set; } = new();
            public List<StudentResult> Students { get; set; } = new();

            // Summary Statistics
            public int TotalStudents => Students?.Count ?? 0;
            public double AverageScore { get; set; }
            public double MaxScore { get; set; }
            public double MinScore { get; set; }
        }

        public async Task ExportToJsonAsync(
            string filePath,
            string examTitle,
            string courseName,
            string schoolName,
            double netCoefficient,
            double baseScore,
            double wrongDeductionFactor,
            IEnumerable<AnswerKeyModel> answerKeys,
            IEnumerable<QuestionSetting> questionSettings,
            IEnumerable<LearningOutcome> learningOutcomes,
            IEnumerable<StudentResult> students)
        {
            var studentList = students.ToList();
            var exportData = new ExamExportData
            {
                ExamTitle = examTitle,
                CourseName = courseName,
                SchoolName = schoolName,
                ExamDate = DateTime.Now,
                NetCoefficient = netCoefficient,
                BaseScore = baseScore,
                WrongDeductionFactor = wrongDeductionFactor,
                AnswerKeys = answerKeys.ToList(),
                QuestionSettings = questionSettings.ToList(),
                LearningOutcomes = learningOutcomes.ToList(),
                Students = studentList,
                AverageScore = studentList.Count > 0 ? studentList.Average(s => s.Score) : 0,
                MaxScore = studentList.Count > 0 ? studentList.Max(s => s.Score) : 0,
                MinScore = studentList.Count > 0 ? studentList.Min(s => s.Score) : 0
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(exportData, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        private const long MaxJsonImportBytes = 50 * 1024 * 1024; // 50 MB

        public async Task<ExamExportData?> ImportFromJsonAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var fi = new FileInfo(filePath);
                if (fi.Length > MaxJsonImportBytes)
                    return null;

                var json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<ExamExportData>(json, options);
            }
            catch
            {
                return null;
            }
        }

        public string GenerateSummary(ExamExportData data)
        {
            return $"""
=== JSON İTHALAT ÖZETİ ===
Sınav: {data.ExamTitle}
Ders: {data.CourseName}
Okul: {data.SchoolName}
Tarih: {data.ExamDate:dd.MM.yyyy HH:mm}

Yapılandırma:
• Net Katsayısı: {data.NetCoefficient}
• Taban Puan: {data.BaseScore}
• Yanlış Sayısı: {data.WrongDeductionFactor}

Veri:
• {data.AnswerKeys.Count} cevap anahtarı
• {data.QuestionSettings.Count} soru ayarı
• {data.LearningOutcomes.Count} kazanım
• {data.Students.Count} öğrenci

İstatistikler:
• Ortalama: {data.AverageScore:F2}
• En Yüksek: {data.MaxScore:F2}
• En Düşük: {data.MinScore:F2}
""";
        }

        public async Task<bool> ValidateJsonFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                if (new FileInfo(filePath).Length > MaxJsonImportBytes)
                    return false;
                var json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<ExamExportData>(json, options);
                return data != null && data.Students != null && data.AnswerKeys != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tüm veritabanı verilerini JSON formatında dışa aktarır (backup için)
        /// </summary>
        public async Task<string> ExportAllDataAsync(DatabaseService? existingDbService = null)
        {
            var exportContainer = new FullExportData
            {
                ExportVersion = "2.1",
                ExportDate = DateTime.Now,
                Application = "OptikFormApp",
                Exams = new List<ExamExportData>()
            };

            bool ownsDb = existingDbService == null;
            var dbService = existingDbService ?? new DatabaseService();

            try
            {
                await dbService.InitializeDatabaseAsync();

                var courses = await dbService.GetCoursesAsync();
                foreach (var course in courses)
                {
                    var exams = await dbService.GetExamsForCourseAsync(course.Id);
                    foreach (var exam in exams)
                    {
                        var results = await dbService.GetResultsForExamAsync(exam.Id);
                        ExamConfigData? cfg = null;
                        if (!string.IsNullOrWhiteSpace(exam.ConfigJson))
                        {
                            try { cfg = JsonSerializer.Deserialize<ExamConfigData>(exam.ConfigJson); } catch { }
                        }

                        var examData = new ExamExportData
                        {
                            ExamTitle = exam.Title,
                            CourseCode = course.Code ?? "",
                            CourseName = course.Name,
                            SchoolName = cfg?.SchoolName ?? "",
                            ExamDate = exam.Date,
                            NetCoefficient = cfg?.NetCoefficient != 0 ? (cfg?.NetCoefficient ?? 1.0) : 1.0,
                            BaseScore = cfg?.BaseScore ?? 0.0,
                            WrongDeductionFactor = cfg != null ? cfg.WrongDeductionFactor : 0.25,
                            AnswerKeys = cfg?.AnswerKeys ?? new List<AnswerKeyModel>(),
                            QuestionSettings = cfg?.QuestionSettings ?? new List<QuestionSetting>(),
                            LearningOutcomes = cfg?.LearningOutcomes ?? new List<LearningOutcome>(),
                            Students = results,
                            AverageScore = results.Count > 0 ? results.Average(s => s.Score) : 0,
                            MaxScore = results.Count > 0 ? results.Max(s => s.Score) : 0,
                            MinScore = results.Count > 0 ? results.Min(s => s.Score) : 0
                        };

                        exportContainer.Exams.Add(examData);
                    }
                }

                exportContainer.TotalCourses = courses.Count;
                exportContainer.TotalExams = exportContainer.Exams.Count;
                exportContainer.TotalStudents = exportContainer.Exams.Sum(e => e.TotalStudents);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Serialize(exportContainer, options);
            }
            finally
            {
                if (ownsDb)
                    dbService.Dispose();
            }
        }

        /// <summary>
        /// Tam yedek içindeki data_export.json dosyasından tüm dersleri, sınavları ve öğrenci sonuçlarını veritabanına aktarır.
        /// Eski sürümlerde WAL nedeniyle optik.db'nin boş kaydedildiği .zip yedeklerini kayıpsız kurtarır.
        /// </summary>
        public async Task<int> ImportAllDataToDatabaseAsync(string jsonPath, DatabaseService dbService)
        {
            if (!File.Exists(jsonPath))
                return 0;

            var json = await File.ReadAllTextAsync(jsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var fullData = JsonSerializer.Deserialize<FullExportData>(json, options);
            if (fullData?.Exams == null || fullData.Exams.Count == 0)
                return 0;

            await dbService.InitializeDatabaseAsync();
            var existingCourses = await dbService.GetCoursesAsync();
            var courseMap = new Dictionary<string, Course>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in existingCourses)
            {
                courseMap[c.Name.Trim()] = c;
            }

            int importedExams = 0;

            foreach (var examData in fullData.Exams)
            {
                string courseName = string.IsNullOrWhiteSpace(examData.CourseName) ? "Genel Ders" : examData.CourseName.Trim();
                string courseCode = string.IsNullOrWhiteSpace(examData.CourseCode)
                    ? $"{examData.ExamDate.Year}-{examData.ExamDate.Year + 1}"
                    : examData.CourseCode.Trim();

                if (!courseMap.TryGetValue(courseName, out var course))
                {
                    course = new Course { Code = courseCode, Name = courseName };
                    course.Id = await dbService.SaveCourseAsync(course);
                    courseMap[courseName] = course;
                }

                var students = examData.Students ?? new List<StudentResult>();
                var answerKeys = examData.AnswerKeys ?? new List<AnswerKeyModel>();
                double wrongFactor = examData.WrongDeductionFactor;
                double netCoeff = examData.NetCoefficient;
                double baseScore = examData.BaseScore;

                // Eski yedeklerde answerKeys boş bırakıldıysa öğrenci doğru cevap matrisinden (QuestionResults) anahtarı ve katsayıları geri kazan
                if (answerKeys.Count == 0 && students.Count > 0)
                {
                    answerKeys = ReconstructAnswerKeysFromStudents(students);
                    InferScoringParametersFromStudents(students, baseScore, ref wrongFactor, ref netCoeff);
                }

                var config = new ExamConfigData
                {
                    AnswerKeys = answerKeys,
                    QuestionSettings = examData.QuestionSettings ?? new List<QuestionSetting>(),
                    LearningOutcomes = examData.LearningOutcomes ?? new List<LearningOutcome>(),
                    SchoolName = string.IsNullOrWhiteSpace(examData.SchoolName) ? "Ağlasun Meslek Yüksekokulu" : examData.SchoolName,
                    NetCoefficient = netCoeff != 0 ? netCoeff : 1.0,
                    BaseScore = baseScore,
                    WrongDeductionFactor = wrongFactor
                };

                var entry = new ExamEntry
                {
                    CourseId = course.Id,
                    Title = string.IsNullOrWhiteSpace(examData.ExamTitle) ? "Sınav" : examData.ExamTitle,
                    Date = examData.ExamDate == default ? DateTime.Now : examData.ExamDate,
                    ConfigJson = JsonSerializer.Serialize(config)
                };

                await dbService.SaveExamAsync(entry, students);
                importedExams++;
            }

            await dbService.CheckpointAsync();
            return importedExams;
        }

        private static List<AnswerKeyModel> ReconstructAnswerKeysFromStudents(List<StudentResult> students)
        {
            var keys = new List<AnswerKeyModel>();
            var bookletGroups = students
                .GroupBy(s => string.IsNullOrWhiteSpace(s.BookletType) ? "A" : s.BookletType.Trim().ToUpperInvariant())
                .OrderBy(g => g.Key);

            foreach (var group in bookletGroups)
            {
                int maxQuestions = group.Max(s => Math.Max(s.RawAnswers?.Length ?? 0, s.QuestionResults?.Count ?? 0));
                if (maxQuestions <= 0) continue;

                char[] keyChars = new char[maxQuestions];
                for (int q = 0; q < maxQuestions; q++)
                {
                    char found = ' ';
                    foreach (var st in group)
                    {
                        if (st.QuestionResults != null && q < st.QuestionResults.Count && st.QuestionResults[q] &&
                            !string.IsNullOrEmpty(st.RawAnswers) && q < st.RawAnswers.Length)
                        {
                            char candidate = char.ToUpperInvariant(st.RawAnswers[q]);
                            if (candidate != ' ' && candidate != '_')
                            {
                                found = candidate;
                                break;
                            }
                        }
                    }
                    keyChars[q] = found == ' ' ? 'A' : found;
                }

                keys.Add(new AnswerKeyModel
                {
                    BookletName = group.Key,
                    Answers = new string(keyChars)
                });
            }

            return keys;
        }

        private static void InferScoringParametersFromStudents(List<StudentResult> students, double baseScore, ref double wrongFactor, ref double netCoeff)
        {
            var withWrong = students.FirstOrDefault(s => s.IncorrectCount > 0 && s.CorrectCount > 0);
            if (withWrong != null)
            {
                double inferredWrong = Math.Round((withWrong.CorrectCount - withWrong.NetCount) / withWrong.IncorrectCount, 4);
                if (inferredWrong >= 0 && inferredWrong <= 1.0)
                    wrongFactor = inferredWrong;
            }

            var withPositiveNet = students.FirstOrDefault(s => s.NetCount > 0 && s.Score > baseScore);
            if (withPositiveNet != null)
            {
                double inferredCoeff = Math.Round((withPositiveNet.Score - baseScore) / withPositiveNet.NetCount, 4);
                if (inferredCoeff > 0)
                    netCoeff = inferredCoeff;
            }
            else
            {
                var withScore = students.FirstOrDefault(s => s.CorrectCount > 0 && s.Score > baseScore);
                if (withScore != null)
                {
                    double inferredCoeff = Math.Round((withScore.Score - baseScore) / withScore.CorrectCount, 4);
                    if (inferredCoeff > 0)
                    {
                        wrongFactor = 0.0;
                        netCoeff = inferredCoeff;
                    }
                }
            }
        }

        public class FullExportData
        {
            public string ExportVersion { get; set; } = "2.1";
            public DateTime ExportDate { get; set; }
            public string Application { get; set; } = "OptikFormApp";
            public int TotalCourses { get; set; }
            public int TotalExams { get; set; }
            public int TotalStudents { get; set; }
            public List<ExamExportData> Exams { get; set; } = new();
        }
    }
}
