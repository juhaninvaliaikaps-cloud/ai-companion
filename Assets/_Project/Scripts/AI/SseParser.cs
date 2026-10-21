using System.Collections.Generic;
using System.Text;

namespace AICompanion.AI
{
    public enum SseKind
    {
        Data = 0,
        Done = 1,
        Malformed = 2
    }

    public struct SseEvent
    {
        public SseKind Kind;
        public string Data;
    }

    /// <summary>
    /// Incremental Server-Sent Events reader. Chunks may split a line or a
    /// multi-line <c>data</c> field. A blank line finishes one event.
    /// </summary>
    public sealed class SseParser
    {
        readonly StringBuilder _line = new StringBuilder();
        readonly StringBuilder _data = new StringBuilder();
        bool _dataStarted;
        bool _malformed;
        bool _cr;

        public void Push(string chunk, List<SseEvent> sink)
        {
            if (chunk == null || sink == null)
                return;

            for (var i = 0; i < chunk.Length; i++)
            {
                var c = chunk[i];
                if (c == '\r')
                {
                    _cr = true;
                    FinishLine(sink);
                    continue;
                }

                if (c == '\n')
                {
                    if (_cr)
                    {
                        _cr = false;
                        continue;
                    }

                    FinishLine(sink);
                    continue;
                }

                _cr = false;
                _line.Append(c);
            }
        }

        public void Flush(List<SseEvent> sink)
        {
            if (sink == null)
                return;

            if (_line.Length > 0 || _cr)
            {
                _cr = false;
                FinishLine(sink);
            }

            if (_dataStarted || _malformed)
                Dispatch(sink);
        }

        void FinishLine(List<SseEvent> sink)
        {
            var line = _line.ToString();
            _line.Length = 0;
            if (line.Length == 0)
            {
                Dispatch(sink);
                return;
            }

            if (line[0] == ':')
                return;

            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                _malformed = true;
                return;
            }

            var field = line.Substring(0, colon);
            var value = line.Substring(colon + 1);
            if (value.Length > 0 && value[0] == ' ')
                value = value.Substring(1);

            if (field != "data")
                return;

            if (_dataStarted)
                _data.Append('\n');
            _data.Append(value);
            _dataStarted = true;
        }

        void Dispatch(List<SseEvent> sink)
        {
            if (!_dataStarted && !_malformed)
                return;

            if (_malformed && !_dataStarted)
            {
                sink.Add(new SseEvent { Kind = SseKind.Malformed, Data = string.Empty });
                _malformed = false;
                return;
            }

            var data = _data.ToString();
            _data.Length = 0;
            _dataStarted = false;
            _malformed = false;

            if (data.Trim() == "[DONE]")
            {
                sink.Add(new SseEvent { Kind = SseKind.Done, Data = data });
                return;
            }

            sink.Add(new SseEvent { Kind = SseKind.Data, Data = data });
        }
    }
}
