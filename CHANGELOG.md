# Changelog

## [Unreleased] - Teknik İyileştirme
### Güvenlik
- Zip slip koruması (BackupService.SafeExtractZip)
- DB null-safety ve JSON deserialize güvenliği
- JSON import boyut limiti (50 MB)
- VersionCheckService response boyut limiti (1 MB)
- SetUserVersionAsync doğrulama (0..100000)

### Performans
- SoftwareOnly render kaldırıldı
- Hot-path GC.Collect kaldırıldı
- CalculateStatistics tek geçiş optimizasyonu

### Doğruluk
- BulkUpdateScoresAsync fabricate count kaldırıldı
- ReconnectAsync kaynak sızıntısı giderildi

### Test
- 44 test (önceki 36 + 8 yeni)
- DatabaseServiceTests, JsonDataServiceTests, VersionCheckServiceTests, GradingEdgeCaseTests, UndoRedoManagerTests, FuzzySearchServiceTests, AppSettingsServiceTests

### Altyapı
- NetAnalyzers, CI workflow, AGENT.md, PLAN.md