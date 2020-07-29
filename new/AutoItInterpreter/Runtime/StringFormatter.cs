using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System;

using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    /// <summary>
    /// Contains a string formatting engine compatible with the specification <see href="https://www.autoitscript.com/autoit3/docs/functions/StringFormat.htm"/>.
    /// </summary>
    public static class StringFormatter
    {
        private static readonly Regex REGEX_FROMAT = new Regex(@"^%(?<flags>[+\-0#]*)(?<width>\d+)?(\.(?<precision>\d+))?(?<type>[diouxXeEfgGs])", RegexOptions.Compiled);
        private static readonly Regex REGEX_ESCAPE = new Regex(@"^\\[rnt\\]", RegexOptions.Compiled);


        /// <summary>
        /// Formats the given string using the given arguments according to the AutoIt-specification.
        /// </summary>
        /// <param name="format">Format string.</param>
        /// <param name="args">Format string arguments.</param>
        /// <returns>Formatted string.</returns>
        public static string FormatString(string format, IEnumerable<Variant> args) => FormatString(format, args.ToArray());

        /// <summary>
        /// Formats the given string using the given arguments according to the AutoIt-specification.
        /// </summary>
        /// <param name="format">Format string.</param>
        /// <param name="args">Format string arguments.</param>
        /// <returns>Formatted string.</returns>
        public static string FormatString(string format, params Variant[] args)
        {
            StringBuilder output = new StringBuilder();
            int arg_index = 0;

            while (format.Length > 0)
                if (format.StartsWith("%%"))
                {
                    output.Append('%');
                    format = format[2..];
                }
                else if (REGEX_FROMAT.Match(format) is { Success: true, Groups: var g, Length: int len })
                {
                    int.TryParse(g["width"].Value, out int width);
                    int.TryParse(g["precision"].Value, out int precision);
                    FormatFlags[] flags = g["flag"].Value.Distinct().ToArray(c => (FormatFlags)c);
                    char type = g["type"].Value[0];
                    Variant arg = arg_index < args.Length ? args[arg_index] : default;

                    string result = type switch
                    {
                        'd' or 'i' => FunctionExtensions.Do(delegate
                        {
                            long value = (long)arg;

                            return (value > 0 && flags.Contains(FormatFlags.Prefix) ? "+" : "") + value.ToString().PadLeft(Math.Max(precision - 1, 0), '0');
                        }),
                        'o' => Convert.ToString((uint)(int)arg, 8).PadLeft(precision, '0'),
                        'u' => ((ulong)(long)arg).ToString().PadLeft(precision, '0'),
                        'x' or 'X' => ((ulong)(long)arg).ToString($"{type}{precision}"),
                        'e' or 'E' or 'f' or 'g' or 'G' => arg.ToNumber().ToString($"{type}{precision}"),
                        _ when precision is 0 => arg.ToString(),
                        _ => arg.ToString()[..(precision + 1)],
                    };

                    if (flags.Contains(FormatFlags.ZeroPadding) && width > result.Length)
                    {
                        string pad = new string('0', result.Length - width);

                        result = result[0] == '-' ? '-' + pad + result[1..] : pad + result;
                    }

                    if (flags.Contains(FormatFlags.Blank))
                        if (type is 'o')
                            result = "0o" + result;
                        else if (type is 'x' or 'X')
                            result = $"0{type}{result}";

                    result = flags.Contains(FormatFlags.LeftAlign) ? result.PadRight(width) : result.PadLeft(width);
                    format = format[len..];
                    ++arg_index;

                    output.Append(result);
                }
                else if (REGEX_FROMAT.Match(format) is { Success: true, Value: string escape })
                {
                    output.Append(escape[1] switch
                    {
                        'r' => '\r',
                        'n' => '\n',
                        't' => '\t',
                        '\\' => '\\',
                        _ => escape[1],
                    });
                    format = format[2..];
                }
                else
                {
                    output.Append(format[0]);
                    format = format[1..];
                }

            return output.ToString();
        }

        private enum FormatFlags
        {
            Prefix = '+',
            LeftAlign = '-',
            ZeroPadding = '0',
            Blank = '#',
        }
    }
}
