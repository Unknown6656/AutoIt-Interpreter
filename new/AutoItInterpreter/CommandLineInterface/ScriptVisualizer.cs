using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Imaging;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.CLI
{
    public static class ScriptVisualizer
    {
        private static readonly Regex REGEX_WHITESPACE = new(@"^\s+", RegexOptions.Compiled);
        private static readonly Regex REGEX_DIRECTIVE = new(@"^#[^\W\d]\w*\b", RegexOptions.Compiled);
        private static readonly Regex REGEX_STRING = new(@"^('[^']*'|""[^""]*"")", RegexOptions.Compiled);
        private static readonly Regex REGEX_KEYWORD = new(@$"^(->|({ScriptFunction.RESERVED_NAMES.Select(Regex.Escape).StringJoin("|")})\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex REGEX_SYMOBLS = new(@"^([\.,()\[\]{}'""]|::)", RegexOptions.Compiled);
        private static readonly Regex REGEX_OPERATORS = new(@"^([\^?:]|<>|[+\-\*/&<>=]=?)(?![\^?:=+\-*/&<>])", RegexOptions.Compiled);
        private static readonly Regex REGEX_OPERATOR_ERROR = new(@"^[\^?:+\-\*/&<>=]+", RegexOptions.Compiled);
        private static readonly Regex REGEX_VARIABLE = new(@"^\$[^\W\d]\w*\b", RegexOptions.Compiled);
        private static readonly Regex REGEX_MACRO = new(@"^@[^\W\d]\w*\b", RegexOptions.Compiled);
        private static readonly Regex REGEX_FUNCCALL = new(@"^[^\W\d]\w*(?=\()", RegexOptions.Compiled);
        private static readonly Regex REGEX_IDENTIFIER = new(@"^[^\W\d]\w*\b", RegexOptions.Compiled);
        private static readonly Regex REGEX_COMMENT = new(@"^;.*", RegexOptions.Compiled);
        private static readonly Regex REGEX_NUMBER = new(@"^(0x[\da-f_]+|[\da-f_]+h|0b[01_]+|0o[0-7_]+|\d+(\.\d+)?(e[+\-]?\d+)?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        public static Dictionary<TokenType, RGBAColor> ColorScheme { get; } = new()
        {
            // [TokenType.NewLine] = RGBAColor.White,
            // [TokenType.Whitespace] = RGBAColor.White,
            [TokenType.Keyword] = RGBAColor.CornflowerBlue,
            [TokenType.FunctionCall] = RGBAColor.LightCyan,
            [TokenType.Identifier] = RGBAColor.White,
            [TokenType.Comment] = RGBAColor.DarkSeaGreen,
            [TokenType.Number] = RGBAColor.Moccasin,
            [TokenType.String] = RGBAColor.LightSalmon,
            [TokenType.Operator] = RGBAColor.Violet,
            [TokenType.Symbol] = RGBAColor.Silver,
            [TokenType.Directive] = RGBAColor.BurlyWood,
            [TokenType.DirectiveOption] = RGBAColor.Wheat,
            [TokenType.Variable] = RGBAColor.PaleGreen,
            [TokenType.Macro] = RGBAColor.RosyBrown,
            [TokenType.UNKNOWN] = RGBAColor.Tomato,
        };


        public static ScriptToken[] TokenizeScript(this ScannedScript script) => TokenizeScript(script.OriginalContent);

        public static ScriptToken[] TokenizeScript(string au3_script) => TokenizeScript(au3_script.SplitIntoLines());

        public static ScriptToken[] TokenizeScript(IEnumerable<string> au3_script_lines)
        {
            List<ScriptToken> tokens = new();
            int comment_level = 0;
            string[] lines = au3_script_lines.ToArray();

            for (int line_index = 0; line_index < lines.Length; ++line_index)
            {
                string line = lines[line_index];
                bool is_directive = false;
                int char_index = 0;

                void add_token(int length, TokenType type)
                {
                    tokens.Add(new ScriptToken(line_index, char_index, length, line[..length], type));

                    char_index += length;
                    line = line[length..];
                }

                while (line.Length > 0)
                {
                    if (line.Match(REGEX_WHITESPACE, out Match match))
                        add_token(match.Length, TokenType.Whitespace);
                    else if (line.Match(ScriptScanner.REGEX_CS, out match))
                    {
                        ++comment_level;

                        add_token(line.Length, TokenType.Comment);
                    }
                    else if (line.Match(ScriptScanner.REGEX_CE, out match))
                    {
                        if (comment_level > 0)
                            --comment_level;

                        add_token(line.Length, TokenType.Comment);
                    }
                    else if (comment_level > 0)
                        add_token(line.Length, TokenType.Comment);
                    else if (line.Match(REGEX_DIRECTIVE, out match))
                    {
                        is_directive = true;

                        add_token(match.Length, TokenType.Directive);
                    }
                    else if (line.Match(REGEX_STRING, out match))
                        add_token(match.Length, TokenType.String);
                    else if (line.Match(REGEX_KEYWORD, out match))
                        add_token(match.Length, TokenType.Keyword);
                    else if (line.Match(REGEX_SYMOBLS, out match))
                        add_token(match.Length, TokenType.Symbol);
                    else if (line.Match(REGEX_OPERATORS, out match))
                        add_token(match.Length, TokenType.Operator);
                    else if (line.Match(REGEX_OPERATOR_ERROR, out match))
                        add_token(match.Length, TokenType.UNKNOWN);
                    else if (line.Match(REGEX_VARIABLE, out match))
                        add_token(match.Length, TokenType.Variable);
                    else if (line.Match(REGEX_MACRO, out match))
                        add_token(match.Length, TokenType.Macro);
                    else if (line.Match(REGEX_COMMENT, out match))
                        add_token(match.Length, TokenType.Comment);
                    else if (line.Match(REGEX_NUMBER, out match))
                        add_token(match.Length, TokenType.Number);
                    else if (line.Match(REGEX_FUNCCALL, out match))
                        add_token(match.Length, is_directive ? TokenType.DirectiveOption : TokenType.FunctionCall);
                    else if (line.Match(REGEX_IDENTIFIER, out match))
                        add_token(match.Length, is_directive ? TokenType.DirectiveOption : TokenType.Identifier);
                    else
                        add_token(1, TokenType.UNKNOWN);
                }

                add_token(0, TokenType.NewLine);
            }

            return tokens.ToArray();
        }

        public static string ConvertToVT100(this IEnumerable<ScriptToken> tokens, bool print_linebreaks_and_line_numbers)
        {
            string text = (from t in tokens
                           orderby t.LineIndex, t.CharIndex
                           select ConvertToVT100(t, print_linebreaks_and_line_numbers)).StringConcat();

            if (!print_linebreaks_and_line_numbers)
                return text;

            string[] lines = text.SplitIntoLines();
            string vt100 = RGBAColor.Gray.ToVT100ForegroundString();
            int b = (int)Math.Log10(lines.Length) + 1;

            return lines.Select((line, number) => $"{vt100} {(number + 1).ToString().PadLeft(b)} │  {line}").StringJoin("\n");
        }

        public static string ConvertToVT100(this ScriptToken token, bool print_linebreaks)
        {
            if (token.Type is TokenType.NewLine)
                return print_linebreaks ? "\n" : string.Empty;
            else if (token.Type is TokenType.Whitespace)
                return token.Content;

            string str = ColorScheme[token.Type].ToVT100ForegroundString() + token.Content;

            return token.Type is TokenType.UNKNOWN ? $"\x1b[4m{str}\x1b[24m" : str;
        }
    }

    public record ScriptToken(int LineIndex, int CharIndex, int TokenLength, string Content, TokenType Type)
    {
        public override string ToString() => $"{LineIndex}:{CharIndex}{(TokenLength > 1 ? ".." + (CharIndex + TokenLength) : "")}: \"{Content}\" ({Type})";

        public IEnumerable<ScriptToken> SplitByLineBreaks()
        {
            List<ScriptToken> tokens = Content.Replace("\r\n", "\n", StringComparison.InvariantCulture)
                                              .Split('\n')
                                              .SelectMany((line, index) => new[]
                                              {
                                                  new ScriptToken(LineIndex + index * 2, index == 0 ? CharIndex : 0, line.Length, line, Type),
                                                  new ScriptToken(LineIndex + index * 2 + 1, 0, 1, "\n", TokenType.NewLine),
                                              })
                                              .ToList();

            if (tokens.Count > 1)
                tokens.RemoveAt(tokens.Count - 1);

            return tokens;
        }

        public static ScriptToken FromString(string text, TokenType type) => new(0, 0, text.Length, text, type);
    }

    public enum TokenType
    {
        Whitespace,
        NewLine,
        Keyword,
        FunctionCall,
        Identifier,
        Comment,
        Number,
        String,
        Operator,
        Symbol,
        Directive,
        DirectiveOption,
        Variable,
        Macro,
        UNKNOWN,
    }
}
