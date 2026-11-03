using System;
using System.Text;

namespace AICompanion.Settings
{
    public static class PortraitMapJson
    {
        public static PortraitMap Parse(string json)
        {
            var map = PortraitMap.CreateDefaults();
            if (string.IsNullOrWhiteSpace(json))
                return map;

            try
            {
                Overlay(map, json);
            }
            catch (FormatException)
            {
                return PortraitMap.CreateDefaults();
            }

            return map;
        }

        static void Overlay(PortraitMap map, string json)
        {
            var scan = new Scan(json);
            scan.Skip();
            if (!scan.Try('{'))
                throw new FormatException();

            var replacedTransitions = false;
            while (true)
            {
                scan.Skip();
                if (scan.Try('}'))
                    return;
                var key = scan.ReadString();
                scan.Skip();
                if (!scan.Try(':'))
                    throw new FormatException();
                scan.Skip();
                if (string.Equals(key, "transitions", StringComparison.OrdinalIgnoreCase))
                {
                    if (!replacedTransitions)
                    {
                        map.ClearTransitions();
                        replacedTransitions = true;
                    }

                    ReadTransitions(scan, map);
                }
                else
                {
                    ReadEntry(scan, map, key);
                }

                scan.Skip();
                scan.Try(',');
            }
        }

        static void ReadEntry(Scan scan, PortraitMap map, string key)
        {
            scan.Skip();
            if (scan.Peek == '"')
            {
                map.Set(key, PortraitFrameSet.Single(scan.ReadString()));
                return;
            }

            if (scan.Peek != '{')
            {
                scan.SkipValue();
                return;
            }

            string open = null;
            string half = null;
            string closed = null;
            string talk = null;
            string between = null;
            scan.Try('{');
            scan.Skip();
            if (!scan.Try('}'))
            {
                while (true)
                {
                    var field = scan.ReadString();
                    scan.Skip();
                    if (!scan.Try(':'))
                        throw new FormatException();
                    scan.Skip();
                    if (scan.Peek == '"')
                    {
                        var value = scan.ReadString();
                        if (Is(field, "open"))
                            open = value;
                        else if (Is(field, "blinkHalf") || Is(field, "blink_half"))
                            half = value;
                        else if (Is(field, "blinkClosed") || Is(field, "blink_closed"))
                            closed = value;
                        else if (Is(field, "talk"))
                            talk = value;
                        else if (Is(field, "inBetween") || Is(field, "in_between"))
                            between = value;
                    }
                    else
                    {
                        scan.SkipValue();
                    }

                    scan.Skip();
                    if (scan.Try('}'))
                        break;
                    if (!scan.Try(','))
                        throw new FormatException();
                    scan.Skip();
                }
            }

            map.Set(key, new PortraitFrameSet(open, half, closed, talk, between));
        }

        static void ReadTransitions(Scan scan, PortraitMap map)
        {
            scan.Skip();
            if (scan.Peek != '{')
            {
                scan.SkipValue();
                return;
            }

            scan.Try('{');
            scan.Skip();
            if (scan.Try('}'))
                return;

            while (true)
            {
                var key = scan.ReadString();
                scan.Skip();
                if (!scan.Try(':'))
                    throw new FormatException();
                scan.Skip();
                string file = null;
                if (scan.Peek == '"')
                {
                    file = scan.ReadString();
                }
                else if (scan.Peek == '{')
                {
                    file = ReadBetweenObject(scan);
                }
                else
                {
                    scan.SkipValue();
                }

                string from;
                string to;
                if (file != null && SplitTransition(key, out from, out to))
                    map.SetTransition(from, to, file);

                scan.Skip();
                if (scan.Try('}'))
                    return;
                if (!scan.Try(','))
                    throw new FormatException();
                scan.Skip();
            }
        }

        static string ReadBetweenObject(Scan scan)
        {
            string file = null;
            scan.Try('{');
            scan.Skip();
            if (scan.Try('}'))
                return null;

            while (true)
            {
                var field = scan.ReadString();
                scan.Skip();
                if (!scan.Try(':'))
                    throw new FormatException();
                scan.Skip();
                if (scan.Peek == '"' && (Is(field, "inBetween") || Is(field, "in_between") || Is(field, "open")))
                    file = scan.ReadString();
                else
                    scan.SkipValue();

                scan.Skip();
                if (scan.Try('}'))
                    return file;
                if (!scan.Try(','))
                    throw new FormatException();
                scan.Skip();
            }
        }

        static bool SplitTransition(string key, out string from, out string to)
        {
            from = null;
            to = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var arrow = key.IndexOf("->", StringComparison.Ordinal);
            if (arrow <= 0 || arrow + 2 >= key.Length)
                return false;
            from = key.Substring(0, arrow).Trim();
            to = key.Substring(arrow + 2).Trim();
            return from.Length > 0 && to.Length > 0;
        }

        static bool Is(string field, string expected)
        {
            return string.Equals(field, expected, StringComparison.OrdinalIgnoreCase);
        }

        sealed class Scan
        {
            readonly string _json;
            int _i;

            public Scan(string json)
            {
                _json = json ?? string.Empty;
            }

            public char Peek
            {
                get { return _i < _json.Length ? _json[_i] : '\0'; }
            }

            public void Skip()
            {
                while (_i < _json.Length && char.IsWhiteSpace(_json[_i]))
                    _i++;
            }

            public bool Try(char c)
            {
                if (_i < _json.Length && _json[_i] == c)
                {
                    _i++;
                    return true;
                }

                return false;
            }

            public string ReadString()
            {
                Skip();
                if (_i >= _json.Length || _json[_i] != '"')
                    throw new FormatException();

                _i++;
                var builder = new StringBuilder();
                while (_i < _json.Length)
                {
                    var c = _json[_i++];
                    if (c == '"')
                        return builder.ToString();
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (_i >= _json.Length)
                        throw new FormatException();
                    var escaped = _json[_i++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        default:
                            builder.Append(escaped);
                            break;
                    }
                }

                throw new FormatException();
            }

            public void SkipValue()
            {
                Skip();
                if (_i >= _json.Length)
                    throw new FormatException();

                var c = _json[_i];
                if (c == '"')
                {
                    ReadString();
                    return;
                }

                if (c == '{')
                {
                    _i++;
                    Skip();
                    if (Try('}'))
                        return;
                    while (true)
                    {
                        ReadString();
                        Skip();
                        if (!Try(':'))
                            throw new FormatException();
                        SkipValue();
                        Skip();
                        if (Try('}'))
                            return;
                        if (!Try(','))
                            throw new FormatException();
                        Skip();
                    }
                }

                if (c == '[')
                {
                    _i++;
                    Skip();
                    if (Try(']'))
                        return;
                    while (true)
                    {
                        SkipValue();
                        Skip();
                        if (Try(']'))
                            return;
                        if (!Try(','))
                            throw new FormatException();
                        Skip();
                    }
                }

                while (_i < _json.Length)
                {
                    var k = _json[_i];
                    if (k == ',' || k == '}' || k == ']' || char.IsWhiteSpace(k))
                        break;
                    _i++;
                }
            }
        }
    }
}
