using System.Collections.Generic;

namespace OptikFormApp.Models
{
    public class QuestionSetting
    {
        public string BookletName { get; set; } = string.Empty;
        public int QuestionNumber { get; set; }
        public bool IsCancelled { get; set; }
        public CancellationMode CancelMode { get; set; } = CancellationMode.FullCredit;
        public bool IsMultipleEnabled { get; set; }
        public string MultipleCorrectAnswers { get; set; } = string.Empty; // e.g. "AB"
        public double PointValue { get; set; } = 0.0; // 0 ise varsayılan sınav net katsayısı kullanılır

        public bool IsCorrect(char studentAnswer)
        {
            if (IsCancelled)
            {
                return CancelMode == CancellationMode.FullCredit;
            }
            if (!IsMultipleEnabled || string.IsNullOrEmpty(MultipleCorrectAnswers)) return false;
            return MultipleCorrectAnswers.Contains(studentAnswer.ToString());
        }
    }
}
