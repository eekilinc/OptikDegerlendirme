using System;
using System.IO;
using System.Threading.Tasks;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class BackupServiceSmokeTests
    {
        [Fact]
        public async Task CreateDatabaseBackup_CreatesFile()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"b_{Guid.NewGuid():N}.db");
            var db = new DatabaseService(dbPath);
            await db.InitializeDatabaseAsync();

            var settingsPath = Path.Combine(Path.GetTempPath(), $"s_{Guid.NewGuid():N}.json");
            var settings = new AppSettingsService(); // default appdata
            var notif = new NotificationService();
            var json = new JsonDataService();
            var backup = new BackupService(db, settings, notif, json);

            var outPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid():N}.db");
            var result = await backup.CreateDatabaseBackupAsync(outPath);
            Assert.True(File.Exists(result));

            db.Dispose();
            try { File.Delete(dbPath); } catch { }
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(dbPath) + "*")) try { File.Delete(f); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }
}