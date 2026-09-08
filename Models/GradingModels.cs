using System;
using System.Collections.Generic;

namespace OptikFormApp.Models
{
    public enum GradingMode
    {
        RelativeEvaluation = 0, // Bağıl Değerlendirme Sistemi (Çan Eğrisi / T-Skor / YÖK Standartları)
        AbsoluteGrading = 1      // Mutlak / Katalog Değerlendirme (Doğrudan Puan Aralıkları)
    }

    public enum CancellationMode
    {
        FullCredit = 0,             // Herkese Tam Doğru (1 Puan)
        DistributeToRemaining = 1,  // Sınavdan Çıkar, Puanı Kalan Sorulara Dağıt
        MarkAsEmpty = 2             // Herkes İçin Boş Say
    }

    public enum ClassLevel
    {
        Superior = 0,   // Üstün Başarılı (> 70.0)
        Excellent = 1,  // Mükemmel (62.5 - 70.0)
        VeryGood = 2,   // Çok İyi (57.5 - 62.5)
        Good = 3,       // İyi (52.5 - 57.5)
        Average = 4,    // Ortalama (47.5 - 52.5)
        Weak = 5,       // Zayıf (42.5 - 47.5)
        Poor = 6,       // Kötü (37.5 - 42.5)
        VeryPoor = 7    // Çok Kötü (<= 37.5)
    }

    public class GradingSettings
    {
        public GradingMode Mode { get; set; } = GradingMode.RelativeEvaluation;

        /// <summary>
        /// Bağıl değerlendirmeye katılım için Ham Başarı Notu (HBN) alt barajı (varsayılan: 20.0).
        /// HBN bu değerin altında olanlar çan eğrisi ortalamasına dahil edilmez ve doğrudan FF alır.
        /// </summary>
        public double RelativeThreshold { get; set; } = 20.0;

        /// <summary>
        /// Mutlak değerlendirme veya asgari geçme notu barajı.
        /// </summary>
        public double PassingGrade { get; set; } = 50.0;

        /// <summary>
        /// Bağıl değerlendirme için asgari öğrenci mevcudu. Mevcut bunun altındaysa otomatik mutlak moda geçilir.
        /// </summary>
        public int MinStudentCountForRelative { get; set; } = 15;

        // Mutlak Katalog Not Aralıkları
        public double AbsoluteAaCutoff { get; set; } = 90.0;
        public double AbsoluteBaCutoff { get; set; } = 85.0;
        public double AbsoluteBbCutoff { get; set; } = 80.0;
        public double AbsoluteCbCutoff { get; set; } = 75.0;
        public double AbsoluteCcCutoff { get; set; } = 70.0;
        public double AbsoluteDcCutoff { get; set; } = 65.0;
        public double AbsoluteDdCutoff { get; set; } = 60.0;
        public double AbsoluteFdCutoff { get; set; } = 50.0;
    }

    public class ClassGradingStats
    {
        public int TotalStudents { get; set; }
        public int EvaluatedStudentsCount { get; set; }
        public int ExcludedStudentsCount { get; set; }
        public double MeanScore { get; set; }
        public double StandardDeviation { get; set; }
        public double HighestScore { get; set; }
        public double LowestScore { get; set; }
        public ClassLevel Level { get; set; } = ClassLevel.Average;
        public string ClassLevelDescription { get; set; } = string.Empty;
        public int PassedCount { get; set; }
        public int ConditionallyPassedCount { get; set; }
        public int FailedCount { get; set; }
        public double PassRate { get; set; } // %
        public Dictionary<string, int> GradeDistribution { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
