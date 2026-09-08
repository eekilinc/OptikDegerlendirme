using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OptikFormApp.Services
{
    public class BookletMappingValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<int, int> ValidMapping { get; set; } = new();
    }

    /// <summary>
    /// A kitapçığından B, C, D gibi diğer kitapçıkların cevap anahtarını
    /// soru eşleme matrisi (permütasyon) ile otomatik türetme servisi.
    /// </summary>
    public class BookletMappingService
    {
        /// <summary>
        /// Kullanıcının yapıştırdığı metni ayrıştırır.
        /// Desteklenen formatlar:
        /// 1) "1->5, 2->1, 3->4" veya "1:5, 2:1" veya "1=5" (Hedef -> Kaynak)
        /// 2) Satır satır: "1 5\n 2 1\n 3 4" (Hedef Kaynak)
        /// 3) Virgülle ayrılmış sıra: "5, 1, 4, 2, 3" (Hedefin 1., 2., 3. sorularının Kaynaktaki numaraları)
        /// </summary>
        public BookletMappingValidationResult ParseAndValidate(string input, int expectedQuestionCount)
        {
            var result = new BookletMappingValidationResult();

            if (string.IsNullOrWhiteSpace(input))
            {
                result.IsValid = false;
                result.ErrorMessage = "Eşleme metni boş olamaz.";
                return result;
            }

            if (expectedQuestionCount <= 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "Geçerli bir soru sayısı belirtilmelidir.";
                return result;
            }

            var mapping = new Dictionary<int, int>();

            // Durum 1: "1->5", "1:5", "1=5", "1-5" okları
            var arrowMatches = Regex.Matches(input, @"(\d+)\s*(?:->|:|=>|=|-)\s*(\d+)");
            if (arrowMatches.Count > 0)
            {
                foreach (Match m in arrowMatches)
                {
                    int targetQ = int.Parse(m.Groups[1].Value);
                    int sourceQ = int.Parse(m.Groups[2].Value);
                    mapping[targetQ] = sourceQ;
                }
            }
            else
            {
                // Durum 2: İki sütunlu satırlar ("1 5\n2 1")
                var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool isTwoColumn = true;
                var tempMap = new Dictionary<int, int>();

                foreach (var rawLine in lines)
                {
                    var tokens = Regex.Split(rawLine.Trim(), @"\s+|,|;");
                    tokens = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

                    if (tokens.Length == 2 && int.TryParse(tokens[0], out int tQ) && int.TryParse(tokens[1], out int sQ))
                    {
                        tempMap[tQ] = sQ;
                    }
                    else
                    {
                        isTwoColumn = false;
                        break;
                    }
                }

                if (isTwoColumn && tempMap.Count > 0)
                {
                    mapping = tempMap;
                }
                else
                {
                    // Durum 3: Tek sıralı liste: "5, 1, 4, 2, 3" veya boşlukla ayrılmış
                    var allNumbers = Regex.Matches(input, @"\d+")
                        .Select(m => int.Parse(m.Value))
                        .ToList();

                    if (allNumbers.Count == expectedQuestionCount)
                    {
                        for (int i = 0; i < allNumbers.Count; i++)
                        {
                            mapping[i + 1] = allNumbers[i];
                        }
                    }
                }
            }

            // Doğrulama (Validation)
            if (mapping.Count != expectedQuestionCount)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Soru sayısı uyumsuz: {expectedQuestionCount} soru bekleniyordu ancak {mapping.Count} adet eşleşme bulundu.";
                return result;
            }

            // Tüm 1..N hedef soruları var mı?
            for (int i = 1; i <= expectedQuestionCount; i++)
            {
                if (!mapping.ContainsKey(i))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Hedef kitapçığın {i}. sorusuna ait bir eşleşme tanımlanmamış.";
                    return result;
                }
            }

            // Kaynak sorular (1..N) benzersiz mi ve aralıkta mı?
            var sourceQuestions = mapping.Values.ToList();
            var outOfRange = sourceQuestions.Where(q => q < 1 || q > expectedQuestionCount).ToList();
            if (outOfRange.Any())
            {
                result.IsValid = false;
                result.ErrorMessage = $"Kaynak kitapçıkta bulunmayan geçersiz soru numaraları var: {string.Join(", ", outOfRange)}";
                return result;
            }

            var duplicateSources = sourceQuestions.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateSources.Any())
            {
                result.IsValid = false;
                result.ErrorMessage = $"Aynı kaynak soru birden fazla kez eşleştirilmiş: {string.Join(", ", duplicateSources)}";
                return result;
            }

            result.IsValid = true;
            result.ValidMapping = mapping;
            return result;
        }

        /// <summary>
        /// Kaynak cevap anahtarı dizisi ve doğrulanmış harita üzerinden hedef kitapçığın cevap anahtarını oluşturur.
        /// </summary>
        /// <param name="sourceAnswers">Örn: "ABCDE"</param>
        /// <param name="mapping">Hedef Soru No -> Kaynak Soru No</param>
        public string GenerateTargetAnswers(string sourceAnswers, IDictionary<int, int> mapping)
        {
            if (string.IsNullOrEmpty(sourceAnswers))
                throw new ArgumentException("Kaynak cevap anahtarı boş olamaz.", nameof(sourceAnswers));

            char[] targetChars = new char[mapping.Count];

            for (int targetQ = 1; targetQ <= mapping.Count; targetQ++)
            {
                if (!mapping.TryGetValue(targetQ, out int sourceQ))
                    throw new InvalidOperationException($"{targetQ}. soru için eşleme bulunamadı.");

                int sourceIndex = sourceQ - 1;
                if (sourceIndex < 0 || sourceIndex >= sourceAnswers.Length)
                    throw new InvalidOperationException($"Kaynak soru no {sourceQ}, kaynak cevap anahtarı sınırları dışında.");

                targetChars[targetQ - 1] = sourceAnswers[sourceIndex];
            }

            return new string(targetChars);
        }
    }
}
