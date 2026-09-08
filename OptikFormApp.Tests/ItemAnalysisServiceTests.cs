using System.Collections.Generic;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class ItemAnalysisServiceTests
    {
        private readonly ItemAnalysisService _itemService;

        public ItemAnalysisServiceTests()
        {
            _itemService = new ItemAnalysisService();
        }

        [Fact]
        public void AnalyzeQuestions_EmptyStudents_ReturnsEmptyList()
        {
            var result = _itemService.AnalyzeQuestions(new List<StudentResult>(), new[] { "A", "B" }, 2);
            Assert.Empty(result);
        }

        [Fact]
        public void AnalyzeQuestions_AllCorrect_DifficultyIndexIsOne()
        {
            var students = new List<StudentResult>
            {
                new() { StudentId = "1", Answers = new List<string> { "A", "B" }, Score = 100 },
                new() { StudentId = "2", Answers = new List<string> { "A", "B" }, Score = 100 }
            };
            var answerKey = new[] { "A", "B" };

            var results = _itemService.AnalyzeQuestions(students, answerKey, 2);

            Assert.Equal(2, results.Count);
            Assert.Equal(1.0, results[0].DifficultyIndex);
            Assert.Equal(2, results[0].CorrectCount);
            Assert.Equal(0, results[0].WrongCount);
            Assert.Equal(0, results[0].EmptyCount);
        }

        [Fact]
        public void AnalyzeQuestions_AllWrong_DifficultyIndexIsZero()
        {
            var students = new List<StudentResult>
            {
                new() { StudentId = "1", Answers = new List<string> { "C", "D" }, Score = 0 },
                new() { StudentId = "2", Answers = new List<string> { "E", "C" }, Score = 0 }
            };
            var answerKey = new[] { "A", "B" };

            var results = _itemService.AnalyzeQuestions(students, answerKey, 2);

            Assert.Equal(2, results.Count);
            Assert.Equal(0.0, results[0].DifficultyIndex);
            Assert.Equal(0, results[0].CorrectCount);
            Assert.Equal(2, results[0].WrongCount);
        }

        [Fact]
        public void CalculateReliability_CalculatesValidStatistics()
        {
            var students = new List<StudentResult>();
            for (int i = 0; i < 20; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"S{i}",
                    Score = (i % 5) * 20, // Scores: 0, 20, 40, 60, 80
                    Answers = new List<string> { i % 2 == 0 ? "A" : "B", i % 3 == 0 ? "B" : "C" }
                });
            }
            var answerKey = new[] { "A", "B" };

            var stats = _itemService.CalculateReliability(students, answerKey, 2);

            Assert.Equal(20, stats.TotalStudents);
            Assert.Equal(2, stats.TotalQuestions);
            Assert.True(stats.MeanScore > 0);
            Assert.True(stats.Variance > 0);
            Assert.True(stats.StandardDeviation > 0);
        }
    }
}
