using System;
using System.Windows;
using System.Windows.Media;
using OptikFormApp.Models;

namespace OptikFormApp.ViewModels
{
    public partial class MainViewModel
    {
        public void AddToLog(string message, LogLevel level = LogLevel.Info)
        {
            if (Application.Current == null)
            {
                Logs.Insert(0, new LogEntry { Message = message, Level = level });
                return;
            }

            Application.Current.Dispatcher.Invoke(() => {
                Logs.Insert(0, new LogEntry { Message = message, Level = level });
                if (Logs.Count > 100) Logs.RemoveAt(100);
            });
        }

        private void SaveSettings()
        {
            _settingsService.Save(new OptikFormApp.Models.AppSettings
            {
                SchoolName = _schoolName,
                DefaultExcelPath = _defaultExcelPath,
                NetCoefficient = _netCoefficient,
                BaseScore = _baseScore,
                WrongDeductionFactor = _wrongDeductionFactor,
                ThemeIndex = _themeIndex,
                LayoutIndex = _layoutIndex,
                FontSize = (int)_fontSize,
                GradingModeIndex = _gradingSettings.Mode == GradingMode.RelativeEvaluation ? 0 : 1,
                RelativeThreshold = _gradingSettings.RelativeThreshold
            });
        }

        private void ApplyTheme(bool dark) 
        {
            if (Application.Current == null) return;
            var res = Application.Current.Resources;
            var cc = new BrushConverter();
            
            if (dark) 
            {
                res["AppBg"] = cc.ConvertFromString("#0F172A");      // Slate-900
                res["CardBg"] = cc.ConvertFromString("#1E293B");     // Slate-800  
                res["TextMain"] = cc.ConvertFromString("#F8FAFC");    // Slate-50
                res["TextMuted"] = cc.ConvertFromString("#94A3B8");  // Slate-400
                res["Border"] = cc.ConvertFromString("#334155");     // Slate-700
                res["HeaderBg"] = cc.ConvertFromString("#0F172A");    // Slate-900
                res["AltRowBg"] = cc.ConvertFromString("#1E293B");   // Slate-800
                res["HoverBg"] = cc.ConvertFromString("#475569");    // Slate-600
                res["ModalBackdrop"] = cc.ConvertFromString("#B3000000"); // Black with opacity
                res["Accent"] = cc.ConvertFromString("#3B82F6");     // Blue-600
                res["AccentHover"] = cc.ConvertFromString("#2563EB");  // Blue-700
                res["Success"] = cc.ConvertFromString("#10B981");     // Green-600
                res["Warning"] = cc.ConvertFromString("#F59E0B");     // Amber-600
                res["Error"] = cc.ConvertFromString("#EF4444");       // Red-600
                
                res["GlassCardBgColor"] = cc.ConvertFromString("#FFFFFF");
                res["GlassCardBorderColor"] = cc.ConvertFromString("#FFFFFF");
                res["ShadowColor"] = cc.ConvertFromString("#000000");
            } 
            else 
            {
                res["AppBg"] = cc.ConvertFromString("#F8FAFC");      // Slate-50
                res["CardBg"] = cc.ConvertFromString("#FFFFFF");       // White
                res["TextMain"] = cc.ConvertFromString("#1E293B");    // Slate-800
                res["TextMuted"] = cc.ConvertFromString("#475569");  // Slate-600 (better contrast)
                res["Border"] = cc.ConvertFromString("#E2E8F0");     // Slate-200
                res["HeaderBg"] = cc.ConvertFromString("#F1F5F9");    // Slate-100
                res["AltRowBg"] = cc.ConvertFromString("#F8FAFC");   // Slate-50
                res["HoverBg"] = cc.ConvertFromString("#EFF6FF");    // Blue-50
                res["ModalBackdrop"] = cc.ConvertFromString("#800F172A"); // Black with opacity
                res["Accent"] = cc.ConvertFromString("#3B82F6");     // Blue-600
                res["AccentHover"] = cc.ConvertFromString("#2563EB");  // Blue-700
                res["Success"] = cc.ConvertFromString("#10B981");     // Green-600
                res["Warning"] = cc.ConvertFromString("#F59E0B");     // Amber-600
                res["Error"] = cc.ConvertFromString("#EF4444");       // Red-600
                
                res["GlassCardBgColor"] = cc.ConvertFromString("#FFFFFF");
                res["GlassCardBorderColor"] = cc.ConvertFromString("#FFFFFF");
                res["ShadowColor"] = cc.ConvertFromString("#000000");
            }
            
            // Color kaynaklarini brush'lardan turet (Color= attribute'lari icin)
            Func<string, Color> getColor = key => ((SolidColorBrush)res[key]).Color;
            res["AppBgColor"] = getColor("AppBg");
            res["CardBgColor"] = getColor("CardBg");
            res["TextMainColor"] = getColor("TextMain");
            res["TextMutedColor"] = getColor("TextMuted");
            res["BorderColor"] = getColor("Border");
            res["HeaderBgColor"] = getColor("HeaderBg");
            res["HoverBgColor"] = getColor("HoverBg");
            res["AccentColor"] = getColor("Accent");
            res["AccentHoverColor"] = getColor("AccentHover");
            res["SuccessColor"] = getColor("Success");
            res["WarningColor"] = getColor("Warning");
            res["ErrorColor"] = getColor("Error");
            res["ShadowColorValue"] = getColor("ShadowColor");

            res[SystemColors.MenuBrushKey] = res["CardBg"];
            res[SystemColors.MenuTextBrushKey] = res["TextMain"];
            res[SystemColors.WindowBrushKey] = res["AppBg"];
            res[SystemColors.WindowTextBrushKey] = res["TextMain"];
            res[SystemColors.ControlBrushKey] = res["CardBg"];
            res[SystemColors.ControlTextBrushKey] = res["TextMain"];
            res[SystemColors.HighlightBrushKey] = res["HoverBg"];
            res[SystemColors.HighlightTextBrushKey] = res["TextMain"];
        }

        private void ApplyFontSize(double size)
        {
            if (Application.Current == null) return;
            var res = Application.Current.Resources;
            res["GlobalFontSize"] = size;
            res["GlobalHeaderFontSize"] = size + 4;
            res["GlobalSmallFontSize"] = size - 2;
        }

        private void RefreshTemplates()
        {
            Templates.Clear();
            foreach (var template in _templateService.GetAllTemplates())
                Templates.Add(template);
        }

        private void ShowAlert(string title, string message)
        {
            AlertTitle = title;
            AlertMessage = message;
            IsAlertOpen = true;
        }

        public void ShowToast(string message, string title = "", ToastType type = ToastType.Info)
        {
            _notificationService.Show(message, title, type);
        }

        public void ShowToastSuccess(string message, string title = "Başarılı")
        {
            _notificationService.ShowSuccess(message, title);
        }

        public void ShowToastError(string message, string title = "Hata")
        {
            _notificationService.ShowError(message, title);
        }

        public void ShowToastWarning(string message, string title = "Uyarı")
        {
            _notificationService.ShowWarning(message, title);
        }

        public void ShowToastInfo(string message, string title = "Bilgi")
        {
            _notificationService.ShowInfo(message, title);
        }
    }
}
