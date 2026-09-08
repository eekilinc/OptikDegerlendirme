using System;
using System.ComponentModel;
using System.Linq;
using OptikFormApp.Models;
using OptikFormApp.Services;

namespace OptikFormApp.ViewModels
{
    public partial class MainViewModel
    {
        private void UpdateBulkPreview()
        {
            var lines = BulkAnswerEntryText.Split('\n', '\r')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToList();
            
            int count = 0;
            foreach (var line in lines)
            {
                var parts = line.Split(' ', '-', '.', ')');
                if (parts.Length >= 2 && int.TryParse(parts[0], out _))
                    count++;
            }
            
            BulkAnswerPreview = count > 0 ? $"{count} geçerli giriş algılandı" : "Henüz geçerli giriş yok";
        }

        private string _quickFilterStatus = "All";
        public string QuickFilterStatus
        {
            get => _quickFilterStatus;
            set
            {
                _quickFilterStatus = value;
                OnPropertyChanged();
                StudentsView.Refresh();
            }
        }

        private bool FilterStudents(object obj)
        {
            if (obj is not StudentResult student) return false;
            
            // Quick Filter Pills
            if (QuickFilterStatus == "Passed" && student.Status != "Geçti") return false;
            if (QuickFilterStatus == "Conditional" && student.Status != "Şartlı Geçti") return false;
            if (QuickFilterStatus == "Failed" && student.Status != "Kaldı") return false;

            // Text search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                bool matchesSearch = student.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     student.StudentId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     FuzzySearchService.ContainsFuzzy(student.FullName, SearchText, 0.6) ||
                                     FuzzySearchService.ContainsFuzzy(student.StudentId, SearchText, 0.7);
                if (!matchesSearch) return false;
            }
            
            // Score range filter
            if (MinScoreFilter.HasValue && student.Score < MinScoreFilter.Value) return false;
            if (MaxScoreFilter.HasValue && student.Score > MaxScoreFilter.Value) return false;
            
            // Booklet type filter
            if (!string.IsNullOrEmpty(SelectedBookletFilter) && student.BookletType != SelectedBookletFilter) return false;
            
            // Correct count filter
            if (MinCorrectFilter.HasValue && student.CorrectCount < MinCorrectFilter.Value) return false;
            
            // Wrong count filter
            if (MaxWrongFilter.HasValue && student.IncorrectCount > MaxWrongFilter.Value) return false;
            
            return true;
        }

        private void ApplySorting()
        {
            StudentsView.SortDescriptions.Clear();
            
            var sortDirection = IsSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            
            StudentsView.SortDescriptions.Add(new SortDescription(SelectedSortColumn, sortDirection));
            StudentsView.Refresh();
        }

        private void ClearAdvancedFilters()
        {
            MinScoreFilter = null;
            MaxScoreFilter = null;
            SelectedBookletFilter = null;
            MinCorrectFilter = null;
            MaxWrongFilter = null;
            SearchText = "";
            StudentsView.Refresh();
        }
    }
}
