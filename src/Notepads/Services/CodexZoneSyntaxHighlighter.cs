// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Windows.UI;
    using Windows.UI.Text;

    /// <summary>
    /// Provides syntax highlighting for common programming languages.
    /// Supports: Python, JavaScript, Java, C#, C/C++, HTML, CSS, SQL, and more.
    /// </summary>
    public static class CodexZoneSyntaxHighlighter
    {
        // Common keywords across multiple languages
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Control flow
            "if", "else", "elif", "switch", "case", "default", "for", "foreach", "while", "do",
            "break", "continue", "return", "goto", "try", "catch", "finally", "throw", "throws",
            
            // Declarations
            "class", "struct", "enum", "interface", "namespace", "package", "import", "using",
            "public", "private", "protected", "internal", "static", "final", "const", "readonly",
            "abstract", "virtual", "override", "sealed", "async", "await", "yield",
            
            // Types and modifiers
            "void", "var", "let", "const", "int", "float", "double", "string", "bool", "boolean",
            "char", "byte", "short", "long", "object", "dynamic", "decimal", "unsigned", "signed",
            
            // Object-oriented
            "new", "this", "super", "base", "extends", "implements", "instanceof", "typeof",
            
            // Functions
            "function", "def", "lambda", "fn",
            
            // Logical
            "and", "or", "not", "in", "is", "as", "null", "nil", "None", "true", "false",
            "True", "False", "undefined", "NaN",
            
            // Python specific
            "print", "range", "len", "self", "pass", "with", "assert", "raise", "except",
            "from", "global", "nonlocal",
            
            // JavaScript specific
            "export", "require", "module", "console", "window", "document",
            
            // SQL specific
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER",
            "TABLE", "INDEX", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON", "AND", "OR",
            "ORDER", "BY", "GROUP", "HAVING", "LIMIT", "OFFSET", "VALUES", "INTO", "SET"
        };

        // Common built-in functions and types
        private static readonly HashSet<string> BuiltInFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // JavaScript/TypeScript
            "console", "log", "error", "warn", "parseInt", "parseFloat", "isNaN", "isFinite",
            "setTimeout", "setInterval", "clearTimeout", "clearInterval", "fetch", "Promise",
            "Array", "Object", "String", "Number", "Boolean", "Date", "Math", "JSON", "Map", "Set",
            
            // Python
            "print", "len", "range", "str", "int", "float", "list", "dict", "tuple", "set",
            "open", "read", "write", "close", "input", "format", "type", "isinstance", "hasattr",
            "getattr", "setattr", "enumerate", "zip", "map", "filter", "reduce", "sorted", "reversed",
            
            // C#
            "Console", "WriteLine", "Write", "ReadLine", "ToString", "Parse", "TryParse",
            "List", "Dictionary", "HashSet", "StringBuilder", "Task", "Async", "Await"
        };

        // Colors for syntax highlighting (VS Code-like dark theme)
        public static class DarkThemeColors
        {
            public static readonly Color Keyword = Color.FromArgb(255, 86, 156, 214);      // Blue
            public static readonly Color String = Color.FromArgb(255, 206, 145, 120);      // Orange/Brown
            public static readonly Color Comment = Color.FromArgb(255, 106, 153, 85);      // Green
            public static readonly Color Number = Color.FromArgb(255, 181, 206, 168);      // Light green
            public static readonly Color Function = Color.FromArgb(255, 220, 220, 170);    // Yellow
            public static readonly Color Type = Color.FromArgb(255, 78, 201, 176);         // Cyan
            public static readonly Color Operator = Color.FromArgb(255, 212, 212, 212);    // Light gray
            public static readonly Color Default = Color.FromArgb(255, 212, 212, 212);     // Light gray
            public static readonly Color Error = Color.FromArgb(255, 244, 71, 71);         // Red
        }

        // Colors for syntax highlighting (VS Code-like light theme)
        public static class LightThemeColors
        {
            public static readonly Color Keyword = Color.FromArgb(255, 0, 0, 255);         // Blue
            public static readonly Color String = Color.FromArgb(255, 163, 21, 21);        // Dark red
            public static readonly Color Comment = Color.FromArgb(255, 0, 128, 0);         // Green
            public static readonly Color Number = Color.FromArgb(255, 9, 134, 88);         // Dark green
            public static readonly Color Function = Color.FromArgb(255, 121, 94, 38);      // Brown
            public static readonly Color Type = Color.FromArgb(255, 38, 127, 153);         // Teal
            public static readonly Color Operator = Color.FromArgb(255, 0, 0, 0);          // Black
            public static readonly Color Default = Color.FromArgb(255, 0, 0, 0);           // Black
            public static readonly Color Error = Color.FromArgb(255, 255, 0, 0);           // Red
        }

        /// <summary>
        /// Token types for syntax highlighting
        /// </summary>
        public enum TokenType
        {
            Default,
            Keyword,
            String,
            Comment,
            Number,
            Function,
            Type,
            Operator,
            Error
        }

        /// <summary>
        /// Represents a token in the text
        /// </summary>
        public struct Token
        {
            public int Start { get; set; }
            public int Length { get; set; }
            public TokenType Type { get; set; }

            public Token(int start, int length, TokenType type)
            {
                Start = start;
                Length = length;
                Type = type;
            }
        }

        /// <summary>
        /// Gets the color for a token type based on the theme
        /// </summary>
        public static Color GetColorForToken(TokenType tokenType, bool isDarkTheme)
        {
            if (isDarkTheme)
            {
                return tokenType switch
                {
                    TokenType.Keyword => DarkThemeColors.Keyword,
                    TokenType.String => DarkThemeColors.String,
                    TokenType.Comment => DarkThemeColors.Comment,
                    TokenType.Number => DarkThemeColors.Number,
                    TokenType.Function => DarkThemeColors.Function,
                    TokenType.Type => DarkThemeColors.Type,
                    TokenType.Operator => DarkThemeColors.Operator,
                    TokenType.Error => DarkThemeColors.Error,
                    _ => DarkThemeColors.Default
                };
            }
            else
            {
                return tokenType switch
                {
                    TokenType.Keyword => LightThemeColors.Keyword,
                    TokenType.String => LightThemeColors.String,
                    TokenType.Comment => LightThemeColors.Comment,
                    TokenType.Number => LightThemeColors.Number,
                    TokenType.Function => LightThemeColors.Function,
                    TokenType.Type => LightThemeColors.Type,
                    TokenType.Operator => LightThemeColors.Operator,
                    TokenType.Error => LightThemeColors.Error,
                    _ => LightThemeColors.Default
                };
            }
        }

        /// <summary>
        /// Tokenizes the text for syntax highlighting
        /// </summary>
        public static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(text)) return tokens;

            int i = 0;
            int length = text.Length;

            while (i < length)
            {
                char c = text[i];

                // Skip whitespace
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // Single-line comments
                if (i + 1 < length)
                {
                    if ((c == '/' && text[i + 1] == '/') || c == '#')
                    {
                        int start = i;
                        while (i < length && text[i] != '\n' && text[i] != '\r')
                        {
                            i++;
                        }
                        tokens.Add(new Token(start, i - start, TokenType.Comment));
                        continue;
                    }

                    // Multi-line comments
                    if (c == '/' && text[i + 1] == '*')
                    {
                        int start = i;
                        i += 2;
                        while (i + 1 < length && !(text[i] == '*' && text[i + 1] == '/'))
                        {
                            i++;
                        }
                        if (i + 1 < length) i += 2;
                        tokens.Add(new Token(start, i - start, TokenType.Comment));
                        continue;
                    }
                }

                // Python/HTML multi-line strings and doc strings
                if (i + 2 < length && ((c == '"' && text[i + 1] == '"' && text[i + 2] == '"') ||
                                       (c == '\'' && text[i + 1] == '\'' && text[i + 2] == '\'')))
                {
                    char quoteChar = c;
                    int start = i;
                    i += 3;
                    while (i + 2 < length && !(text[i] == quoteChar && text[i + 1] == quoteChar && text[i + 2] == quoteChar))
                    {
                        i++;
                    }
                    if (i + 2 < length) i += 3;
                    tokens.Add(new Token(start, i - start, TokenType.String));
                    continue;
                }

                // Strings (single and double quotes)
                if (c == '"' || c == '\'' || c == '`')
                {
                    char quoteChar = c;
                    int start = i;
                    i++;
                    while (i < length && text[i] != quoteChar)
                    {
                        if (text[i] == '\\' && i + 1 < length)
                        {
                            i += 2; // Skip escaped character
                        }
                        else if (text[i] == '\n' || text[i] == '\r')
                        {
                            break; // End of line without closing quote
                        }
                        else
                        {
                            i++;
                        }
                    }
                    if (i < length && text[i] == quoteChar) i++;
                    tokens.Add(new Token(start, i - start, TokenType.String));
                    continue;
                }

                // Numbers (including hex, binary, octal, and floats)
                if (char.IsDigit(c) || (c == '.' && i + 1 < length && char.IsDigit(text[i + 1])))
                {
                    int start = i;
                    
                    // Check for hex, binary, octal
                    if (c == '0' && i + 1 < length)
                    {
                        char next = char.ToLower(text[i + 1]);
                        if (next == 'x' || next == 'b' || next == 'o')
                        {
                            i += 2;
                            while (i < length && (char.IsLetterOrDigit(text[i])))
                            {
                                i++;
                            }
                            tokens.Add(new Token(start, i - start, TokenType.Number));
                            continue;
                        }
                    }

                    // Regular number (including float)
                    bool hasDecimal = false;
                    bool hasExponent = false;
                    while (i < length)
                    {
                        char ch = text[i];
                        if (char.IsDigit(ch))
                        {
                            i++;
                        }
                        else if (ch == '.' && !hasDecimal && !hasExponent)
                        {
                            hasDecimal = true;
                            i++;
                        }
                        else if ((ch == 'e' || ch == 'E') && !hasExponent)
                        {
                            hasExponent = true;
                            i++;
                            if (i < length && (text[i] == '+' || text[i] == '-'))
                            {
                                i++;
                            }
                        }
                        else if (ch == '_')
                        {
                            i++; // Allow underscores in numbers (like Python 3.6+)
                        }
                        else
                        {
                            break;
                        }
                    }
                    // Handle suffixes like f, d, l, u
                    if (i < length && "fFdDlLuU".IndexOf(text[i]) >= 0)
                    {
                        i++;
                    }
                    tokens.Add(new Token(start, i - start, TokenType.Number));
                    continue;
                }

                // Identifiers and keywords
                if (char.IsLetter(c) || c == '_' || c == '@' || c == '$')
                {
                    int start = i;
                    while (i < length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    {
                        i++;
                    }

                    string word = text.Substring(start, i - start);

                    // Check if it's followed by parenthesis (function call)
                    int nextNonSpace = i;
                    while (nextNonSpace < length && char.IsWhiteSpace(text[nextNonSpace]))
                    {
                        nextNonSpace++;
                    }

                    TokenType tokenType;
                    if (Keywords.Contains(word))
                    {
                        tokenType = TokenType.Keyword;
                    }
                    else if (BuiltInFunctions.Contains(word) || 
                             (nextNonSpace < length && text[nextNonSpace] == '('))
                    {
                        tokenType = TokenType.Function;
                    }
                    else if (char.IsUpper(word[0]) && word.Length > 1)
                    {
                        // PascalCase is likely a type/class name
                        tokenType = TokenType.Type;
                    }
                    else
                    {
                        tokenType = TokenType.Default;
                    }

                    tokens.Add(new Token(start, i - start, tokenType));
                    continue;
                }

                // Operators and punctuation
                if ("+-*/%=<>!&|^~?:;,.()[]{}".IndexOf(c) >= 0)
                {
                    int start = i;
                    // Handle multi-character operators
                    if (i + 1 < length)
                    {
                        string twoChar = text.Substring(i, 2);
                        if (twoChar == "==" || twoChar == "!=" || twoChar == "<=" || twoChar == ">=" ||
                            twoChar == "&&" || twoChar == "||" || twoChar == "++" || twoChar == "--" ||
                            twoChar == "+=" || twoChar == "-=" || twoChar == "*=" || twoChar == "/=" ||
                            twoChar == "<<" || twoChar == ">>" || twoChar == "=>" || twoChar == "->")
                        {
                            i += 2;
                            tokens.Add(new Token(start, 2, TokenType.Operator));
                            continue;
                        }
                    }
                    i++;
                    tokens.Add(new Token(start, 1, TokenType.Operator));
                    continue;
                }

                // Skip any other character
                i++;
            }

            return tokens;
        }

        /// <summary>
        /// Applies syntax highlighting to a RichEditBox document
        /// </summary>
        public static void ApplyHighlighting(ITextDocument document, bool isDarkTheme)
        {
            if (document == null) return;

            // Get the text
            document.GetText(TextGetOptions.None, out string text);
            if (string.IsNullOrEmpty(text)) return;

            // Trim trailing paragraph marker
            if (text.Length > 0 && text[text.Length - 1] == '\r')
            {
                text = text.Substring(0, text.Length - 1);
            }

            // Tokenize
            var tokens = Tokenize(text);

            // Apply colors
            document.BatchDisplayUpdates();
            try
            {
                // Reset to default color first
                var fullRange = document.GetRange(0, text.Length);
                fullRange.CharacterFormat.ForegroundColor = GetColorForToken(TokenType.Default, isDarkTheme);

                // Apply token colors
                foreach (var token in tokens)
                {
                    if (token.Type != TokenType.Default && token.Start + token.Length <= text.Length)
                    {
                        var range = document.GetRange(token.Start, token.Start + token.Length);
                        range.CharacterFormat.ForegroundColor = GetColorForToken(token.Type, isDarkTheme);
                    }
                }
            }
            finally
            {
                document.ApplyDisplayUpdates();
            }
        }
    }
}
