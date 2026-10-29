# AGENT.md — Optik Değerlendirme (Teknik Analiz & Çalışma Rehberi)

> Bu dosya, projenin teknik durumunu, mimarisini, güvenlik/performans notlarını ve agent çalışma kurallarını özetler. Tüm iş PLAN.md üzerinden takip edilir.

## 1. Proje Özeti

- **Ad:** Optik Değerlendirme Sistemi
- **Sürüm:** 1.5.0
- **Tür:** Windows masaüstü (WPF), .NET 9.0
- **Dil:** C# 13 (nullable enable, implicit usings)
- **Veritabanı:** SQLite (Microsoft.Data.Sqlite 10.0.5), WAL modu
- **Mimari:** MVVM + Dependency Injection (Microsoft.Extensions.DependencyInjection)
- **Dağıtım:** Self-contained win-x64, Inno Setup installer
- **Hedef kitle:** AÇLASUN MYO sınav değerlendirme

## 2. Dizin Yapısı

```
OptikFormApp/                (ana WPF proje)
├── Models/                  (POCO modeller)
├── Services/                (iş mantığı + veri erişimi)
├── ViewModels/              (MVVM + converter'lar)
├── Views/                   (XAML + code-behind)
├── Assets/
OptikFormApp.Core/           (core servisler — şu an ana uygulamada KULLANILMIYOR, ölü kod)
OptikFormApp.Tests/          (xunit, 36 test, net9.0-windows)
docs/                        (ALGORITHMS, ARCHITECTURE_DECISIONS, STYLE_GUIDE)
.github/workflows/           (release pipeline, sadece tag push)
```

## 3. Mimari & Bağımlılıklar

- **DI:** `ServiceProvider.cs` singleton/transient servisleri kaydeder. `DatabaseService`, `AppSettingsService`, `JsonDataService`, `BackupService` singleton; parser/export/validation transient.
- **MVVM:** `MainViewModel` (partial, 5 dosya) + çok sayıda converter. Code-behind minimal (modal aç/kapa).
- **Veri katmanı:** `DatabaseService` tek `SqliteConnection` paylaşımlı (singleton). Parametreli SQL kullanılıyor; `BulkDeleteResultsAsync` placeholder string'i güvenli (sadece `@eid{i}`), `SetUserVersionAsync` int interpolasyonu (düşük risk).
- **Dış bağımlılıklar:** ClosedXML, QuestPDF, ScottPlot.WPF, Serilog, Newtonsoft.Json, System.Drawing.Common.

## 4. Build & Test

```powershell
dotnet build OptikFormApp.csproj
dotnet build OptikFormApp.Tests/OptikFormApp.Tests.csproj
dotnet test  OptikFormApp.Tests/OptikFormApp.Tests.csproj
```

- Mevcut durum: build **0 uyarı/0 hata**, testler **36/36 başarılı** (140 ms).

## 5. Güvenlik Bulguları (Analiz)

| # | Alan | Bulgular | Risk |
|---|------|----------|------|
| S1 | Zip geri yükleme (`BackupService.RestoreFullBackupAsync`, `GetBackupInfoAsync`) | `ZipFile.ExtractToDirectory(..., true)` path traversal (zip slip) potansiyeli; overwrite açık | Orta |
| S2 | `DatabaseService.SetUserVersionAsync` | SQL string interpolasyonu (`PRAGMA user_version = {version}`) — int tipi, düşük risk ama doğrulanmalı | Düşük |
| S3 | `GetResultsForExamAsync` | `reader.GetString` null/DBNull durumunda exception; JSON deserialize try/catch yok | Orta (güvenilirlik) |
| S4 | Loglama | `app_debug.log` / Serilog dosyaları öğrenci adı/numarası içerebilir (PII) | Orta (gizlilik) |
| S5 | `VersionCheckService` | GitHub release body/asset URL'leri doğrulama/hash yok; HTTPS kullanılıyor | Düşük |
| S6 | JSON import (`ImportFromJsonAsync`, `ValidateJsonFileAsync`) | Boyut limiti yok; deserialization polymorphic değil (POCO), güvenli | Düşük |
| S7 | Dosya yolu | Kullanıcı seçimi (SaveFileDialog) üzerinden; traversal koruması yok ama kullanıcı odaklı | Düşük |
| S8 | SQLite at rest | Şifreleme yok (SQLCipher yok) | Düşük (masaüstü) |
| S9 | `ReconnectAsync` | Eski `_connection` dispose edilmiyor (kaynak sızıntısı) | Orta |
| S10 | Eşzamanlılık | Tek `SqliteConnection` üzerinde paralel komut; thread-safety yok | Orta/Yüksek |

## 6. Performans Bulguları

| # | Alan | Bulgular |
|---|------|----------|
| P1 | `PerformanceOptimizer.OptimizeUI` | `RenderMode.SoftwareOnly` zorluyor — modern GPU'larda performansı düşürür |
| P2 | `OptimizeMemory` / `MemoryLeakDetector.OptimizeMemory` | Hot path'te `GC.Collect()` çağrısı (duraklama riski) |
| P3 | `CalculateStatistics` | O(keys × questions × students) iç içe döngü; 5000 öğrenci × 50 soru için ağır |
| P4 | `BulkUpdateScoresAsync` | Sabit 50 soru varsayımı + yanlış sayım hesaplama (doğruluk hatası) |
| P5 | DB erişimi | `ExportAllDataAsync` ikinci `DatabaseService` açıyor (WAL izin verir ama bağlantı çoğalması) |
| P6 | Async | Senkron `File.WriteAllText` (settings) UI thread'i bloke edebilir |
| P7 | `MemoryLeakDetector` | Pencere sayısı eşiği (10) keyfi; gerçek leak tespiti yok |
| P8 | Virtualization | `EnableAdvancedVirtualization` yalnızca MainWindow'a uygulanıyor, DataGrid'lere değil |

## 7. Test Kapsamı (Mevcut)

Test edilen: `BookletMapping`, `GradingService`, `ItemAnalysisService`, `OpticalParserService`, `StatisticsReportService`, `SuccessPredictionService`, `ValidationService`.

**Test edilmeyen:** `DatabaseService`, `BackupService`, `JsonDataService`, `AppSettingsService` (her iki), `VersionCheckService`, `FuzzySearchService`, `TemplateService`, `UndoRedoManager`, `KeyboardShortcutService`, `LocalizationService`, `ThemeService`, `NotificationService`, `ProgressService`, `BookletMappingService`, `CsvExportService`, `ExcelExportService`, `PdfReportService`, `GradingService` (kısmi), `MainViewModel`.

## 8. Teknik Borç / Mimari

- `OptikFormApp.Core` projesi ana uygulamada **hiç kullanılmıyor**; `AppSettings`/`AppSettingsService`/`LoggingService`/`GlobalExceptionHandler`/`BackupService` yineleniyor. Çift bakım yükü.
- `MainViewModel` 5 partial dosyada; çok büyük, test edilemiyor (UI bağımlılığı).
- `ServiceProvider` thread-safe singleton (lock var) — OK.
- CI/CD yalnızca tag push'ta çalışır; PR/push'ta build/test yok.
- Kod analizörü (Roslyn analyzers) yapılandırılmamış.

## 9. Agent Çalışma Kuralları

1. **Her iş PLAN.md üzerinden** takip edilir; tamamlanan maddeler `[x]` ile işaretlenir, yeni iş PLAN.md'ye eklenir.
2. Bir değişiklik yapmadan önce ilgili servis/model okunur; mevcut testler çalıştırılır.
3. Güvenlik/performans değişikliklerinde önce test yazılır (TDD), sonra kod.
4. DB şeması değişikliklerinde migration/version bump yapılır (`SetUserVersionAsync`).
5. PII içeren loglar yazılmaz; hata mesajlarında öğrenci numarası maskeleyin.
6. `OptikFormApp.Core` ile ana uygulama arasındaki yinelemeyi artırmayın; yeni kod ana uygulamada.
7. Değişiklik sonrası `dotnet build` + `dotnet test` yeşil olmalı.
8. Büyük refactor'lar ayrı branch/worktree ile yapılır (openchamber).

## 10. Hızlı Referans

- DB dosyası: `%APPDATA%\OptikDegerlendirme\optik.db`
- Ayarlar: `%APPDATA%\OptikDegerlendirme\appsettings.json` (ana) / `OptikFormApp\settings.json` (eski)
- Loglar: `%APPDATA%\OptikDegerlendirme\app_debug.log`, `logs\optik-*.log`
- Test komutu: `dotnet test OptikFormApp.Tests/OptikFormApp.Tests.csproj`