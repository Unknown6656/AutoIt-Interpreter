using System.Globalization;
using System.Text;
using System;

namespace AutoItCoreLibrary
{
    public sealed class FormatStringEngine
    {
        private readonly string _format;
        private VariableEscapeState _varesc;
        private EngineState _escape;
        private string _wdh;
        private string _prc;
        private int _aindex;
        private int _cindex;
        private bool _numsign;
        private bool _signed;
        private bool _aleft;
        private bool _0pad;


        public FormatStringEngine(string format)
        {
            _format = format ?? "";
            _cindex = 0;
        }

        public string Format(params object[] args)
        {
            StringBuilder sb = new StringBuilder();
            int lastindex = 0;

            ResetState();

            try
            {
                while (_cindex < _format.Length)
                {
                    lastindex = _cindex;

                    sb.Append(ProcessCharacters(GetNextChars(), args));
                }
            }
            catch
            {
                string debug = _format;

                if (lastindex < _cindex)
                {
                    debug = debug.Substring(lastindex);
                    debug = debug.Remove(Math.Min(debug.Length, _cindex - lastindex));
                    debug = $"Invalid format specifier '{debug}' in format string '{_format}'.";
                }
                else
                    debug = $"Invalid format string '{_format}' or not enough arguments.";

                throw new InvalidOperationException(debug);
            }

            return sb.ToString();
        }

        private string GetNextChars(int cnt = 1)
        {
            string res = _format.Substring(_cindex, cnt);

            _cindex += cnt;

            return res;
        }

        private void BackPaddle(int cnt = 1) => _cindex -= cnt;

        private void ResetState()
        {
            _varesc = VariableEscapeState.Flag;
            _escape = EngineState.Regular;
            _numsign = false;
            _signed = false;
            _aleft = false;
            _0pad = false;
            _aindex = 0;
            _cindex = 0;
            _wdh = "";
            _prc = "";
        }

        private string ProcessCharacters(string chars, object[] args)
        {
            char first = chars[0];

            switch (_escape)
            {
                case EngineState.Regular:
                    switch (first)
                    {
                        case '\\':
                            _escape = EngineState.BackslashEscape;

                            return "";
                        case '%':
                            _escape = EngineState.VariableEscape;
                            _varesc = VariableEscapeState.Flag;

                            return "";
                        default:
                            return chars;
                    }
                case EngineState.BackslashEscape:
                    _escape = EngineState.Regular;

                    switch (first)
                    {
                        case 'r':
                            return "\r";
                        case 'n':
                            return "\n";
                        case 't':
                            return "\t";
                        case 'v':
                            return "\x0b";
                        case 'b':
                            return "\x08";
                        case 'a':
                            return "\x07";
                        case 'f':
                            return "\x0c";
                        case 'd':
                            return "\x7f";
                        case '0':
                            return "\0";
                        case '\\':
                            return "\\";
                        case 'x':
                            string hex = GetNextChars(2);

                            return ((char)int.Parse(hex, NumberStyles.HexNumber)).ToString();
                        case 'u':
                            string unc = GetNextChars(4);

                            return ((char)int.Parse(unc, NumberStyles.HexNumber)).ToString();
                        default:
                            return chars;
                    }
                case EngineState.VariableEscape:
                    switch (_varesc)
                    {
                        case VariableEscapeState.Flag:
                            switch (first)
                            {
                                case '-':
                                    _aleft = true;

                                    break;
                                case '+':
                                    _signed = true;

                                    break;
                                case '0':
                                    _varesc = VariableEscapeState.Width;
                                    _0pad = true;

                                    break;
                                case '#':
                                    _numsign = true;

                                    break;
                                default:
                                    if (char.IsDigit(first))
                                        _varesc = VariableEscapeState.Width;
                                    else if (first == '.')
                                        _varesc = VariableEscapeState.Precision;
                                    else
                                    {
                                        _varesc = VariableEscapeState.Type;
                                        BackPaddle();
                                    }

                                    break;
                            }

                            return "";
                        case VariableEscapeState.Width:
                            if (char.IsDigit(first))
                                _wdh += first;
                            else if (first == '.')
                                _varesc = VariableEscapeState.Precision;
                            else
                            {
                                _varesc = VariableEscapeState.Type;
                                BackPaddle();
                            }

                            return "";
                        case VariableEscapeState.Precision:
                            if (char.IsDigit(first))
                                _prc += first;
                            else
                            {
                                _varesc = VariableEscapeState.Type;
                                BackPaddle();
                            }

                            return "";
                        case VariableEscapeState.Type:
                            object param = (_aindex < args.Length ? args[_aindex] : null) ?? AutoItVariantType.Empty;
                            long precision = long.Parse('0' + _prc);
                            long width = long.Parse('0' + _wdh);
                            string prefix = "";
                            string res = "";
                            char? sig = null;

                            _aindex++;
                            _escape = EngineState.Regular;

                            switch (first)
                            {
                                case 'd':
                                case 'i':
                                    {
                                        long val = (long)param;

                                        if (val < 0)
                                        {
                                            sig = '-';
                                            val = -val;
                                            _0pad = false;
                                        }
                                        else if (_signed)
                                            sig = '+';

                                        res = val.ToString();

                                        break;
                                    }
                                case 'u':
                                    res = ((ulong)param).ToString();

                                    break;
                                case 'x':
                                case 'X':
                                    res = ((ulong)param).ToString(first.ToString());

                                    if (_numsign)
                                        prefix = $"0{first}";

                                    break;
                                case 'o':
                                case 'O':
                                    res = Convert.ToString((long)param, 8);

                                    if (_numsign)
                                        prefix = "0";

                                    break;
                                case 'b':
                                case 'B':
                                    res = Convert.ToString((long)param, 2);

                                    if (_numsign)
                                        prefix = "0b";

                                    break;
                                case 'f':
                                case 'F':
                                case 'g':
                                case 'G':
                                    res = ((decimal)param).ToString();

                                    if ((_numsign || precision > 0) && !res.Contains("."))
                                        res += ".0";

                                    int idx = res.IndexOf('.');

                                    if ((idx > 0) && (precision > 0) && (res.Length - (idx + 1) < precision))
                                        res += new string('0', (int)(precision - res.Length + idx + 1)); // TODO: ?

                                    if (char.ToLower(first) == 'f')
                                        res = res.Remove((int)(idx + precision));

                                    break;
                                case 'e':
                                case 'E':

                                    // TODO
#warning TODO

                                    break;
                                case 'a':
                                case 'A':
                                    if (param is AutoItVariantType variant)
                                    {
                                        _0pad = false;

                                        if (variant.IsString)
                                            res = variant.ToString();
                                        else if (variant.IsArray)
                                            res = variant.ToArrayString();
                                        else if (variant.IsObject)
                                            res = variant.ToCOMString();

                                        break;
                                    }
                                    else
                                        goto case 's';
                                case 's':
                                    _0pad = false;
                                    res = param.ToString();

                                    break;
                                default:
                                    throw new InvalidOperationException();
                            }

                            if (sig is char c)
                                prefix += c;

                            if (res.Length + prefix.Length < width)
                                if (_aleft)
                                    res = prefix + res + new string(' ', (int)(width - res.Length));
                                else if (_0pad)
                                    res = prefix + new string('0', (int)(width - res.Length)) + res;
                                else
                                    res = new string(' ', (int)(width - res.Length)) + prefix + res;

                            return res;
                    }

                    break;
            }

            throw new InvalidOperationException();
        }

        private enum EngineState
        {
            Regular = 0,
            VariableEscape = 1,
            BackslashEscape = 2,
        }

        private enum VariableEscapeState
        {
            Flag = 0,
            Width = 1,
            Precision = 2,
            Type = 3,
        }
    }
}
