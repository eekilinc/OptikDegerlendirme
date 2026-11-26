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
            var s = new OptikFormApp.Models.AppSettings
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
            };
            SaveAcademicToolsSettings(s);
            _settingsService.Save(s);
        }

        private void ApplyTheme(bool dark)
        {
            if (Application.Current == null) return;
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ApplyTheme(dark));
                return;
            }
            var res = Application.Current.Resources;
            var cc = new BrushConverter();
            
            if (dark) 
            {
                res["AppBg"] = cc.ConvertFromString("#0B1120");      // Slate-950 (Ana zemin)
                res["HeaderBg"] = cc.ConvertFromString("#111827");   // Slate-900 (Üst/alt şerit ve gömülü alanlar)
                res["CardBg"] = cc.ConvertFromString("#1E293B");     // Slate-800 (Yükseltilmiş kart yüzeyi)
                res["AltRowBg"] = cc.ConvertFromString("#162032");   // Tablo alternatif satır zemini (CardBg'den ayrışır)
                res["HoverBg"] = cc.ConvertFromString("#283548");    // Dengeli kurumsal vurgu/seçim yüzeyi
                res["TextMain"] = cc.ConvertFromString("#F1F5F9");   // Göz yormayan yüksek kontrast metin
                res["TextMuted"] = cc.ConvertFromString("#94A3B8");  // Slate-400 ikincil metin
                res["Border"] = cc.ConvertFromString("#334155");     // Slate-700 kenarlık
                res["ModalBackdrop"] = cc.ConvertFromString("#B8020617"); // Derin koyu arka plan karartması
                res["Accent"] = cc.ConvertFromString("#3B82F6");     // Blue-500
                res["AccentHover"] = cc.ConvertFromString("#60A5FA"); // Blue-400 (Koyu zeminde yüksek okunabilirlik)
                res["Success"] = cc.ConvertFromString("#10B981");     // Emerald-500
                res["Warning"] = cc.ConvertFromString("#F59E0B");     // Amber-500
                res["Error"] = cc.ConvertFromString("#EF4444");       // Red-500
                
                res["AccentBgLight"] = cc.ConvertFromString("#172554");  // Koyu kurumsal lacivert rozet zemini
                res["SuccessBgLight"] = cc.ConvertFromString("#064E3B"); // Koyu zümrüt rozet zemini
                res["WarningBgLight"] = cc.ConvertFromString("#451A03"); // Koyu kehribar rozet zemini
                res["ErrorBgLight"] = cc.ConvertFromString("#450A0A");   // Koyu bordo rozet zemini
                res["AccentPurple"] = cc.ConvertFromString("#A78BFA");
                res["AccentViolet"] = cc.ConvertFromString("#8B5CF6");
                res["AccentOrange"] = cc.ConvertFromString("#F59E0B");
                res["AccentGreen"] = cc.ConvertFromString("#34D399");
                res["AccentRed"] = cc.ConvertFromString("#DC2626");
                res["AccentCyan"] = cc.ConvertFromString("#22D3EE");
                res["AccentTeal"] = cc.ConvertFromString("#2DD4BF");
                
                res["GlassCardBgColor"] = cc.ConvertFromString("#1E293B");
                res["GlassCardBorderColor"] = cc.ConvertFromString("#334155");
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
                
                res["AccentBgLight"] = cc.ConvertFromString("#DBEAFE"); // Light blue bg
                res["SuccessBgLight"] = cc.ConvertFromString("#DCFCE7"); // Light green bg
                res["WarningBgLight"] = cc.ConvertFromString("#FEF3C7"); // Light orange bg
                res["ErrorBgLight"] = cc.ConvertFromString("#FEE2E2"); // Light red bg
                res["AccentPurple"] = cc.ConvertFromString("#8B5CF6");
                res["AccentViolet"] = cc.ConvertFromString("#7C3AED");
                res["AccentOrange"] = cc.ConvertFromString("#D97706");
                res["AccentGreen"] = cc.ConvertFromString("#059669");
                res["AccentRed"] = cc.ConvertFromString("#B91C1C");
                res["AccentCyan"] = cc.ConvertFromString("#06B6D4");
                res["AccentTeal"] = cc.ConvertFromString("#14B8A6");
                
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
            res["AccentBgLightColor"] = getColor("AccentBgLight");
            res["SuccessBgLightColor"] = getColor("SuccessBgLight");
            res["WarningBgLightColor"] = getColor("WarningBgLight");
            res["ErrorBgLightColor"] = getColor("ErrorBgLight");
            res["AccentPurpleColor"] = getColor("AccentPurple");
            res["AccentVioletColor"] = getColor("AccentViolet");
            res["AccentOrangeColor"] = getColor("AccentOrange");
            res["AccentGreenColor"] = getColor("AccentGreen");
            res["AccentRedColor"] = getColor("AccentRed");
            res["AccentCyanColor"] = getColor("AccentCyan");
            res["AccentTealColor"] = getColor("AccentTeal");

            res[SystemColors.MenuBrushKey] = res["CardBg"];
            res[SystemColors.MenuTextBrushKey] = res["TextMain"];
            res[SystemColors.WindowBrushKey] = res["AppBg"];
            res[SystemColors.WindowTextBrushKey] = res["TextMain"];
            res[SystemColors.ControlBrushKey] = res["CardBg"];
            res[SystemColors.ControlTextBrushKey] = res["TextMain"];
            res[SystemColors.HighlightBrushKey] = res["HoverBg"];
            res[SystemColors.HighlightTextBrushKey] = res["TextMain"];

            // Dönüştürücü tabanlı hücre renklerini (cevap kutuları, log satırları, detay modalı) anında tazele
            res["IsDarkThemeActive"] = dark;
            StudentsView?.Refresh();
            if (Logs != null)
            {
                System.Windows.Data.CollectionViewSource.GetDefaultView(Logs)?.Refresh();
            }
            if (SelectedStudentDetail != null)
            {
                System.Windows.Data.CollectionViewSource.GetDefaultView(SelectedStudentDetail.ColoredAnswers)?.Refresh();
                OnPropertyChanged(nameof(SelectedStudentDetail));
            }
        }

        private void ApplyFontSize(double size)
        {
            if (Application.Current == null) return;
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ApplyFontSize(size));
                return;
            }
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
