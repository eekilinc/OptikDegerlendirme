using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace OptikFormApp.Services
{
    /// <summary>
    /// Tam yedekleme ve geri yükleme servisi
    /// Veritabanı + ayarlar + şablonlar + JSON dışa aktarım tek zip dosyasında
    /// </summary>
    public class BackupService
    {
        private readonly DatabaseService _dbService;
        private readonly AppSettingsService _settingsService;
        private readonly NotificationService _notificationService;
        private readonly JsonDataService _jsonDataService;

        private string GetAppDataDirectory()
        {
            var dir = Path.GetDirectoryName(_dbService.DatabasePath);
            if (!string.IsNullOrWhiteSpace(dir))
                return dir;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OptikDegerlendirme");
        }

        public BackupService(
            DatabaseService dbService,
            AppSettingsService settingsService,
            NotificationService notificationService,
            JsonDataService jsonDataService)
        {
            _dbService = dbService;
            _settingsService = settingsService;
            _notificationService = notificationService;
            _jsonDataService = jsonDataService;
        }

        /// <summary>
        /// Tam yedek oluşturur (WAL-safe DB + Settings + Templates + Metadata + JSON Export)
        /// </summary>
        public async Task<string> CreateFullBackupAsync(string? customPath = null)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupName = $"OptikBackup_{timestamp}.zip";
            var backupPath = customPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), backupName);

            var tempDir = Path.Combine(Path.GetTempPath(), $"OptikBackup_{timestamp}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Veritabanını SQLite Online Backup API + WAL Checkpoint ile tek parça tutarlı olarak yedekle
                var dbDest = Path.Combine(tempDir, "optik.db");
                await _dbService.BackupDatabaseAsync(dbDest);

                // 2. Ayarları JSON olarak kaydet
                var settings = _settingsService.Load();
                var settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(tempDir, "settings.json"), settingsJson);

                // 3. Şablonları (Templates) kopyala
                var templatesDir = Path.Combine(GetAppDataDirectory(), "Templates");
                if (Directory.Exists(templatesDir))
                {
                    CopyDirectoryRecursive(templatesDir, Path.Combine(tempDir, "Templates"));
                }

                // 4. Metadata oluştur
                var metadata = new BackupMetadata
                {
                    BackupDate = DateTime.Now,
                    AppVersion = GetAppVersion(),
                    DatabaseVersion = await GetDatabaseVersionAsync(),
                    Description = "Tam Yedek",
                    BackupType = "Full"
                };
                var metaJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(tempDir, "metadata.json"), metaJson);

                // 5. Tüm sınav verilerini JSON olarak dışa aktar (ikincil kurtarma katmanı)
                var exportData = await _jsonDataService.ExportAllDataAsync(_dbService);
                await File.WriteAllTextAsync(Path.Combine(tempDir, "data_export.json"), exportData);

                // 6. Zip dosyası oluştur
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                ZipFile.CreateFromDirectory(tempDir, backupPath, CompressionLevel.Optimal, false);

                _notificationService.ShowSuccess($"Yedekleme tamamlandı: {backupPath}");
                return backupPath;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        /// <summary>
        /// Sadece veritabanı yedeği
        /// </summary>
        public async Task<string> CreateDatabaseBackupAsync(string? customPath = null)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupName = $"OptikDB_{timestamp}.db";
            var backupPath = customPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), backupName);

            await _dbService.BackupDatabaseAsync(backupPath);

            _notificationService.ShowSuccess($"Veritabanı yedeği oluşturuldu: {backupPath}");
            return backupPath;
        }

        /// <summary>
        /// Sadece ayarları dışa aktar
        /// </summary>
        public async Task<string> ExportSettingsAsync(string? customPath = null)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"OptikSettings_{timestamp}.json";
            var exportPath = customPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

            var settings = _settingsService.Load();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json);

            _notificationService.ShowSuccess($"Ayarlar dışa aktarıldı: {exportPath}");
            return exportPath;
        }

        /// <summary>
        /// Tam yedeği geri yükle (.zip). Hem güncel optik.db hem de eski sürümlerdeki data_export.json kurtarmasını destekler.
        /// </summary>
        public async Task RestoreFullBackupAsync(string zipPath, bool overwriteExisting = true)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Yedek dosyası bulunamadı", zipPath);

            var tempDir = Path.Combine(Path.GetTempPath(), $"OptikRestore_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                SafeExtractZip(zipPath, tempDir);

                var appDataDir = GetAppDataDirectory();
                Directory.CreateDirectory(appDataDir);

                // Mevcut veritabanının WAL-safe güvenlik yedeğini al
                var safetyBackup = Path.Combine(appDataDir, $"pre_restore_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                if (File.Exists(_dbService.DatabasePath))
                {
                    try { await _dbService.BackupDatabaseAsync(safetyBackup); } catch { }
                }

                var dbBackup = Path.Combine(tempDir, "optik.db");
                var dataExportPath = Path.Combine(tempDir, "data_export.json");

                if (File.Exists(dbBackup) && (overwriteExisting || !File.Exists(_dbService.DatabasePath)))
                {
                    await _dbService.RestoreDatabaseFromFileAsync(dbBackup);
                }

                // Eski sürümlerde WAL checkpoint yapılmadan oluşturulmuş .zip yedeklerinde optik.db boş olabilir;
                // bu durumda data_export.json içindeki tüm ders, sınav ve öğrenci kayıtlarını otomatik kurtar.
                var restoredCourses = await _dbService.GetCoursesAsync();
                if (restoredCourses.Count == 0 && File.Exists(dataExportPath))
                {
                    await _jsonDataService.ImportAllDataToDatabaseAsync(dataExportPath, _dbService);
                }

                // Şablonları geri yükle
                var extractedTemplates = Path.Combine(tempDir, "Templates");
                if (Directory.Exists(extractedTemplates))
                {
                    var targetTemplates = Path.Combine(appDataDir, "Templates");
                    CopyDirectoryRecursive(extractedTemplates, targetTemplates);
                }

                // Ayarları geri yükle
                var settingsPath = Path.Combine(tempDir, "settings.json");
                if (File.Exists(settingsPath))
                {
                    var settingsJson = await File.ReadAllTextAsync(settingsPath);
                    var settings = JsonSerializer.Deserialize<Models.AppSettings>(
                        settingsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (settings != null)
                    {
                        _settingsService.Save(settings);
                    }
                }

                await _dbService.CheckpointAsync();
                _notificationService.ShowSuccess("Yedek başarıyla geri yüklendi.");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        /// <summary>
        /// Veritabanını geri yükle (.db)
        /// </summary>
        public async Task RestoreDatabaseAsync(string dbPath)
        {
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("Veritabanı dosyası bulunamadı", dbPath);

            var appDataDir = GetAppDataDirectory();
            Directory.CreateDirectory(appDataDir);

            var safetyBackup = Path.Combine(appDataDir, $"pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(_dbService.DatabasePath))
            {
                try { await _dbService.BackupDatabaseAsync(safetyBackup); } catch { }
            }

            await _dbService.RestoreDatabaseFromFileAsync(dbPath);
            _notificationService.ShowSuccess("Veritabanı geri yüklendi.");
        }

        /// <summary>
        /// Ayarları içe aktar
        /// </summary>
        public async Task ImportSettingsAsync(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Ayar dosyası bulunamadı", jsonPath);

            var json = await File.ReadAllTextAsync(jsonPath);
            var settings = JsonSerializer.Deserialize<Models.AppSettings>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (settings == null)
                throw new InvalidDataException("Ayar dosyası geçersiz");

            _settingsService.Save(settings);
            _notificationService.ShowSuccess("Ayarlar içe aktarıldı.");
        }

        /// <summary>
        /// Yedek dosyası bilgilerini oku
        /// </summary>
        public async Task<BackupInfo?> GetBackupInfoAsync(string zipPath)
        {
            if (!File.Exists(zipPath))
                return null;

            var tempDir = Path.Combine(Path.GetTempPath(), $"OptikInfo_{Guid.NewGuid():N}");
            try
            {
                SafeExtractZip(zipPath, tempDir);
                var metaPath = Path.Combine(tempDir, "metadata.json");
                if (File.Exists(metaPath))
                {
                    var json = await File.ReadAllTextAsync(metaPath);
                    var metadata = JsonSerializer.Deserialize<BackupMetadata>(json);
                    if (metadata != null)
                    {
                        var fileInfo = new FileInfo(zipPath);
                        return new BackupInfo
                        {
                            FilePath = zipPath,
                            FileSize = fileInfo.Length,
                            CreatedDate = metadata.BackupDate,
                            AppVersion = metadata.AppVersion,
                            DatabaseVersion = metadata.DatabaseVersion,
                            Description = metadata.Description,
                            BackupType = metadata.BackupType
                        };
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        /// <summary>
        /// Mevcut yedekleri listele
        /// </summary>
        public List<BackupInfo> ListBackups(string directory)
        {
            var backups = new List<BackupInfo>();
            if (!Directory.Exists(directory))
                return backups;

            var files = Directory.GetFiles(directory, "OptikBackup_*.zip");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                backups.Add(new BackupInfo
                {
                    FilePath = file,
                    FileSize = fileInfo.Length,
                    CreatedDate = fileInfo.CreationTime,
                    AppVersion = "Bilinmiyor",
                    BackupType = "Full"
                });
            }

            return backups.OrderByDescending(b => b.CreatedDate).ToList();
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }

        /// <summary>
        /// Zip slip (path traversal) korumalı çıkarma. Hedef dizin dışına çıkan girdiler reddedilir.
        /// </summary>
        private static void SafeExtractZip(string zipPath, string destinationDir)
        {
            var destRoot = Path.GetFullPath(destinationDir);
            if (!destRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                destRoot += Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var destPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
                if (!destPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Güvensiz zip girişi (path traversal): {entry.FullName}");

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destPath);
                }
                else
                {
                    var parent = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(destPath, overwrite: true);
                }
            }
        }

        private string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        }

        private async Task<string> GetDatabaseVersionAsync()
        {
            try
            {
                var version = await _dbService.GetUserVersionAsync();
                return version.ToString();
            }
            catch
            {
                return "0";
            }
        }
    }

    public class BackupMetadata
    {
        public DateTime BackupDate { get; set; }
        public string AppVersion { get; set; } = "";
        public string DatabaseVersion { get; set; } = "";
        public string Description { get; set; } = "";
        public string BackupType { get; set; } = "Full";
    }

    public class BackupInfo
    {
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public string AppVersion { get; set; } = "";
        public string DatabaseVersion { get; set; } = "";
        public string Description { get; set; } = "";
        public string BackupType { get; set; } = "";

        public string FileSizeFormatted => FormatBytes(FileSize);
        public string CreatedDateFormatted => CreatedDate.ToString("dd.MM.yyyy HH:mm");

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
