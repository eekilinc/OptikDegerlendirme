using System.Collections.Generic;
using System.Linq;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class BookletMappingTests
    {
        private readonly BookletMappingService _mappingService;
        private readonly ItemAnalysisService _itemService;

        public BookletMappingTests()
        {
            _mappingService = new BookletMappingService();
            _itemService = new ItemAnalysisService();
        }

        [Fact]
        public void ParseAndValidate_ArrowFormat_ReturnsValidMapping()
        {
            string input = "1->5, 2->1, 3->4, 4->2, 5->3";
            var result = _mappingService.ParseAndValidate(input, 5);

            Assert.True(result.IsValid);
            Assert.Equal(5, result.ValidMapping.Count);
            Assert.Equal(5, result.ValidMapping[1]);
            Assert.Equal(1, result.ValidMapping[2]);
            Assert.Equal(4, result.ValidMapping[3]);
            Assert.Equal(2, result.ValidMapping[4]);
            Assert.Equal(3, result.ValidMapping[5]);
        }

        [Fact]
        public void ParseAndValidate_TwoColumnFormat_ReturnsValidMapping()
        {
            string input = "1 5\r\n2 1\r\n3 4\r\n4 2\r\n5 3";
            var result = _mappingService.ParseAndValidate(input, 5);

            Assert.True(result.IsValid);
            Assert.Equal(5, result.ValidMapping.Count);
            Assert.Equal(4, result.ValidMapping[3]);
        }

        [Fact]
        public void ParseAndValidate_SingleListFormat_ReturnsValidMapping()
        {
            string input = "5, 1, 4, 2, 3";
            var result = _mappingService.ParseAndValidate(input, 5);

            Assert.True(result.IsValid);
            Assert.Equal(5, result.ValidMapping.Count);
            Assert.Equal(5, result.ValidMapping[1]);
            Assert.Equal(3, result.ValidMapping[5]);
        }

        [Fact]
        public void ParseAndValidate_QuestionCountMismatch_ReturnsInvalid()
        {
            string input = "1->5, 2->1";
            var result = _mappingService.ParseAndValidate(input, 5);

            Assert.False(result.IsValid);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ParseAndValidate_DuplicateSourceQuestion_ReturnsInvalid()
        {
            string input = "1->5, 2->5, 3->4, 4->2, 5->3";
            var result = _mappingService.ParseAndValidate(input, 5);

            Assert.False(result.IsValid);
            Assert.Contains("birden fazla", result.ErrorMessage);
        }

        [Fact]
        public void ParseAndValidate_OutOfRangeQuestion_ReturnsInvalid()
        {
            string input = "1->6, 2->1, 3->4, 4->2, 5->3";
            var result = _mappingService.ParseAndValidate(input, 5);

            Assert.False(result.IsValid);
            Assert.Contains("geçersiz", result.ErrorMessage);
        }

        [Fact]
        public void GenerateTargetAnswers_ValidMapping_PermutesAnswersCorrectly()
        {
            string sourceAnswers = "ABCDE";
            var mapping = new Dictionary<int, int>
            {
                { 1, 5 }, // 5 -> E
                { 2, 1 }, // 1 -> A
                { 3, 4 }, // 4 -> D
                { 4, 2 }, // 2 -> B
                { 5, 3 }  // 3 -> C
            };

            string target = _mappingService.GenerateTargetAnswers(sourceAnswers, mapping);

            Assert.Equal("EADBC", target);
        }

        [Fact]
        public void DistractorAnalysis_IdentifiesMisleadingDistractor()
        {
            // 10 students: top 3 (Score 90-100), bottom 3 (Score 10-20)
            var students = new List<StudentResult>();

            // Top students choose option 'B' (distractor) on question 1
            for (int i = 1; i <= 3; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"TOP_{i}",
                    Score = 90 + i,
                    Answers = new List<string> { "B" } // Tuzağa düşmüşler
                });
            }

            // Middle students
            for (int i = 4; i <= 7; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"MID_{i}",
                    Score = 50,
                    Answers = new List<string> { "A" }
                });
            }

            // Bottom students choose option 'C' or 'A' on question 1 (none choose B)
            for (int i = 8; i <= 10; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"BOT_{i}",
                    Score = 10,
                    Answers = new List<string> { "C" }
                });
            }

            string[] answerKey = new[] { "A" };
            var distractorStats = _itemService.AnalyzeDistractors(students, answerKey, 1);

            Assert.Single(distractorStats);
            var q1 = distractorStats[0];
            Assert.True(q1.HasMisleadingDistractor);

            var optB = q1.Options.First(o => o.Option == "B");
            Assert.Equal(ItemAnalysisService.DistractorQuality.MisleadingDistractor, optB.Quality);
            Assert.Contains("Ters Çeldirici", optB.EvaluationMessage);
        }

        [Fact]
        public void DistractorAnalysis_IdentifiesGoodDistractor()
        {
            var students = new List<StudentResult>();

            // Top students answer correctly 'A'
            for (int i = 1; i <= 3; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"TOP_{i}",
                    Score = 90 + i,
                    Answers = new List<string> { "A" }
                });
            }

            // Middle students
            for (int i = 4; i <= 7; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"MID_{i}",
                    Score = 50,
                    Answers = new List<string> { "A" }
                });
            }

            // Bottom students fall into distractor 'B'
            for (int i = 8; i <= 10; i++)
            {
                students.Add(new StudentResult
                {
                    StudentId = $"BOT_{i}",
                    Score = 10,
                    Answers = new List<string> { "B" }
                });
            }

            string[] answerKey = new[] { "A" };
            var distractorStats = _itemService.AnalyzeDistractors(students, answerKey, 1);

            var q1 = distractorStats[0];
            var optB = q1.Options.First(o => o.Option == "B");

            Assert.Equal(ItemAnalysisService.DistractorQuality.GoodDistractor, optB.Quality);
            Assert.Contains("İyi Çeldirici", optB.EvaluationMessage);
        }
    }
}
