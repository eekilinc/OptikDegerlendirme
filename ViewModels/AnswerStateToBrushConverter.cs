using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OptikFormApp.Models;

namespace OptikFormApp.ViewModels
{
    public class AnswerStateToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush LightCorrect = CreateFrozen("#059669");
        private static readonly SolidColorBrush LightIncorrect = CreateFrozen("#DC2626");
        private static readonly SolidColorBrush LightEmpty = CreateFrozen("#94A3B8");
        private static readonly SolidColorBrush LightNeutral = CreateFrozen("#1E293B");

        private static readonly SolidColorBrush DarkCorrect = CreateFrozen("#34D399");
        private static readonly SolidColorBrush DarkIncorrect = CreateFrozen("#F87171");
        private static readonly SolidColorBrush DarkEmpty = CreateFrozen("#64748B");
        private static readonly SolidColorBrush DarkNeutral = CreateFrozen("#E2E8F0");

        private static SolidColorBrush CreateFrozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDark = Application.Current?.Resources["IsDarkThemeActive"] is true;

            if (value is AnswerState state)
            {
                if (isDark)
                {
                    return state switch
                    {
                        AnswerState.Correct => DarkCorrect,
                        AnswerState.Incorrect => DarkIncorrect,
                        AnswerState.Empty => DarkEmpty,
                        _ => DarkNeutral
                    };
                }

                return state switch
                {
                    AnswerState.Correct => LightCorrect,
                    AnswerState.Incorrect => LightIncorrect,
                    AnswerState.Empty => LightEmpty,
                    _ => LightNeutral
                };
            }

            return isDark ? DarkNeutral : LightNeutral;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
