using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OptikFormApp.Models;

namespace OptikFormApp.ViewModels
{
    public class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush LightSuccess = CreateFrozen("#059669");
        private static readonly SolidColorBrush LightWarning = CreateFrozen("#D97706");
        private static readonly SolidColorBrush LightError = CreateFrozen("#DC2626");
        private static readonly SolidColorBrush LightInfo = CreateFrozen("#475569");

        private static readonly SolidColorBrush DarkSuccess = CreateFrozen("#34D399");
        private static readonly SolidColorBrush DarkWarning = CreateFrozen("#FBBF24");
        private static readonly SolidColorBrush DarkError = CreateFrozen("#F87171");
        private static readonly SolidColorBrush DarkInfo = CreateFrozen("#CBD5E1");

        private static SolidColorBrush CreateFrozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isDark = Application.Current?.Resources["IsDarkThemeActive"] is true;

            if (value is LogLevel level)
            {
                if (isDark)
                {
                    return level switch
                    {
                        LogLevel.Success => DarkSuccess,
                        LogLevel.Warning => DarkWarning,
                        LogLevel.Error => DarkError,
                        _ => DarkInfo
                    };
                }

                return level switch
                {
                    LogLevel.Success => LightSuccess,
                    LogLevel.Warning => LightWarning,
                    LogLevel.Error => LightError,
                    _ => LightInfo
                };
            }

            return isDark ? DarkInfo : LightInfo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
