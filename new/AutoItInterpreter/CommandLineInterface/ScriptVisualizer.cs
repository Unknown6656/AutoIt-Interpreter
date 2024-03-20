using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Runtime;
using Unknown6656.Imaging;
using Unknown6656.Common;
using Unknown6656.Generics;

namespace Unknown6656.AutoIt3.CLI;


/// <summary>
/// The module responsible for the display of AutoIt scripts on the terminal.
/// </summary>
public static class ScriptVisualizer
{
    private static readonly Regex REGEX_WHITESPACE = new(@"^\s+", RegexOptions.Compiled);
    private static readonly Regex REGEX_DIRECTIVE = new(@"^#[^\W\d][\w\-]*\b", RegexOptions.Compiled);
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


    /// <summary>
    /// The color scheme which determines how individual tokens shall be highlighted.
    /// </summary>
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

    /// <summary>
    /// Tokenizes the given AutoIt script and returns an array of <see cref="ScriptToken"/>s.
    /// </summary>
    /// <param name="script">The script to be tokenized.</param>
    /// <returns>Array of <see cref="ScriptToken"/>s which collectively represent the tokenized script.</returns>
    public static ScriptToken[] TokenizeScript(this ScannedScript script) => TokenizeScript(script.OriginalContent);

    /// <summary>
    /// Tokenizes the given AutoIt script and returns an array of <see cref="ScriptToken"/>s.
    /// </summary>
    /// <param name="au3_script">The script to be tokenized. Note: this parameter should contain the actual script, <i>not</i> a file path pointing towards an existing script file.</param>
    /// <returns>Array of <see cref="ScriptToken"/>s which collectively represent the tokenized script.</returns>
    public static ScriptToken[] TokenizeScript(string au3_script) => TokenizeScript(au3_script.SplitIntoLines());

    /// <summary>
    /// Tokenizes the given AutoIt script and returns an array of <see cref="ScriptToken"/>s.
    /// </summary>
    /// <param name="au3_script_lines">The source code lines as read from an AutoIt script.</param>
    /// <returns>Array of <see cref="ScriptToken"/>s which collectively represent the tokenized script.</returns>
    public static ScriptToken[] TokenizeScript(IEnumerable<string> au3_script_lines)
    {
        List<ScriptToken> tokens = [];
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

        return [.. tokens];
    }

    /// <summary>
    /// Converts the given script tokens to its VT-100 terminal escape codes and text representation.
    /// </summary>
    /// <param name="tokens">The tokens to be printed.</param>
    /// <param name="print_linebreaks_and_line_numbers">Indicates whether line breaks and line numbers shall be printed.</param>
    /// <returns>The VT-100 representation of the given tokens.</returns>
    public static string ConvertToVT100(this IEnumerable<ScriptToken> tokens, bool print_linebreaks_and_line_numbers)
    {
        string text = (from t in tokens
                       orderby t.LineIndex, t.CharIndex
                       where print_linebreaks_and_line_numbers || t.Type is not TokenType.NewLine
                       select ConvertToVT100(t, print_linebreaks_and_line_numbers)).StringConcat();

        if (!print_linebreaks_and_line_numbers)
            return text;

        string[] lines = text.SplitIntoLines();
        string vt100 = RGBAColor.Gray.ToVT100ForegroundString();
        int b = (int)Math.Log10(lines.Length) + 1;

        return lines.Select((line, number) => $"{vt100} {(number + 1).ToString().PadLeft(b)} │  {line}").StringJoin("\n");
    }

    /// <summary>
    /// Converts the given script token to its VT-100 terminal escape codes and text representation.
    /// </summary>
    /// <param name="token">The token to be printed.</param>
    /// <param name="print_linebreaks">Indicates whether line breaks shall be preserved.</param>
    /// <returns>The VT-100 representation of the given token.</returns>
    public static string ConvertToVT100(this ScriptToken token, bool print_linebreaks)
    {
        string content;

        if (token.Type is TokenType.NewLine or TokenType.Whitespace)
        {
            content = "\x1b[0m" + (print_linebreaks && token.Type is TokenType.NewLine ? "\n" : token.Content.Replace("\r\n", "\n"));

            if (!print_linebreaks)
                content = content.Replace("\n", "");
        }
        else
            content = ColorScheme[token.Type].ToVT100ForegroundString() + token.Content;

        return token.Type is TokenType.UNKNOWN ? $"\x1b[4m{content}\x1b[24m" : content;
    }
}

/// <summary>
/// Represents an AutoIt script token, i.e. a sequence of characters (from an AutoIt script) which are associated with a certain <see cref="TokenType"/>.
/// </summary>
/// <param name="LineIndex">The zero-based line index.</param>
/// <param name="CharIndex">The zero-based character index (from the start of the line <paramref name="LineIndex"/>).</param>
/// <param name="TokenLength">The length of the token in characters.</param>
/// <param name="Content">The textual content of the token.</param>
/// <param name="Type">The token type associated with the token.</param>
public record ScriptToken(int LineIndex, int CharIndex, int TokenLength, string Content, TokenType Type)
{
    public override string ToString() => $"{LineIndex}:{CharIndex}{(TokenLength > 1 ? ".." + (CharIndex + TokenLength) : "")}: \"{Content}\" ({Type})";

    /// <summary>
    /// Splits the current instance of <see cref="ScriptToken"/> by line breaks (<c>\n</c> or <c>\r\n</c>) and returns an enumeration of split tokens, as well as the line breaks, themselves.
    /// </summary>
    /// <returns>An enumeration of tokens resulting from the split by line breaks.</returns>
    public IEnumerable<ScriptToken> SplitByLineBreaks()
    {
        List<ScriptToken> tokens = Content.Replace("\r\n", "\n")
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

    /// <summary>
    /// Creates a token based on the given string and token type. The properties <see cref="LineIndex"/> and <see cref="CharIndex"/> will have the value <c>0</c>.
    /// </summary>
    /// <param name="text">The string content of the token.</param>
    /// <param name="type">The associated token type.</param>
    /// <returns>The newly created script token.</returns>
    public static ScriptToken FromString(string text, TokenType type) => new(0, 0, text.Length, text, type);
}

/// <summary>
/// An enumeration of known token types.
/// <para/>
/// <i>Note that the order of these enum items influence the sorting while displaying auto-complete suggestions inside an interactive shell.</i>
/// </summary>
public enum TokenType
{
    /// <summary>
    /// Represents a sequence of whitespace characters (ignoring line breaks, which are represented by <see cref="NewLine"/>).
    /// </summary>
    Whitespace,
    /// <summary>
    /// Represents a new line (<c>\n</c> or <c>\r\n</c>).
    /// </summary>
    NewLine,
    /// <summary>
    /// Represents a known AutoIt keyword. See <see href="https://www.autoitscript.com/autoit3/docs/keywords.htm"/>.
    /// </summary>
    Keyword,
    /// <summary>
    /// Represents a variable (which starts with '<c>$</c>').
    /// </summary>
    Variable,
    /// <summary>
    /// Represents a macro (which starts with '<c>@</c>').
    /// </summary>
    Macro,
    /// <summary>
    /// Represents an AutoIt directive. See <see href="https://www.autoitscript.com/autoit3/docs/intro/lang_directives.htm"/>.
    /// </summary>
    Directive,
    /// <summary>
    /// Represents an option for an AutoIt directive. See <see href="https://www.autoitscript.com/autoit3/docs/intro/lang_directives.htm"/>.
    /// </summary>
    DirectiveOption,
    /// <summary>
    /// Represents a function call.
    /// </summary>
    FunctionCall,
    /// <summary>
    /// Represents an AutoIt identifier (e.g. function name).
    /// </summary>
    Identifier,
    /// <summary>
    /// Represents a comment or comment block. See <see href="https://www.autoitscript.com/autoit3/docs/lang_comments.htm"/>.
    /// </summary>
    Comment,
    /// <summary>
    /// Represents a numeric literal (integer or floating-point).
    /// </summary>
    Number,
    /// <summary>
    /// Represents a string literal.
    /// </summary>
    String,
    /// <summary>
    /// Represents an AutoIt operator. See <see href="https://www.autoitscript.com/autoit3/docs/lang_operators.htm"/>.
    /// </summary>
    Operator,
    /// <summary>
    /// Represents a generic symbol character which is not an operator.
    /// </summary>
    Symbol,
    /// <summary>
    /// An unknown AutoIt token (usually indicating a syntax error).
    /// </summary>
    UNKNOWN,
}
