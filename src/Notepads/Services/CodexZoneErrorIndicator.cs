// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides simple error detection for code, including:
    /// - Unmatched brackets, parentheses, and braces
    /// - Missing semicolons (where expected)
    /// - Basic indentation issues
    /// </summary>
    public static class CodexZoneErrorIndicator
    {
        /// <summary>
        /// Represents a code error
        /// </summary>
        public struct CodeError
        {
            public int Position { get; set; }
            public int Length { get; set; }
            public int Line { get; set; }
            public string Message { get; set; }
            public ErrorType Type { get; set; }

            public CodeError(int position, int length, int line, string message, ErrorType type)
            {
                Position = position;
                Length = length;
                Line = line;
                Message = message;
                Type = type;
            }
        }

        /// <summary>
        /// Types of errors that can be detected
        /// </summary>
        public enum ErrorType
        {
            UnmatchedOpenBracket,
            UnmatchedCloseBracket,
            UnmatchedQuote,
            IndentationError,
            PossibleMissingSemicolon
        }

        /// <summary>
        /// Bracket pair information
        /// </summary>
        private struct BracketInfo
        {
            public char OpenBracket { get; set; }
            public int Position { get; set; }
            public int Line { get; set; }
        }

        /// <summary>
        /// Detects errors in the given code text
        /// </summary>
        public static List<CodeError> DetectErrors(string text)
        {
            var errors = new List<CodeError>();
            if (string.IsNullOrEmpty(text)) return errors;

            // Detect bracket mismatches
            errors.AddRange(DetectBracketMismatches(text));

            // Detect indentation issues
            errors.AddRange(DetectIndentationIssues(text));

            // Detect possible missing semicolons (for C-style languages)
            errors.AddRange(DetectPossibleMissingSemicolons(text));

            return errors;
        }

        /// <summary>
        /// Detects unmatched brackets, parentheses, and braces
        /// </summary>
        private static List<CodeError> DetectBracketMismatches(string text)
        {
            var errors = new List<CodeError>();
            var bracketStack = new Stack<BracketInfo>();
            var bracketPairs = new Dictionary<char, char>
            {
                { '(', ')' },
                { '[', ']' },
                { '{', '}' }
            };

            bool inString = false;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;
            char stringChar = '\0';
            int line = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // Track line numbers
                if (c == '\n')
                {
                    line++;
                    inSingleLineComment = false;
                    continue;
                }

                if (c == '\r')
                {
                    continue;
                }

                // Skip comments
                if (!inString && !inMultiLineComment && i + 1 < text.Length)
                {
                    if ((c == '/' && text[i + 1] == '/') || c == '#')
                    {
                        inSingleLineComment = true;
                        continue;
                    }
                    if (c == '/' && text[i + 1] == '*')
                    {
                        inMultiLineComment = true;
                        i++;
                        continue;
                    }
                }

                if (inSingleLineComment) continue;

                if (inMultiLineComment)
                {
                    if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        inMultiLineComment = false;
                        i++;
                    }
                    continue;
                }

                // Handle strings
                if (!inString && (c == '"' || c == '\'' || c == '`'))
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }

                if (inString)
                {
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        i++; // Skip escaped character
                        continue;
                    }
                    if (c == stringChar)
                    {
                        inString = false;
                        stringChar = '\0';
                    }
                    continue;
                }

                // Check brackets
                if (bracketPairs.ContainsKey(c))
                {
                    bracketStack.Push(new BracketInfo { OpenBracket = c, Position = i, Line = line });
                }
                else if (bracketPairs.ContainsValue(c))
                {
                    char expectedOpen = bracketPairs.First(p => p.Value == c).Key;

                    if (bracketStack.Count == 0)
                    {
                        errors.Add(new CodeError(
                            i, 1, line,
                            $"Unmatched closing bracket '{c}'",
                            ErrorType.UnmatchedCloseBracket));
                    }
                    else if (bracketStack.Peek().OpenBracket != expectedOpen)
                    {
                        var top = bracketStack.Peek();
                        errors.Add(new CodeError(
                            i, 1, line,
                            $"Mismatched bracket: expected '{bracketPairs[top.OpenBracket]}' but found '{c}'",
                            ErrorType.UnmatchedCloseBracket));
                    }
                    else
                    {
                        bracketStack.Pop();
                    }
                }
            }

            // Add errors for unclosed brackets
            while (bracketStack.Count > 0)
            {
                var bracket = bracketStack.Pop();
                errors.Add(new CodeError(
                    bracket.Position, 1, bracket.Line,
                    $"Unclosed bracket '{bracket.OpenBracket}'",
                    ErrorType.UnmatchedOpenBracket));
            }

            // Check for unclosed string
            if (inString)
            {
                errors.Add(new CodeError(
                    text.Length - 1, 1, line,
                    "Unclosed string literal",
                    ErrorType.UnmatchedQuote));
            }

            return errors;
        }

        /// <summary>
        /// Detects basic indentation issues (inconsistent spacing)
        /// </summary>
        private static List<CodeError> DetectIndentationIssues(string text)
        {
            var errors = new List<CodeError>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            bool usesTabs = false;
            bool usesSpaces = false;
            int position = 0;

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                string line = lines[lineNum];
                if (string.IsNullOrWhiteSpace(line))
                {
                    position += line.Length + 1; // +1 for newline
                    continue;
                }

                // Check the leading whitespace
                int leadingSpaces = 0;
                int leadingTabs = 0;
                bool hasMixedIndentation = false;

                for (int i = 0; i < line.Length && char.IsWhiteSpace(line[i]); i++)
                {
                    if (line[i] == ' ')
                    {
                        leadingSpaces++;
                        if (leadingTabs > 0) hasMixedIndentation = true;
                    }
                    else if (line[i] == '\t')
                    {
                        leadingTabs++;
                        if (leadingSpaces > 0) hasMixedIndentation = true;
                    }
                }

                // Track what indentation style is being used
                if (leadingTabs > 0) usesTabs = true;
                if (leadingSpaces > 0) usesSpaces = true;

                // Report mixed indentation on the same line
                if (hasMixedIndentation)
                {
                    errors.Add(new CodeError(
                        position, leadingSpaces + leadingTabs, lineNum + 1,
                        "Mixed tabs and spaces in indentation",
                        ErrorType.IndentationError));
                }

                position += line.Length + 1; // +1 for newline
            }

            return errors;
        }

        /// <summary>
        /// Detects lines that might be missing semicolons (for C-style languages)
        /// </summary>
        private static List<CodeError> DetectPossibleMissingSemicolons(string text)
        {
            var errors = new List<CodeError>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            // Keywords that typically don't require semicolons
            var noSemicolonAfter = new HashSet<string>
            {
                "if", "else", "for", "while", "do", "switch", "case", "default",
                "try", "catch", "finally", "class", "struct", "enum", "interface",
                "namespace", "def", "function", "fn", "{", "}", "//", "#", "/*", "*/"
            };

            int position = 0;

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                string line = lines[lineNum].TrimEnd();
                int lineStart = position;
                position += lines[lineNum].Length + 1;

                if (string.IsNullOrWhiteSpace(line)) continue;

                string trimmedLine = line.Trim();

                // Skip empty lines, comments, and preprocessor directives
                if (string.IsNullOrEmpty(trimmedLine) ||
                    trimmedLine.StartsWith("//") ||
                    trimmedLine.StartsWith("#") ||
                    trimmedLine.StartsWith("/*") ||
                    trimmedLine.StartsWith("*"))
                {
                    continue;
                }

                // Skip lines that end with certain characters
                char lastChar = trimmedLine[trimmedLine.Length - 1];
                if (lastChar == ';' || lastChar == '{' || lastChar == '}' ||
                    lastChar == ':' || lastChar == ',' || lastChar == '\\' ||
                    lastChar == '(' || lastChar == '[')
                {
                    continue;
                }

                // Skip lines that start with control flow keywords
                var firstWord = trimmedLine.Split(new[] { ' ', '\t', '(' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstWord != null && noSemicolonAfter.Contains(firstWord.ToLower()))
                {
                    continue;
                }

                // Check if this looks like a statement that should end with semicolon
                // Look for patterns like: variable = value, function call, return statement, etc.
                bool looksLikeStatement = false;

                // Assignment statements
                if (trimmedLine.Contains("=") && !trimmedLine.Contains("==") && !trimmedLine.Contains("!=") &&
                    !trimmedLine.Contains(">=") && !trimmedLine.Contains("<=") && !trimmedLine.Contains("=>"))
                {
                    looksLikeStatement = true;
                }
                // Return statements
                else if (trimmedLine.StartsWith("return ") || trimmedLine == "return")
                {
                    looksLikeStatement = true;
                }
                // Break/continue statements
                else if (trimmedLine == "break" || trimmedLine == "continue")
                {
                    looksLikeStatement = true;
                }
                // Function calls ending with )
                else if (lastChar == ')' && !trimmedLine.StartsWith("if") && !trimmedLine.StartsWith("while") &&
                         !trimmedLine.StartsWith("for") && !trimmedLine.StartsWith("switch"))
                {
                    looksLikeStatement = true;
                }

                if (looksLikeStatement)
                {
                    // Check if next non-empty line starts with certain characters that make semicolon unnecessary
                    bool nextLineOk = false;
                    for (int j = lineNum + 1; j < lines.Length && j < lineNum + 3; j++)
                    {
                        string nextLine = lines[j].Trim();
                        if (string.IsNullOrEmpty(nextLine)) continue;

                        if (nextLine.StartsWith("{") || nextLine.StartsWith("else") ||
                            nextLine.StartsWith("catch") || nextLine.StartsWith("finally"))
                        {
                            nextLineOk = true;
                        }
                        break;
                    }

                    if (!nextLineOk)
                    {
                        // Find the position of the end of the line
                        int errorPos = lineStart + line.Length - 1;
                        errors.Add(new CodeError(
                            errorPos, 1, lineNum + 1,
                            "Possible missing semicolon",
                            ErrorType.PossibleMissingSemicolon));
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Gets error positions for highlighting
        /// </summary>
        public static List<(int Start, int Length)> GetErrorPositions(string text)
        {
            var errors = DetectErrors(text);
            return errors.Select(e => (e.Position, e.Length)).ToList();
        }
    }
}
