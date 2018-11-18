using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Minsk.CodeAnalysis;
using Minsk.CodeAnalysis.Syntax;
using Minsk.CodeAnalysis.Text;

namespace Minsk
{
    internal sealed class MinskRepl : Repl
    {
        private readonly Dictionary<VariableSymbol, object> _variables = new Dictionary<VariableSymbol, object>();
        private readonly (string Name, Action Handler, string Help)[] _commands;

        private Compilation _previousCompilation;
        private bool _showTree;

        public MinskRepl()
        {
            _commands = new (string, Action, string)[] {
                ("cls", EvaluateClsCommand, "Clears the screen."),
                ("clear", EvaluateClearCommand, "Clears all submissions."),
                ("showTree", EvaluateShowTreeCommand, "Turns output of syntax trees on or off."),
                ("ls", EvaluateLsCommand, "Lists all variables."),
                ("help", EvaluateHelpCommand, "Shows this help"),
            };
        }

        protected override void EvaluateMetaCommand(string commandName)
        {
            var command = _commands.SingleOrDefault(c => c.Name == commandName);
            if (command.Handler != null)
                command.Handler();
            else
                base.EvaluateMetaCommand(commandName);
        }

        private static void EvaluateClsCommand()
        {
            Console.Clear();
        }

        private void EvaluateClearCommand()
        {
            _variables.Clear();
            _previousCompilation = null;
        }

        private void EvaluateShowTreeCommand()
        {
            _showTree = !_showTree;
            if (_showTree)
                Console.WriteLine("Showing trees.");
            else
                Console.WriteLine("Not showing trees.");
        }

        private void EvaluateLsCommand()
        {
            var compilation = _previousCompilation;
            var seenNames = new HashSet<string>();
            while (compilation != null)
            {
                foreach (var variable in compilation.Variables)
                {
                    if (seenNames.Add(variable.Name))
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        if (variable.IsReadOnly)
                            Console.Write("let");
                        else
                            Console.Write("var");
                        Console.Write(" ");
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write(variable.Name);
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(" = ");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(_variables[variable]);
                        Console.WriteLine();
                    }
                }
                compilation = compilation.Previous;
            }
        }

        private void EvaluateHelpCommand()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine();

            var maxNameLength = _commands.Select(c => c.Name.Length).Max() + 4;

            foreach (var command in _commands)
            {
                Console.Write("    ");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("#");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write(command.Name);
                Console.ResetColor();
                var space = new string(' ', maxNameLength - command.Name.Length);
                Console.Write(space);
                Console.Write(command.Help);
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        protected override bool IsCompleteSubmission(string text)
        {
            if (text.StartsWith("#"))
                return true;

            var syntaxTree = SyntaxTree.Parse(text);
            return !syntaxTree.Diagnostics.Any();
        }

        protected override void EvaluateSubmission(string text)
        {
            var syntaxTree = SyntaxTree.Parse(text);

            if (_showTree)
                syntaxTree.Root.WriteTo(Console.Out);

            var compilation = _previousCompilation == null
                ? new Compilation(syntaxTree)
                : _previousCompilation.ContinueWith(syntaxTree);
            var result = compilation.Evaluate(_variables);

            if (result.Diagnostics.Any())
            {
                PrintDiagnostics(compilation, result.Diagnostics);
            }
            else
            {
                PrintResult(result.Value);
                _previousCompilation = compilation;
            }
        }

        protected override void PaintLine(string text)
        {
            var tokens = SyntaxTree.ParseTokens(text);

            foreach (var t in tokens)
            {
                var isKeyword = t.Kind.ToString().EndsWith("Keyword");
                if (isKeyword)
                    Console.ForegroundColor = ConsoleColor.Blue;
                else if (t.Kind == SyntaxKind.NumberToken)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                else if (t.Kind == SyntaxKind.IdentifierToken)
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                else
                    Console.ForegroundColor = ConsoleColor.DarkGray;

                Console.Write(t.Text);
            }

            Console.ResetColor();
        }

        private static void PrintResult(object value)
        {
            if (value == null)
                return;

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        private static void PrintDiagnostics(Compilation compilation, ImmutableArray<Diagnostic> diagnostics)
        {
            var syntaxTree = compilation.SyntaxTree;

            foreach (var diagnostic in diagnostics)
            {
                var lineIndex = syntaxTree.Text.GetLineIndex(diagnostic.Span.Start);
                var line = syntaxTree.Text.Lines[lineIndex];
                var lineNumber = lineIndex + 1;
                var character = diagnostic.Span.Start - line.Start + 1;

                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write($"({lineNumber}, {character}): ");
                Console.WriteLine(diagnostic);
                Console.ResetColor();

                var prefixSpan = TextSpan.FromBounds(line.Start, diagnostic.Span.Start);
                var suffixSpan = TextSpan.FromBounds(diagnostic.Span.End, line.End);

                var prefix = syntaxTree.Text.ToString(prefixSpan);
                var error = syntaxTree.Text.ToString(diagnostic.Span);
                var suffix = syntaxTree.Text.ToString(suffixSpan);

                Console.Write("    ");
                Console.Write(prefix);

                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write(error);
                Console.ResetColor();

                Console.Write(suffix);

                Console.WriteLine();
            }

            Console.WriteLine();
        }
    }
}
