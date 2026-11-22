using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-178: closes a JSON object a model stopped writing halfway (its token budget ran out,
    /// or it wandered off). It cuts back to the last value that was complete and closes every
    /// array and object still open, so the fields written so far can still be read. Returns null
    /// when no value was complete. A finished object is returned unchanged. Plain C#.
    /// </summary>
    public static class JsonRepair
    {
        public static string CloseTruncated(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            var start = text.IndexOf('{');
            if (start < 0)
                return null;

            var stack = new List<char>();
            var inString = false;
            var escaped = false;
            var stringIsKey = false;
            var expectKey = false;
            var safeEnd = -1;
            string safeClosers = null;

            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                    {
                        inString = false;
                        if (!stringIsKey)
                            Mark(i + 1, stack, ref safeEnd, ref safeClosers);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        stringIsKey = stack.Count > 0 && stack[stack.Count - 1] == '{' && expectKey;
                        break;
                    case '{':
                    case '[':
                        stack.Add(c);
                        expectKey = c == '{';
                        break;
                    case '}':
                    case ']':
                        if (stack.Count == 0)
                            return null;
                        stack.RemoveAt(stack.Count - 1);
                        if (stack.Count == 0)
                            return text.Substring(start, i - start + 1);
                        expectKey = false;
                        Mark(i + 1, stack, ref safeEnd, ref safeClosers);
                        break;
                    case ':':
                        expectKey = false;
                        break;
                    case ',':
                        // Whatever came before the comma is complete.
                        Mark(i, stack, ref safeEnd, ref safeClosers);
                        expectKey = stack.Count > 0 && stack[stack.Count - 1] == '{';
                        break;
                }
            }

            if (safeEnd < 0 || safeClosers == null)
                return null;
            var body = text.Substring(start, safeEnd - start).TrimEnd();
            while (body.EndsWith(","))
                body = body.Substring(0, body.Length - 1).TrimEnd();
            return body + safeClosers;
        }

        static void Mark(int end, List<char> stack, ref int safeEnd, ref string closers)
        {
            safeEnd = end;
            var sb = new StringBuilder(stack.Count);
            for (var i = stack.Count - 1; i >= 0; i--)
                sb.Append(stack[i] == '{' ? '}' : ']');
            closers = sb.ToString();
        }
    }
}
