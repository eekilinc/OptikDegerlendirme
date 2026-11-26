using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OptikFormApp.Models;

namespace OptikFormApp.ViewModels
{
    public class AnswerStateToBackgroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush LightCorrectBg = CreateFrozen("#D1FAE5");
        private static readonly SolidColorBrush LightIncorrectBg = CreateFrozen("#FEE2E2");
        private static readonly SolidColorBrush LightEmptyBg = CreateFrozen("#F1F5F9");
        private static readonly SolidColorBrush LightNeutralBg = CreateFrozen("#FFFFFF");

        private static readonly SolidColorBrush DarkCorrectBg = CreateFrozen("#064E3B");
        private static readonly SolidColorBrush DarkIncorrectBg = CreateFrozen("#450A0A");
        private static readonly SolidColorBrush DarkEmptyBg = CreateFrozen("#111827");
        private static readonly SolidColorBrush DarkNeutralBg = CreateFrozen("#1E293B");

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
                        AnswerState.Correct => DarkCorrectBg,
                        AnswerState.Incorrect => DarkIncorrectBg,
                        AnswerState.Empty => DarkEmptyBg,
                        _ => DarkNeutralBg
                    };
                }

                return state switch
                {
                    AnswerState.Correct => LightCorrectBg,
                    AnswerState.Incorrect => LightIncorrectBg,
                    AnswerState.Empty => LightEmptyBg,
                    _ => LightNeutralBg
                };
            }

            return isDark ? DarkNeutralBg : LightNeutralBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
