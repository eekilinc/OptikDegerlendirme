using System;
using System.Collections.Generic;
using System.Linq;
using OptikFormApp.Models;

namespace OptikFormApp.Services
{
    public class StatisticsReportService
    {
        public class ClassStatistics
        {
            public double AverageScore { get; set; }
            public double MedianScore { get; set; }
            public string ModeScoreText { get; set; } = "-";
            public double StandardDeviation { get; set; }
            public double Variance { get; set; }
            public double MinScore { get; set; }
            public double MaxScore { get; set; }
            public double Range { get; set; }
            public double InterquartileRange { get; set; }
            public double VariationCoefficient { get; set; }
            public string HomogeneityInterpretation { get; set; } = "Normal";
            public double Skewness { get; set; }
            public string SkewnessInterpretation { get; set; } = "Simetrik Dağılım";
            public double Kurtosis { get; set; }
            public string KurtosisInterpretation { get; set; } = "Normal Dağılım (Mezokurtik)";
            public double StandardErrorOfMeasurement { get; set; }
            public string ConfidenceInterval68 { get; set; } = "-";
            public string ConfidenceInterval95 { get; set; } = "-";
            public double ReliabilityKR20 { get; set; }
            public double CronbachAlpha { get; set; }
            public double SuccessRate { get; set; }
            public int TotalStudents { get; set; }
            public int PassedCount { get; set; }
            public int FailedCount { get; set; }
            public string PedagogicalEvaluation { get; set; } = "";
        }

        public class ScoreDistribution
        {
            public string Range { get; set; } = "";
            public int Count { get; set; }
            public double Percentage { get; set; }
        }

        public class TrendAnalysis
        {
            public string Metric { get; set; } = "";
            public double CurrentValue { get; set; }
            public double PreviousValue { get; set; }
            public double Change { get; set; }
            public string Trend { get; set; } = "";
        }

        public class QuestionCorrelation
        {
            public int Question1 { get; set; }
            public int Question2 { get; set; }
            public double Correlation { get; set; }
            public string Relationship { get; set; } = "";
        }

        public ClassStatistics CalculateClassStatistics(List<StudentResult> students, double passingScore = 50, double kr20 = 0, double cronbachAlpha = 0)
        {
            if (students == null || students.Count == 0)
                return new ClassStatistics();

            var scores = students.Select(s => s.Score).OrderBy(s => s).ToList();
            int count = scores.Count;
            var avg = scores.Average();
            var min = scores.Min();
            var max = scores.Max();
            var range = max - min;
            var passed = scores.Count(s => s >= passingScore);

            // Varyans ve Standart Sapma
            double variance;
            if (count > 1)
                variance = scores.Sum(s => Math.Pow(s - avg, 2)) / (count - 1);
            else
                variance = 0;
            var stdDev = Math.Sqrt(variance);

            // Medyan
            double median;
            if (count % 2 == 0)
                median = (scores[count / 2 - 1] + scores[count / 2]) / 2.0;
            else
                median = scores[count / 2];

            // Mod (Tepe Değer)
            var modeGroup = scores
                .GroupBy(s => Math.Round(s, 1))
                .Select(g => new { Score = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            string modeText = "-";
            if (modeGroup.Count > 0)
            {
                var maxFrequency = modeGroup.First().Count;
                if (maxFrequency > 1)
                {
                    var modes = modeGroup.Where(m => m.Count == maxFrequency).Select(m => m.Score.ToString("F1")).Take(3);
                    modeText = string.Join(", ", modes);
                }
                else
                {
                    modeText = "Tekrar Yok (Mod Yok)";
                }
            }

            // Çeyrekler Açıklığı (IQR = Q3 - Q1)
            double q1 = scores[(int)(count * 0.25)];
            double q3 = scores[(int)(count * 0.75)];
            double iqr = Math.Round(q3 - q1, 2);

            // Bağıl Değişkenlik / Varyasyon Katsayısı V = (S / X) * 100
            double variationCoeff = avg > 0 ? (stdDev / avg) * 100 : 0;
            string homogeneity = "Normal Değişkenlik";
            if (variationCoeff < 20)
                homogeneity = "Homojen Grup (Puanlar Birbirine Çok Yakın)";
            else if (variationCoeff > 25)
                homogeneity = "Heterojen Grup (Geniş Başarı Yelpazesi)";

            // Çarpıklık Katsayısı (Skewness)
            double skewness = CalculateSkewness(scores, avg, stdDev);
            string skewnessText = "Simetrik Dağılım (Dengeli Sınav)";
            if (skewness > 0.5)
                skewnessText = "Sağa Çarpık (Sınav Zor / Düşük Puan Yığılması)";
            else if (skewness < -0.5)
                skewnessText = "Sola Çarpık (Sınav Kolay / Yüksek Puan Yığılması)";

            // Basıklık Katsayısı (Kurtosis)
            double kurtosis = CalculateKurtosis(scores, avg, stdDev);
            string kurtosisText = "Normal Dağılım (Mezokurtik)";
            if (kurtosis > 0.5)
                kurtosisText = "Sivri Dağılım (Leptokurtik - Ortalamada Yoğun)";
            else if (kurtosis < -0.5)
                kurtosisText = "Basık Dağılım (Platikurtik - Yayvan Dağılım)";

            // Ölçmenin Standart Hatası (SeM = S * sqrt(1 - r))
            double effectiveReliability = kr20 > 0 ? kr20 : (cronbachAlpha > 0 ? cronbachAlpha : 0.75);
            if (effectiveReliability > 1.0) effectiveReliability = 0.99;
            if (effectiveReliability < 0) effectiveReliability = 0;

            double sem = Math.Round(stdDev * Math.Sqrt(Math.Max(0, 1 - effectiveReliability)), 2);
            string ci68 = $"[{Math.Max(0, avg - sem):F1} - {Math.Min(100, avg + sem):F1}] (±{sem:F1})";
            string ci95 = $"[{Math.Max(0, avg - 1.96 * sem):F1} - {Math.Min(100, avg + 1.96 * sem):F1}] (±{(1.96 * sem):F1})";

            // Akıllı Pedagojik Sınav Değerlendirme Özeti
            string pedagogical = GeneratePedagogicalEvaluation(
                avg, median, stdDev, skewness, kurtosis, variationCoeff, effectiveReliability, (double)passed / count * 100);

            return new ClassStatistics
            {
                AverageScore = Math.Round(avg, 2),
                MedianScore = Math.Round(median, 2),
                ModeScoreText = modeText,
                StandardDeviation = Math.Round(stdDev, 2),
                Variance = Math.Round(variance, 2),
                MinScore = min,
                MaxScore = max,
                Range = Math.Round(range, 2),
                InterquartileRange = iqr,
                VariationCoefficient = Math.Round(variationCoeff, 2),
                HomogeneityInterpretation = homogeneity,
                Skewness = Math.Round(skewness, 2),
                SkewnessInterpretation = skewnessText,
                Kurtosis = Math.Round(kurtosis, 2),
                KurtosisInterpretation = kurtosisText,
                StandardErrorOfMeasurement = sem,
                ConfidenceInterval68 = ci68,
                ConfidenceInterval95 = ci95,
                ReliabilityKR20 = Math.Round(kr20, 2),
                CronbachAlpha = Math.Round(cronbachAlpha, 2),
                SuccessRate = Math.Round((double)passed / count * 100, 2),
                TotalStudents = count,
                PassedCount = passed,
                FailedCount = count - passed,
                PedagogicalEvaluation = pedagogical
            };
        }

        private double CalculateSkewness(List<double> values, double mean, double stdDev)
        {
            int n = values.Count;
            if (n < 3 || stdDev <= 0.0001) return 0;

            double m3 = values.Sum(x => Math.Pow(x - mean, 3)) / n;
            double s3 = Math.Pow(stdDev, 3);
            return m3 / s3;
        }

        private double CalculateKurtosis(List<double> values, double mean, double stdDev)
        {
            int n = values.Count;
            if (n < 4 || stdDev <= 0.0001) return 0;

            double m4 = values.Sum(x => Math.Pow(x - mean, 4)) / n;
            double s4 = Math.Pow(stdDev, 4);
            return (m4 / s4) - 3.0; // Excess kurtosis
        }

        private string GeneratePedagogicalEvaluation(
            double avg, double median, double stdDev, double skewness, double kurtosis, 
            double variationCoeff, double reliability, double passRate)
        {
            var parts = new List<string>();

            // 1. Güvenilirlik Yorumu
            if (reliability >= 0.80)
                parts.Add($"🛡️ Test Yüksek Güvenilirlikte (KR-20/Alfa: {reliability:F2}): Ölçme sonuçları oldukça kararlı ve tutarlıdır.");
            else if (reliability >= 0.65)
                parts.Add($"🛡️ Test Kabul Edilebilir Güvenilirlikte ({reliability:F2}): Genel hatlarıyla kullanılabilir, ancak soru sayısının artırılması veya çeldirici düzenlemesi güvenilirliği daha da pekiştirecektir.");
            else
                parts.Add($"⚠️ Test Güvenilirliği Düşük ({reliability:F2}): Soruların ayırt edicilik ve zorluk derecelerinin revize edilmesi önerilir.");

            // 2. Zorluk ve Dağılım Şekli Yorumu
            if (skewness > 0.6)
                parts.Add("📉 Dağılım Sağa Çarpık (Pozitif): Sorular sınıf seviyesine göre zor gelmiş ve öğrenci notları düşük puanlarda yığılmıştır.");
            else if (skewness < -0.6)
                parts.Add("📈 Dağılım Sola Çarpık (Negatif): Sorular sınıf için oldukça kolay gelmiş ve yüksek puanlarda yığılma görülmektedir.");
            else
                parts.Add("⚖️ Dağılım Simetrik ve Dengeli: Notlar çan eğrisi standardına uygun şekilde ortalama etrafında dengeli dağılmıştır.");

            // 3. Grup Homojenliği Yorumu
            if (variationCoeff < 20)
                parts.Add($"👥 Grup Homojen Yapıda (Varyasyon: %{variationCoeff:F1}): Öğrencilerin başarı düzeyleri birbirine çok yakındır.");
            else if (variationCoeff > 25)
                parts.Add($"👥 Grup Heterojen Yapıda (Varyasyon: %{variationCoeff:F1}): Sınıf içi bireysel farklılıklar belirgindir, geniş bir başarı yelpazesi mevcuttur.");
            else
                parts.Add($"👥 Grup Değişkenliği Normal (Varyasyon: %{variationCoeff:F1}): Standart sınıf başarı dağılımı mevcuttur.");

            // 4. Genel Başarı
            parts.Add($"🎯 Başarı Durumu: Sınıf başarı ortalaması %{passRate:F1}, aritmetik ortalama {avg:F1}, medyan {median:F1} olarak gerçekleşmiştir.");

            return string.Join("\n\n", parts);
        }

        public List<ScoreDistribution> GetScoreDistribution(List<StudentResult> students, int binCount = 10)
        {
            if (students == null || students.Count == 0)
                return new List<ScoreDistribution>();

            var distribution = new List<ScoreDistribution>();
            int total = students.Count;

            int step = 100 / binCount;
            for (int i = 0; i < binCount; i++)
            {
                int min = i * step;
                int max = (i + 1) * step;
                int count = students.Count(s => (s.Score >= min && s.Score < max) || (i == binCount - 1 && s.Score >= min && s.Score <= max));

                distribution.Add(new ScoreDistribution
                {
                    Range = $"{min}-{max}",
                    Count = count,
                    Percentage = total > 0 ? Math.Round((double)count / total * 100, 1) : 0
                });
            }

            return distribution;
        }

        public List<QuestionCorrelation> AnalyzeQuestionCorrelations(List<StudentResult> students, int maxQuestions = 50)
        {
            var correlations = new List<QuestionCorrelation>();
            if (students == null || students.Count < 5) return correlations;

            var questionCount = Math.Min(students.First().QuestionResults.Count, maxQuestions);
            if (questionCount < 2) return correlations;

            var random = new Random(42);
            var samplePairs = new HashSet<(int, int)>();

            int maxPairs = Math.Min(25, questionCount * (questionCount - 1) / 2);
            int attempts = 0;
            while (samplePairs.Count < maxPairs && attempts < 200)
            {
                attempts++;
                var q1 = random.Next(questionCount);
                var q2 = random.Next(questionCount);
                if (q1 != q2)
                {
                    var pair = q1 < q2 ? (q1, q2) : (q2, q1);
                    samplePairs.Add(pair);
                }
            }

            foreach (var (q1, q2) in samplePairs)
            {
                var corr = CalculateCorrelation(students, q1, q2);
                string rel;
                if (Math.Abs(corr) < 0.25) rel = "Zayıf İlişki";
                else if (Math.Abs(corr) < 0.6) rel = "Orta Düzey İlişki";
                else rel = "Güçlü İlişki ⭐";

                correlations.Add(new QuestionCorrelation
                {
                    Question1 = q1 + 1,
                    Question2 = q2 + 1,
                    Correlation = Math.Round(corr, 3),
                    Relationship = rel
                });
            }

            return correlations.OrderByDescending(c => Math.Abs(c.Correlation)).ToList();
        }

        private double CalculateCorrelation(List<StudentResult> students, int q1, int q2)
        {
            if (students == null || students.Count == 0) return 0;
            var firstStudent = students.First();
            if (firstStudent.QuestionResults == null || 
                q1 < 0 || q1 >= firstStudent.QuestionResults.Count ||
                q2 < 0 || q2 >= firstStudent.QuestionResults.Count)
                return 0;

            var x = students.Select(s => s.QuestionResults != null && q1 < s.QuestionResults.Count && s.QuestionResults[q1] ? 1.0 : 0.0).ToList();
            var y = students.Select(s => s.QuestionResults != null && q2 < s.QuestionResults.Count && s.QuestionResults[q2] ? 1.0 : 0.0).ToList();

            var n = x.Count;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (a, b) => a * b).Sum();
            var sumX2 = x.Sum(v => v * v);
            var sumY2 = y.Sum(v => v * v);

            var numerator = n * sumXY - sumX * sumY;
            var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));

            return denominator == 0 ? 0 : numerator / denominator;
        }

        public Dictionary<string, double> CalculatePercentiles(List<StudentResult> students)
        {
            var percentiles = new Dictionary<string, double>();
            if (students == null || students.Count == 0) return percentiles;

            var sorted = students.Select(s => s.Score).OrderBy(s => s).ToList();
            var count = sorted.Count;

            percentiles["%10 (Alt Sınır)"] = sorted[(int)(count * 0.1)];
            percentiles["%25 (Q1 - Alt Çeyrek)"] = sorted[(int)(count * 0.25)];
            percentiles["%50 (Medyan - Ortanca)"] = sorted[(int)(count * 0.5)];
            percentiles["%75 (Q3 - Üst Çeyrek)"] = sorted[(int)(count * 0.75)];
            percentiles["%90 (Üst Sınır)"] = sorted[(int)Math.Min(count - 1, count * 0.9)];

            return percentiles;
        }

        public string GenerateReportSummary(List<StudentResult> students, string examName = "")
        {
            if (students == null || students.Count == 0)
                return "Veri yok.";

            var stats = CalculateClassStatistics(students);
            var dist = GetScoreDistribution(students, 10);
            var percentiles = CalculatePercentiles(students);

            var report = $"""
========================================================================
            AĞLASUN MYO - DETAYLI SINAV İSTATİSTİK RAPORU
========================================================================
Sınav Adı        : {examName}
Rapor Tarihi     : {DateTime.Now:dd.MM.yyyy HH:mm:ss}
Sınava Giren Sayı: {stats.TotalStudents} Öğrenci

[ 1. MERKEZİ EĞİLİM VE YAYILMA ÖLÇÜLERİ ]
------------------------------------------------------------------------
• Aritmetik Ortalama     : {stats.AverageScore:F2}
• Medyan (Ortanca)       : {stats.MedianScore:F2}
• Mod (Tepe Değer)       : {stats.ModeScoreText}
• Standart Sapma         : {stats.StandardDeviation:F2}
• Varyans                : {stats.Variance:F2}
• En Düşük / En Yüksek   : {stats.MinScore:F1} / {stats.MaxScore:F1}
• Puan Açıklığı (Ranj)   : {stats.Range:F1}
• Çeyrekler Açıklığı(IQR): {stats.InterquartileRange:F1}
• Bağıl Değişkenlik (V)  : %{stats.VariationCoefficient:F1} -> {stats.HomogeneityInterpretation}

[ 2. DAĞILIM BİÇİMİ VE GÜVENİLİRLİK ]
------------------------------------------------------------------------
• Çarpıklık (Skewness)   : {stats.Skewness:F2} ({stats.SkewnessInterpretation})
• Basıklık (Kurtosis)    : {stats.Kurtosis:F2} ({stats.KurtosisInterpretation})
• Ölçmenin Std. Hatası   : ±{stats.StandardErrorOfMeasurement:F2} Puan
• %68 Güven Aralığı      : {stats.ConfidenceInterval68}
• %95 Güven Aralığı      : {stats.ConfidenceInterval95}

[ 3. BAŞARI VE YÜZDELİKLER ]
------------------------------------------------------------------------
• Genel Başarı Oranı     : %{stats.SuccessRate:F1}
• Başarılı / Başarısız   : {stats.PassedCount} Geçti / {stats.FailedCount} Kaldı
• %10 Dilimi (Alt)       : {percentiles.GetValueOrDefault("%10 (Alt Sınır)", 0):F1}
• %25 Dilimi (Q1)        : {percentiles.GetValueOrDefault("%25 (Q1 - Alt Çeyrek)", 0):F1}
• %50 Dilimi (Medyan)    : {percentiles.GetValueOrDefault("%50 (Medyan - Ortanca)", 0):F1}
• %75 Dilimi (Q3)        : {percentiles.GetValueOrDefault("%75 (Q3 - Üst Çeyrek)", 0):F1}
• %90 Dilimi (Üst)       : {percentiles.GetValueOrDefault("%90 (Üst Sınır)", 0):F1}

[ 4. AKILLI PEDAGOJİK DEĞERLENDİRME ]
------------------------------------------------------------------------
{stats.PedagogicalEvaluation}

[ 5. PUAN ARALIKLARI FREKANS DAĞILIMI ]
------------------------------------------------------------------------
""";

            foreach (var d in dist)
            {
                var bar = new string('█', Math.Min(30, (int)Math.Round(d.Percentage / 3.3)));
                report += $"\n{d.Range,-10} : {d.Count,3} öğrenci (%{d.Percentage,4:F1}) {bar}";
            }

            report += "\n========================================================================";
            return report;
        }
    }
}
