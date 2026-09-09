using System.Collections.Generic;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class StatisticsReportServiceTests
    {
        private readonly StatisticsReportService _service = new();

        [Fact]
        public void CalculateClassStatistics_EmptyList_ReturnsDefaultStatistics()
        {
            var stats = _service.CalculateClassStatistics(new List<StudentResult>());
            Assert.Equal(0, stats.TotalStudents);
            Assert.Equal(0, stats.AverageScore);
            Assert.Equal(0, stats.Range);
        }

        [Fact]
        public void CalculateClassStatistics_KnownScores_ComputesAccurateMetrics()
        {
            // Scores: 40, 50, 60, 70, 80
            var students = new List<StudentResult>
            {
                new() { StudentId = "1", Score = 40 },
                new() { StudentId = "2", Score = 50 },
                new() { StudentId = "3", Score = 60 },
                new() { StudentId = "4", Score = 70 },
                new() { StudentId = "5", Score = 80 },
            };

            var stats = _service.CalculateClassStatistics(students, kr20: 0.85, cronbachAlpha: 0.84);

            Assert.Equal(5, stats.TotalStudents);
            Assert.Equal(60, stats.AverageScore);
            Assert.Equal(60, stats.MedianScore);
            Assert.Equal(40, stats.MinScore);
            Assert.Equal(80, stats.MaxScore);
            Assert.Equal(40, stats.Range);
            Assert.True(stats.StandardDeviation > 0);
            Assert.True(stats.Variance > 0);
            Assert.True(stats.VariationCoefficient > 0);
            Assert.Equal(0.85, stats.ReliabilityKR20);
            Assert.Equal(0.84, stats.CronbachAlpha);
            Assert.True(stats.StandardErrorOfMeasurement > 0);
            Assert.False(string.IsNullOrEmpty(stats.HomogeneityInterpretation));
            Assert.False(string.IsNullOrEmpty(stats.PedagogicalEvaluation));
        }

        [Fact]
        public void CalculateClassStatistics_SymmetricDistribution_SkewnessNearZero()
        {
            // Symmetric distribution: 20, 40, 50, 60, 80
            var students = new List<StudentResult>
            {
                new() { StudentId = "1", Score = 20 },
                new() { StudentId = "2", Score = 40 },
                new() { StudentId = "3", Score = 50 },
                new() { StudentId = "4", Score = 60 },
                new() { StudentId = "5", Score = 80 },
            };

            var stats = _service.CalculateClassStatistics(students);

            Assert.True(System.Math.Abs(stats.Skewness) < 0.1, $"Expected skewness near 0, but was {stats.Skewness}");
            Assert.Contains("Simetrik", stats.SkewnessInterpretation);
        }
    }
}
