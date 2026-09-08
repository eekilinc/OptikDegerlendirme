using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class OpticalParserServiceTests : IDisposable
    {
        private readonly OpticalParserService _parserService;
        private readonly string _tempFilePath;

        static OpticalParserServiceTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public OpticalParserServiceTests()
        {
            _parserService = new OpticalParserService();
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_optical_{Guid.NewGuid():N}.txt");
        }

        public void Dispose()
        {
            if (File.Exists(_tempFilePath))
            {
                try { File.Delete(_tempFilePath); } catch { }
            }
        }

        [Fact]
        public async Task ParseFileAsync_WhenFileDoesNotExist_ReturnsError()
        {
            var result = await _parserService.ParseFileAsync("non_existent_file.txt");

            Assert.Empty(result.Students);
            Assert.Empty(result.AnswerKeys);
            Assert.Contains(result.Errors, e => e.Contains("Dosya bulunamadı"));
        }

        [Fact]
        public async Task ParseFileAsync_WithValidStudentLine_ParsesCorrectly()
        {
            // Name: 22 chars, StudentId: 10 chars, Booklet: 1 char, Answers: remainder
            // "AHMET YILMAZ          " (22) + "1234567890" (10) + "A" (1) + "ABCDE" (5)
            string line = "AHMET YILMAZ          1234567890AABCDE";
            await File.WriteAllLinesAsync(_tempFilePath, new[] { line }, Encoding.GetEncoding("windows-1254"));

            var (students, answerKeys, errors) = await _parserService.ParseFileAsync(_tempFilePath);

            Assert.Single(students);
            var student = students[0];
            Assert.Equal("AHMET YILMAZ", student.FullName);
            Assert.Equal("1234567890", student.StudentId);
            Assert.Equal("A", student.BookletType);
            Assert.Equal("ABCDE", student.RawAnswers);
            Assert.Equal(5, student.Answers.Count);
            Assert.Equal("A", student.Answers[0]);
            Assert.Equal("E", student.Answers[4]);
        }

        [Fact]
        public async Task ParseFileAsync_WithAnswerKeyLine_ParsesAsAnswerKey()
        {
            // 22 spaces + 10 spaces + 'A' + 'ABCDE'
            string namePart = new string(' ', 22);
            string idPart = new string(' ', 10);
            string line = $"{namePart}{idPart}AABCDE";

            await File.WriteAllLinesAsync(_tempFilePath, new[] { line }, Encoding.GetEncoding("windows-1254"));

            var (students, answerKeys, errors) = await _parserService.ParseFileAsync(_tempFilePath);

            Assert.Empty(students);
            Assert.Single(answerKeys);
            var key = answerKeys[0];
            Assert.Equal("A", key.BookletName);
            Assert.Equal("ABCDE", key.Answers);
        }

        [Fact]
        public async Task ParseFileAsync_WhenLineIsTooShort_AddsError()
        {
            string shortLine = "KISA SATIR";
            await File.WriteAllLinesAsync(_tempFilePath, new[] { shortLine }, Encoding.GetEncoding("windows-1254"));

            var (students, answerKeys, errors) = await _parserService.ParseFileAsync(_tempFilePath);

            Assert.Empty(students);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("VERİ EKSİK"));
        }
    }
}
