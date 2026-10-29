using System;
using System.IO;
using System.Linq;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class ExportSmokeTests
    {
        [Fact]
        public void CsvExport_CreatesFile()
        {
            var students = new[] { new StudentResult { FullName = "Ali", StudentId = "1", BookletType = "A", RawAnswers = "A", Score = 10 } }.ToList();
            var path = Path.Combine(Path.GetTempPath(), $"t_{Guid.NewGuid():N}.csv");
            new CsvExportService().ExportToCsv(students, path);
            Assert.True(File.Exists(path) && new FileInfo(path).Length > 0);
            File.Delete(path);
        }

        [Fact]
        public void ExcelExport_CreatesFile()
        {
            var students = new[] { new StudentResult { FullName = "Ali", StudentId = "1", BookletType = "A", RawAnswers = "A", Score = 10 } }.ToList();
            var path = Path.Combine(Path.GetTempPath(), $"t_{Guid.NewGuid():N}.xlsx");
            new ExcelExportService().ExportToExcel(students, new System.Collections.Generic.List<QuestionStatisticItem>(), Enumerable.Empty<LearningOutcome>(), path);
            Assert.True(File.Exists(path) && new FileInfo(path).Length > 0);
            File.Delete(path);
        }
    }
}