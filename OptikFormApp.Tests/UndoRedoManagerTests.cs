using System;
using OptikFormApp.Services;
using Xunit;

namespace OptikFormApp.Tests
{
    public class UndoRedoManagerTests
    {
        private class Cmd : IUndoableCommand
        {
            public string Description => "cmd";
            public int Value;
            public void Execute() { Value = 1; }
            public void Undo() { Value = 0; }
            public void Redo() { Value = 1; }
        }

        [Fact]
        public void Execute_Undo_Redo_Works()
        {
            var m = new UndoRedoManager(10);
            var c = new Cmd();
            m.ExecuteCommand(c);
            Assert.True(m.CanUndo);
            Assert.Equal(1, c.Value);
            m.Undo();
            Assert.Equal(0, c.Value);
            Assert.True(m.CanRedo);
            m.Redo();
            Assert.Equal(1, c.Value);
        }

        [Fact]
        public void Clear_Resets()
        {
            var m = new UndoRedoManager(10);
            m.ExecuteCommand(new Cmd());
            m.Clear();
            Assert.False(m.CanUndo);
            Assert.False(m.CanRedo);
        }
    }
}