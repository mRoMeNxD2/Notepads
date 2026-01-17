// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.TextEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Notepads.Services;
    using Windows.UI;
    using Windows.UI.Core;
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Documents;
    using Windows.UI.Xaml.Media;

    public partial class TextEditorCore
    {
        private const int CodexZoneUpdateDelayMs = 120;

        private static readonly HashSet<string> CodexZoneKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "and", "as", "async", "await", "base", "bool", "boolean", "break", "byte", "case",
            "catch", "char", "class", "const", "continue", "decimal", "def", "default", "del", "do", "double",
            "dynamic", "elif", "else", "enum", "except", "extends", "false", "finally", "float", "for", "foreach",
            "from", "function", "global", "goto", "if", "implements", "import", "in", "instanceof", "int",
            "interface", "internal", "is", "lambda", "let", "long", "namespace", "new", "nil", "none", "null",
            "object", "or", "override", "package", "pass", "private", "protected", "public", "raise", "return",
            "short", "static", "string", "struct", "super", "switch", "this", "throw", "throws", "true", "try",
            "typeof", "using", "var", "virtual", "void", "while", "with", "yield"
        };

        private readonly List<TextHighlighter> _codexZoneHighlighters = new List<TextHighlighter>();
        private CancellationTokenSource _codexZoneUpdateTokenSource;
        private bool _isCodexZoneEnabled;
        private bool _isCodexZoneApplying;

        public bool IsCodexZoneEnabled
        {
            get => _isCodexZoneEnabled;
            set
            {
                if (_isCodexZoneEnabled == value)
                {
                    return;
                }

                _isCodexZoneEnabled = value;

                if (_isCodexZoneEnabled)
                {
                    ScheduleCodexZoneRefresh(force: true);
                }
                else
                {
                    ClearCodexZoneFormatting();
                }
            }
        }

        public void RefreshCodexZoneTheme()
        {
            if (_isCodexZoneEnabled)
            {
                ScheduleCodexZoneRefresh(force: true);
            }
        }

        private void ScheduleCodexZoneRefresh(bool force = false)
        {
            if (!_isCodexZoneEnabled || !_loaded || _isCodexZoneApplying)
            {
                return;
            }

            if (force)
            {
                ApplyCodexZoneHighlighting();
                return;
            }

            _codexZoneUpdateTokenSource?.Cancel();
            var tokenSource = new CancellationTokenSource();
            _codexZoneUpdateTokenSource = tokenSource;
            _ = DebounceCodexZoneUpdateAsync(tokenSource.Token);
        }

        private async Task DebounceCodexZoneUpdateAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(CodexZoneUpdateDelayMs, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, ApplyCodexZoneHighlighting);
        }

        private void ApplyCodexZoneHighlighting()
        {
            if (!_isCodexZoneEnabled || !_loaded || _isCodexZoneApplying)
            {
                return;
            }

            _isCodexZoneApplying = true;

            try
            {
                Document.GetText(TextGetOptions.None, out var rawText);
                var text = TrimRichEditBoxText(rawText);
                var palette = GetCodexZonePalette();

                Document.BatchDisplayUpdates();

                try
                {
                    ResetCodexZoneFormatting(text.Length, palette.BaseForeground);

                    if (string.IsNullOrEmpty(text))
                    {
                        ClearCodexZoneHighlighters();
                        return;
                    }

                    var selectionStart = Document.Selection.StartPosition;
                    var selectionEnd = Document.Selection.EndPosition;

                    var tokens = TokenizeCodexZone(text, out var excludedRanges);
                    ApplyTokenFormatting(tokens, palette);
                    ApplyErrorIndicators(text, excludedRanges, palette);

                    Document.Selection.SetRange(selectionStart, selectionEnd);
                }
                finally
                {
                    Document.ApplyDisplayUpdates();
                }
            }
            finally
            {
                _isCodexZoneApplying = false;
            }
        }

        private void ApplyTokenFormatting(IReadOnlyList<CodexZoneToken> tokens, CodexZonePalette palette)
        {
            if (tokens.Count == 0)
            {
                return;
            }

            foreach (var token in tokens)
            {
                var color = palette.GetColorForToken(token.Type);
                if (token.Length <= 0)
                {
                    continue;
                }

                var range = Document.GetRange(token.Start, token.Start + token.Length);
                range.CharacterFormat.ForegroundColor = color;
            }
        }

        private void ApplyErrorIndicators(string text, List<TextRange> excludedRanges, CodexZonePalette palette)
        {
            ClearCodexZoneHighlighters();

            var errorRanges = GetCodexZoneErrorRanges(text, excludedRanges);
            AddCodexZoneHighlighter(errorRanges.SyntaxRanges, palette.ErrorIndicator);
            AddCodexZoneHighlighter(errorRanges.IndentationRanges, palette.IndentationIndicator);
        }

        private void AddCodexZoneHighlighter(IEnumerable<TextRange> ranges, Color color)
        {
            var rangeList = ranges.ToList();
            if (rangeList.Count == 0)
            {
                return;
            }

            var highlighter = new TextHighlighter
            {
                Background = new SolidColorBrush(color)
            };

            foreach (var range in rangeList)
            {
                if (range.Length <= 0)
                {
                    continue;
                }

                highlighter.Ranges.Add(range);
            }

            TextHighlighters.Add(highlighter);
            _codexZoneHighlighters.Add(highlighter);
        }

        private void ResetCodexZoneFormatting(int textLength, Color baseForeground)
        {
            if (textLength <= 0)
            {
                return;
            }

            var range = Document.GetRange(0, textLength);
            range.CharacterFormat.ForegroundColor = baseForeground;
        }

        private void ClearCodexZoneFormatting()
        {
            _codexZoneUpdateTokenSource?.Cancel();
            ClearCodexZoneHighlighters();

            if (!_loaded)
            {
                return;
            }

            Document.GetText(TextGetOptions.None, out var rawText);
            var text = TrimRichEditBoxText(rawText);
            ResetCodexZoneFormatting(text.Length, ResolveBaseForeground());
        }

        private void ClearCodexZoneHighlighters()
        {
            if (_codexZoneHighlighters.Count == 0)
            {
                return;
            }

            foreach (var highlighter in _codexZoneHighlighters)
            {
                TextHighlighters.Remove(highlighter);
            }

            _codexZoneHighlighters.Clear();
        }

        private void DisposeCodexZoneResources()
        {
            _codexZoneUpdateTokenSource?.Cancel();
            ClearCodexZoneHighlighters();
        }

        private CodexZonePalette GetCodexZonePalette()
        {
            var theme = ActualTheme == ElementTheme.Default ? ThemeSettingsService.ThemeMode : ActualTheme;
            var isDark = theme == ElementTheme.Dark;
            var baseForeground = ResolveBaseForeground();

            return new CodexZonePalette(
                baseForeground,
                keyword: isDark ? Color.FromArgb(255, 86, 156, 214) : Color.FromArgb(255, 0, 0, 255),
                number: isDark ? Color.FromArgb(255, 181, 206, 168) : Color.FromArgb(255, 9, 134, 88),
                text: isDark ? Color.FromArgb(255, 206, 145, 120) : Color.FromArgb(255, 163, 21, 21),
                comment: isDark ? Color.FromArgb(255, 106, 153, 85) : Color.FromArgb(255, 0, 128, 0),
                errorIndicator: isDark ? Color.FromArgb(70, 255, 83, 83) : Color.FromArgb(70, 220, 0, 0),
                indentationIndicator: isDark ? Color.FromArgb(60, 255, 163, 0) : Color.FromArgb(60, 255, 140, 0));
        }

        private Color ResolveBaseForeground()
        {
            return (Foreground as SolidColorBrush)?.Color
                   ?? (ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black);
        }

        private static List<CodexZoneToken> TokenizeCodexZone(string text, out List<TextRange> excludedRanges)
        {
            var tokens = new List<CodexZoneToken>();
            excludedRanges = new List<TextRange>();
            var index = 0;

            while (index < text.Length)
            {
                var current = text[index];
                if (current == '/' && index + 1 < text.Length)
                {
                    var next = text[index + 1];
                    if (next == '/')
                    {
                        var end = IndexOfLineEnd(text, index + 2);
                        AddToken(tokens, excludedRanges, CodexZoneTokenType.Comment, index, end - index);
                        index = end;
                        continue;
                    }

                    if (next == '*')
                    {
                        var start = index;
                        var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                        if (end == -1)
                        {
                            end = text.Length;
                        }
                        else
                        {
                            end += 2;
                        }

                        AddToken(tokens, excludedRanges, CodexZoneTokenType.Comment, start, end - start);
                        index = end;
                        continue;
                    }
                }

                if (current == '#')
                {
                    var end = IndexOfLineEnd(text, index + 1);
                    AddToken(tokens, excludedRanges, CodexZoneTokenType.Comment, index, end - index);
                    index = end;
                    continue;
                }

                if (current == '-' && index + 1 < text.Length && text[index + 1] == '-' && IsDashLineComment(text, index))
                {
                    var end = IndexOfLineEnd(text, index + 2);
                    AddToken(tokens, excludedRanges, CodexZoneTokenType.Comment, index, end - index);
                    index = end;
                    continue;
                }

                if (current == '"' || current == '\'' || current == '`')
                {
                    var quote = current;
                    var start = index;
                    var escaped = false;
                    index++;

                    while (index < text.Length)
                    {
                        var ch = text[index];
                        if (escaped)
                        {
                            escaped = false;
                            index++;
                            continue;
                        }

                        if (ch == '\\')
                        {
                            escaped = true;
                            index++;
                            continue;
                        }

                        if (ch == quote)
                        {
                            index++;
                            break;
                        }

                        if ((ch == '\r' || ch == '\n') && quote != '`')
                        {
                            break;
                        }

                        index++;
                    }

                    AddToken(tokens, excludedRanges, CodexZoneTokenType.String, start, index - start);
                    continue;
                }

                if (char.IsDigit(current))
                {
                    var start = index;
                    if (current == '0' && index + 1 < text.Length && (text[index + 1] == 'x' || text[index + 1] == 'X'))
                    {
                        index += 2;
                        while (index < text.Length && IsHexDigit(text[index]))
                        {
                            index++;
                        }
                    }
                    else
                    {
                        index++;
                        while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.' || text[index] == '_'))
                        {
                            index++;
                        }
                    }

                    AddToken(tokens, excludedRanges, CodexZoneTokenType.Number, start, index - start);
                    continue;
                }

                if (IsIdentifierStart(current))
                {
                    var start = index;
                    index++;
                    while (index < text.Length && IsIdentifierPart(text[index]))
                    {
                        index++;
                    }

                    var keyword = text.Substring(start, index - start);
                    if (CodexZoneKeywords.Contains(keyword))
                    {
                        AddToken(tokens, excludedRanges, CodexZoneTokenType.Keyword, start, index - start);
                    }

                    continue;
                }

                index++;
            }

            return tokens;
        }

        private static void AddToken(List<CodexZoneToken> tokens, List<TextRange> excludedRanges, CodexZoneTokenType type, int start, int length)
        {
            if (length <= 0)
            {
                return;
            }

            if (tokens.Count > 0)
            {
                var last = tokens[tokens.Count - 1];
                if (last.Type == type && last.Start + last.Length == start)
                {
                    tokens[tokens.Count - 1] = new CodexZoneToken(type, last.Start, last.Length + length);
                }
                else
                {
                    tokens.Add(new CodexZoneToken(type, start, length));
                }
            }
            else
            {
                tokens.Add(new CodexZoneToken(type, start, length));
            }

            if (type == CodexZoneTokenType.Comment || type == CodexZoneTokenType.String)
            {
                excludedRanges.Add(new TextRange { StartIndex = start, Length = length });
            }
        }

        private static CodexZoneErrorRanges GetCodexZoneErrorRanges(string text, List<TextRange> excludedRanges)
        {
            var normalizedRanges = excludedRanges.OrderBy(range => range.StartIndex).ToList();
            var syntaxRanges = new List<TextRange>();
            var indentationRanges = new List<TextRange>();
            var usesSemicolons = HasSemicolonsOutsideRanges(text, normalizedRanges);
            var rangeIndex = 0;

            var braceStack = new Stack<(char Brace, int Index)>();
            for (var index = 0; index < text.Length; index++)
            {
                if (IsIndexInRanges(index, normalizedRanges, ref rangeIndex))
                {
                    continue;
                }

                var ch = text[index];
                if (ch == '(' || ch == '[' || ch == '{')
                {
                    braceStack.Push((ch, index));
                }
                else if (ch == ')' || ch == ']' || ch == '}')
                {
                    if (braceStack.Count == 0 || !IsBraceMatch(braceStack.Peek().Brace, ch))
                    {
                        syntaxRanges.Add(new TextRange { StartIndex = index, Length = 1 });
                    }
                    else
                    {
                        braceStack.Pop();
                    }
                }
            }

            while (braceStack.Count > 0)
            {
                var brace = braceStack.Pop();
                syntaxRanges.Add(new TextRange { StartIndex = brace.Index, Length = 1 });
            }

            var indentSize = AppSettingsService.EditorDefaultTabIndents;
            var expectsTabs = indentSize == -1;
            var expectedSpaceIndent = indentSize > 0 ? indentSize : 4;

            var lineStart = 0;
            while (lineStart <= text.Length)
            {
                var lineEnd = IndexOfLineEnd(text, lineStart);
                if (lineEnd < lineStart)
                {
                    break;
                }

                var firstNonWhitespace = lineStart;
                while (firstNonWhitespace < lineEnd &&
                       (text[firstNonWhitespace] == ' ' || text[firstNonWhitespace] == '\t'))
                {
                    firstNonWhitespace++;
                }

                if (firstNonWhitespace < lineEnd)
                {
                    var lineRangeIndex = 0;
                    if (!IsIndexInRanges(firstNonWhitespace, normalizedRanges, ref lineRangeIndex))
                    {
                        var indentLength = firstNonWhitespace - lineStart;
                        if (indentLength > 0)
                        {
                            var indent = text.Substring(lineStart, indentLength);
                            var hasTabs = indent.IndexOf('\t') >= 0;
                            var hasSpaces = indent.IndexOf(' ') >= 0;
                            var indentIssue = hasTabs && hasSpaces;

                            if (!indentIssue)
                            {
                                if (expectsTabs && hasSpaces)
                                {
                                    indentIssue = true;
                                }
                                else if (!expectsTabs && hasTabs)
                                {
                                    indentIssue = true;
                                }
                                else if (!expectsTabs && hasSpaces && indentLength % expectedSpaceIndent != 0)
                                {
                                    indentIssue = true;
                                }
                            }

                            if (indentIssue)
                            {
                                indentationRanges.Add(new TextRange { StartIndex = lineStart, Length = indentLength });
                            }
                        }

                        if (usesSemicolons)
                        {
                            var lastCodeIndex = -1;
                            var lineRangeCursor = 0;
                            for (var index = lineStart; index < lineEnd; index++)
                            {
                                if (IsIndexInRanges(index, normalizedRanges, ref lineRangeCursor))
                                {
                                    continue;
                                }

                                if (!char.IsWhiteSpace(text[index]))
                                {
                                    lastCodeIndex = index;
                                }
                            }

                            if (lastCodeIndex >= 0 && !IsAllowedLineEnd(text[lastCodeIndex]))
                            {
                                syntaxRanges.Add(new TextRange { StartIndex = lastCodeIndex, Length = 1 });
                            }
                        }
                    }
                }

                if (lineEnd >= text.Length)
                {
                    break;
                }

                if (text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n')
                {
                    lineStart = lineEnd + 2;
                }
                else
                {
                    lineStart = lineEnd + 1;
                }
            }

            return new CodexZoneErrorRanges(syntaxRanges, indentationRanges);
        }

        private static bool HasSemicolonsOutsideRanges(string text, List<TextRange> excludedRanges)
        {
            var rangeIndex = 0;
            for (var index = 0; index < text.Length; index++)
            {
                if (IsIndexInRanges(index, excludedRanges, ref rangeIndex))
                {
                    continue;
                }

                if (text[index] == ';')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBraceMatch(char open, char close)
        {
            return (open == '(' && close == ')')
                   || (open == '[' && close == ']')
                   || (open == '{' && close == '}');
        }

        private static bool IsAllowedLineEnd(char ch)
        {
            return ch == ';' || ch == '{' || ch == '}' || ch == ')' || ch == ']' || ch == ':' || ch == ',';
        }

        private static bool IsIndexInRanges(int index, IList<TextRange> ranges, ref int rangeIndex)
        {
            while (rangeIndex < ranges.Count &&
                   index >= ranges[rangeIndex].StartIndex + ranges[rangeIndex].Length)
            {
                rangeIndex++;
            }

            if (rangeIndex >= ranges.Count)
            {
                return false;
            }

            var currentRange = ranges[rangeIndex];
            return index >= currentRange.StartIndex && index < currentRange.StartIndex + currentRange.Length;
        }

        private static bool IsIdentifierStart(char ch)
        {
            return char.IsLetter(ch) || ch == '_';
        }

        private static bool IsIdentifierPart(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        private static int IndexOfLineEnd(string text, int startIndex)
        {
            for (var index = startIndex; index < text.Length; index++)
            {
                var ch = text[index];
                if (ch == '\r' || ch == '\n')
                {
                    return index;
                }
            }

            return text.Length;
        }

        private static bool IsHexDigit(char ch)
        {
            return (ch >= '0' && ch <= '9') ||
                   (ch >= 'a' && ch <= 'f') ||
                   (ch >= 'A' && ch <= 'F');
        }

        private static bool IsDashLineComment(string text, int index)
        {
            if (index == 0)
            {
                return true;
            }

            var prev = text[index - 1];
            return prev == '\r' || prev == '\n' || char.IsWhiteSpace(prev);
        }

        private readonly struct CodexZoneToken
        {
            public CodexZoneToken(CodexZoneTokenType type, int start, int length)
            {
                Type = type;
                Start = start;
                Length = length;
            }

            public CodexZoneTokenType Type { get; }

            public int Start { get; }

            public int Length { get; }
        }

        private enum CodexZoneTokenType
        {
            Keyword,
            String,
            Comment,
            Number
        }

        private sealed class CodexZonePalette
        {
            public CodexZonePalette(
                Color baseForeground,
                Color keyword,
                Color number,
                Color text,
                Color comment,
                Color errorIndicator,
                Color indentationIndicator)
            {
                BaseForeground = baseForeground;
                Keyword = keyword;
                Number = number;
                Text = text;
                Comment = comment;
                ErrorIndicator = errorIndicator;
                IndentationIndicator = indentationIndicator;
            }

            public Color BaseForeground { get; }

            public Color Keyword { get; }

            public Color Number { get; }

            public Color Text { get; }

            public Color Comment { get; }

            public Color ErrorIndicator { get; }

            public Color IndentationIndicator { get; }

            public Color GetColorForToken(CodexZoneTokenType type)
            {
                switch (type)
                {
                    case CodexZoneTokenType.Keyword:
                        return Keyword;
                    case CodexZoneTokenType.Number:
                        return Number;
                    case CodexZoneTokenType.Comment:
                        return Comment;
                    case CodexZoneTokenType.String:
                        return Text;
                    default:
                        return BaseForeground;
                }
            }
        }

        private readonly struct CodexZoneErrorRanges
        {
            public CodexZoneErrorRanges(List<TextRange> syntaxRanges, List<TextRange> indentationRanges)
            {
                SyntaxRanges = syntaxRanges;
                IndentationRanges = indentationRanges;
            }

            public List<TextRange> SyntaxRanges { get; }

            public List<TextRange> IndentationRanges { get; }
        }
    }
}
