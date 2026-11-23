using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using OptikFormApp.Models;

namespace OptikFormApp.Services
{
    public class RosterEntry
    {
        public string StudentId { get; set; } = "";
        public string FullName { get; set; } = "";
    }

    public class SuspectedMismatchItem
    {
        public StudentResult OpticalStudent { get; set; } = null!;
        public string OpticalId { get; set; } = "";
        public string OpticalName { get; set; } = "";
        public string SuggestedRosterId { get; set; } = "";
        public string SuggestedRosterName { get; set; } = "";
        public double SimilarityPercent { get; set; }
    }

    public class RosterMatchResult
    {
        public int TotalRosterCount { get; set; }
        public int MatchedCount { get; set; }
        public List<RosterEntry> AbsentStudents { get; set; } = new();
        public List<SuspectedMismatchItem> SuspectedMismatches { get; set; } = new();
        public List<StudentResult> UnlistedOpticalStudents { get; set; } = new();
    }

    public class RosterMatchService
    {
        public List<RosterEntry> LoadRosterFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Sınıf listesi dosyası bulunamadı.", filePath);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".xlsx" || ext == ".xlsm")
                return LoadFromExcel(filePath);

            return LoadFromCsv(filePath);
        }

        private List<RosterEntry> LoadFromExcel(string filePath)
        {
            var list = new List<RosterEntry>();
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            var usedRange = ws.RangeUsed();
            if (usedRange == null) return list;

            int idCol = 1;
            int nameCol = 2;
            int startRow = 1;

            // İlk satırda başlık tespiti
            var firstRow = usedRange.FirstRow();
            int colCount = usedRange.ColumnCount();
            bool hasHeader = false;
            for (int c = 1; c <= colCount; c++)
            {
                string val = firstRow.Cell(c).GetString().Trim().ToLowerInvariant();
                if (val.Contains("no") || val.Contains("numara") || val.Contains("id"))
                {
                    idCol = c;
                    hasHeader = true;
                }
                else if (val.Contains("ad") || val.Contains("isim") || val.Contains("soyad") || val.Contains("öğrenci"))
                {
                    nameCol = c;
                    hasHeader = true;
                }
            }
            if (hasHeader) startRow = 2;

            int rowCount = usedRange.RowCount();
            for (int r = startRow; r <= rowCount; r++)
            {
                var row = usedRange.Row(r);
                string id = NormalizeId(row.Cell(idCol).GetString());
                string name = colCount >= nameCol ? row.Cell(nameCol).GetString().Trim() : "";
                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name)) continue;

                // Eğer 3. sütun Soyad ise birleştir
                if (hasHeader && colCount >= nameCol + 1)
                {
                    string hNext = firstRow.Cell(nameCol + 1).GetString().Trim().ToLowerInvariant();
                    if (hNext.Contains("soyad"))
                    {
                        string surname = row.Cell(nameCol + 1).GetString().Trim();
                        if (!string.IsNullOrWhiteSpace(surname))
                            name = $"{name} {surname}".Trim();
                    }
                }

                list.Add(new RosterEntry { StudentId = id, FullName = name });
            }
            return list;
        }

        private List<RosterEntry> LoadFromCsv(string filePath)
        {
            var list = new List<RosterEntry>();
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0) return list;

            char sep = lines[0].Contains(';') ? ';' : (lines[0].Contains('\t') ? '\t' : ',');
            int start = 0;
            string headerLower = lines[0].ToLowerInvariant();
            if (headerLower.Contains("no") || headerLower.Contains("ad") || headerLower.Contains("numara"))
                start = 1;

            for (int i = start; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var parts = lines[i].Split(sep);
                string id = parts.Length > 0 ? NormalizeId(parts[0]) : "";
                string name = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) && !double.TryParse(parts[2].Trim(), out _))
                    name = $"{name} {parts[2].Trim().Trim('"')}".Trim();

                if (!string.IsNullOrWhiteSpace(id) || !string.IsNullOrWhiteSpace(name))
                    list.Add(new RosterEntry { StudentId = id, FullName = name });
            }
            return list;
        }

        public RosterMatchResult Match(List<RosterEntry> roster, List<StudentResult> opticalStudents)
        {
            var res = new RosterMatchResult { TotalRosterCount = roster.Count };
            var opticalById = new Dictionary<string, StudentResult>(StringComparer.OrdinalIgnoreCase);
            var unmatchedOptical = new List<StudentResult>();

            foreach (var st in opticalStudents)
            {
                string normId = NormalizeId(st.StudentId);
                if (!string.IsNullOrEmpty(normId) && !opticalById.ContainsKey(normId))
                    opticalById[normId] = st;
            }

            var matchedOpticalSet = new HashSet<StudentResult>();
            var missingRoster = new List<RosterEntry>();

            foreach (var r in roster)
            {
                string normId = NormalizeId(r.StudentId);
                if (!string.IsNullOrEmpty(normId) && opticalById.TryGetValue(normId, out var matched))
                {
                    res.MatchedCount++;
                    matchedOpticalSet.Add(matched);
                }
                else
                {
                    missingRoster.Add(r);
                }
            }

            foreach (var st in opticalStudents)
            {
                if (!matchedOpticalSet.Contains(st))
                    unmatchedOptical.Add(st);
            }

            // Numarasını yanlış kodlayanları Fuzzy isim benzerliğiyle yakala
            var usedRoster = new HashSet<RosterEntry>();
            var remainingOptical = new List<StudentResult>();

            foreach (var opt in unmatchedOptical)
            {
                RosterEntry? bestRoster = null;
                double bestSim = 0;

                foreach (var r in missingRoster)
                {
                    if (usedRoster.Contains(r)) continue;
                    double sim = FuzzySearchService.SimilarityRatio(opt.FullName ?? "", r.FullName ?? "");
                    if (sim > bestSim)
                    {
                        bestSim = sim;
                        bestRoster = r;
                    }
                }

                if (bestRoster != null && bestSim >= 0.65)
                {
                    usedRoster.Add(bestRoster);
                    res.SuspectedMismatches.Add(new SuspectedMismatchItem
                    {
                        OpticalStudent = opt,
                        OpticalId = opt.StudentId ?? "",
                        OpticalName = opt.FullName ?? "",
                        SuggestedRosterId = bestRoster.StudentId ?? "",
                        SuggestedRosterName = bestRoster.FullName ?? "",
                        SimilarityPercent = Math.Round(bestSim * 100, 1)
                    });
                }
                else
                {
                    remainingOptical.Add(opt);
                }
            }

            res.AbsentStudents = missingRoster.Where(r => !usedRoster.Contains(r)).ToList();
            res.UnlistedOpticalStudents = remainingOptical;
            return res;
        }

        public void ExportAbsentToCsv(List<RosterEntry> absentStudents, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("OgrenciNo;AdSoyad;Durum");
            foreach (var a in absentStudents)
            {
                sb.AppendLine($"{a.StudentId};{a.FullName};G (Girmedi)");
            }
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        private static string NormalizeId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return raw.Trim().Trim('"').Replace(" ", "");
        }
    }
}
