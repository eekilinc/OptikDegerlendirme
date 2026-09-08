using System;
using System.Collections.Generic;
using System.Linq;
using OptikFormApp.Models;

namespace OptikFormApp.Services
{
    /// <summary>
    /// YÖK standartlarında Bağıl Değerlendirme Sistemi (BDS), Çan Eğrisi,
    /// T-Skor, Z-Skor ve Mutlak (Katalog) Harf Notu Hesaplama Servisi.
    /// </summary>
    public class GradingService
    {
        public ClassGradingStats CalculateGrades(List<StudentResult> students, GradingSettings? settings = null)
        {
            settings ??= new GradingSettings();
            var stats = new ClassGradingStats
            {
                TotalStudents = students.Count,
                GradeDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "AA", 0 }, { "BA", 0 }, { "BB", 0 }, { "CB", 0 },
                    { "CC", 0 }, { "DC", 0 }, { "DD", 0 }, { "FD", 0 }, { "FF", 0 }
                }
            };

            if (students.Count == 0)
                return stats;

            // Sıralama belirleme (Score desc, NetCount desc)
            var sorted = students.OrderByDescending(s => s.Score).ThenByDescending(s => s.NetCount).ToList();
            int totalCount = sorted.Count;
            for (int i = 0; i < totalCount; i++)
            {
                sorted[i].Rank = i + 1;
                // Yüzdelik dilim: İlk %X içinde
                sorted[i].Percentile = Math.Round((1.0 - ((double)i / totalCount)) * 100, 1);
            }

            stats.HighestScore = sorted.First().Score;
            stats.LowestScore = sorted.Last().Score;

            // Değerlendirme modunu belirle (Mevcut az ise otomatik Mutlak moda geçilebilir)
            bool useRelative = settings.Mode == GradingMode.RelativeEvaluation && students.Count >= settings.MinStudentCountForRelative;

            if (useRelative)
            {
                CalculateRelativeGrading(students, settings, stats);
            }
            else
            {
                CalculateAbsoluteGrading(students, settings, stats);
            }

            // Genel Özet İstatistikleri
            stats.PassedCount = students.Count(s => s.Status == "Geçti");
            stats.ConditionallyPassedCount = students.Count(s => s.Status == "Şartlı Geçti");
            stats.FailedCount = students.Count(s => s.Status == "Kaldı");
            stats.PassRate = students.Count > 0 ? Math.Round(((double)(stats.PassedCount + stats.ConditionallyPassedCount) / students.Count) * 100, 2) : 0;

            foreach (var s in students)
            {
                if (!string.IsNullOrEmpty(s.LetterGrade) && stats.GradeDistribution.ContainsKey(s.LetterGrade))
                {
                    stats.GradeDistribution[s.LetterGrade]++;
                }
            }

            return stats;
        }

        private void CalculateRelativeGrading(List<StudentResult> students, GradingSettings settings, ClassGradingStats stats)
        {
            // 1. Baraj altı (HBN < RelativeThreshold) olanları ayır (Doğrudan FF)
            var excludedStudents = students.Where(s => s.Score < settings.RelativeThreshold).ToList();
            var evaluatedStudents = students.Where(s => s.Score >= settings.RelativeThreshold).ToList();

            stats.ExcludedStudentsCount = excludedStudents.Count;
            stats.EvaluatedStudentsCount = evaluatedStudents.Count;

            foreach (var student in excludedStudents)
            {
                student.LetterGrade = "FF";
                student.GradePoint = 0.0;
                student.Status = "Kaldı";
                student.ZScore = -3.0;
                student.TScore = 20.0;
            }

            if (evaluatedStudents.Count == 0)
            {
                stats.MeanScore = 0;
                stats.StandardDeviation = 0;
                stats.Level = ClassLevel.VeryPoor;
                stats.ClassLevelDescription = "Çok Kötü (Tüm Öğrenciler Baraj Altı)";
                return;
            }

            // 2. Çan eğrisi havuzu için Sınıf Ortalaması (Mean) ve Standart Sapma (Sample StdDev)
            double mean = evaluatedStudents.Average(s => s.Score);
            double sumOfSquares = evaluatedStudents.Sum(s => Math.Pow(s.Score - mean, 2));
            double stdDev = evaluatedStudents.Count > 1 ? Math.Sqrt(sumOfSquares / (evaluatedStudents.Count - 1)) : 0;

            stats.MeanScore = Math.Round(mean, 2);
            stats.StandardDeviation = Math.Round(stdDev, 2);

            // 3. Sınıf Düzeyi Tespiti
            var (classLevel, levelDesc, offset) = GetClassLevelInfo(mean);
            stats.Level = classLevel;
            stats.ClassLevelDescription = levelDesc;

            double safeStdDev = stdDev > 0.001 ? stdDev : 1.0;

            // 4. Öğrencilerin Z ve T Skorlarını hesaplayıp harf notunu ata
            foreach (var student in evaluatedStudents)
            {
                double z = (student.Score - mean) / safeStdDev;
                double t = 50.0 + (10.0 * z);

                student.ZScore = Math.Round(z, 2);
                student.TScore = Math.Round(t, 2);

                var (letter, point, status) = DetermineRelativeGrade(t, offset);
                student.LetterGrade = letter;
                student.GradePoint = point;
                student.Status = status;
            }
        }

        private void CalculateAbsoluteGrading(List<StudentResult> students, GradingSettings settings, ClassGradingStats stats)
        {
            stats.EvaluatedStudentsCount = students.Count;
            stats.ExcludedStudentsCount = 0;

            double mean = students.Average(s => s.Score);
            double sumOfSquares = students.Sum(s => Math.Pow(s.Score - mean, 2));
            double stdDev = students.Count > 1 ? Math.Sqrt(sumOfSquares / (students.Count - 1)) : 0;

            stats.MeanScore = Math.Round(mean, 2);
            stats.StandardDeviation = Math.Round(stdDev, 2);
            stats.Level = ClassLevel.Average;
            stats.ClassLevelDescription = "Mutlak (Katalog) Değerlendirme";

            double safeStdDev = stdDev > 0.001 ? stdDev : 1.0;

            foreach (var student in students)
            {
                double z = (student.Score - mean) / safeStdDev;
                student.ZScore = Math.Round(z, 2);
                student.TScore = Math.Round(50.0 + (10.0 * z), 2);

                var (letter, point, status) = DetermineAbsoluteGrade(student.Score, settings);
                student.LetterGrade = letter;
                student.GradePoint = point;
                student.Status = status;
            }
        }

        public (ClassLevel level, string description, int tOffset) GetClassLevelInfo(double mean)
        {
            if (mean > 70.0)
                return (ClassLevel.Superior, "Üstün Başarılı (X̄ > 70)", 4);
            if (mean > 62.5)
                return (ClassLevel.Excellent, "Mükemmel (62.5 < X̄ ≤ 70)", 3);
            if (mean > 57.5)
                return (ClassLevel.VeryGood, "Çok İyi (57.5 < X̄ ≤ 62.5)", 2);
            if (mean > 52.5)
                return (ClassLevel.Good, "İyi (52.5 < X̄ ≤ 57.5)", 1);
            if (mean > 47.5)
                return (ClassLevel.Average, "Ortalama (47.5 < X̄ ≤ 52.5)", 0);
            if (mean > 42.5)
                return (ClassLevel.Weak, "Zayıf (42.5 < X̄ ≤ 47.5)", -1);
            if (mean > 37.5)
                return (ClassLevel.Poor, "Kötü (37.5 < X̄ ≤ 42.5)", -2);

            return (ClassLevel.VeryPoor, "Çok Kötü (X̄ ≤ 37.5)", -3);
        }

        public (string letter, double point, string status) DetermineRelativeGrade(double t, int offset)
        {
            // Standart Ortalama sınıf için T-Skor aralıkları:
            // AA >= 67, BA >= 62, BB >= 57, CB >= 52, CC >= 47, DC >= 42, DD >= 37, FD >= 32, FF < 32
            // Sınıf düzeyine göre offset uygulanır (Üstün Başarılı: +4, Çok Kötü: -3)
            double aa = 67 + offset;
            double ba = 62 + offset;
            double bb = 57 + offset;
            double cb = 52 + offset;
            double cc = 47 + offset;
            double dc = 42 + offset;
            double dd = 37 + offset;
            double fd = 32 + offset;

            if (t >= aa) return ("AA", 4.0, "Geçti");
            if (t >= ba) return ("BA", 3.5, "Geçti");
            if (t >= bb) return ("BB", 3.0, "Geçti");
            if (t >= cb) return ("CB", 2.5, "Geçti");
            if (t >= cc) return ("CC", 2.0, "Geçti");
            if (t >= dc) return ("DC", 1.5, "Şartlı Geçti");
            if (t >= dd) return ("DD", 1.0, "Şartlı Geçti");
            if (t >= fd) return ("FD", 0.5, "Kaldı");
            return ("FF", 0.0, "Kaldı");
        }

        public (string letter, double point, string status) DetermineAbsoluteGrade(double score, GradingSettings settings)
        {
            if (score >= settings.AbsoluteAaCutoff) return ("AA", 4.0, "Geçti");
            if (score >= settings.AbsoluteBaCutoff) return ("BA", 3.5, "Geçti");
            if (score >= settings.AbsoluteBbCutoff) return ("BB", 3.0, "Geçti");
            if (score >= settings.AbsoluteCbCutoff) return ("CB", 2.5, "Geçti");
            if (score >= settings.AbsoluteCcCutoff) return ("CC", 2.0, "Geçti");
            if (score >= settings.AbsoluteDcCutoff) return ("DC", 1.5, "Şartlı Geçti");
            if (score >= settings.AbsoluteDdCutoff) return ("DD", 1.0, "Şartlı Geçti");
            if (score >= settings.AbsoluteFdCutoff) return ("FD", 0.5, "Kaldı");
            return ("FF", 0.0, "Kaldı");
        }
    }
}
