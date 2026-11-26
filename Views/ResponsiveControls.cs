using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace OptikFormApp.Views
{
    /// <summary>
    /// Mevcut genişliğe göre sütun sayısını otomatik ayarlayan (ör. 6 -> 3 -> 2 -> 1)
    /// kurumsal responsive UniformGrid kontrolü.
    /// </summary>
    public class ResponsiveUniformGrid : UniformGrid
    {
        public static readonly DependencyProperty MinColumnWidthProperty =
            DependencyProperty.Register(
                nameof(MinColumnWidth),
                typeof(double),
                typeof(ResponsiveUniformGrid),
                new FrameworkPropertyMetadata(170.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MaxColumnsProperty =
            DependencyProperty.Register(
                nameof(MaxColumns),
                typeof(int),
                typeof(ResponsiveUniformGrid),
                new FrameworkPropertyMetadata(6, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MinColumnsProperty =
            DependencyProperty.Register(
                nameof(MinColumns),
                typeof(int),
                typeof(ResponsiveUniformGrid),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinColumnWidth
        {
            get => (double)GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        public int MaxColumns
        {
            get => (int)GetValue(MaxColumnsProperty);
            set => SetValue(MaxColumnsProperty, value);
        }

        public int MinColumns
        {
            get => (int)GetValue(MinColumnsProperty);
            set => SetValue(MinColumnsProperty, value);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            double availableWidth = double.IsInfinity(constraint.Width) ? ActualWidth : constraint.Width;
            if (availableWidth > 0 && MinColumnWidth > 0)
            {
                int minCols = Math.Max(1, MinColumns);
                int maxCols = Math.Max(minCols, MaxColumns);

                int fitCols = (int)Math.Floor((availableWidth + 8.0) / MinColumnWidth);
                fitCols = Math.Clamp(fitCols, minCols, maxCols);

                int visibleChildren = 0;
                foreach (UIElement? child in InternalChildren)
                {
                    if (child != null && child.Visibility != Visibility.Collapsed)
                    {
                        visibleChildren++;
                    }
                }

                if (visibleChildren > 0 && fitCols > visibleChildren)
                {
                    fitCols = Math.Max(1, visibleChildren);
                }

                Columns = fitCols;
                Rows = 0;
            }

            return base.MeasureOverride(constraint);
        }
    }

    /// <summary>
    /// İki çocuklu (Sol: Başlık veya Arama Kutusu, Sağ: Aksiyon/Filtre Grubu) esnek araç çubuğu paneli.
    /// Geniş ekranda tek satırda hizalar; dar ekranda çakışmadan alt alta iki satıra sarar.
    /// </summary>
    public class ResponsiveToolbarPanel : Panel
    {
        public static readonly DependencyProperty MinFirstWidthProperty =
            DependencyProperty.Register(
                nameof(MinFirstWidth),
                typeof(double),
                typeof(ResponsiveToolbarPanel),
                new FrameworkPropertyMetadata(250.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty StretchFirstProperty =
            DependencyProperty.Register(
                nameof(StretchFirst),
                typeof(bool),
                typeof(ResponsiveToolbarPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                nameof(HorizontalSpacing),
                typeof(double),
                typeof(ResponsiveToolbarPanel),
                new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                nameof(VerticalSpacing),
                typeof(double),
                typeof(ResponsiveToolbarPanel),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinFirstWidth
        {
            get => (double)GetValue(MinFirstWidthProperty);
            set => SetValue(MinFirstWidthProperty, value);
        }

        public bool StretchFirst
        {
            get => (bool)GetValue(StretchFirstProperty);
            set => SetValue(StretchFirstProperty, value);
        }

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        private bool _isStacked;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalChildren.Count == 0)
                return default;

            if (InternalChildren.Count == 1)
            {
                var only = InternalChildren[0];
                only.Measure(availableSize);
                return only.DesiredSize;
            }

            var first = InternalChildren[0];
            var second = InternalChildren[1];

            if (second.Visibility == Visibility.Collapsed)
            {
                _isStacked = false;
                first.Measure(availableSize);
                return first.DesiredSize;
            }

            if (first.Visibility == Visibility.Collapsed)
            {
                _isStacked = false;
                second.Measure(availableSize);
                return second.DesiredSize;
            }

            double maxW = availableSize.Width;
            if (double.IsInfinity(maxW) || maxW <= 0)
            {
                _isStacked = false;
                first.Measure(availableSize);
                second.Measure(availableSize);
                return new Size(
                    first.DesiredSize.Width + HorizontalSpacing + second.DesiredSize.Width,
                    Math.Max(first.DesiredSize.Height, second.DesiredSize.Height));
            }

            second.Measure(new Size(maxW, availableSize.Height));
            Size s2 = second.DesiredSize;

            double requiredFirstW = MinFirstWidth;
            if (!StretchFirst)
            {
                first.Measure(new Size(maxW, availableSize.Height));
                requiredFirstW = Math.Max(MinFirstWidth, first.DesiredSize.Width);
            }

            if (maxW >= requiredFirstW + HorizontalSpacing + s2.Width)
            {
                _isStacked = false;
                double firstSlotW = StretchFirst
                    ? Math.Max(0, maxW - s2.Width - HorizontalSpacing)
                    : Math.Min(first.DesiredSize.Width, Math.Max(0, maxW - s2.Width - HorizontalSpacing));

                first.Measure(new Size(firstSlotW, availableSize.Height));
                return new Size(maxW, Math.Max(first.DesiredSize.Height, s2.Height));
            }
            else
            {
                _isStacked = true;
                first.Measure(new Size(maxW, availableSize.Height));
                second.Measure(new Size(maxW, availableSize.Height));
                return new Size(maxW, first.DesiredSize.Height + VerticalSpacing + second.DesiredSize.Height);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count == 0)
                return finalSize;

            if (InternalChildren.Count == 1)
            {
                InternalChildren[0].Arrange(new Rect(new Point(0, 0), finalSize));
                return finalSize;
            }

            var first = InternalChildren[0];
            var second = InternalChildren[1];

            if (second.Visibility == Visibility.Collapsed)
            {
                first.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
                return finalSize;
            }

            if (first.Visibility == Visibility.Collapsed)
            {
                second.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
                return finalSize;
            }

            if (!_isStacked)
            {
                double s2W = Math.Min(second.DesiredSize.Width, finalSize.Width);
                double s1W = StretchFirst
                    ? Math.Max(0, finalSize.Width - s2W - HorizontalSpacing)
                    : Math.Min(first.DesiredSize.Width, Math.Max(0, finalSize.Width - s2W - HorizontalSpacing));

                double y1 = Math.Max(0, (finalSize.Height - first.DesiredSize.Height) / 2.0);
                double y2 = Math.Max(0, (finalSize.Height - second.DesiredSize.Height) / 2.0);

                first.Arrange(new Rect(0, y1, s1W, first.DesiredSize.Height));
                second.Arrange(new Rect(Math.Max(0, finalSize.Width - s2W), y2, s2W, second.DesiredSize.Height));
            }
            else
            {
                double h1 = first.DesiredSize.Height;
                double h2 = second.DesiredSize.Height;
                first.Arrange(new Rect(0, 0, finalSize.Width, h1));
                second.Arrange(new Rect(0, h1 + VerticalSpacing, finalSize.Width, h2));
            }

            return finalSize;
        }
    }
}
