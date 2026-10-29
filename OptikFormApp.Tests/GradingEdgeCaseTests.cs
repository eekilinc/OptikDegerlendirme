using System;
using System.Collections.Generic;
using System.Linq;
using OptikFormApp.Models;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class GradingEdgeCaseTests
    {
        private readonly OpticalParserService _svc = new();

        [Fact]
        public void EvaluateStudents_EmptyAnswerKey_Skips()
        {
            var students = new List<StudentResult> { new() { FullName="A", StudentId="1", BookletType="A", RawAnswers="AB", Answers=new List<string>{"A","B"} } };
            var keys = new List<AnswerKeyModel> { new() { BookletName="A", Answers="" } };
            _svc.EvaluateStudents(students, keys, new List<QuestionSetting>(), 0.25, 1, 0);
            Assert.Equal(0, students[0].CorrectCount);
        }

        [Fact]
        public void EvaluateStudents_DifferentLengths_Handles()
        {
            var students = new List<StudentResult> { new() { FullName="A", StudentId="1", BookletType="A", RawAnswers="A", Answers=new List<string>{"A"} } };
            var keys = new List<AnswerKeyModel> { new() { BookletName="A", Answers="AB" } };
            _svc.EvaluateStudents(students, keys, new List<QuestionSetting>(), 0.25, 1, 0);
            Assert.Equal(1, students[0].CorrectCount);
            Assert.Equal(1, students[0].EmptyCount);
        }

        [Fact]
        public void EvaluateStudents_CancelledFullCredit()
        {
            var students = new List<StudentResult> { new() { FullName="A", StudentId="1", BookletType="A", RawAnswers="B", Answers=new List<string>{"B"} } };
            var keys = new List<AnswerKeyModel> { new() { BookletName="A", Answers="A" } };
            var settings = new List<QuestionSetting> { new() { BookletName="A", QuestionNumber=1, IsCancelled=true, CancelMode=CancellationMode.FullCredit, PointValue=1 } };
            _svc.EvaluateStudents(students, keys, settings, 0.25, 1, 0);
            Assert.Equal(1, students[0].CorrectCount);
        }

        [Fact]
        public void EvaluateStudents_RankTie()
        {
            var students = new List<StudentResult>
            {
                new() { FullName="A", StudentId="1", BookletType="A", RawAnswers="A", Answers=new List<string>{"A"}, Score=10 },
                new() { FullName="B", StudentId="2", BookletType="A", RawAnswers="A", Answers=new List<string>{"A"}, Score=10 }
            };
            var keys = new List<AnswerKeyModel> { new() { BookletName="A", Answers="A" } };
            _svc.EvaluateStudents(students, keys, new List<QuestionSetting>(), 0.25, 1, 0);
            Assert.Equal(1, students[0].Rank);
            Assert.Equal(1, students[1].Rank);
        }
    }
}