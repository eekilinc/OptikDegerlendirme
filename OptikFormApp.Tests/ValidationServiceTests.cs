using System.Collections.Generic;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class ValidationServiceTests
    {
        private readonly ValidationService _validationService;

        public ValidationServiceTests()
        {
            _validationService = new ValidationService();
        }

        [Fact]
        public void Validate_CleanData_ReturnsNoIssues()
        {
            var students = new List<StudentResult>
            {
                new() { StudentId = "101", FullName = "Öğrenci 1", BookletType = "A", RawAnswers = "ABCDE" },
                new() { StudentId = "102", FullName = "Öğrenci 2", BookletType = "A", RawAnswers = "BCDEA" }
            };
            var keys = new List<AnswerKeyModel>
            {
                new() { BookletName = "A", Answers = "ABCDE" }
            };

            var issues = _validationService.Validate(students, keys);

            Assert.Empty(issues);
        }

        [Fact]
        public void Validate_DuplicateStudentId_DetectsIssue()
        {
            var students = new List<StudentResult>
            {
                new() { StudentId = "101", FullName = "Öğrenci 1", BookletType = "A", RawAnswers = "ABCDE" },
                new() { StudentId = "101", FullName = "Öğrenci 2 Mükerrer", BookletType = "A", RawAnswers = "ABCDE" }
            };
            var keys = new List<AnswerKeyModel>
            {
                new() { BookletName = "A", Answers = "ABCDE" }
            };

            var issues = _validationService.Validate(students, keys);

            Assert.Contains(issues, i => i.Title == "Mükerrer Öğrenci Numarası");
        }

        [Fact]
        public void Validate_UndefinedBookletType_DetectsError()
        {
            var students = new List<StudentResult>
            {
                new() { StudentId = "101", FullName = "Öğrenci 1", BookletType = "B", RawAnswers = "ABCDE" }
            };
            var keys = new List<AnswerKeyModel>
            {
                new() { BookletName = "A", Answers = "ABCDE" } // Only 'A' defined, 'B' is missing
            };

            var issues = _validationService.Validate(students, keys);

            Assert.Contains(issues, i => i.Title == "Tanımsız Kitapçık Türü" && i.Severity == ValidationSeverity.Error);
        }

        [Fact]
        public void Validate_EmptyAnswerKey_DetectsError()
        {
            var students = new List<StudentResult>
            {
                new() { StudentId = "101", FullName = "Öğrenci 1", BookletType = "A", RawAnswers = "ABCDE" }
            };
            var keys = new List<AnswerKeyModel>
            {
                new() { BookletName = "A", Answers = "" } // Empty key
            };

            var issues = _validationService.Validate(students, keys);

            Assert.Contains(issues, i => i.Title == "Boş Cevap Anahtarı" && i.Severity == ValidationSeverity.Error);
        }
    }
}
