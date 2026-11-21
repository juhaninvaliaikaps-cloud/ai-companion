using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-156: a small, tolerant JSON reader for files other apps write (lorebooks), whose
    /// shapes JsonUtility can't read, such as SillyTavern's entries keyed by number.
    /// Objects become <see cref="Dictionary{TKey,TValue}"/> of string to object, arrays
    /// <see cref="List{T}"/> of object, numbers double, and true/false bool. It refuses
    /// anything deeper than <see cref="MaxDepth"/> or longer than <see cref="MaxLength"/>.
    /// </summary>
    public static class JsonTree
    {
        public const int MaxDepth = 64;
        public const int MaxLength = 8 * 1024 * 1024;

        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || json.Length > MaxLength)
                return false;
            var reader = new Reader(json);
            try
            {
                reader.SkipSpace();
                value = reader.Value(0);
                reader.SkipSpace();
                return reader.AtEnd;
            }
            catch (FormatException)
            {
                value = null;
                return false;
            }
        }

        public static Dictionary<string, object> Object(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static Dictionary<string, object> Object(Dictionary<string, object> parent, string key)
        {
            object value;
            return parent != null && parent.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        public static object Get(Dictionary<string, object> parent, string key)
        {
            object value;
            return parent != null && parent.TryGetValue(key, out value) ? value : null;
        }

        public static string String(Dictionary<string, object> parent, string key)
        {
            var value = Get(parent, key);
            if (value is string text)
                return text;
            if (value is double number)
                return number.ToString(CultureInfo.InvariantCulture);
            return string.Empty;
        }

        /// <summary>A bool, or <paramref name="fallback"/> when the key is missing or not a bool.</summary>
        public static bool Bool(Dictionary<string, object> parent, string key, bool fallback)
        {
            var value = Get(parent, key);
            if (value is bool flag)
                return flag;
            if (value is double number)
                return Math.Abs(number) > double.Epsilon;
            if (value is string text)
            {
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return fallback;
        }

        public static bool TryNumber(Dictionary<string, object> parent, string key, out double number)
        {
            var value = Get(parent, key);
            if (value is double d)
            {
                number = d;
                return true;
            }

            if (value is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return true;
            number = 0d;
            return false;
        }

        sealed class Reader
        {
            readonly string _s;
            int _i;

            public Reader(string s)
            {
                _s = s;
                // A byte-order mark at the start is not part of the JSON.
                if (_s.Length > 0 && _s[0] == '﻿')
                    _i = 1;
            }

            public bool AtEnd => _i >= _s.Length;

            public void SkipSpace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i]))
                    _i++;
            }

            public object Value(int depth)
            {
                if (depth > MaxDepth)
                    throw new FormatException("too deep");
                SkipSpace();
                if (AtEnd)
                    throw new FormatException("end");
                var c = _s[_i];
                switch (c)
                {
                    case '{': return ObjectValue(depth);
                    case '[': return ArrayValue(depth);
                    case '"': return StringValue();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                    default: return NumberValue();
                }
            }

            Dictionary<string, object> ObjectValue(int depth)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++;
                SkipSpace();
                if (Peek('}'))
                {
                    _i++;
                    return result;
                }

                while (true)
                {
                    SkipSpace();
                    if (AtEnd || _s[_i] != '"')
                        throw new FormatException("key");
                    var key = StringValue();
                    SkipSpace();
                    Expect(':');
                    result[key] = Value(depth + 1);
                    SkipSpace();
                    if (Peek(','))
                    {
                        _i++;
                        continue;
                    }

                    Expect('}');
                    return result;
                }
            }

            List<object> ArrayValue(int depth)
            {
                var result = new List<object>();
                _i++;
                SkipSpace();
                if (Peek(']'))
                {
                    _i++;
                    return result;
                }

                while (true)
                {
                    result.Add(Value(depth + 1));
                    SkipSpace();
                    if (Peek(','))
                    {
                        _i++;
                        continue;
                    }

                    Expect(']');
                    return result;
                }
            }

            string StringValue()
            {
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                        throw new FormatException("string");
                    var c = _s[_i++];
                    if (c == '"')
                        return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (AtEnd)
                        throw new FormatException("escape");
                    var e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length)
                                throw new FormatException("unicode");
                            int code;
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                throw new FormatException("unicode");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default:
                            throw new FormatException("escape");
                    }
                }
            }

            double NumberValue()
            {
                var start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0)
                    _i++;
                double number;
                if (_i == start || !double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    throw new FormatException("number");
                return number;
            }

            void Word(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                    throw new FormatException("word");
                _i += word.Length;
            }

            bool Peek(char c)
            {
                return _i < _s.Length && _s[_i] == c;
            }

            void Expect(char c)
            {
                if (!Peek(c))
                    throw new FormatException("expected " + c);
                _i++;
            }
        }
    }
}
