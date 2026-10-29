using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class FuzzySearchServiceTests
    {
        [Fact]
        public void LevenshteinDistance_Identical_Zero()
        {
            Assert.Equal(0, FuzzySearchService.LevenshteinDistance("abc", "abc"));
        }

        [Fact]
        public void SimilarityRatio_Identical_One()
        {
            Assert.Equal(1.0, FuzzySearchService.SimilarityRatio("abc", "abc"));
        }

        [Fact]
        public void IsFuzzyMatch_True()
        {
            Assert.True(FuzzySearchService.IsFuzzyMatch("ahmet", "ahmett"));
        }

        [Fact]
        public void ContainsFuzzy_True()
        {
            Assert.True(FuzzySearchService.ContainsFuzzy("ahmet yilmaz", "ahmet"));
        }
    }
}