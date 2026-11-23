using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OptikFormApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;
using QuestPDF;
using SkiaSharp;

namespace OptikFormApp.Services
{
    public class ExamReportMetadata
    {
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string ExamTitle { get; set; } = "";
        public string AcademicTerm { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public double PassingScore { get; set; } = 50;
        public double WrongDeductionFactor { get; set; } = 0.25;
        public List<string> CancelledQuestions { get; set; } = new();
        public List<string> MultiCorrectQuestions { get; set; } = new();

        public string DeductionRuleText => WrongDeductionFactor switch
        {
            <= 0.001 => "Yanlış Götürme Yok (0)",
            >= 0.30 and <= 0.36 => "3 Yanlış 1 Doğruyu Götürür (0.33)",
            >= 0.24 and <= 0.26 => "4 Yanlış 1 Doğruyu Götürür (0.25)",
            _ => $"Özel Katsayı ({WrongDeductionFactor:F2})"
        };

        public static string GetCurrentAcademicTerm()
        {
            var now = DateTime.Now;
            return now.Month switch
            {
                >= 9 => $"{now.Year}-{now.Year + 1} Güz Dönemi",
                1 => $"{now.Year - 1}-{now.Year} Güz Dönemi",
                >= 2 and <= 6 => $"{now.Year - 1}-{now.Year} Bahar Dönemi",
                _ => $"{now.Year - 1}-{now.Year} Yaz Okulu"
            };
        }
    }

    public class PdfReportService
    {
        public record PdfProgressReport(
            int CompletedCount,
            int TotalCount,
            string CurrentStudentName,
            double Percentage
        );

        /// <summary>
        /// Senkron PDF üretimi (mevcut metod - geriye uyumluluk için)
        /// </summary>
        public void GenerateStudentReports(
            List<StudentResult> targetStudents, 
            List<StudentResult> allStudents, 
            IEnumerable<LearningOutcome> outcomes, 
            string outputFolder, 
            string schoolName = "Okul Adı")
        {
            GenerateStudentReportsAsync(targetStudents, allStudents, outcomes, outputFolder, schoolName, null, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asenkron paralel PDF üretimi - Progress reporting ve Cancellation desteği ile
        /// </summary>
        public async Task GenerateStudentReportsAsync(
            List<StudentResult> targetStudents,
            List<StudentResult> allStudents,
            IEnumerable<LearningOutcome> outcomes,
            string outputFolder,
            string schoolName = "Okul Adı",
            IProgress<PdfProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (targetStudents == null || targetStudents.Count == 0)
                throw new ArgumentException("En az bir öğrenci gereklidir.", nameof(targetStudents));

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            double examAverage = allStudents?.Count > 0 ? allStudents.Average(x => x.Score) : 0;
            double examNetAverage = allStudents?.Count > 0 ? allStudents.Average(x => x.NetCount) : 0;
            int totalStudents = allStudents?.Count ?? 0;
            var outcomesList = outcomes?.ToList() ?? new List<LearningOutcome>();

            int completedCount = 0;
            int totalCount = targetStudents.Count;
            var progressLock = new object();

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount),
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(targetStudents, options, async (student, ct) =>
            {
                string safeName = string.Join("_", student.FullName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(outputFolder, $"Karne_{student.StudentId}_{safeName}.pdf");

                await Task.Run(() => GenerateSingleStudentPdf(
                    student, outcomesList, schoolName, examAverage, examNetAverage, totalStudents, filePath
                ), ct);

                lock (progressLock)
                {
                    completedCount++;
                    progress?.Report(new PdfProgressReport(
                        completedCount, totalCount, student.FullName, (double)completedCount / totalCount * 100));
                }
            });
        }

        private void GenerateSingleStudentPdf(
            StudentResult student, List<LearningOutcome> outcomes, string schoolName,
            double examAverage, double examNetAverage, int totalStudents, string filePath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor("#ffffff");
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.TimesNewRoman));

                    page.Header().Column(hdr =>
                    {
                        hdr.Item().Height(4).Background("#1e3a5f");
                        hdr.Item().PaddingTop(1).Height(1.5f).Background("#3b82f6");
                        hdr.Item().PaddingTop(10).PaddingBottom(8).BorderBottom(1).BorderColor("#e2e8f0").Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(string.IsNullOrWhiteSpace(schoolName) ? "AĞLASUN MESLEK YÜKSEKOKULU" : schoolName.ToUpper()).FontSize(10).SemiBold().FontColor("#475569");
                                col.Item().Text("BİREYSEL SINAV SONUÇ KARNESİ").FontSize(16).Bold().FontColor("#1e3a5f");
                            });
                            row.ConstantItem(130).AlignRight().AlignMiddle().Column(col =>
                            {
                                col.Item().AlignRight().Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").FontSize(8.5f).FontColor("#64748b");
                                col.Item().AlignRight().Text($"Sıralama: #{student.Rank} / {totalStudents}").FontSize(9.5f).Bold().FontColor("#1d4ed8");
                            });
                        });
                    });

                    page.Content().PaddingVertical(12).Column(x =>
                    {
                        x.Spacing(14);

                        // Öğrenci Kimlik Kartı
                        x.Item().Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1f);
                            });

                            table.Cell().PaddingVertical(2).Text(t => { t.Span("Ad Soyad: ").SemiBold().FontColor("#475569"); t.Span(student.FullName).Bold().FontColor("#1e293b"); });
                            table.Cell().PaddingVertical(2).Text(t => { t.Span("Öğrenci No: ").SemiBold().FontColor("#475569"); t.Span(student.StudentId).Bold().FontColor("#1e293b"); });
                            table.Cell().PaddingVertical(2).Text(t => { t.Span("Kitapçık Türü: ").SemiBold().FontColor("#475569"); t.Span(string.IsNullOrWhiteSpace(student.BookletType) ? "-" : student.BookletType).Bold().FontColor("#7c3aed"); });
                            table.Cell().PaddingVertical(2).Text(t => { t.Span("Sınıf Ortalaması: ").SemiBold().FontColor("#475569"); t.Span($"{examAverage:F2} Puan ({examNetAverage:F2} Net)").FontColor("#1e293b"); });
                        });

                        // 4'lü Özet KPI Kartları
                        x.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Border(1).BorderColor("#bbf7d0").Background("#f0fdf4").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("DOĞRU").FontSize(8).Bold().FontColor("#166534");
                                c.Item().AlignCenter().Text(student.CorrectCount.ToString()).FontSize(18).Bold().FontColor("#15803d");
                            });
                            row.RelativeItem().Border(1).BorderColor("#fecaca").Background("#fef2f2").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("YANLIŞ").FontSize(8).Bold().FontColor("#991b1b");
                                c.Item().AlignCenter().Text(student.IncorrectCount.ToString()).FontSize(18).Bold().FontColor("#dc2626");
                            });
                            row.RelativeItem().Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("BOŞ").FontSize(8).Bold().FontColor("#475569");
                                c.Item().AlignCenter().Text(student.EmptyCount.ToString()).FontSize(18).Bold().FontColor("#64748b");
                            });
                            row.RelativeItem().Border(1).BorderColor("#bfdbfe").Background("#eff6ff").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("PUAN / NET").FontSize(8).Bold().FontColor("#1e40af");
                                c.Item().AlignCenter().Text($"{student.Score:F2}").FontSize(18).Bold().FontColor("#1d4ed8");
                                c.Item().AlignCenter().Text($"{student.NetCount:F2} Net").FontSize(8).FontColor("#3b82f6");
                            });
                        });

                        // Vektör Performans Karşılaştırma Grafiği (Öğrenci vs Sınıf Ortalaması)
                        x.Item().Border(1).BorderColor("#e2e8f0").Background("#ffffff").Padding(10).Column(comp =>
                        {
                            double diff = student.Score - examAverage;
                            string diffText = diff >= 0 ? $"Sınıf ortalamasının +{diff:F2} puan üzerinde" : $"Sınıf ortalamasının {diff:F2} puan altında";
                            comp.Item().PaddingBottom(6).Row(r =>
                            {
                                r.RelativeItem().Text("SINIF ORTALAMASI İLE KARŞILAŞTIRMA").FontSize(9).Bold().FontColor("#1e3a5f");
                                r.AutoItem().Text(diffText).FontSize(8.5f).Bold().FontColor(diff >= 0 ? "#15803d" : "#b91c1c");
                            });
                            comp.Item().Height(46).Canvas((canvas, size) =>
                            {
                                float labelW = 85f;
                                float barAreaW = Math.Max(40f, size.Width - labelW - 45f);
                                using var track = new SKPaint { Color = SKColor.Parse("#e2e8f0"), IsAntialias = true };
                                using var txt = new SKPaint { Color = SKColor.Parse("#334155"), TextSize = 8.5f, IsAntialias = true, FakeBoldText = true };
                                using var valTxt = new SKPaint { Color = SKColor.Parse("#1e293b"), TextSize = 8.5f, IsAntialias = true, FakeBoldText = true };

                                // Satır 1: Öğrenci Puanı
                                canvas.DrawText("Öğrenci Puanı", 0, 14, txt);
                                canvas.DrawRoundRect(new SKRect(labelW, 5, labelW + barAreaW, 17), 3, 3, track);
                                using var stuPaint = new SKPaint { Color = SKColor.Parse(student.Score >= examAverage ? "#15803d" : "#2563eb"), IsAntialias = true };
                                float stuW = Math.Max(3f, (float)(barAreaW * Math.Clamp(student.Score / 100.0, 0, 1)));
                                canvas.DrawRoundRect(new SKRect(labelW, 5, labelW + stuW, 17), 3, 3, stuPaint);
                                canvas.DrawText($"{student.Score:F1}", labelW + barAreaW + 6, 14, valTxt);

                                // Satır 2: Sınıf Ortalaması
                                canvas.DrawText("Sınıf Ortalaması", 0, 36, txt);
                                canvas.DrawRoundRect(new SKRect(labelW, 27, labelW + barAreaW, 39), 3, 3, track);
                                using var avgPaint = new SKPaint { Color = SKColor.Parse("#7c3aed"), IsAntialias = true };
                                float avgW = Math.Max(3f, (float)(barAreaW * Math.Clamp(examAverage / 100.0, 0, 1)));
                                canvas.DrawRoundRect(new SKRect(labelW, 27, labelW + avgW, 39), 3, 3, avgPaint);
                                canvas.DrawText($"{examAverage:F1}", labelW + barAreaW + 6, 36, valTxt);
                            });
                        });

                        // Cevap Anahtarı Karşılaştırma Izgarası
                        x.Item().Background("#1e3a5f").Padding(6).Text("SORU BAZLI CEVAP DÖKÜMÜ").FontSize(9.5f).Bold().FontColor("#ffffff");
                        x.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                for (int i = 0; i < 10; i++) columns.RelativeColumn();
                            });

                            for (int i = 0; i < student.ColoredAnswers.Count; i++)
                            {
                                var ans = student.ColoredAnswers[i];
                                string cellBg = ans.State == AnswerState.Correct ? "#f0fdf4" :
                                                ans.State == AnswerState.Incorrect ? "#fef2f2" : "#f8fafc";
                                string fg = ans.State == AnswerState.Correct ? "#15803d" :
                                            ans.State == AnswerState.Incorrect ? "#dc2626" : "#94a3b8";

                                table.Cell().Background(cellBg).Border(0.75f).BorderColor("#e2e8f0").Padding(3).AlignCenter().Column(c =>
                                {
                                    c.Item().AlignCenter().Text((i + 1).ToString()).FontSize(7).FontColor("#64748b");
                                    c.Item().AlignCenter().Text(ans.Character.ToString()).FontSize(9.5f).Bold().FontColor(fg);
                                });
                            }
                        });

                        // Konu / Öğrenim Çıktısı Bazlı Başarı
                        if (outcomes?.Count > 0)
                        {
                            var studentOutcomes = outcomes.Where(o =>
                                string.Equals(o.BookletName, student.BookletType, StringComparison.OrdinalIgnoreCase)).ToList();

                            if (studentOutcomes.Count > 0)
                            {
                                x.Item().PaddingTop(6).Background("#1e3a5f").Padding(6).Text("KONU / ÖĞRENİM ÇIKTISI BAZLI BAŞARI ANALİZİ").FontSize(9.5f).Bold().FontColor("#ffffff");
                                x.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3);
                                        cols.ConstantColumn(45);
                                        cols.ConstantColumn(55);
                                        cols.ConstantColumn(80);
                                        cols.ConstantColumn(55);
                                        cols.ConstantColumn(55);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background("#f1f5f9").Padding(4).Text("Konu / Kazanım").Bold().FontSize(8);
                                        header.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Soru").Bold().FontSize(8);
                                        header.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Doğru").Bold().FontSize(8);
                                        header.Cell().Background("#f1f5f9").Padding(4).Text("Grafik").Bold().FontSize(8);
                                        header.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Başarı").Bold().FontSize(8);
                                        header.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Sınıf").Bold().FontSize(8);
                                    });

                                    foreach (var topic in studentOutcomes)
                                    {
                                        var questions = topic.QuestionNumbers;
                                        int topicQCount = questions.Count;
                                        int topicStdCorrect = 0;

                                        foreach (var q in questions)
                                        {
                                            if (q - 1 >= 0 && q - 1 < student.QuestionResults.Count && student.QuestionResults[q - 1])
                                                topicStdCorrect++;
                                        }

                                        double topicSuccess = topicQCount > 0 ? ((double)topicStdCorrect / topicQCount) * 100 : 0;
                                        string barHex = topicSuccess >= 60 ? "#15803d" : topicSuccess >= 40 ? "#ca8a04" : "#dc2626";

                                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(topic.Name).FontSize(8.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(topicQCount.ToString()).FontSize(8.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(topicStdCorrect.ToString()).FontSize(8.5f);
                                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Height(12).Canvas((canvas, size) =>
                                        {
                                            using var tr = new SKPaint { Color = SKColor.Parse("#e2e8f0"), IsAntialias = true };
                                            canvas.DrawRoundRect(new SKRect(0, 2, size.Width, size.Height - 2), 2.5f, 2.5f, tr);
                                            if (topicSuccess > 0)
                                            {
                                                using var fill = new SKPaint { Color = SKColor.Parse(barHex), IsAntialias = true };
                                                float w = Math.Max(3f, (float)(size.Width * Math.Clamp(topicSuccess / 100.0, 0, 1)));
                                                canvas.DrawRoundRect(new SKRect(0, 2, w, size.Height - 2), 2.5f, 2.5f, fill);
                                            }
                                        });
                                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"%{topicSuccess:F0}").FontSize(8.5f).Bold().FontColor(barHex);
                                        table.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"%{topic.GlobalSuccessRate:F0}").FontSize(8.5f).FontColor("#64748b");
                                    }
                                });
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span($"{schoolName} — Bireysel Sınav Sonuç Karnesi | Sayfa ").FontSize(8.5f).FontColor("#94a3b8");
                        x.CurrentPageNumber();
                    });
                });
            })
            .GeneratePdf(filePath);
        }

        /// <summary>
        /// Tüm sınav analizlerini tek PDF raporunda birleştirir
        /// </summary>
        public void GenerateFullReport(
            string examName,
            List<StudentResult> students,
            List<ItemAnalysisService.QuestionItemStats> questionStats,
            List<QuestionDifficulty> questionDifficulties,
            ObservableCollection<LearningOutcome> learningOutcomes,
            ItemAnalysisService.ReliabilityStats reliabilityStats,
            StatisticsReportService.ClassStatistics classStats,
            ObservableCollection<StatisticsReportService.ScoreDistribution> scoreDistribution,
            ObservableCollection<StatisticsReportService.QuestionCorrelation> questionCorrelations,
            Dictionary<string, double> percentiles,
            ObservableCollection<AnswerKeyModel> answerKeys,
            string outputFilePath,
            ExamReportMetadata? metadata = null)
        {
            if (students == null || students.Count == 0)
                throw new ArgumentException("Öğrenci listesi boş olamaz.", nameof(students));

            var meta = metadata ?? new ExamReportMetadata
            {
                CourseName = examName,
                ExamTitle = examName,
                AcademicTerm = ExamReportMetadata.GetCurrentAcademicTerm()
            };
            if (string.IsNullOrWhiteSpace(meta.AcademicTerm))
                meta.AcademicTerm = ExamReportMetadata.GetCurrentAcademicTerm();

            string courseDisplay = !string.IsNullOrWhiteSpace(meta.CourseCode)
                ? $"{meta.CourseCode} — {meta.CourseName}"
                : (!string.IsNullOrWhiteSpace(meta.CourseName) ? meta.CourseName : examName);
            string cancelledDisplay = meta.CancelledQuestions.Count > 0 ? string.Join(", ", meta.CancelledQuestions) : "Yok";
            string multiDisplay = meta.MultiCorrectQuestions.Count > 0 ? string.Join(", ", meta.MultiCorrectQuestions) : "Yok";

            var statsService = new StatisticsReportService();
            var topStudents = statsService.GetTopStudents(students, 5);
            var lowStudents = statsService.GetLowestStudents(students, 5);
            var bookletComparison = statsService.GetBookletComparison(students, meta.PassingScore);

            Document.Create(container =>
            {
                // AKADEMİK KAPAK SAYFASI
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor("#ffffff");
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.TimesNewRoman));

                    page.Content().Column(x =>
                    {
                        x.Spacing(20);

                        // Üst Dekoratif Çizgi
                        x.Item().Height(5).Background("#1e3a5f");
                        x.Item().PaddingTop(2).Height(2).Background("#3b82f6");

                        // Kurumsal Başlık Bölümü
                        x.Item().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("BURDUR MEHMET AKİF ERSOY ÜNİVERSİTESİ").FontSize(11).SemiBold().FontColor("#475569");
                            c.Item().AlignCenter().PaddingTop(3).Text("AĞLASUN MESLEK YÜKSEKOKULU").FontSize(13).Bold().FontColor("#1e3a5f");
                            c.Item().PaddingTop(14).AlignCenter().Text("AKADEMİK SINAV DEĞERLENDİRME RAPORU").FontSize(21).Bold().FontColor("#1e3a5f");
                            c.Item().PaddingTop(5).AlignCenter().Text("PSYCHOMETRIC ITEM ANALYSIS & STATISTICAL REPORT").FontSize(10).Italic().FontColor("#64748b");
                            c.Item().PaddingVertical(14).LineHorizontal(1).LineColor("#cbd5e1");
                        });

                        // Sınav Bilgileri & Değerlendirme Kuralları (Yan Yana 2 Kurumsal Kutu)
                        x.Item().Row(row =>
                        {
                            row.Spacing(10);

                            // Sol: Sınav Künyesi
                            row.RelativeItem(1.15f).Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(14).Column(c =>
                            {
                                c.Item().PaddingBottom(8).Text("SINAV KÜNYESİ").FontSize(11).Bold().FontColor("#1e3a5f");
                                c.Item().Table(t =>
                                {
                                    t.ColumnsDefinition(cd => { cd.ConstantColumn(95); cd.RelativeColumn(); });
                                    t.Cell().PaddingVertical(2).Text("Akademik Dönem:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(meta.AcademicTerm).FontSize(9.5f).FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("Ders:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(courseDisplay).FontSize(9.5f).Bold().FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("Sınav:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(string.IsNullOrWhiteSpace(meta.ExamTitle) ? examName : meta.ExamTitle).FontSize(9.5f).FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("Tarih:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(DateTime.Now.ToString("dd MMMM yyyy, HH:mm", new System.Globalization.CultureInfo("tr-TR"))).FontSize(9.5f).FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("Katılım / Soru:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text($"{students.Count} Öğrenci  •  {questionDifficulties?.Count ?? 0} Soru").FontSize(9.5f).FontColor("#1e293b");
                                });
                            });

                            // Sağ: Sınav Değerlendirme Kuralları Özeti
                            row.RelativeItem(1f).Border(1).BorderColor("#dbeafe").Background("#eff6ff").Padding(14).Column(c =>
                            {
                                c.Item().PaddingBottom(8).Text("DEĞERLENDİRME KURALLARI").FontSize(11).Bold().FontColor("#1e3a5f");
                                c.Item().Table(t =>
                                {
                                    t.ColumnsDefinition(cd => { cd.ConstantColumn(85); cd.RelativeColumn(); });
                                    t.Cell().PaddingVertical(2).Text("Yanlış Kuralı:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(meta.DeductionRuleText).FontSize(9.5f).Bold().FontColor("#1d4ed8");
                                    t.Cell().PaddingVertical(2).Text("Baraj Puanı:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text($"{meta.PassingScore:F0} Puan").FontSize(9.5f).SemiBold().FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("İptal Sorular:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(cancelledDisplay).FontSize(9.5f).FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("Çoklu Doğru:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text(multiDisplay).FontSize(9.5f).FontColor("#1e293b");
                                    t.Cell().PaddingVertical(2).Text("Uç Puanlar:").FontSize(9.5f).SemiBold().FontColor("#475569");
                                    t.Cell().PaddingVertical(2).Text($"Min {classStats?.MinScore:F1} / Max {classStats?.MaxScore:F1}").FontSize(9.5f).SemiBold().FontColor("#15803d");
                                });
                            });
                        });

                        // Özet İstatistikler - Modern Kartlar (5 Kart)
                        x.Item().AlignCenter().PaddingTop(6).Text("TEMEL İSTATİSTİK ÖZETİ").FontSize(13).Bold().FontColor("#1e3a5f");
                        x.Item().Row(r =>
                        {
                            r.Spacing(6);
                            r.RelativeItem().Border(1).BorderColor("#dbeafe").Background("#eff6ff").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("ORTALAMA").FontSize(8).FontColor("#64748b");
                                c.Item().AlignCenter().Text(classStats?.AverageScore.ToString("F2") ?? "0.00").FontSize(18).Bold().FontColor("#1d4ed8");
                            });
                            r.RelativeItem().Border(1).BorderColor("#dcfce7").Background("#f0fdf4").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("BAŞARI %").FontSize(8).FontColor("#64748b");
                                c.Item().AlignCenter().Text($"%{classStats?.SuccessRate:F1}").FontSize(18).Bold().FontColor("#15803d");
                            });
                            r.RelativeItem().Border(1).BorderColor("#f3e8ff").Background("#faf5ff").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("MEDYAN").FontSize(8).FontColor("#64748b");
                                c.Item().AlignCenter().Text(classStats?.MedianScore.ToString("F2") ?? "0.00").FontSize(18).Bold().FontColor("#7c3aed");
                            });
                            r.RelativeItem().Border(1).BorderColor("#ffedd5").Background("#fff7ed").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("STD. SAPMA").FontSize(8).FontColor("#64748b");
                                c.Item().AlignCenter().Text(classStats?.StandardDeviation.ToString("F2") ?? "0.00").FontSize(18).Bold().FontColor("#c2410c");
                            });
                            r.RelativeItem().Border(1).BorderColor("#fef3c7").Background("#fffbeb").Padding(10).AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("KR-20").FontSize(8).FontColor("#64748b");
                                c.Item().AlignCenter().Text(reliabilityStats?.KR20.ToString("F2") ?? "0.00").FontSize(18).Bold().FontColor("#b45309");
                            });
                        });

                        // Pedagojik Özet Kutusu
                        if (!string.IsNullOrWhiteSpace(classStats?.PedagogicalEvaluation))
                        {
                            x.Item().Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(14).Column(c =>
                            {
                                c.Item().PaddingBottom(6).Text("AKADEMİK DEĞERLENDİRME ÖZETİ").FontSize(10).Bold().FontColor("#1e3a5f");
                                c.Item().Text(classStats.PedagogicalEvaluation.Replace("🛡️ ", "").Replace("⚠️ ", "").Replace("📉 ", "").Replace("📈 ", "").Replace("⚖️ ", "").Replace("👥 ", "").Replace("🎯 ", ""))
                                    .FontSize(9).LineHeight(1.35f).FontColor("#334155");
                            });
                        }

                        // Alt Dekoratif Çizgi
                        x.Item().PaddingTop(14).Height(2).Background("#3b82f6");
                        x.Item().PaddingTop(2).Height(4).Background("#1e3a5f");
                    });
                });

                // AKADEMİK DERECE, KRİTİK GRUP & KİTAPÇIK ADALETİ SAYFASI
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor("#ffffff");
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.TimesNewRoman));

                    page.Header().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Text($"{examName} — Derece Listeleri ve Kitapçık Analizi").FontSize(10.5f).Bold().FontColor("#1e3a5f");
                        row.ConstantItem(80).AlignRight().Text(x => { x.Span("Sayfa ").FontSize(9.5f).FontColor("#64748b"); x.CurrentPageNumber(); });
                    });

                    page.Content().PaddingVertical(10).Column(x =>
                    {
                        x.Spacing(14);

                        // En Yüksek Puan Alanlar (İlk 5)
                        x.Item().Background("#15803d").Padding(7).Text("EN YÜKSEK PUAN ALAN ÖĞRENCİLER (ONUR LİSTESİ — İLK 5)").FontSize(10.5f).Bold().FontColor("#ffffff");
                        x.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(35); cd.ConstantColumn(80); cd.RelativeColumn(3); cd.ConstantColumn(45); cd.ConstantColumn(85); cd.ConstantColumn(45); cd.ConstantColumn(55); });
                            t.Header(h =>
                            {
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).AlignCenter().Text("Sıra").FontSize(8).Bold().FontColor("#166534");
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).Text("Öğr. No").FontSize(8).Bold().FontColor("#166534");
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).Text("Ad Soyad").FontSize(8).Bold().FontColor("#166534");
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).AlignCenter().Text("Kitap.").FontSize(8).Bold().FontColor("#166534");
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).AlignCenter().Text("D / Y / B").FontSize(8).Bold().FontColor("#166534");
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).AlignCenter().Text("Net").FontSize(8).Bold().FontColor("#166534");
                                h.Cell().Background("#f0fdf4").BorderBottom(1).BorderColor("#bbf7d0").Padding(4).AlignCenter().Text("Puan").FontSize(8).Bold().FontColor("#166534");
                            });
                            foreach (var st in topStudents)
                            {
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"#{st.Rank}").FontSize(8).Bold();
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(st.StudentId).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(st.FullName).FontSize(8).SemiBold();
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.BookletType).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.CorrectWrongEmptyText).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.NetCount.ToString("F2")).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.Score.ToString("F2")).FontSize(8).Bold().FontColor("#15803d");
                            }
                        });

                        // En Düşük Puan Alanlar (Son 5)
                        x.Item().Background("#b91c1c").Padding(7).Text("EN DÜŞÜK PUAN ALAN ÖĞRENCİLER (DESTEK GEREKEN GRUP — SON 5)").FontSize(10.5f).Bold().FontColor("#ffffff");
                        x.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(35); cd.ConstantColumn(80); cd.RelativeColumn(3); cd.ConstantColumn(45); cd.ConstantColumn(85); cd.ConstantColumn(45); cd.ConstantColumn(55); });
                            t.Header(h =>
                            {
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).AlignCenter().Text("Sıra").FontSize(8).Bold().FontColor("#991b1b");
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).Text("Öğr. No").FontSize(8).Bold().FontColor("#991b1b");
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).Text("Ad Soyad").FontSize(8).Bold().FontColor("#991b1b");
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).AlignCenter().Text("Kitap.").FontSize(8).Bold().FontColor("#991b1b");
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).AlignCenter().Text("D / Y / B").FontSize(8).Bold().FontColor("#991b1b");
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).AlignCenter().Text("Net").FontSize(8).Bold().FontColor("#991b1b");
                                h.Cell().Background("#fef2f2").BorderBottom(1).BorderColor("#fecaca").Padding(4).AlignCenter().Text("Puan").FontSize(8).Bold().FontColor("#991b1b");
                            });
                            foreach (var st in lowStudents)
                            {
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"#{st.Rank}").FontSize(8).Bold();
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(st.StudentId).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(st.FullName).FontSize(8).SemiBold();
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.BookletType).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.CorrectWrongEmptyText).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.NetCount.ToString("F2")).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(st.Score.ToString("F2")).FontSize(8).Bold().FontColor("#b91c1c");
                            }
                        });

                        // Kitapçık Bazlı Adalet Analizi (Tam Genişlik)
                        x.Item().Background("#1e3a5f").Padding(7).Text("KİTAPÇIK BAZLI PERFORMANS & ADALET ANALİZİ").FontSize(10.5f).Bold().FontColor("#ffffff");
                        x.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(60); cd.ConstantColumn(55); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                            t.Header(h =>
                            {
                                h.Cell().Background("#f1f5f9").Padding(4).Text("Kitapçık").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Öğrenci").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Ortalama").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Medyan").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Std. Sapma").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("En Düşük").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("En Yüksek").FontSize(8).Bold();
                                h.Cell().Background("#f1f5f9").Padding(4).AlignCenter().Text("Başarı %").FontSize(8).Bold();
                            });
                            foreach (var bk in bookletComparison)
                            {
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(bk.BookletName).FontSize(8).Bold().FontColor("#7c3aed");
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(bk.StudentCount.ToString()).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(bk.AverageScore.ToString("F2")).FontSize(8).Bold();
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(bk.MedianScore.ToString("F1")).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(bk.StandardDeviation.ToString("F2")).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(bk.MinScore.ToString("F1")).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(bk.MaxScore.ToString("F1")).FontSize(8);
                                t.Cell().BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"%{bk.PassRate:F1}").FontSize(8).Bold().FontColor("#15803d");
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Ağlasun MYO — Akademik Sınav Değerlendirme Raporu | Sayfa ").FontSize(9).FontColor("#94a3b8"); x.CurrentPageNumber(); });
                });

                // SAYFA 1: DETAYLI İSTATİSTİKLER
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor("#ffffff");
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.TimesNewRoman));

                    page.Header().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Text($"{examName} - Sınav Analiz Raporu").FontSize(11).Bold().FontColor("#1e3a5f");
                        row.ConstantItem(80).AlignRight().Text(x => { x.Span("Sayfa ").FontSize(10).FontColor("#64748b"); x.CurrentPageNumber(); });
                    });

                    page.Content().Column(x =>
                    {
                        x.Spacing(12);

                        // Bölüm 1: Zorluk Analizi
                        x.Item().Background("#1e3a5f").Padding(8).Text("1. SORU ZORLUK DAĞILIMI").FontSize(12).Bold().FontColor("#ffffff");
                        
                        if (questionDifficulties?.Count > 0)
                        {
                            var easy = questionDifficulties.Count(q => q.DifficultyLevel == "Kolay");
                            var medium = questionDifficulties.Count(q => q.DifficultyLevel == "Orta");
                            var hard = questionDifficulties.Count(q => q.DifficultyLevel == "Zor");
                            var veryHard = questionDifficulties.Count(q => q.DifficultyLevel == "Çok Zor");
                            
                            x.Item().PaddingTop(8).Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.ConstantColumn(80); cd.RelativeColumn(); cd.ConstantColumn(60); cd.ConstantColumn(80); });
                                
                                t.Header(h =>
                                {
                                    h.Cell().Background("#1e3a5f").Padding(6).Text("Zorluk").FontSize(9).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#1e3a5f").Padding(6).Text("Görsel").FontSize(9).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#1e3a5f").Padding(6).AlignCenter().Text("Sayı").FontSize(9).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#1e3a5f").Padding(6).AlignCenter().Text("Oran %").FontSize(9).Bold().FontColor("#ffffff");
                                });

                                int total = questionDifficulties.Count;

                                void AddDiffRow(string label, int countVal, string bgHex, string borderHex, string textHex, string barHex)
                                {
                                    double ratio = total > 0 ? (double)countVal / total : 0;
                                    t.Cell().Background(bgHex).BorderBottom(1).BorderColor(borderHex).Padding(5).Text(label).FontSize(9).SemiBold().FontColor(textHex);
                                    t.Cell().Background(bgHex).BorderBottom(1).BorderColor(borderHex).Padding(5).Height(14).Canvas((canvas, size) =>
                                    {
                                        using var trackPaint = new SKPaint { Color = SKColor.Parse("#e2e8f0"), IsAntialias = true, Style = SKPaintStyle.Fill };
                                        canvas.DrawRoundRect(new SKRect(0, 3, size.Width, size.Height - 3), 3, 3, trackPaint);
                                        if (ratio > 0)
                                        {
                                            using var barPaint = new SKPaint { Color = SKColor.Parse(barHex), IsAntialias = true, Style = SKPaintStyle.Fill };
                                            float w = Math.Max(4f, (float)(size.Width * ratio));
                                            canvas.DrawRoundRect(new SKRect(0, 3, w, size.Height - 3), 3, 3, barPaint);
                                        }
                                    });
                                    t.Cell().Background(bgHex).BorderBottom(1).BorderColor(borderHex).Padding(5).AlignCenter().Text(countVal.ToString()).FontSize(9).Bold();
                                    t.Cell().Background(bgHex).BorderBottom(1).BorderColor(borderHex).Padding(5).AlignCenter().Text($"%{ratio * 100:F1}").FontSize(9);
                                }

                                AddDiffRow("Kolay", easy, "#f0fdf4", "#bbf7d0", "#166534", "#22c55e");
                                AddDiffRow("Orta", medium, "#fffbeb", "#fcd34d", "#92400e", "#f59e0b");
                                AddDiffRow("Zor", hard, "#fef2f2", "#fecaca", "#991b1b", "#ef4444");
                                AddDiffRow("Çok Zor", veryHard, "#f8fafc", "#e2e8f0", "#475569", "#64748b");
                            });
                        }

                        // Bölüm 2: Vektör Puan Dağılım Histogramı & Gaussian Çan Eğrisi
                        x.Item().PaddingTop(14).Background("#1e3a5f").Padding(8).Text("2. PUAN DAĞILIM HİSTOGRAMI VE NORMAL DAĞILIM (ÇAN EĞRİSİ)").FontSize(11.5f).Bold().FontColor("#ffffff");
                        
                        if (scoreDistribution?.Count > 0)
                        {
                            var distSnapshot = scoreDistribution.ToList();
                            double meanVal = classStats?.AverageScore ?? 50;
                            double stdVal = classStats?.StandardDeviation ?? 15;
                            int totalStuds = students.Count;

                            x.Item().PaddingTop(8).Border(1).BorderColor("#e2e8f0").Background("#ffffff").Padding(10).Column(chartCol =>
                            {
                                chartCol.Item().Height(190).Canvas((canvas, size) =>
                                {
                                    float padLeft = 32f, padRight = 16f, padTop = 20f, padBottom = 28f;
                                    float chartW = size.Width - padLeft - padRight;
                                    float chartH = size.Height - padTop - padBottom;
                                    if (chartW <= 20 || chartH <= 20) return;

                                    int maxCount = Math.Max(1, distSnapshot.Max(d => d.Count));
                                    // Y-axis gridlines
                                    using var gridPaint = new SKPaint { Color = SKColor.Parse("#e2e8f0"), StrokeWidth = 0.75f, IsAntialias = true };
                                    using var axisPaint = new SKPaint { Color = SKColor.Parse("#64748b"), StrokeWidth = 1.2f, IsAntialias = true };
                                    using var labelPaint = new SKPaint { Color = SKColor.Parse("#475569"), TextSize = 7.5f, IsAntialias = true, TextAlign = SKTextAlign.Center };
                                    using var yLabelPaint = new SKPaint { Color = SKColor.Parse("#64748b"), TextSize = 7.5f, IsAntialias = true, TextAlign = SKTextAlign.Right };

                                    int gridSteps = 4;
                                    for (int g = 0; g <= gridSteps; g++)
                                    {
                                        float y = padTop + chartH - (chartH * g / gridSteps);
                                        canvas.DrawLine(padLeft, y, padLeft + chartW, y, g == 0 ? axisPaint : gridPaint);
                                        int tickVal = (int)Math.Round((double)maxCount * g / gridSteps);
                                        canvas.DrawText(tickVal.ToString(), padLeft - 5, y + 3, yLabelPaint);
                                    }
                                    // Y axis
                                    canvas.DrawLine(padLeft, padTop, padLeft, padTop + chartH, axisPaint);

                                    // Vector Histogram Bars
                                    int bins = distSnapshot.Count;
                                    float slotW = chartW / bins;
                                    float barW = slotW * 0.68f;
                                    using var barFill = new SKPaint { Color = SKColor.Parse("#3b82f6").WithAlpha(210), IsAntialias = true, Style = SKPaintStyle.Fill };
                                    using var barBorder = new SKPaint { Color = SKColor.Parse("#1d4ed8"), StrokeWidth = 0.8f, IsAntialias = true, Style = SKPaintStyle.Stroke };
                                    using var valPaint = new SKPaint { Color = SKColor.Parse("#1e293b"), TextSize = 7.5f, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };

                                    for (int i = 0; i < bins; i++)
                                    {
                                        var d = distSnapshot[i];
                                        float xCenter = padLeft + slotW * (i + 0.5f);
                                        float barH = (float)((double)d.Count / maxCount * chartH);
                                        float top = padTop + chartH - barH;
                                        var rect = new SKRect(xCenter - barW / 2f, top, xCenter + barW / 2f, padTop + chartH);
                                        if (d.Count > 0)
                                        {
                                            canvas.DrawRoundRect(rect, 3, 3, barFill);
                                            canvas.DrawRoundRect(rect, 3, 3, barBorder);
                                            canvas.DrawText(d.Count.ToString(), xCenter, top - 4, valPaint);
                                        }
                                        canvas.DrawText(d.Range, xCenter, padTop + chartH + 13, labelPaint);
                                    }

                                    // Gaussian Normal Distribution Bell Curve Overlay
                                    if (stdVal > 0.5 && totalStuds > 1)
                                    {
                                        double binWidth = 100.0 / bins;
                                        double peakPdf = 1.0 / (stdVal * Math.Sqrt(2 * Math.PI));
                                        double peakExpectedCount = peakPdf * totalStuds * binWidth;
                                        double curveScale = Math.Max(maxCount, peakExpectedCount);

                                        using var curvePath = new SKPath();
                                        int samples = 80;
                                        for (int s = 0; s <= samples; s++)
                                        {
                                            double scoreX = 100.0 * s / samples;
                                            double z = (scoreX - meanVal) / stdVal;
                                            double pdf = Math.Exp(-0.5 * z * z) / (stdVal * Math.Sqrt(2 * Math.PI));
                                            double expectedCount = pdf * totalStuds * binWidth;
                                            float px = padLeft + (float)(scoreX / 100.0 * chartW);
                                            float py = padTop + chartH - (float)(Math.Min(1.0, expectedCount / curveScale) * chartH);
                                            if (s == 0) curvePath.MoveTo(px, py);
                                            else curvePath.LineTo(px, py);
                                        }
                                        using var curvePaint = new SKPaint
                                        {
                                            Color = SKColor.Parse("#7c3aed"),
                                            StrokeWidth = 2.0f,
                                            IsAntialias = true,
                                            Style = SKPaintStyle.Stroke
                                        };
                                        canvas.DrawPath(curvePath, curvePaint);

                                        // Mean (μ) vertical dashed reference line
                                        float meanX = padLeft + (float)(Math.Clamp(meanVal, 0, 100) / 100.0 * chartW);
                                        using var meanPaint = new SKPaint
                                        {
                                            Color = SKColor.Parse("#dc2626"),
                                            StrokeWidth = 1.2f,
                                            IsAntialias = true,
                                            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 3 }, 0)
                                        };
                                        canvas.DrawLine(meanX, padTop, meanX, padTop + chartH, meanPaint);
                                        using var meanLabel = new SKPaint { Color = SKColor.Parse("#dc2626"), TextSize = 7.5f, IsAntialias = true, TextAlign = SKTextAlign.Center, FakeBoldText = true };
                                        canvas.DrawText($"Ort: {meanVal:F1}", meanX, padTop - 4, meanLabel);
                                    }
                                });

                                // Legend
                                chartCol.Item().PaddingTop(4).AlignCenter().Row(leg =>
                                {
                                    leg.Spacing(16);
                                    leg.AutoItem().Text("■ Puan Frekansı (Öğrenci Sayısı)").FontSize(8).FontColor("#2563eb");
                                    leg.AutoItem().Text("— Gaussian Normal Dağılım (Çan Eğrisi)").FontSize(8).Bold().FontColor("#7c3aed");
                                    leg.AutoItem().Text("┆ Sınıf Ortalaması (μ)").FontSize(8).FontColor("#dc2626");
                                });
                            });
                        }

                        // Bölüm 3: Percentil Tablosu
                        x.Item().PaddingTop(15).Background("#1e3a5f").Padding(8).Text("3. PERCENTİL DEĞERLERİ").FontSize(12).Bold().FontColor("#ffffff");
                        
                        if (percentiles?.Count > 0)
                        {
                            x.Item().PaddingTop(8).Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                                
                                foreach (var p in percentiles.OrderBy(kv => kv.Key))
                                {
                                    t.Cell().Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(8).AlignCenter().Column(c =>
                                    {
                                        c.Item().Text(p.Key).FontSize(9).FontColor("#64748b");
                                        c.Item().Text(p.Value.ToString("F2")).FontSize(14).Bold().FontColor("#1e293b");
                                    });
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Sınav Değerlendirme Raporu | Sayfa ").FontSize(9).FontColor("#94a3b8"); x.CurrentPageNumber(); });
                });

                // SAYFA 2: ÖĞRENCİ LİSTESİ
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor("#ffffff");
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.TimesNewRoman));

                    page.Header().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Text($"{examName} - Öğrenci Listesi").FontSize(11).Bold().FontColor("#1e3a5f");
                        row.ConstantItem(80).AlignRight().Text(x => { x.Span("Sayfa ").FontSize(10).FontColor("#64748b"); x.CurrentPageNumber(); });
                    });

                    page.Content().PaddingVertical(10).Column(x =>
                    {
                        x.Item().PaddingBottom(10).Background("#1e3a5f").Padding(8).Text("4. SIRALAMALI ÖĞRENCİ LİSTESİ").FontSize(12).Bold().FontColor("#ffffff");
                        
                        x.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.ConstantColumn(35); cd.RelativeColumn(4); cd.ConstantColumn(80); cd.ConstantColumn(60); cd.ConstantColumn(60); cd.ConstantColumn(60); cd.ConstantColumn(50); });
                            
                            t.Header(h =>
                            {
                                h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Sıra").FontSize(8).Bold().FontColor("#ffffff");
                                h.Cell().Background("#334155").Padding(5).Text("Ad Soyad").FontSize(8).Bold().FontColor("#ffffff");
                                h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Öğr. No").FontSize(8).Bold().FontColor("#ffffff");
                                h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Kitapçık").FontSize(8).Bold().FontColor("#ffffff");
                                h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Doğru").FontSize(8).Bold().FontColor("#ffffff");
                                h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Yanlış").FontSize(8).Bold().FontColor("#ffffff");
                                h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Puan").FontSize(8).Bold().FontColor("#ffffff");
                            });
                            
                            bool alternate = false;
                            foreach (var s in students.OrderBy(st => st.Rank))
                            {
                                var bgColor = alternate ? "#f8fafc" : "#ffffff";
                                
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(s.Rank.ToString()).FontSize(8);
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(s.FullName).FontSize(8);
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(s.StudentId).FontSize(8);
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(s.BookletType).FontSize(8);
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(s.CorrectCount.ToString()).FontSize(8).FontColor("#15803d");
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(s.IncorrectCount.ToString()).FontSize(8).FontColor("#dc2626");
                                t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(s.Score.ToString("F2")).FontSize(8).Bold().FontColor("#1d4ed8");
                                
                                alternate = !alternate;
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Sınav Değerlendirme Raporu | Sayfa ").FontSize(9).FontColor("#94a3b8"); x.CurrentPageNumber(); });
                });

                // SAYFA 3: MADDE ANALİZİ
                if (questionStats != null && questionStats.Count > 0)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor("#ffffff");
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.TimesNewRoman));

                        page.Header().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(8).Row(row =>
                        {
                            row.RelativeItem().Text($"{examName} - Madde Analizi").FontSize(11).Bold().FontColor("#1e3a5f");
                            row.ConstantItem(80).AlignRight().Text(x => { x.Span("Sayfa ").FontSize(10).FontColor("#64748b"); x.CurrentPageNumber(); });
                        });

                        page.Content().PaddingVertical(10).Column(x =>
                        {
                            x.Item().PaddingBottom(10).Background("#1e3a5f").Padding(8).Text("5. MADDE İSTATİSTİKLERİ").FontSize(12).Bold().FontColor("#ffffff");
                            
                            x.Item().PaddingBottom(5).Text("Tablo 1. Soru Bazında İstatistiksel Değerler").FontSize(10).Italic().FontColor("#475569");
                            
                            x.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd => { 
                                    cd.ConstantColumn(30); 
                                    cd.ConstantColumn(55); 
                                    cd.ConstantColumn(55); 
                                    cd.ConstantColumn(55); 
                                    cd.ConstantColumn(70); 
                                    cd.RelativeColumn(); 
                                });
                                
                                t.Header(h =>
                                {
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Soru").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("p (Zorluk)").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("r (Ayırt.)").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("rpb").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Doğru/Toplam").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Değerlendirme").FontSize(8).Bold().FontColor("#ffffff");
                                });
                                
                                bool alternate = false;
                                foreach (var q in questionStats.OrderBy(qs => qs.QuestionNumber))
                                {
                                    var bgColor = alternate ? "#f8fafc" : "#ffffff";
                                    
                                    string difficultyLevel = q.DifficultyIndex switch
                                    {
                                        > 0.8 => "Çok Kolay",
                                        > 0.6 => "Kolay",
                                        > 0.4 => "Orta",
                                        > 0.2 => "Zor",
                                        _ => "Çok Zor"
                                    };
                                    
                                    string evalColor = q.DifficultyIndex switch
                                    {
                                        > 0.7 and < 0.9 => "#15803d",  // İdeal
                                        > 0.4 and <= 0.7 => "#ca8a04", // Kabul edilebilir
                                        _ => "#dc2626" // Gözden geçirilmeli
                                    };
                                    
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(q.QuestionNumber.ToString()).FontSize(8).Bold();
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text((q.DifficultyIndex * 100).ToString("F1")).FontSize(8);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(q.DiscriminationIndex.ToString("F2")).FontSize(8);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(q.PointBiserial.ToString("F2")).FontSize(8);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"{q.CorrectCount}/{q.TotalStudents}").FontSize(8);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(difficultyLevel).FontSize(8).FontColor(evalColor);
                                    
                                    alternate = !alternate;
                                }
                            });
                            
                            // CEVAP ANAHTARI BÖLÜMÜ - Tek satırda yatay liste
                            var primaryKey = answerKeys?.FirstOrDefault(k => !string.IsNullOrEmpty(k.Answers));
                            if (primaryKey != null)
                            {
                                x.Item().PaddingTop(10).Background("#15803d").Padding(5).Text("CEVAP ANAHTARI").FontSize(10).Bold().FontColor("#ffffff");
                                
                                // 10'lu gruplar halinde göster
                                int groupSize = 10;
                                for (int i = 0; i < primaryKey.Answers.Length; i += groupSize)
                                {
                                    var group = Enumerable.Range(i, Math.Min(groupSize, primaryKey.Answers.Length - i))
                                        .Select(idx => $"{idx + 1}-{primaryKey.Answers[idx].ToString().ToUpper()}");
                                    
                                    string line = string.Join("    ", group); // 4 boşlukla ayır
                                    
                                    x.Item().PaddingTop(3).PaddingBottom(3).Text(line)
                                        .FontSize(10)
                                        .FontColor("#1e293b")
                                        .FontFamily(Fonts.CourierNew);
                                }
                            }
                            
                            // Tablo 2: Şık Dağılımı Detayları (Kompakt)
                            x.Item().PaddingTop(15).PaddingBottom(3).Text("Tablo 2. Şık Dağılımı (%)").FontSize(10).Italic().FontColor("#475569");
                            
                            x.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd => { 
                                    cd.ConstantColumn(28);   // Soru No
                                    cd.ConstantColumn(32);   // A
                                    cd.ConstantColumn(32);   // B
                                    cd.ConstantColumn(32);   // C
                                    cd.ConstantColumn(32);   // D
                                    cd.ConstantColumn(32);   // E
                                    cd.ConstantColumn(32);   // Boş
                                    cd.ConstantColumn(35);   // Doğru
                                });
                                
                                // Başlık
                                t.Header(h =>
                                {
                                    h.Cell().Background("#1e3a5f").Padding(2).AlignCenter().Text("Soru").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(2).AlignCenter().Text("A").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(2).AlignCenter().Text("B").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(2).AlignCenter().Text("C").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(2).AlignCenter().Text("D").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(2).AlignCenter().Text("E").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(2).AlignCenter().Text("Boş").FontSize(7).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#1e3a5f").Padding(2).AlignCenter().Text("Doğru").FontSize(7).Bold().FontColor("#ffffff");
                                });
                                
                                bool altRow = false;
                                foreach (var q in questionStats.OrderBy(qs => qs.QuestionNumber))
                                {
                                    var bg = altRow ? "#f8fafc" : "#ffffff";
                                    var dist = q.OptionDistribution;
                                    
                                    // GERÇEK DOĞRU CEVABI cevap anahtarından al
                                    string correctOpt = "?";
                                    if (primaryKey != null && q.QuestionNumber <= primaryKey.Answers.Length)
                                    {
                                        correctOpt = primaryKey.Answers[q.QuestionNumber - 1].ToString().ToUpper();
                                    }
                                    
                                    t.Cell().Background(bg).BorderBottom(1).BorderColor("#e2e8f0").Padding(1).AlignCenter().Text(q.QuestionNumber.ToString()).FontSize(7).Bold();
                                    
                                    // Her şık için değer - doğru şık yeşil, diğerleri gri
                                    for (int i = 0; i < 5; i++)
                                    {
                                        string optLetter = ((char)('A' + i)).ToString();
                                        string color = (optLetter == correctOpt) ? "#15803d" : "#64748b";
                                        double pct = (dist != null && dist.Length > i) ? dist[i] * 100 : 0;
                                        t.Cell().Background(bg).BorderBottom(1).BorderColor("#e2e8f0").Padding(1).AlignCenter()
                                            .Text($"{pct:F0}").FontSize(7).FontColor(color);
                                    }
                                    
                                    // Boş sayısı
                                    double emptyPct = q.EmptyCount / (double)q.TotalStudents * 100;
                                    t.Cell().Background(bg).BorderBottom(1).BorderColor("#e2e8f0").Padding(1).AlignCenter()
                                        .Text($"{emptyPct:F0}").FontSize(7).FontColor("#94a3b8");
                                    
                                    // Doğru cevap gösterimi
                                    t.Cell().Background("#dcfce7").BorderBottom(1).BorderColor("#e2e8f0").Padding(1).AlignCenter()
                                        .Text(correctOpt).FontSize(8).Bold().FontColor("#15803d");
                                    
                                    altRow = !altRow;
                                }
                            });
                            
                            x.Item().PaddingTop(15).Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(10).Column(c =>
                            {
                                c.Item().PaddingBottom(5).Text("YORUM AÇIKLAMALARI:").FontSize(9).Bold().FontColor("#1e293b");
                                c.Item().Text("• p (Zorluk İndeksi): 0.70-0.90 arası ideal, 0.40-0.70 kabul edilebilir, diğerleri gözden geçirilmeli").FontSize(8).FontColor("#475569");
                                c.Item().Text("• r (Ayırt Edicilik): Pozitif değerler iyi, negatif değerler sorunun gözden geçirilmesi gerekir").FontSize(8).FontColor("#475569");
                                c.Item().Text("• rpb (Point-Biserial Korelasyon): 0.20 üzeri kabul edilebilir, 0.40 üzeri mükemmel").FontSize(8).FontColor("#475569");
                            });
                        });

                        page.Footer().AlignCenter().Text(x => { x.Span("Sınav Değerlendirme Raporu | Sayfa ").FontSize(9).FontColor("#94a3b8"); x.CurrentPageNumber(); });
                    });
                }
                else
                {
                    // Madde analizi verisi yoksa boş sayfa göster
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor("#ffffff");
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.TimesNewRoman));

                        page.Header().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(8).Row(row =>
                        {
                            row.RelativeItem().Text($"{examName} - Madde Analizi").FontSize(11).Bold().FontColor("#1e3a5f");
                            row.ConstantItem(80).AlignRight().Text(x => { x.Span("Sayfa ").FontSize(10).FontColor("#64748b"); x.CurrentPageNumber(); });
                        });

                        page.Content().PaddingVertical(10).Column(x =>
                        {
                            x.Item().PaddingBottom(10).Background("#dc2626").Padding(8).Text("MADDE ANALİZİ VERİSİ YOK").FontSize(12).Bold().FontColor("#ffffff");
                            x.Item().PaddingTop(20).Text($"QuestionStats sayısı: {(questionStats?.Count ?? 0)}").FontSize(10).FontColor("#dc2626");
                            x.Item().PaddingTop(10).Text("Lütfen önce değerlendirme işlemini tamamlayın.").FontSize(10).FontColor("#475569");
                        });

                        page.Footer().AlignCenter().Text(x => { x.Span("Sınav Değerlendirme Raporu | Sayfa ").FontSize(9).FontColor("#94a3b8"); x.CurrentPageNumber(); });
                    });
                }

                // SAYFA 4: KAZANIM ANALİZİ
                // SON SAYFA: ÖĞRENİM ÇIKTILARI (YÖKAK / AKREDİTASYON) VE RESMİ ONAY / İMZA BLOĞU
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor("#ffffff");
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.TimesNewRoman));

                    page.Header().BorderBottom(1).BorderColor("#e2e8f0").PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Text($"{courseDisplay} — Kazanım Analizi ve Resmi Onay").FontSize(10.5f).Bold().FontColor("#1e3a5f");
                        row.ConstantItem(80).AlignRight().Text(x => { x.Span("Sayfa ").FontSize(9.5f).FontColor("#64748b"); x.CurrentPageNumber(); });
                    });

                    page.Content().PaddingVertical(10).Column(x =>
                    {
                        x.Spacing(14);

                        if (learningOutcomes?.Count > 0)
                        {
                            double GetRate(LearningOutcome o) => o.GlobalSuccessRate > 0 ? o.GlobalSuccessRate : o.SuccessRate;
                            var strongest = learningOutcomes.OrderByDescending(GetRate).First();
                            var weakest = learningOutcomes.OrderBy(GetRate).First();

                            x.Item().Background("#1e3a5f").Padding(8).Text("6. ÖĞRENİM ÇIKTILARI & KONU BAZLI BAŞARI ANALİZİ (YÖKAK / AKREDİTASYON)").FontSize(11).Bold().FontColor("#ffffff");

                            // En Güçlü & En Zayıf Konu Özeti
                            x.Item().Row(r =>
                            {
                                r.Spacing(10);
                                r.RelativeItem().Border(1).BorderColor("#bbf7d0").Background("#f0fdf4").Padding(8).Column(c =>
                                {
                                    c.Item().Text("EN GÜÇLÜ KONU / ÇIKTI").FontSize(8).Bold().FontColor("#166534");
                                    c.Item().Text($"{strongest.Name} (%{GetRate(strongest):F1})").FontSize(9.5f).Bold().FontColor("#15803d");
                                });
                                r.RelativeItem().Border(1).BorderColor("#fecaca").Background("#fef2f2").Padding(8).Column(c =>
                                {
                                    c.Item().Text("GELİŞTİRİLMESİ GEREKEN KONU / ÇIKTI").FontSize(8).Bold().FontColor("#991b1b");
                                    c.Item().Text($"{weakest.Name} (%{GetRate(weakest):F1})").FontSize(9.5f).Bold().FontColor("#b91c1c");
                                });
                            });

                            x.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.ConstantColumn(28); cd.RelativeColumn(4); cd.ConstantColumn(65); cd.ConstantColumn(95); cd.ConstantColumn(55); cd.ConstantColumn(65); });
                                
                                t.Header(h =>
                                {
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("No").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).Text("Öğrenim Çıktısı / Konu").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Sorular").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).Text("Başarı Grafiği").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Başarı %").FontSize(8).Bold().FontColor("#ffffff");
                                    h.Cell().Background("#334155").Padding(5).AlignCenter().Text("Düzey").FontSize(8).Bold().FontColor("#ffffff");
                                });
                                
                                int idx = 1;
                                bool alternate = false;
                                foreach (var o in learningOutcomes)
                                {
                                    var bgColor = alternate ? "#f8fafc" : "#ffffff";
                                    double rate = GetRate(o);
                                    
                                    string level = rate switch
                                    {
                                        >= 80 => "Mükemmel",
                                        >= 60 => "Yeterli",
                                        >= 40 => "Orta",
                                        _ => "Gelişmeli"
                                    };
                                    
                                    string levelColor = rate switch
                                    {
                                        >= 80 => "#15803d",
                                        >= 60 => "#2563eb",
                                        >= 40 => "#ca8a04",
                                        _ => "#dc2626"
                                    };
                                    
                                    string qRaw = string.IsNullOrWhiteSpace(o.QuestionNumbersRaw) ? (o.QuestionNumbers?.Count.ToString() ?? "-") : o.QuestionNumbersRaw;
                                    
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(idx.ToString()).FontSize(8);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Text(o.Name).FontSize(8).SemiBold();
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(qRaw).FontSize(8);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).Height(12).Canvas((canvas, size) =>
                                    {
                                        using var tr = new SKPaint { Color = SKColor.Parse("#e2e8f0"), IsAntialias = true };
                                        canvas.DrawRoundRect(new SKRect(0, 2, size.Width, size.Height - 2), 2.5f, 2.5f, tr);
                                        if (rate > 0)
                                        {
                                            using var fill = new SKPaint { Color = SKColor.Parse(levelColor), IsAntialias = true };
                                            float w = Math.Max(3f, (float)(size.Width * Math.Clamp(rate / 100.0, 0, 1)));
                                            canvas.DrawRoundRect(new SKRect(0, 2, w, size.Height - 2), 2.5f, 2.5f, fill);
                                        }
                                    });
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text($"%{rate:F1}").FontSize(8).Bold().FontColor(levelColor);
                                    t.Cell().Background(bgColor).BorderBottom(1).BorderColor("#e2e8f0").Padding(4).AlignCenter().Text(level).FontSize(8).FontColor(levelColor);
                                    
                                    idx++;
                                    alternate = !alternate;
                                }
                            });
                            
                            x.Item().Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("≥ %80: Mükemmel").FontSize(8).Bold().FontColor("#15803d");
                                r.RelativeItem().Text("%60 – 79: Yeterli").FontSize(8).Bold().FontColor("#2563eb");
                                r.RelativeItem().Text("%40 – 59: Orta").FontSize(8).Bold().FontColor("#ca8a04");
                                r.RelativeItem().Text("< %40: Geliştirilmeli").FontSize(8).Bold().FontColor("#dc2626");
                            });
                        }

                        // RESMİ ONAY & İMZA BLOĞU
                        x.Item().PaddingTop(18).Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(16).Column(sig =>
                        {
                            sig.Item().AlignCenter().PaddingBottom(10).Text("RESMİ DEĞERLENDİRME ONAY VE İMZA BLOĞU").FontSize(10.5f).Bold().FontColor("#1e3a5f");
                            sig.Item().PaddingBottom(14).AlignCenter().Text($"Bu rapor, {courseDisplay} dersinin {(string.IsNullOrWhiteSpace(meta.ExamTitle) ? examName : meta.ExamTitle)} ({meta.AcademicTerm}) optik değerlendirme sonuçlarını ve istatistiksel analizlerini belgelemek amacıyla düzenlenmiştir.")
                                .FontSize(8.5f).Italic().FontColor("#475569");

                            sig.Item().Row(r =>
                            {
                                r.Spacing(24);
                                // Sol: Dersi Veren Öğretim Elemanı
                                r.RelativeItem().Border(1).BorderColor("#e2e8f0").Background("#ffffff").Padding(12).Column(c =>
                                {
                                    c.Item().AlignCenter().Text("DERSİ VEREN ÖĞRETİM ELEMANI").FontSize(8.5f).Bold().FontColor("#1e3a5f");
                                    c.Item().PaddingTop(8).AlignCenter().Text(string.IsNullOrWhiteSpace(meta.InstructorName) ? "Ad Soyad / Unvan: ........................................." : meta.InstructorName).FontSize(9).FontColor("#334155");
                                    c.Item().PaddingTop(6).AlignCenter().Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").FontSize(8.5f).FontColor("#64748b");
                                    c.Item().PaddingTop(28).AlignCenter().Text("İmza: ..............................................").FontSize(8.5f).FontColor("#64748b");
                                });

                                // Sağ: Bölüm / Program Başkanlığı
                                r.RelativeItem().Border(1).BorderColor("#e2e8f0").Background("#ffffff").Padding(12).Column(c =>
                                {
                                    c.Item().AlignCenter().Text("BÖLÜM / PROGRAM BAŞKANLIĞI ONAYI").FontSize(8.5f).Bold().FontColor("#1e3a5f");
                                    c.Item().PaddingTop(8).AlignCenter().Text("Ad Soyad / Unvan: .........................................").FontSize(9).FontColor("#334155");
                                    c.Item().PaddingTop(6).AlignCenter().Text("Tarih: .... / .... / 20....").FontSize(8.5f).FontColor("#64748b");
                                    c.Item().PaddingTop(28).AlignCenter().Text("İmza / Mühür: .....................................").FontSize(8.5f).FontColor("#64748b");
                                });
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(x => { x.Span("Ağlasun MYO — Akademik Sınav Değerlendirme Raporu | Sayfa ").FontSize(9).FontColor("#94a3b8"); x.CurrentPageNumber(); });
                });

            }).GeneratePdf(outputFilePath);
        }
    }
}
