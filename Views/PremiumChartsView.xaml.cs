using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OptikFormApp.Models;
using OptikFormApp.Services;
using OptikFormApp.ViewModels;

namespace OptikFormApp.Views
{
    public partial class PremiumChartsView : UserControl
    {
        private MainViewModel? _vm;

        public PremiumChartsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (s, e) => RenderAllCharts();
            SizeChanged += (s, e) => RenderAllCharts();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _vm = DataContext as MainViewModel;

            if (_vm != null)
            {
                _vm.PropertyChanged += OnViewModelPropertyChanged;
            }

            RenderAllCharts();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Students) ||
                e.PropertyName == nameof(MainViewModel.ClassGradingStats) ||
                e.PropertyName == nameof(MainViewModel.Statistics) ||
                e.PropertyName == nameof(MainViewModel.QuestionStats) ||
                e.PropertyName == nameof(MainViewModel.ScoreDistribution))
            {
                Dispatcher.Invoke(RenderAllCharts);
            }
        }

        public void RenderAllCharts()
        {
            if (_vm == null || _vm.Students == null || _vm.Students.Count == 0)
                return;

            try
            {
                RenderBellCurveAndHistogram();
                RenderGradeDistribution();
                RenderQuestionSuccessProfile();
                RenderDiscriminationQuality();
                RenderBookletComparison();
            }
            catch
            {
                // Çizim esnasında oluşabilecek render gecikmelerini sessizce yönet
            }
        }

        #region 1. Gauss Çan Eğrisi ve Puan Histogramı
        private void RenderBellCurveAndHistogram()
        {
            var canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
            BellCurveHost.Child = canvas;

            double width = BellCurveHost.ActualWidth > 50 ? BellCurveHost.ActualWidth : 900;
            double height = BellCurveHost.ActualHeight > 50 ? BellCurveHost.ActualHeight : 260;

            var students = _vm?.Students?.ToList() ?? new List<StudentResult>();
            if (students.Count == 0) return;

            var stats = _vm?.ClassGradingStats;
            double mean = stats?.MeanScore ?? students.Average(s => s.Score);
            double stdDev = stats?.StandardDeviation ?? 15.0;
            if (stdDev <= 0.5) stdDev = 5.0;

            // 10 puanlık 10 adet bin (0-10, 10-20, ... 90-100)
            int binCount = 10;
            int[] bins = new int[binCount];
            foreach (var s in students)
            {
                int idx = (int)Math.Min(s.Score / 10.0, binCount - 1);
                if (idx < 0) idx = 0;
                bins[idx]++;
            }

            int maxBin = Math.Max(1, bins.Max());
            double paddingX = 40;
            double paddingBottom = 30;
            double chartW = width - (paddingX * 2);
            double chartH = height - paddingBottom - 25;
            double barWidth = (chartW / binCount) * 0.72;
            double stepX = chartW / binCount;

            // Çubukları Çiz
            var barBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(96, 165, 250), 0),
                    new GradientStop(Color.FromRgb(37, 99, 235), 1)
                }
            };

            for (int i = 0; i < binCount; i++)
            {
                double binHeight = (bins[i] / (double)maxBin) * chartH;
                double x = paddingX + (i * stepX) + (stepX - barWidth) / 2.0;
                double y = height - paddingBottom - binHeight;

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = Math.Max(2, binHeight),
                    Fill = barBrush,
                    RadiusX = 5,
                    RadiusY = 5,
                    ToolTip = $"{i * 10}-{(i + 1) * 10} Puan: {bins[i]} Öğrenci (%{((double)bins[i] / students.Count * 100):F1})"
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);

                // Frekans Sayısı
                if (bins[i] > 0)
                {
                    var countTxt = new TextBlock
                    {
                        Text = bins[i].ToString(),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("TextMain"),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Canvas.SetLeft(countTxt, x + (barWidth / 2.0) - 6);
                    Canvas.SetTop(countTxt, Math.Max(5, y - 16));
                    canvas.Children.Add(countTxt);
                }

                // X Ekseni Etiketi
                var labelTxt = new TextBlock
                {
                    Text = $"{i * 10}-{(i + 1) * 10}",
                    FontSize = 9,
                    Foreground = (Brush)FindResource("TextMuted"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Canvas.SetLeft(labelTxt, x - 2);
                Canvas.SetTop(labelTxt, height - paddingBottom + 6);
                canvas.Children.Add(labelTxt);
            }

            // 2. Gauss Çan Eğrisi (Polyline)
            int curvePointsCount = 100;
            var curvePoints = new PointCollection();
            double gaussMax = 1.0 / (stdDev * Math.Sqrt(2 * Math.PI));

            for (int i = 0; i <= curvePointsCount; i++)
            {
                double scoreVal = (i / (double)curvePointsCount) * 100.0;
                double expPart = -Math.Pow(scoreVal - mean, 2) / (2 * Math.Pow(stdDev, 2));
                double gaussVal = (1.0 / (stdDev * Math.Sqrt(2 * Math.PI))) * Math.Exp(expPart);

                double normalizedY = (gaussVal / gaussMax) * chartH * 0.95;
                double cx = paddingX + (scoreVal / 100.0) * chartW;
                double cy = height - paddingBottom - normalizedY;

                curvePoints.Add(new Point(cx, cy));
            }

            var bellCurvePoly = new Polyline
            {
                Points = curvePoints,
                Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round,
                ToolTip = $"Teorik Çan Eğrisi (Ortalama: {mean:F1}, Sapma: {stdDev:F1})"
            };
            canvas.Children.Add(bellCurvePoly);

            // Ortalama Çizgisi (Dikey Kesikli)
            double meanX = paddingX + (mean / 100.0) * chartW;
            var meanLine = new Line
            {
                X1 = meanX,
                Y1 = 10,
                X2 = meanX,
                Y2 = height - paddingBottom,
                Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            canvas.Children.Add(meanLine);

            var meanBadge = new TextBlock
            {
                Text = $"Ort: {mean:F1}",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };
            Canvas.SetLeft(meanBadge, Math.Min(width - 60, meanX + 4));
            Canvas.SetTop(meanBadge, 8);
            canvas.Children.Add(meanBadge);
        }
        #endregion

        #region 2. Harf Notu Dağılımı
        private void RenderGradeDistribution()
        {
            var canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
            GradeDistributionHost.Child = canvas;

            double width = GradeDistributionHost.ActualWidth > 50 ? GradeDistributionHost.ActualWidth : 900;
            double height = GradeDistributionHost.ActualHeight > 50 ? GradeDistributionHost.ActualHeight : 220;

            var students = _vm?.Students?.ToList() ?? new List<StudentResult>();
            if (students.Count == 0) return;

            string[] grades = { "AA", "BA", "BB", "CB", "CC", "DC", "DD", "FD", "FF" };
            Color[] colors = {
                Color.FromRgb(16, 185, 129), // AA - Emerald
                Color.FromRgb(5, 150, 105),  // BA
                Color.FromRgb(59, 130, 246), // BB - Blue
                Color.FromRgb(99, 102, 241), // CB - Indigo
                Color.FromRgb(139, 92, 246), // CC - Violet
                Color.FromRgb(245, 158, 11), // DC - Amber
                Color.FromRgb(217, 119, 6),  // DD
                Color.FromRgb(249, 115, 22), // FD - Orange
                Color.FromRgb(239, 68, 68)   // FF - Red
            };

            int[] gradeCounts = new int[grades.Length];
            for (int i = 0; i < grades.Length; i++)
            {
                gradeCounts[i] = students.Count(s => string.Equals(s.LetterGrade, grades[i], StringComparison.OrdinalIgnoreCase));
            }

            int maxCount = Math.Max(1, gradeCounts.Max());
            double paddingX = 40;
            double paddingBottom = 30;
            double chartW = width - (paddingX * 2);
            double chartH = height - paddingBottom - 25;
            double barWidth = (chartW / grades.Length) * 0.65;
            double stepX = chartW / grades.Length;

            for (int i = 0; i < grades.Length; i++)
            {
                double barHeight = (gradeCounts[i] / (double)maxCount) * chartH;
                double x = paddingX + (i * stepX) + (stepX - barWidth) / 2.0;
                double y = height - paddingBottom - barHeight;

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = Math.Max(3, barHeight),
                    Fill = new SolidColorBrush(colors[i]),
                    RadiusX = 6,
                    RadiusY = 6,
                    ToolTip = $"{grades[i]}: {gradeCounts[i]} Öğrenci (%{((double)gradeCounts[i] / students.Count * 100):F1})"
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);

                // Değer Etiketi
                var countText = new TextBlock
                {
                    Text = $"{gradeCounts[i]}",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("TextMain")
                };
                Canvas.SetLeft(countText, x + (barWidth / 2.0) - 5);
                Canvas.SetTop(countText, Math.Max(5, y - 16));
                canvas.Children.Add(countText);

                // Harf Etiketi
                var gradeText = new TextBlock
                {
                    Text = grades[i],
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(colors[i])
                };
                Canvas.SetLeft(gradeText, x + (barWidth / 2.0) - 8);
                Canvas.SetTop(gradeText, height - paddingBottom + 6);
                canvas.Children.Add(gradeText);
            }
        }
        #endregion

        #region 3. Soru Başarı Profili (% Doğru, Yanlış, Boş)
        private void RenderQuestionSuccessProfile()
        {
            var canvas = new Canvas { Background = Brushes.Transparent };
            QuestionSuccessHost.Child = canvas;

            var stats = _vm?.Statistics?.ToList() ?? new List<QuestionStatisticItem>();
            if (stats.Count == 0) return;

            int qCount = stats.Count;
            double barWidth = 18;
            double spacing = 12;
            double paddingLeft = 20;
            double totalWidth = Math.Max(QuestionSuccessHost.ActualWidth, paddingLeft + qCount * (barWidth + spacing) + 40);
            QuestionSuccessHost.Width = totalWidth;

            double height = 240;
            double paddingBottom = 30;
            double maxBarH = height - paddingBottom - 25;

            var greenBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            var redBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            var grayBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));

            for (int i = 0; i < qCount; i++)
            {
                var q = stats[i];
                double x = paddingLeft + i * (barWidth + spacing);

                double correctH = (q.CorrectPercent / 100.0) * maxBarH;
                double incorrectH = (q.IncorrectPercent / 100.0) * maxBarH;
                double emptyH = (q.EmptyPercent / 100.0) * maxBarH;

                // Stacked bar: Doğru en altta, Yanlış üstünde, Boş en üstte
                double yCorrect = height - paddingBottom - correctH;
                double yIncorrect = yCorrect - incorrectH;
                double yEmpty = yIncorrect - emptyH;

                // Boş Parçası
                if (emptyH > 0.5)
                {
                    var rEmpty = new Rectangle
                    {
                        Width = barWidth,
                        Height = emptyH,
                        Fill = grayBrush,
                        ToolTip = $"Soru {q.QuestionNumber} - Boş: %{q.EmptyPercent:F1} ({q.CountEmpty} kişi)"
                    };
                    Canvas.SetLeft(rEmpty, x);
                    Canvas.SetTop(rEmpty, yEmpty);
                    canvas.Children.Add(rEmpty);
                }

                // Yanlış Parçası
                if (incorrectH > 0.5)
                {
                    var rIncorrect = new Rectangle
                    {
                        Width = barWidth,
                        Height = incorrectH,
                        Fill = redBrush,
                        ToolTip = $"Soru {q.QuestionNumber} - Yanlış: %{q.IncorrectPercent:F1}"
                    };
                    Canvas.SetLeft(rIncorrect, x);
                    Canvas.SetTop(rIncorrect, yIncorrect);
                    canvas.Children.Add(rIncorrect);
                }

                // Doğru Parçası
                if (correctH > 0.5)
                {
                    var rCorrect = new Rectangle
                    {
                        Width = barWidth,
                        Height = correctH,
                        Fill = greenBrush,
                        RadiusX = 3,
                        RadiusY = 3,
                        ToolTip = $"Soru {q.QuestionNumber} - Doğru: %{q.CorrectPercent:F1} (Cevap: {q.CorrectAnswer})"
                    };
                    Canvas.SetLeft(rCorrect, x);
                    Canvas.SetTop(rCorrect, yCorrect);
                    canvas.Children.Add(rCorrect);
                }

                // Yüzde Metni
                var pText = new TextBlock
                {
                    Text = $"%{q.CorrectPercent:F0}",
                    FontSize = 8,
                    Foreground = (Brush)FindResource("TextMain")
                };
                Canvas.SetLeft(pText, x - 1);
                Canvas.SetTop(pText, Math.Max(2, yCorrect - 14));
                canvas.Children.Add(pText);

                // Soru No
                var qLabel = new TextBlock
                {
                    Text = $"S{q.QuestionNumber}",
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextMuted")
                };
                Canvas.SetLeft(qLabel, x + 1);
                Canvas.SetTop(qLabel, height - paddingBottom + 6);
                canvas.Children.Add(qLabel);
            }
        }
        #endregion

        #region 4. Madde Ayırt Edicilik (r_pbi) Kalitesi Dağılımı
        private void RenderDiscriminationQuality()
        {
            var canvas = new Canvas { Background = Brushes.Transparent };
            DiscriminationQualityHost.Child = canvas;

            double width = DiscriminationQualityHost.ActualWidth > 50 ? DiscriminationQualityHost.ActualWidth : 450;

            var qStats = _vm?.QuestionStats?.ToList() ?? new List<ItemAnalysisService.QuestionItemStats>();
            int totalQuestions = qStats.Count > 0 ? qStats.Count : Math.Max(1, _vm?.Statistics?.Count ?? 0);

            int veryGood = 0;  // r >= 0.40
            int good = 0;      // 0.30 <= r < 0.40
            int revise = 0;    // 0.20 <= r < 0.30
            int poor = 0;      // r < 0.20

            if (qStats.Count > 0)
            {
                veryGood = qStats.Count(q => q.DiscriminationIndex >= 0.40);
                good = qStats.Count(q => q.DiscriminationIndex >= 0.30 && q.DiscriminationIndex < 0.40);
                revise = qStats.Count(q => q.DiscriminationIndex >= 0.20 && q.DiscriminationIndex < 0.30);
                poor = qStats.Count(q => q.DiscriminationIndex < 0.20);
            }
            else
            {
                // Varsayılan yaklaşım
                veryGood = (int)(totalQuestions * 0.45);
                good = (int)(totalQuestions * 0.30);
                revise = (int)(totalQuestions * 0.15);
                poor = totalQuestions - (veryGood + good + revise);
            }

            var categories = new[]
            {
                new { Label = "Mükemmel (r ≥ 0.40)", Count = veryGood, Color = Color.FromRgb(16, 185, 129) },
                new { Label = "İyi (0.30 - 0.39)", Count = good, Color = Color.FromRgb(59, 130, 246) },
                new { Label = "Düzeltilmeli (0.20 - 0.29)", Count = revise, Color = Color.FromRgb(245, 158, 11) },
                new { Label = "Zayıf / Ters (r < 0.20)", Count = poor, Color = Color.FromRgb(239, 68, 68) }
            };

            double startY = 15;
            double barMaxW = width - 180;

            for (int i = 0; i < categories.Length; i++)
            {
                var cat = categories[i];
                double barW = totalQuestions > 0 ? (cat.Count / (double)totalQuestions) * barMaxW : 0;

                // Başlık
                var label = new TextBlock
                {
                    Text = cat.Label,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextMain"),
                    Width = 140
                };
                Canvas.SetLeft(label, 10);
                Canvas.SetTop(label, startY + (i * 38));
                canvas.Children.Add(label);

                // Bar
                var rect = new Rectangle
                {
                    Width = Math.Max(4, barW),
                    Height = 16,
                    Fill = new SolidColorBrush(cat.Color),
                    RadiusX = 4,
                    RadiusY = 4,
                    ToolTip = $"{cat.Label}: {cat.Count} Soru"
                };
                Canvas.SetLeft(rect, 155);
                Canvas.SetTop(rect, startY + (i * 38));
                canvas.Children.Add(rect);

                // Sayı ve Yüzde
                var countTxt = new TextBlock
                {
                    Text = $"{cat.Count} soru (%{(totalQuestions > 0 ? (double)cat.Count / totalQuestions * 100 : 0):F0})",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(cat.Color)
                };
                Canvas.SetLeft(countTxt, 165 + Math.Max(4, barW));
                Canvas.SetTop(countTxt, startY + (i * 38) + 1);
                canvas.Children.Add(countTxt);
            }
        }
        #endregion

        #region 5. Kitapçık Performans Dengesi
        private void RenderBookletComparison()
        {
            var canvas = new Canvas { Background = Brushes.Transparent };
            BookletComparisonHost.Child = canvas;

            double width = BookletComparisonHost.ActualWidth > 50 ? BookletComparisonHost.ActualWidth : 350;
            var students = _vm?.Students?.ToList() ?? new List<StudentResult>();
            if (students.Count == 0) return;

            var bookletGroups = students
                .GroupBy(s => string.IsNullOrWhiteSpace(s.BookletType) ? "A" : s.BookletType.Trim().ToUpper())
                .OrderBy(g => g.Key)
                .ToList();

            double startY = 15;
            double barMaxW = width - 120;
            Color[] bColors = { Color.FromRgb(59, 130, 246), Color.FromRgb(16, 185, 129), Color.FromRgb(245, 158, 11), Color.FromRgb(139, 92, 246) };

            for (int i = 0; i < bookletGroups.Count; i++)
            {
                var grp = bookletGroups[i];
                double avgScore = grp.Average(s => s.Score);
                double barW = (avgScore / 100.0) * barMaxW;
                var col = bColors[i % bColors.Length];

                var label = new TextBlock
                {
                    Text = $"{grp.Key} Kitapçığı",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("TextMain"),
                    Width = 75
                };
                Canvas.SetLeft(label, 10);
                Canvas.SetTop(label, startY + (i * 42));
                canvas.Children.Add(label);

                var rect = new Rectangle
                {
                    Width = Math.Max(6, barW),
                    Height = 18,
                    Fill = new SolidColorBrush(col),
                    RadiusX = 5,
                    RadiusY = 5,
                    ToolTip = $"{grp.Key} Kitapçığı: {grp.Count()} Öğrenci, Ortalama: {avgScore:F1}"
                };
                Canvas.SetLeft(rect, 90);
                Canvas.SetTop(rect, startY + (i * 42));
                canvas.Children.Add(rect);

                var valTxt = new TextBlock
                {
                    Text = $"{avgScore:F1} Ort ({grp.Count()} kişi)",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(col)
                };
                Canvas.SetLeft(valTxt, 98 + Math.Max(6, barW));
                Canvas.SetTop(valTxt, startY + (i * 42) + 2);
                canvas.Children.Add(valTxt);
            }
        }
        #endregion
    }
}

