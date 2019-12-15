using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VsUtil.Util
{
    public class EditorMessage
    {
        public readonly SnapshotSpan Span;
        public readonly MessageText FailureText;
        public EditorMessage(SnapshotSpan span, MessageText failureText)
        {
            Span = span;
            FailureText = failureText;
        }

        public static EditorMessage Clone(EditorMessage error)
        {
            return new EditorMessage(error.Span, error.FailureText);
        }

        public static EditorMessage CloneAndTranslateTo(EditorMessage error, ITextSnapshot newSnapshot)
        {
            var newSpan = error.Span.TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive);

            // We want to only translate the error if the length of the error span did not change (if it did change, it would imply that
            // there was some text edit inside the error and, therefore, that the error is no longer valid).
            return (newSpan.Length == error.Span.Length)
                   ? new EditorMessage(newSpan, error.FailureText)
                   : null;
        }
    }

    public class MessageText
    {
        public const string ErrorText = ": error:";
        public const string WarningText = ": warning:";
        public const string NoteText = ": note:";
        public int NextIndex = -1;
        public string m_file;
        public int m_line;
        public int m_column;
        public string m_description;
        public string m_code;
        public string m_errorLine;
        public string m_columnMarker;
        public string m_fixLine;
        enum LineState
        {
            ERROR_STRING, 
            COLUMN_MARKER, 
            FIX_LINE
        }; 
        LineState m_state = LineState.ERROR_STRING;
        enum Type
        {
            NOTE, 
            WARNING, 
            ERROR
        }
        private Type m_type;

        public bool Warning { get => m_type == Type.WARNING; set { m_type = Type.WARNING; } }
        internal bool Note { get => m_type == Type.NOTE; set { m_type = Type.NOTE; } }
        internal bool Error { get => m_type == Type.ERROR; set { m_type = Type.ERROR; } }
        public bool Accept( string line)
        {
            switch (m_state)
            {
                case LineState.ERROR_STRING:
                    m_state = LineState.COLUMN_MARKER;
                    m_errorLine = line;
                    return true;
                case LineState.COLUMN_MARKER:
                    m_columnMarker = line;
                    if (line.Contains("~~~"))
                    {
                        m_state = LineState.FIX_LINE;
                        return true;
                    }
                    return false;
                case LineState.FIX_LINE:
                    m_fixLine = line;
                    return false;
            }
            return false;
        }

        public string ToErrorString()
        {
            var ret = m_file + "(" + m_line + "," + m_column + ")";
            switch (m_type)
            {
                case Type.WARNING:
                    ret += WarningText;
                    break;
                case Type.ERROR:
                    ret += ErrorText;
                    break;
                case Type.NOTE:
                    ret += NoteText;
                    break;
            }
            ret += m_description;
            return ret;
        }
    }

    public class AnalysisOutputParser
    {
        public List<MessageText> Failures { get; set; }
        private CountdownEvent m_event;
        private CancellationTokenSource m_cancellation;


        public AnalysisOutputParser(int files)
        {
            m_event = new CountdownEvent(files);
            m_cancellation = new CancellationTokenSource();
            Failures = new List<MessageText>();
        }

        public void ParseOutput(string output)
        {
            List<MessageText> messages = new List<MessageText>();
            var split = output.Split(new char[] { '\n', '\r' }).Where(s => s.Length > 0);
            MessageText message = null;
            foreach (var line in split)
            {
                if (message == null)
                {
                    message = GetMessage(line);
                    continue;
                }
                if (!message.Accept(line))
                {
                    if (!message.Note) // todo handle notes
                        messages.Add(message);
                    message = GetMessage(line);
                }
            }
            if (message != null && !message.Note)
                messages.Add(message);
            
            Failures.AddRange(messages);
            OutputComplete();
        }

        public static MessageText GetMessage(string line)
        {
            var errorPos = line.IndexOf(MessageText.ErrorText);
            if (errorPos > 0)
                return CreateMessage(errorPos, errorPos + MessageText.ErrorText.Length, line, false);

            var warningPos = line.IndexOf(MessageText.WarningText);
            if (warningPos > 0)
                return CreateMessage(warningPos, warningPos + MessageText.WarningText.Length, line, true);

            var notePos = line.IndexOf(MessageText.NoteText);
            if (notePos > 0)
            {
                var f = CreateMessage(notePos, notePos + MessageText.NoteText.Length, line, false);
                f.Note = true;
                return f;
            }

            return null;
        }

        private static MessageText CreateMessage(int eofPos, int locPos, string line, bool isWarning)
        {
            var ret = new MessageText();
            ret.Warning = isWarning;
            var fileLine = line.Substring(0, eofPos);
            var columnPos = fileLine.LastIndexOf(":");
            if (columnPos <= 0)
                return null;
            var pos = fileLine.Substring(columnPos+1);
            if (!int.TryParse(pos, out ret.m_column))
                return null;
            fileLine = fileLine.Substring(0,columnPos);

            var linePos = fileLine.LastIndexOf(':');
            if (linePos <= 0)
                return null;
            pos = fileLine.Substring(linePos+1);
            if (!int.TryParse(pos, out ret.m_line))
                return null;
            ret.m_file = Path.GetFullPath(new Uri(fileLine.Substring(0, linePos)).LocalPath);
         
            // +2 for two ':' characters
            ret.m_description = line.Substring(locPos);
            int codeStart = ret.m_description.LastIndexOf("[");
            if (codeStart > 0)
                ret.m_code = ret.m_description.Substring(codeStart);
            return ret;
        }

        internal void WaitCompletion()
        {
            try
            {
                m_event.Wait(m_cancellation.Token);
            }
            catch (OperationCanceledException) { } // ignore error - todo: handle output parsing
        }

        internal void OutputComplete()
        {
           m_event.Signal();
        }

        internal void Cancel()
        {
            m_cancellation.Cancel();
        }
    }
}
