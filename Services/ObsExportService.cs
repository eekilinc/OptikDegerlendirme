using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using OptikFormApp.Models;

namespace OptikFormApp.Services
{
    public enum ObsRoundingMode
    {
        Standard = 0, // 54.5 -> 55 (MidpointRounding.AwayFromZero)
        Ceiling = 1,  // 54.1 -> 55
        Decimal = 2   // 54.50 (2 ondalık)
    }

    public class ObsGradeRow
    {
        public int RowNumber { get; set; }
        public string StudentId { get; set; } = "";
        public string FullName { get; set; } = "";
        public double RawScore { get; set; }
        public double RoundedScore { get; set; }
        public string FormattedScore { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class ObsExportService
    {
        public List<ObsGradeRow> BuildObsRows(IEnumerable<StudentResult> students, ObsRoundingMode mode)
        {
            if (students == null) return new List<ObsGradeRow>();

            return students
                .OrderBy(s => s.StudentId, StringComparer.OrdinalIgnoreCase)
                .Select((s, idx) =>
                {
                    double rounded = mode switch
                    {
                        ObsRoundingMode.Standard => Math.Round(s.Score, 0, MidpointRounding.AwayFromZero),
                        ObsRoundingMode.Ceiling => Math.Ceiling(s.Score),
                        _ => Math.Round(s.Score, 2)
                    };
                    rounded = Math.Clamp(rounded, 0, 100);

                    string formatted = mode == ObsRoundingMode.Decimal
                        ? rounded.ToString("F2", new CultureInfo("tr-TR"))
                        : ((int)rounded).ToString(CultureInfo.InvariantCulture);

                    return new ObsGradeRow
                    {
                        RowNumber = idx + 1,
                        StudentId = s.StudentId,
                        FullName = s.FullName,
                        RawScore = Math.Round(s.Score, 2),
                        RoundedScore = rounded,
                        FormattedScore = formatted,
                        Status = s.Status ?? ""
                    };
                })
                .ToList();
        }

        public string BuildClipboardText(List<ObsGradeRow> rows, bool scoresOnly = false)
        {
            if (rows == null || rows.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var r in rows)
            {
                if (scoresOnly)
                    sb.AppendLine(r.FormattedScore);
                else
                    sb.AppendLine($"{r.StudentId}\t{r.FormattedScore}");
            }
            return sb.ToString().TrimEnd();
        }

        public void ExportToExcel(List<ObsGradeRow> rows, string filePath, string courseTitle = "OBS Not Listesi")
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("OBS Not Listesi");

            ws.Cell(1, 1).Value = "Öğrenci No";
            ws.Cell(1, 2).Value = "Ad Soyad";
            ws.Cell(1, 3).Value = "Ham Puan";
            ws.Cell(1, 4).Value = "OBS Notu";

            var headerRange = ws.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            headerRange.Style.Font.FontColor = XLColor.White;

            int rowIdx = 2;
            foreach (var r in rows)
            {
                ws.Cell(rowIdx, 1).Value = r.StudentId;
                ws.Cell(rowIdx, 2).Value = r.FullName;
                ws.Cell(rowIdx, 3).Value = r.RawScore;
                ws.Cell(rowIdx, 4).Value = r.RoundedScore;
                rowIdx++;
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        public void ExportToCsv(List<ObsGradeRow> rows, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("OgrenciNo;AdSoyad;OBSNotu");
            foreach (var r in rows)
            {
                sb.AppendLine($"{r.StudentId};{r.FullName};{r.FormattedScore}");
            }
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
