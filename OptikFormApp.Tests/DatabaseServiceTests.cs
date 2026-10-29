using System;
using System.IO;
using System.Threading.Tasks;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class DatabaseServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DatabaseService _db;

        public DatabaseServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
            _db = new DatabaseService(_dbPath);
        }

        public void Dispose()
        {
            _db.Dispose();
            try { File.Delete(_dbPath); } catch { }
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_dbPath) + "*")) try { File.Delete(f); } catch { }
        }

        [Fact]
        public async Task Initialize_And_CourseCrud_Works()
        {
            await _db.InitializeDatabaseAsync();
            int id = await _db.SaveCourseAsync(new Course { Code = "C1", Name = "Math" });
            Assert.True(id > 0);
            var courses = await _db.GetCoursesAsync();
            Assert.Single(courses);
            Assert.Equal("Math", courses[0].Name);
        }

        [Fact]
        public async Task Exam_Save_And_Load_Roundtrip()
        {
            await _db.InitializeDatabaseAsync();
            int cid = await _db.SaveCourseAsync(new Course { Code = "C1", Name = "Math" });

            var exam = new ExamEntry { CourseId = cid, Title = "Midterm", Date = DateTime.Today, ConfigJson = "{}" };
            var results = new System.Collections.Generic.List<StudentResult>
            {
                new StudentResult { StudentId = "1", FullName = "Ali", BookletType = "A", RawAnswers = "AB", Score = 10, CorrectCount = 1, IncorrectCount = 0, EmptyCount = 1, QuestionResults = new System.Collections.Generic.List<bool>{true,false} }
            };
            int eid = await _db.SaveExamAsync(exam, results);
            Assert.True(eid > 0);

            var loaded = await _db.GetResultsForExamAsync(eid);
            Assert.Single(loaded);
            Assert.Equal("Ali", loaded[0].FullName);
            Assert.Equal(2, loaded[0].RawAnswers.Length);
        }

        [Fact]
        public async Task DeleteExam_Cascades()
        {
            await _db.InitializeDatabaseAsync();
            int cid = await _db.SaveCourseAsync(new Course { Code = "C1", Name = "Math" });
            var exam = new ExamEntry { CourseId = cid, Title = "E", Date = DateTime.Today, ConfigJson = "{}" };
            int eid = await _db.SaveExamAsync(exam, new System.Collections.Generic.List<StudentResult>());
            await _db.DeleteExamAsync(eid);
            var exams = await _db.GetExamsForCourseAsync(cid);
            Assert.Empty(exams);
        }

        [Fact]
        public async Task SetUserVersion_Validates()
        {
            await _db.InitializeDatabaseAsync();
            await _db.SetUserVersionAsync(5);
            Assert.Equal(5, await _db.GetUserVersionAsync());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _db.SetUserVersionAsync(-1));
        }
    }
}