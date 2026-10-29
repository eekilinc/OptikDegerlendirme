using System;
using System.IO;
using OptikFormApp.Core.Models;
using OptikFormApp.Core.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class AppSettingsServiceTests
    {
        [Fact]
        public void LoadSave_Roundtrip()
        {
            var path = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}.json");
            var svc = new AppSettingsService(path);
            var s = new AppSettings { MaxStudents = 42, DefaultLanguage = "en-US" };
            svc.SaveSettings(s);
            svc.ClearCache();
            var loaded = svc.LoadSettings();
            Assert.Equal(42, loaded.MaxStudents);
            Assert.Equal("en-US", loaded.DefaultLanguage);
            File.Delete(path);
        }

        [Fact]
        public void ResetToDefaults()
        {
            var path = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}.json");
            var svc = new AppSettingsService(path);
            svc.SaveSettings(new AppSettings { MaxStudents = 999 });
            svc.ResetToDefaults();
            var loaded = svc.LoadSettings();
            Assert.Equal(5000, loaded.MaxStudents); // core default
            File.Delete(path);
        }
    }
}