using System;
using System.Collections.Generic;
using System.Linq;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class GradingServiceTests
    {
        private readonly GradingService _gradingService;
        private readonly OpticalParserService _parserService;
        private readonly ItemAnalysisService _itemAnalysisService;

        public GradingServiceTests()
        {
            _gradingService = new GradingService();
            _parserService = new OpticalParserService();
            _itemAnalysisService = new ItemAnalysisService();
        }

        [Fact]
        public void CalculateGrades_EmptyList_ReturnsDefaultStats()
        {
            var stats = _gradingService.CalculateGrades(new List<StudentResult>());
            Assert.Equal(0, stats.TotalStudents);
            Assert.Equal(0, stats.EvaluatedStudentsCount);
            Assert.Equal(0, stats.PassedCount);
        }

        [Fact]
        public void CalculateGrades_StudentsBelowThreshold_AssignsFFAndExcludesFromBellCurve()
        {
            // 20 threshold, 3 students: 10, 15, 80
            var students = new List<StudentResult>
            {
                new StudentResult { StudentId = "1", Score = 10.0, NetCount = 2.0 },
                new StudentResult { StudentId = "2", Score = 15.0, NetCount = 3.0 },
                new StudentResult { StudentId = "3", Score = 80.0, NetCount = 16.0 },
            };

            var settings = new GradingSettings
            {
                Mode = GradingMode.RelativeEvaluation,
                RelativeThreshold = 20.0,
                MinStudentCountForRelative = 1 // Test için minimum sayıyı düşür
            };

            var stats = _gradingService.CalculateGrades(students, settings);

            Assert.Equal(3, stats.TotalStudents);
            Assert.Equal(2, stats.ExcludedStudentsCount);
            Assert.Equal(1, stats.EvaluatedStudentsCount);

            // Baraj altındakiler
            Assert.Equal("FF", students[0].LetterGrade);
            Assert.Equal("Kaldı", students[0].Status);
            Assert.Equal("FF", students[1].LetterGrade);
            Assert.Equal("Kaldı", students[1].Status);

            // Sadece 80 alan öğrencinin ortalaması hesaplanır
            Assert.Equal(80.0, stats.MeanScore);
        }

        [Fact]
        public void CalculateGrades_RelativeEvaluation_CalculatesValidZScoreAndTScore()
        {
            // Sınıf mevcudu 20 öğrenci, puanlar 40 ile 80 arasında homojen
            var students = new List<StudentResult>();
            for (int i = 0; i < 20; i++)
            {
                double score = 40.0 + (i * 2.0); // 40, 42, ..., 78
                students.Add(new StudentResult
                {
                    StudentId = $"STU_{i}",
                    FullName = $"Öğrenci {i}",
                    Score = score,
                    NetCount = score / 5.0
                });
            }

            var settings = new GradingSettings
            {
                Mode = GradingMode.RelativeEvaluation,
                RelativeThreshold = 20.0,
                MinStudentCountForRelative = 15
            };

            var stats = _gradingService.CalculateGrades(students, settings);

            Assert.Equal(20, stats.TotalStudents);
            Assert.True(stats.MeanScore > 55.0 && stats.MeanScore < 65.0);
            Assert.True(stats.StandardDeviation > 0);

            // En yüksek puanlı öğrenci
            var topStudent = students.OrderByDescending(s => s.Score).First();
            Assert.True(topStudent.TScore > 50.0);
            Assert.True(topStudent.ZScore > 0);
            Assert.Contains(topStudent.LetterGrade, new[] { "AA", "BA", "BB" });
            Assert.Equal("Geçti", topStudent.Status);

            // En düşük puanlı öğrenci
            var bottomStudent = students.OrderBy(s => s.Score).First();
            Assert.True(bottomStudent.TScore < 50.0);
            Assert.True(bottomStudent.ZScore < 0);
        }

        [Fact]
        public void CalculateGrades_AbsoluteGradingMode_UsesCutoffThresholds()
        {
            var students = new List<StudentResult>
            {
                new StudentResult { StudentId = "1", Score = 95.0 }, // AA
                new StudentResult { StudentId = "2", Score = 82.0 }, // BB
                new StudentResult { StudentId = "3", Score = 68.0 }, // DC
                new StudentResult { StudentId = "4", Score = 45.0 }  // FF
            };

            var settings = new GradingSettings
            {
                Mode = GradingMode.AbsoluteGrading
            };

            var stats = _gradingService.CalculateGrades(students, settings);

            Assert.Equal("AA", students[0].LetterGrade);
            Assert.Equal("Geçti", students[0].Status);

            Assert.Equal("BB", students[1].LetterGrade);
            Assert.Equal("Geçti", students[1].Status);

            Assert.Equal("DC", students[2].LetterGrade);
            Assert.Equal("Şartlı Geçti", students[2].Status);

            Assert.Equal("FF", students[3].LetterGrade);
            Assert.Equal("Kaldı", students[3].Status);
        }

        [Fact]
        public void EvaluateStudents_WeightedQuestionPoints_CalculatesCorrectScore()
        {
            // 2 soru: Soru 1 = 20 puan, Soru 2 = 80 puan
            var keys = new List<AnswerKeyModel>
            {
                new AnswerKeyModel { BookletName = "A", Answers = "AB" }
            };

            var settings = new List<QuestionSetting>
            {
                new QuestionSetting { BookletName = "A", QuestionNumber = 1, PointValue = 20.0 },
                new QuestionSetting { BookletName = "A", QuestionNumber = 2, PointValue = 80.0 }
            };

            // Öğrenci 1: Sadece Soru 1 doğru ("AC") -> 20 puan - (80 * 0.25) = 0 puan
            // Öğrenci 2: Sadece Soru 2 doğru ("CB") -> 80 puan - (20 * 0.25) = 75 puan
            var students = new List<StudentResult>
            {
                new StudentResult { StudentId = "1", BookletType = "A", Answers = new List<string> { "A", "C" } },
                new StudentResult { StudentId = "2", BookletType = "A", Answers = new List<string> { "C", "B" } }
            };

            _parserService.EvaluateStudents(students, keys, settings, wrongDeductionFactor: 0.25);

            Assert.Equal(0.0, students[0].Score);
            Assert.Equal(75.0, students[1].Score);
            Assert.Equal(1, students[1].Rank);
            Assert.Equal(2, students[0].Rank);
        }

        [Fact]
        public void EvaluateStudents_CancellationMode_DistributeToRemaining_ScalesUpCorrectly()
        {
            // 2 soru, her biri normalde 50 puan (1 net = 50 katsayı).
            // Soru 2 iptal edildi ve kalan sorulara dağıtıldı (DistributeToRemaining).
            // Kalan 1 soru 100 tam puana orantılanmalı.
            var keys = new List<AnswerKeyModel>
            {
                new AnswerKeyModel { BookletName = "A", Answers = "AB" }
            };

            var settings = new List<QuestionSetting>
            {
                new QuestionSetting 
                { 
                    BookletName = "A", 
                    QuestionNumber = 2, 
                    IsCancelled = true, 
                    CancelMode = CancellationMode.DistributeToRemaining 
                }
            };

            var students = new List<StudentResult>
            {
                // Soru 1 doğru, Soru 2 yanlış/fark etmez (çünkü iptal)
                new StudentResult { StudentId = "1", BookletType = "A", Answers = new List<string> { "A", "C" } }
            };

            _parserService.EvaluateStudents(students, keys, settings, wrongDeductionFactor: 0.25, netCoefficient: 50.0);

            // 1 doğru / (2 soru - 1 iptal) * 2 soru = 2.0 net -> 2.0 * 50 = 100 puan!
            Assert.Equal(100.0, students[0].Score);
        }

        [Fact]
        public void EvaluateStudents_CancellationMode_MarkAsEmpty_DoesNotGivePoints()
        {
            var keys = new List<AnswerKeyModel>
            {
                new AnswerKeyModel { BookletName = "A", Answers = "A" }
            };

            var settings = new List<QuestionSetting>
            {
                new QuestionSetting 
                { 
                    BookletName = "A", 
                    QuestionNumber = 1, 
                    IsCancelled = true, 
                    CancelMode = CancellationMode.MarkAsEmpty 
                }
            };

            var students = new List<StudentResult>
            {
                new StudentResult { StudentId = "1", BookletType = "A", Answers = new List<string> { "A" } }
            };

            _parserService.EvaluateStudents(students, keys, settings, wrongDeductionFactor: 0.25, netCoefficient: 100.0);

            Assert.Equal(0, students[0].CorrectCount);
            Assert.Equal(1, students[0].EmptyCount);
            Assert.Equal(0.0, students[0].Score);
        }

        [Fact]
        public void ItemAnalysisService_WithMultiBookletEvaluatedStudents_UsesAccurateResults()
        {
            // 2 öğrenci: Biri A kitapçığı ("A"), biri B kitapçığı ("B"). İkisi de sorusunu doğru yaptı.
            var keys = new List<AnswerKeyModel>
            {
                new AnswerKeyModel { BookletName = "A", Answers = "A" },
                new AnswerKeyModel { BookletName = "B", Answers = "B" }
            };

            var students = new List<StudentResult>
            {
                new StudentResult { StudentId = "1", BookletType = "A", Answers = new List<string> { "A" } },
                new StudentResult { StudentId = "2", BookletType = "B", Answers = new List<string> { "B" } }
            };

            // Önce EvaluateStudents çalıştırılır
            _parserService.EvaluateStudents(students, keys, new List<QuestionSetting>());

            Assert.True(students[0].QuestionResults[0]);
            Assert.True(students[1].QuestionResults[0]);

            // Madde analizi çalıştırıldığında iki öğrenci de doğru sayılmalı (Güçlük p = 1.0)
            var itemStats = _itemAnalysisService.AnalyzeQuestions(students, new[] { "A" }, 1);

            Assert.Single(itemStats);
            Assert.Equal(1.0, itemStats[0].DifficultyIndex);
            Assert.Equal(2, itemStats[0].CorrectCount);
            Assert.Equal(0, itemStats[0].WrongCount);
        }
    }
}
