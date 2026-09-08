using System.Collections.Generic;
using System.Linq;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class SuccessPredictionServiceTests
    {
        private readonly SuccessPredictionService _predictionService;

        public SuccessPredictionServiceTests()
        {
            _predictionService = new SuccessPredictionService();
        }

        private static List<StudentResult> CreateStudentCohort(int count = 100)
        {
            var list = new List<StudentResult>();
            for (int i = 0; i < count; i++)
            {
                double score = (i + 1); // 1 to 100
                int correct = (int)(score / 5);
                int wrong = 20 - correct;
                list.Add(new StudentResult
                {
                    StudentId = $"STU_{i:D3}",
                    FullName = $"Öğrenci {i + 1}",
                    Score = score,
                    CorrectCount = correct,
                    IncorrectCount = wrong,
                    EmptyCount = 0,
                    NetCount = correct - (wrong * 0.25),
                    Rank = count - i,
                    Answers = Enumerable.Repeat("A", 20).ToList()
                });
            }
            return list;
        }

        [Fact]
        public void PredictStudentSuccess_SingleStudent_ReturnsDefaultPrediction()
        {
            var student = new StudentResult { StudentId = "101", FullName = "Ali Veli", Score = 80 };
            var result = _predictionService.PredictStudentSuccess(student, new List<StudentResult> { student });

            Assert.Equal("101", result.StudentId);
            Assert.Equal("Ali Veli", result.FullName);
            Assert.Equal(80, result.CurrentScore);
            Assert.Equal(80, result.PredictedScore);
        }

        [Fact]
        public void PredictStudentSuccess_HighScoringStudent_HasLowRisk()
        {
            var cohort = CreateStudentCohort(100);
            var topStudent = cohort.Last(); // Score: 100

            var result = _predictionService.PredictStudentSuccess(topStudent, cohort, passingScore: 50);

            Assert.Equal(SuccessPredictionService.RiskLevel.Low, result.Risk);
            Assert.True(result.GradeProbability >= 0.5);
            Assert.True(result.PredictedScore >= 80);
        }

        [Fact]
        public void PredictStudentSuccess_LowScoringStudent_HasHighRisk()
        {
            var cohort = CreateStudentCohort(100);
            var bottomStudent = cohort.First(); // Score: 1

            var result = _predictionService.PredictStudentSuccess(bottomStudent, cohort, passingScore: 50);

            Assert.Equal(SuccessPredictionService.RiskLevel.High, result.Risk);
            Assert.True(result.GradeProbability < 0.5);
            Assert.True(result.PredictedScore < 40);
        }

        [Fact]
        public void PredictClassSuccess_CalculatesSummaryCorrectly()
        {
            var cohort = CreateStudentCohort(100);

            var summary = _predictionService.PredictClassSuccess(cohort, passingScore: 50);

            Assert.Equal(100, summary.TotalStudents);
            Assert.True(summary.LowRiskCount > 0);
            Assert.True(summary.HighRiskCount > 0);
            Assert.True(summary.AveragePredictedScore > 0);
        }
    }
}
