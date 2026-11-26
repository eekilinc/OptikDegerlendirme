using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class BackupRestoreTests
    {
        [Fact]
        public async Task CreateFullBackupAndRestore_InWalMode_PersistsAcrossFreshDatabaseRestart()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"OptikBackupTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            try
            {
                string sourceDbPath = Path.Combine(tempRoot, "source", "optik.db");
                string targetDbPath = Path.Combine(tempRoot, "target", "optik.db");
                string zipPath = Path.Combine(tempRoot, "OptikBackup_Test.zip");

                var settingsService = new AppSettingsService();
                var notificationService = new NotificationService();
                var jsonService = new JsonDataService();

                // 1. Kaynak WAL veritabanına ders, sınav ve öğrenci verisi yaz
                using (var sourceDb = new DatabaseService(sourceDbPath))
                {
                    await sourceDb.InitializeDatabaseAsync();
                    int courseId = await sourceDb.SaveCourseAsync(new Course { Code = "BİL101", Name = "Nesne Tabanlı Programlama" });

                    var cfg = new ExamConfigData
                    {
                        SchoolName = "Ağlasun MYO",
                        NetCoefficient = 5.0,
                        BaseScore = 0.0,
                        WrongDeductionFactor = 0.0,
                        AnswerKeys = new List<AnswerKeyModel> { new() { BookletName = "A", Answers = "ABCDE" } }
                    };

                    var students = new List<StudentResult>
                    {
                        new()
                        {
                            StudentId = "2514506015",
                            FullName = "AZRA ÇETİN",
                            BookletType = "A",
                            RawAnswers = "ABCDE",
                            Score = 100,
                            CorrectCount = 5,
                            IncorrectCount = 0,
                            EmptyCount = 0,
                            QuestionResults = new List<bool> { true, true, true, true, true }
                        }
                    };

                    await sourceDb.SaveExamAsync(new ExamEntry
                    {
                        CourseId = courseId,
                        Title = "Vize",
                        Date = DateTime.Now,
                        ConfigJson = JsonSerializer.Serialize(cfg)
                    }, students);

                    var backupService = new BackupService(sourceDb, settingsService, notificationService, jsonService);
                    await backupService.CreateFullBackupAsync(zipPath);
                }

                Assert.True(File.Exists(zipPath));

                // 2. Hedef veritabanında aktif WAL oturumu varken yedeği geri yükle
                using (var targetDb = new DatabaseService(targetDbPath))
                {
                    await targetDb.InitializeDatabaseAsync();
                    await targetDb.SaveCourseAsync(new Course { Code = "ESKİ", Name = "Silinecek Eski Ders" });

                    var restoreService = new BackupService(targetDb, settingsService, notificationService, jsonService);
                    await restoreService.RestoreFullBackupAsync(zipPath, overwriteExisting: true);

                    var liveCourses = await targetDb.GetCoursesAsync();
                    Assert.Single(liveCourses);
                    Assert.Equal("Nesne Tabanlı Programlama", liveCourses[0].Name);
                }

                // 3. Uygulama yeniden başlatılmış gibi sıfırdan yeni bir DatabaseService aç ve kalıcılığı doğrula
                using (var restartedDb = new DatabaseService(targetDbPath))
                {
                    await restartedDb.InitializeDatabaseAsync();
                    var courses = await restartedDb.GetCoursesAsync();
                    Assert.Single(courses);
                    Assert.Equal("BİL101", courses[0].Code);
                    Assert.Equal("Nesne Tabanlı Programlama", courses[0].Name);

                    var exams = await restartedDb.GetExamsForCourseAsync(courses[0].Id);
                    Assert.Single(exams);
                    Assert.Equal("Vize", exams[0].Title);

                    var results = await restartedDb.GetResultsForExamAsync(exams[0].Id);
                    Assert.Single(results);
                    Assert.Equal("2514506015", results[0].StudentId);
                    Assert.Equal(100, results[0].Score);
                }
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        [Fact]
        public async Task RestoreFullBackup_WithLegacyEmptyDbZip_RecoversCoursesExamsAndAnswerKeysFromJson()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"OptikLegacyZipTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            try
            {
                string zipBuildDir = Path.Combine(tempRoot, "zip_src");
                Directory.CreateDirectory(zipBuildDir);

                // Eski v1.9.0 hatasını simüle et: optik.db boş (0 tablo), tüm veriler data_export.json içinde
                string emptyDbPath = Path.Combine(zipBuildDir, "optik.db");
                using (var emptyConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={emptyDbPath};Pooling=False;"))
                {
                    await emptyConn.OpenAsync();
                    await emptyConn.CloseAsync();
                }

                var legacyExport = new JsonDataService.FullExportData
                {
                    ExportVersion = "2.0",
                    ExportDate = DateTime.Now,
                    TotalCourses = 1,
                    TotalExams = 1,
                    TotalStudents = 1,
                    Exams = new List<JsonDataService.ExamExportData>
                    {
                        new()
                        {
                            ExamTitle = "Vize",
                            CourseName = "Programlama Temelleri",
                            ExamDate = new DateTime(2026, 11, 23, 16, 9, 0),
                            AnswerKeys = new List<AnswerKeyModel>(), // Eski sürümde boş kaydediliyordu
                            Students = new List<StudentResult>
                            {
                                new()
                                {
                                    StudentId = "2514506015",
                                    FullName = "AZRA ÇETİN",
                                    BookletType = "A",
                                    RawAnswers = "DCBA",
                                    CorrectCount = 3,
                                    IncorrectCount = 1,
                                    EmptyCount = 0,
                                    NetCount = 3,
                                    Score = 75,
                                    QuestionResults = new List<bool> { true, true, true, false }
                                }
                            }
                        }
                    }
                };

                string json = JsonSerializer.Serialize(legacyExport, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(Path.Combine(zipBuildDir, "data_export.json"), json);

                string legacyZipPath = Path.Combine(tempRoot, "LegacyBackup.zip");
                ZipFile.CreateFromDirectory(zipBuildDir, legacyZipPath);

                string targetDbPath = Path.Combine(tempRoot, "restored", "optik.db");
                using (var targetDb = new DatabaseService(targetDbPath))
                {
                    await targetDb.InitializeDatabaseAsync();
                    var backupService = new BackupService(targetDb, new AppSettingsService(), new NotificationService(), new JsonDataService());
                    await backupService.RestoreFullBackupAsync(legacyZipPath, overwriteExisting: true);
                }

                // Yeniden başlatma simülasyonu
                using (var restartedDb = new DatabaseService(targetDbPath))
                {
                    await restartedDb.InitializeDatabaseAsync();
                    var courses = await restartedDb.GetCoursesAsync();
                    Assert.Single(courses);
                    Assert.Equal("Programlama Temelleri", courses[0].Name);

                    var exams = await restartedDb.GetExamsForCourseAsync(courses[0].Id);
                    Assert.Single(exams);

                    var recoveredConfig = JsonSerializer.Deserialize<ExamConfigData>(exams[0].ConfigJson);
                    Assert.NotNull(recoveredConfig);
                    Assert.NotEmpty(recoveredConfig!.AnswerKeys);
                    Assert.StartsWith("DCB", recoveredConfig.AnswerKeys[0].Answers);
                    Assert.Equal(25.0, recoveredConfig.NetCoefficient);

                    var results = await restartedDb.GetResultsForExamAsync(exams[0].Id);
                    Assert.Single(results);
                    Assert.Equal(75, results[0].Score);
                }
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        [Fact]
        public async Task RestoreDatabase_And_RealBackupFilesIfPresent_RestoreAllCoursesExamsAndStudents()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"OptikDbRestoreTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            var settingsService = new AppSettingsService();
            var originalSettings = settingsService.Load();

            try
            {
                // Sentetik .db yedeği oluştur ve aktif WAL veritabanının üzerine geri yükle
                string srcPath = Path.Combine(tempRoot, "src", "optik.db");
                string backupDbFile = Path.Combine(tempRoot, "OptikDB_Test.db");
                string dstPath = Path.Combine(tempRoot, "dst", "optik.db");

                using (var srcDb = new DatabaseService(srcPath))
                {
                    await srcDb.InitializeDatabaseAsync();
                    int c1 = await srcDb.SaveCourseAsync(new Course { Code = "2026", Name = "Yazılım Kurulum Yönetim" });
                    await srcDb.SaveExamAsync(
                        new ExamEntry { CourseId = c1, Title = "Vize", Date = DateTime.Now, ConfigJson = "{}" },
                        new List<StudentResult> { new() { StudentId = "1001", FullName = "Test Öğrenci", BookletType = "A", RawAnswers = "ABC", Score = 85 } });

                    var svc = new BackupService(srcDb, settingsService, new NotificationService(), new JsonDataService());
                    await svc.CreateDatabaseBackupAsync(backupDbFile);
                }

                using (var dstDb = new DatabaseService(dstPath))
                {
                    await dstDb.InitializeDatabaseAsync();
                    await dstDb.SaveCourseAsync(new Course { Code = "OLD", Name = "Eski Ders" });

                    var svc = new BackupService(dstDb, settingsService, new NotificationService(), new JsonDataService());
                    await svc.RestoreDatabaseAsync(backupDbFile);
                }

                using (var restarted = new DatabaseService(dstPath))
                {
                    await restarted.InitializeDatabaseAsync();
                    var courses = await restarted.GetCoursesAsync();
                    Assert.Single(courses);
                    Assert.Equal("Yazılım Kurulum Yönetim", courses[0].Name);
                }

                // Kullanıcının yerel z:\ sürücüsündeki gerçek yedek dosyaları mevcutsa izole geçici klasörde doğrula
                const string realZip = @"z:\OptikBackup_20261124_163446.zip";
                const string realDb = @"z:\OptikDB_20261124_163502.db";

                if (File.Exists(realZip))
                {
                    string realZipTarget = Path.Combine(tempRoot, "real_zip", "optik.db");
                    using (var db = new DatabaseService(realZipTarget))
                    {
                        await db.InitializeDatabaseAsync();
                        var svc = new BackupService(db, settingsService, new NotificationService(), new JsonDataService());
                        await svc.RestoreFullBackupAsync(realZip, overwriteExisting: true);
                    }
                    using (var restartedZipDb = new DatabaseService(realZipTarget))
                    {
                        await restartedZipDb.InitializeDatabaseAsync();
                        var courses = await restartedZipDb.GetCoursesAsync();
                        Assert.Equal(4, courses.Count);
                        int totalStudents = 0;
                        foreach (var c in courses)
                        {
                            var exams = await restartedZipDb.GetExamsForCourseAsync(c.Id);
                            foreach (var ex in exams)
                                totalStudents += (await restartedZipDb.GetResultsForExamAsync(ex.Id)).Count;
                        }
                        Assert.Equal(217, totalStudents);
                    }
                }

                if (File.Exists(realDb))
                {
                    string realDbTarget = Path.Combine(tempRoot, "real_db", "optik.db");
                    using (var db = new DatabaseService(realDbTarget))
                    {
                        await db.InitializeDatabaseAsync();
                        var svc = new BackupService(db, settingsService, new NotificationService(), new JsonDataService());
                        await svc.RestoreDatabaseAsync(realDb);
                    }
                    using (var restartedRealDb = new DatabaseService(realDbTarget))
                    {
                        await restartedRealDb.InitializeDatabaseAsync();
                        var courses = await restartedRealDb.GetCoursesAsync();
                        Assert.Equal(4, courses.Count);
                        int totalStudents = 0;
                        foreach (var c in courses)
                        {
                            var exams = await restartedRealDb.GetExamsForCourseAsync(c.Id);
                            foreach (var ex in exams)
                                totalStudents += (await restartedRealDb.GetResultsForExamAsync(ex.Id)).Count;
                        }
                        Assert.Equal(217, totalStudents);
                    }
                }
            }
            finally
            {
                settingsService.Save(originalSettings);
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }
}
