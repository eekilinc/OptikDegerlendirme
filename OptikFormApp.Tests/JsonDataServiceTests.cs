using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class JsonDataServiceTests
    {
        [Fact]
        public async Task ExportImport_Roundtrip()
        {
            var svc = new JsonDataService();
            var path = Path.Combine(Path.GetTempPath(), $"t_{Guid.NewGuid():N}.json");
            var students = new[] { new StudentResult { StudentId = "1", FullName = "Ali", BookletType = "A", RawAnswers = "AB", Score = 10 } };
            var keys = new[] { new AnswerKeyModel { BookletName = "A", Answers = "AB" } };
            await svc.ExportToJsonAsync(path, "Exam", "Course", "School", 1, 0, 0.25, keys, Enumerable.Empty<QuestionSetting>(), Enumerable.Empty<LearningOutcome>(), students);
            Assert.True(File.Exists(path));

            var data = await svc.ImportFromJsonAsync(path);
            Assert.NotNull(data);
            Assert.Single(data!.Students);
            Assert.Equal("Ali", data.Students[0].FullName);
            Assert.Single(data.AnswerKeys);

            File.Delete(path);
        }

        [Fact]
        public async Task ValidateJsonFile_InvalidReturnsFalse()
        {
            var svc = new JsonDataService();
            var path = Path.Combine(Path.GetTempPath(), $"bad_{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(path, "{ not json }");
            Assert.False(await svc.ValidateJsonFileAsync(path));
            File.Delete(path);
        }
    }
}