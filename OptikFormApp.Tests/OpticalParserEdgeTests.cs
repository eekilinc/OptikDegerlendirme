using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class OpticalParserEdgeTests : IDisposable
    {
        private readonly string _path;
        public OpticalParserEdgeTests() { _path = Path.Combine(Path.GetTempPath(), $"p_{Guid.NewGuid():N}.txt"); Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        public void Dispose() { if (File.Exists(_path)) try { File.Delete(_path); } catch { } }

        [Fact]
        public async Task ShortLine_ProducesError()
        {
            await File.WriteAllTextAsync(_path, "SHORT", Encoding.GetEncoding(1254));
            var parser = new OpticalParserService();
            var (students, keys, errors) = await parser.ParseFileAsync(_path);
            Assert.Empty(students);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public async Task AnswerKeyLine_WithoutName_ParsedAsKey()
        {
            // line with no name/id but booklet+answers
            string line = new string(' ', 22) + new string(' ', 10) + "A" + "ABCDE";
            await File.WriteAllTextAsync(_path, line, Encoding.GetEncoding(1254));
            var parser = new OpticalParserService();
            var (students, keys, errors) = await parser.ParseFileAsync(_path);
            Assert.Empty(students);
            Assert.Single(keys);
            Assert.Equal("A", keys[0].BookletName);
        }

        [Fact]
        public async Task NonExistentFile_ReturnsError()
        {
            var parser = new OpticalParserService();
            var (students, keys, errors) = await parser.ParseFileAsync("nope_file_xyz.txt");
            Assert.Empty(students);
            Assert.Contains(errors, e => e.Contains("bulunamad") || e.Contains("Dosya"));
        }
    }
}