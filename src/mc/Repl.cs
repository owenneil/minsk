using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Minsk
{
    internal abstract class Repl
    {
        private readonly List<string> _history = new List<string>();
        private int _historyIndex;
        
        private readonly List<string> _lines = new List<string>();
        private int _startLeft;
        private int _startTop;
        private int _lineIndex;
        private int _position;
        private int _endLeft;
        private int _endTop;
        private string _submission;

        public void Run()
        {
            while (true)
            {
                var submission = EditSubmission();
                if (string.IsNullOrEmpty(submission))
                    return;

                if (submission.StartsWith("#"))
                    EvaluateMetaCommand(submission.Substring(1));
                else
                    EvaluateSubmission(submission);

                _history.Add(submission);
                _historyIndex = _history.Count;
            }
        }

        private void Render()
        {
            Console.CursorVisible = false;
            Console.SetCursorPosition(_startLeft, _startTop);

            for (var i = 0; i < _lines.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.Green;

                if (i == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");

                Console.ResetColor();

                PaintLine(_lines[i]);

                Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));
            }

            var newEndLeft = Console.CursorLeft;
            var newEndTop = Console.CursorTop;

            FillBlanks(newEndLeft, newEndTop, _endLeft, _endTop);

            _endLeft = newEndLeft;
            _endTop = newEndTop;

            Console.CursorVisible = true;
        }

        private void FillBlanks(int startLeft, int startTop, int endLeft, int endTop)
        {
            if (startTop > endTop)
                return;

            if (startTop == endTop)
            {
                var delta = endLeft - startLeft;
                if (delta < 0)
                    return;

                Console.SetCursorPosition(startLeft, startTop);
                Console.Write(new string(' ', delta));
            }
            else
            {
                Console.SetCursorPosition(startLeft, startTop);
                Console.Write(new string(' ', Console.WindowWidth - startLeft));

                for (var i = startTop + 1; i < endTop; i++)
                {
                    Console.SetCursorPosition(0, i);
                    Console.Write(new string(' ', Console.WindowWidth));
                }

                Console.SetCursorPosition(0, endTop);
                Console.Write(new string(' ', endLeft));
            }
        }

        private int GetVisualLineIndex()
        {
            var visualLineIndex = 0;

            for (var i = 0; i < _lineIndex; i++)
            {
                var totalLength = 2 + _lines[i].Length;
                var height = 1 + totalLength / Console.WindowWidth;
                visualLineIndex += height;
            }

            return visualLineIndex;
        }

        private void UpdateCursorPosition()
        {
            var left = (2 + _position) % Console.WindowWidth;
            var top = _startTop + GetVisualLineIndex();
            Console.SetCursorPosition(left, top);
        }

        private string EditSubmission()
        {
            _startLeft = Console.CursorLeft;
            _startTop = Console.CursorTop;
            _lines.Clear();
            _lines.Add("");
            _lineIndex = 0;
            _position = 0;
            _submission = null;
            Render();
            UpdateCursorPosition();

            while (_submission == null)
            {
                var key = Console.ReadKey(true);
                Handle(key);
            }

            return _submission;
        }

        protected virtual void EvaluateMetaCommand(string command)
        {
            Console.WriteLine($"Unknown command: #{command}");
        }

        protected virtual bool IsCompleteSubmission(string text)
        {
            return true;
        }

        protected abstract void EvaluateSubmission(string text);

        protected virtual void PaintLine(string text)
        {
            Console.Write(text);
        }

        private void Handle(ConsoleKeyInfo key)
        {
            if (key.Modifiers == default(ConsoleModifiers))
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        Enter();
                        return;
                    case ConsoleKey.Escape:
                        Clear();
                        return;
                    case ConsoleKey.Backspace:
                        DeleteLeft();
                        return;
                    case ConsoleKey.Delete:
                        DeleteRight();
                        return;
                    case ConsoleKey.Home:
                        MoveLineHome();
                        return;
                    case ConsoleKey.End:
                        MoveLineEnd();
                        return;
                    case ConsoleKey.LeftArrow:
                        MoveLeft();
                        return;
                    case ConsoleKey.RightArrow:
                        MoveRight();
                        return;
                    case ConsoleKey.UpArrow:
                        MoveUp();
                        return;
                    case ConsoleKey.DownArrow:
                        MoveDown();
                        return;
                    case ConsoleKey.Tab:
                        InsertTab();
                        return;
                    case ConsoleKey.PageUp:
                        UsePreviousHistoryItem();
                        return;
                    case ConsoleKey.PageDown:
                        UseNextHistoryItem();
                        return;
                }                
            }
            else if (key.Modifiers == ConsoleModifiers.Control)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        InsertEnter();
                        return;
                    case ConsoleKey.Backspace:
                        DeleteLeftWord();
                        return;
                    case ConsoleKey.Delete:
                        DeleteRightWord();
                        return;
                    case ConsoleKey.LeftArrow:
                        MoveLeftWord();
                        return;
                    case ConsoleKey.RightArrow:
                        MoveRightWord();
                        return;
                }                
            }

            Type(key.KeyChar);
        }

        private void Enter()
        {
            if (_lines.Count == 0)
                return;

            var line = _lines[_lineIndex];

            if (_lineIndex == 0 && line == "")
            {
                _submission = string.Empty;
                Console.WriteLine();
                return;
            }

            var text = string.Join(Environment.NewLine, _lines);

            if (line == "" || IsCompleteSubmission(text))
            {
                _lineIndex = _lines.Count - 1;
                _position = _lines[_lineIndex].Length;
                _submission = text;
                UpdateCursorPosition();
                Console.WriteLine();
                return;
            }

            _lines.Add("");
            _lineIndex = _lines.Count - 1;
            _position = 0;

            Render();
            UpdateCursorPosition();
        }

        private void Clear()
        {
            _lines[_lineIndex] = "";
            _position = 0;

            Render();
            UpdateCursorPosition();
        }

        private void DeleteLeft()
        {
            if (_position == 0)
            {
                if (_lines.Count > 1)
                {
                    var currentLine = _lines[_lineIndex];
                    var previousLine = _lines[_lineIndex - 1];
                    var mergedLine = previousLine + currentLine; 
                    _lines.RemoveAt(_lineIndex);
                    _lineIndex--;
                    _lines[_lineIndex] = mergedLine;
                    _position = previousLine.Length;

                    Render();
                    UpdateCursorPosition();
                }

                return;
            }
            
            _lines[_lineIndex] = _lines[_lineIndex].Remove(_position - 1, 1);
            _position--;

            Render();
            UpdateCursorPosition();
        }

        private void DeleteLeftWord()
        {
            if (_position == 0)
                return;

            var wordStart = FindWordStart();
            var length = _position - wordStart;
            _lines[_lineIndex] = _lines[_lineIndex].Remove(wordStart, length);
            _position -= length;

            Render();
            UpdateCursorPosition();
        }

        private void DeleteRight()
        {
            if (_position == _lines[_lineIndex].Length)
                return;
            
            _lines[_lineIndex] = _lines[_lineIndex].Remove(_position, 1);

            Render();
            UpdateCursorPosition();
        }

        private void DeleteRightWord()
        {
            if (_position == _lines[_lineIndex].Length)
                return;

            var wordEnd = FindWordEnd();
            var length = wordEnd - _position;
            _lines[_lineIndex] = _lines[_lineIndex].Remove(_position, length);

            Render();
            UpdateCursorPosition();
        }

        private void MoveLineHome()
        {
            _position = 0;
            UpdateCursorPosition();
        }

        private void MoveLineEnd()
        {
            _position = _lines[_lineIndex].Length;
            UpdateCursorPosition();
        }

        private void MoveLeft()
        {
            if (_position == 0)
                return;

            _position--;

            UpdateCursorPosition();
        }

        private void MoveLeftWord()
        {
            if (_position == 0)
                return;

            _position = FindWordStart();

            UpdateCursorPosition();
        }

        private void MoveRight()
        {
            if (_position == _lines[_lineIndex].Length)
                return;

            _position++;

            UpdateCursorPosition();
        }

        private void MoveRightWord()
        {
            _position = FindWordEnd();

            UpdateCursorPosition();
        }

        private void MoveUp()
        {
            if (_lineIndex == 0)
                return;

            _lineIndex--;
            _position = Math.Min(_lines[_lineIndex].Length, _position);

            UpdateCursorPosition();
        }

        private void MoveDown()
        {
            if (_lineIndex == _lines.Count - 1)
                return;

            _lineIndex++;
            _position = Math.Min(_lines[_lineIndex].Length, _position);

            UpdateCursorPosition();
        }

        private void InsertTab()
        {
            var numberOfSpaces = 4 - _position % 4;

            var indent = new string(' ', numberOfSpaces);
            _lines[_lineIndex] = _lines[_lineIndex].Insert(_position, indent);
            _position += numberOfSpaces;

            Render();
            UpdateCursorPosition();
        }

        private void InsertEnter()
        {
            if (_lines.Count == 0)
                return;

            var oldLine = _lines[_lineIndex].Substring(0, _position);
            var newLine = _lines[_lineIndex].Substring(_position);

            _lines[_lineIndex] = oldLine;
            _lines.Insert(_lineIndex + 1, newLine);
            _lineIndex++;
            _position = 0;

            Render();
            UpdateCursorPosition();
        }

        private void Type(char c)
        {
            if (c < (int)32)
                return;

            _lines[_lineIndex] = _lines[_lineIndex].Insert(_position, c.ToString());
            _position++;

            Render();
            UpdateCursorPosition();
        }

        private void UsePreviousHistoryItem()
        {
            if (_history.Count == 0)
                return;

            _historyIndex--;

            if (_historyIndex < 0)
                _historyIndex = _history.Count - 1;

            UseHistoryItem(_historyIndex);
        }

        private void UseNextHistoryItem()
        {
            if (_history.Count == 0)
                return;

            _historyIndex++;

            if (_historyIndex > _history.Count - 1)
                _historyIndex = 0;

            UseHistoryItem(_historyIndex);
        }

        private void UseHistoryItem(int index)
        {
            var historyItem = _history[index];
            var lines = historyItem.Split(Environment.NewLine);
            _lines.Clear();
            _lines.AddRange(lines);
            _lineIndex = _lines.Count - 1;
            _position = _lines[_lineIndex].Length;

            Render();
            UpdateCursorPosition();
        }

        private int FindWordStart()
        {
            var line = _lines[_lineIndex];
            var result = _position;

            while (result > 0 && char.IsWhiteSpace(line[result - 1]))
                result--;

            while (result > 0 && !char.IsWhiteSpace(line[result - 1]))
                result--;
            
            return result;
        }

        private int FindWordEnd()
        {
            var line = _lines[_lineIndex];
            var result = _position;

            while (result < line.Length && char.IsWhiteSpace(line[result]))
                result++;

            while (result < line.Length && !char.IsWhiteSpace(line[result]))
                result++;
            
            return result;
        }
    }
}
