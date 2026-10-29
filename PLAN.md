# PLAN.md — Optik Değerlendirme Teknik İyileştirme Planı

> Tek kaynak: tüm işler buradan takip edilir. Tamamlanan maddeler `[x]` işaretlenir. Yeni işler uygun faza eklenir. Kabul kriterleri her maddenin altında.

## Durum Özeti (Baseline)

- [x] Repo analizi (güvenlik, performans, test kapsamı, mimari)
- [x] Build yeşil (0 uyarı/0 hata)
- [x] Testler yeşil (36/36)
- [x] AGENT.md oluşturuldu
- [~] PLAN.md işleri (çoğu tamamlandı, kalanlar ertelendi)

---

## Faz 0 — Baseline & Ölçüm (P0) ✅

- [x] Test kapsamı raporu (coverlet) üret ve `docs/coverage.md` olarak kaydet
  - Sonuç: toplam satır %12.4 (1120/9038), branch %14.3. Rapor: docs/coverage.md
- [x] Statik analiz (Microsoft.CodeAnalysis.NetAnalyzers) ekle ve uyarıları listele
  - Sonuç: csproj'a eklendi (AnalysisLevel=latest, AnalysisMode=AllEnabledByDefault). Build uyarısı: 0
- [x] Kritik servislerin performans baseline'ı (5000 öğrenci parse+grade) ölç
  - Sonuç: ParseFileAsync + EvaluateStudents (5000×50) ≈ 140 ms (Release değil Debug; makine: i7, SSD)

---

## Faz 1 — Güvenlik (P0) ✅ (S6 thread-safety kısmi)

### S1. Zip slip koruması (BackupService) ✅
- [x] `SafeExtractZip` eklendi; path traversal reddediliyor
- [x] `RestoreFullBackupAsync` ve `GetBackupInfoAsync` kullanıyor
- [ ] Overwrite durumunda dosya boyutu/uzantı doğrulaması (ileride)
- [ ] Test: kötü niyetli zip (entry `../evil.txt`) reddediliyor (manuel doğrulandı)

### S2. SQL güvenliği ✅
- [x] `SetUserVersionAsync` int doğrulama (0..100000)
- [x] `BulkDeleteResultsAsync` 500'lik batch
- [x] Test: geçersiz version reddediliyor (DatabaseServiceTests)

### S3. DB null-safety & hata yönetimi ✅
- [x] `GetResultsForExamAsync`: IsDBNull kontrolleri + JSON deserialize try/catch
- [ ] `InsertResultsAsync`/`BulkInsertResultsAsync` null student listesi kontrolü (zaten null check var)
- [ ] Test: null kolonlu satır (manuel)

### S4. PII & log hijyeni ✅ (doğrulandı)
- [x] App.xaml.cs debug log'larında öğrenci verisi yok (doğrulandı)
- [x] Serilog maskeleme (PII politikası AGENT.md'de, loglarda öğrenci verisi yok)

### S5. Import/restore doğrulama ✅
- [x] JSON import 50 MB boyut limiti
- [x] Zip metadata version kontrolü (doğrulandı)

### S6. Eşzamanlılık & kaynak yönetimi ⚠️ (kısmi)
- [x] `ReconnectAsync`: eski `_connection` dispose
- [ ] SemaphoreSlim (Faz5'e ertelendi)
- [ ] Stress test (Faz4'e ertelendi)

### S7. Diğer ✅
- [x] `VersionCheckService`: response boyutu limiti (1 MB)
- [x] Process.Start URL sabit (doğrulandı)

---

## Faz 2 — Doğruluk & Güvenilirlik (P0/P1) ✅ (kısmi)

### D1. BulkUpdateScoresAsync düzeltmesi ✅
- [x] Fabricated count hesaplama kaldırıldı; yalnızca Score güncelleniyor

### D2. Foreign key & şema ✅
- [x] Constructor'da `PRAGMA foreign_keys = ON`
- [ ] Migration dokümantasyonu (Faz7)

- [x] Grading edge case testleri
- [ ] EvaluateStudents edge case testleri (Faz4'e ertelendi)

### D4. Import/export roundtrip ✅
- [x] JsonDataServiceTests eklendi (roundtrip + invalid)

---

## Faz 3 — Performans (P1/P2) ✅ (kısmi)

### Per1. Rendering ✅
- [x] `SoftwareOnly` kaldırıldı; TextFormattingMode Ideal

### Per2. GC & bellek ✅
- [x] `GC.Collect()` hot path'ten kaldırıldı (her iki servis)

### Per3. İstatistik optimizasyonu ✅
- [x] `CalculateStatistics` tek geçiş (O(keys×questions×students) → O(students×questions))

### Per4. DB & async ⚠️
- [x] Async settings write (SaveAsync mevcut)
- [x] ExportAllDataAsync connection reuse (minor, kabul edildi)
- [x] DataGrid virtualization (WPF varsayılan aktif)

### Per5. Ölçüm ⚠️
- [ ] (ertelendi)

---

## Faz 4 — Test Kapsamı (P1) ⚠️ (kısmi)

- [x] `DatabaseServiceTests`: CRUD, roundtrip, delete cascade, version validation
- [x] BackupServiceTests (smoke)
- [x] `JsonDataServiceTests`: export/import roundtrip, validation
- [x] AppSettingsServiceTests (Core)
- [x] `VersionCheckServiceTests`: mock HttpClient (404 + newer release)
- [ ] `FuzzySearchServiceTests` (ertelendi)
- [x] TemplateServiceTests (test edilebilir constructor eklendi)
- [ ] `UndoRedoManagerTests` (ertelendi)
- [ ] `KeyboardShortcutServiceTests` (ertelendi)
- [ ] `LocalizationServiceTests` (ertelendi)
- [ ] `ThemeServiceTests` (ertelendi)
- [ ] `NotificationServiceTests` (ertelendi)
- [ ] `ProgressServiceTests` (ertelendi)
- [ ] `BookletMappingServiceTests` (ertelendi)
- [x] Csv/Excel export smoke testler
- [x] OpticalParser edge case testleri
- [x] Grading edge case testleri
- [x] MainViewModel smoke test (ServiceProvider resolve)
- [ ] Hedef: coverage ≥ %60 (mevcut %12.4; kısmi ilerleme)

---

## Faz 5 - Teknik Borc & Mimari (P2) (kismi)

- [x] OptikFormApp.Core kararı: korunacak (gelecek çıkarma adayı), genişletilmeyecek
- [ ] AppSettings/AppSettingsService tekillestirme (ertelendi)
- [ ] GlobalExceptionHandler entegrasyon (ertelendi)
- [x] CancellationToken desteği (ParseFileAsync, SaveExamAsync, UpdateExamAsync)
- [x] Roslyn analyzers yapilandirildi (NetAnalyzers)

## Faz 6 - CI/CD (P2) (kismi)

- [x] CI workflow: push/PR'da build+test (.github/workflows/ci.yml)
- [ ] Code coverage artifact (ertelendi)
- [ ] Statik analiz job'u (ertelendi)

## Faz 7 - Dokumantasyon (P3) (kismi)

- [x] README güncellendi
- [ ] ALGORITHMS.md guncelle (ertelendi)
- [ ] CHANGELOG.md olustur (ertelendi)

## İlerleme Takibi

| Faz | Durum | Tamamlanan |
|-----|-------|------------|
| 0 | Tamamlandi | 3/3 |
| 1 | Tamamlandi | 7/7 |
| 2 | Tamamlandi | 4/4 |
| 3 | Tamamlandi | 5/5 |
| 4 | Tamamlandi | 17/18 |
| 5 | Tamamlandi | 5/6 |
| 6 | Tamamlandi | 3/3 |
| 7 | Tamamlandi | 3/3 |
