using CodingToolBox.Service;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsUtil.Util;

namespace CodingToolBox.ErrorWindow
{
    class MessageTagger : ITagger<IErrorTag>, IDisposable
    {
        private readonly List<EditorMessage> m_failures = new List<EditorMessage>();
        private readonly MessageGeneratorService m_service;
        private readonly ITextBuffer m_buffer;
        public MessageTagger(string filePath, MessageGeneratorService service, ITextBuffer buffer) 
        {
            m_buffer = buffer;
            m_service = service;
            m_service.SubscribeTagger(this);
            Update(false);
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            m_service.UnsubscribeTagger(this);
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (m_failures != null && m_failures.Count > 0)
            {
                var first = m_failures.First();
                if (spans.Count > 0 && spans.First().Snapshot.Version != first.Span.Snapshot.Version)
                    Update(false);

                foreach (var f in m_failures)
                {
                    if (spans.IntersectsWith(f.Span))                    {
                        yield return new TagSpan<IErrorTag>(f.Span, new ErrorTag(PredefinedErrorTypeNames.Warning, f.FailureText.m_description));
                    }
                }
            }
        }

        internal void Update(bool raise = true)
        {
            var snapshot = m_buffer.CurrentSnapshot;
            var messages = m_service.Filter(m_service.GetFileNameFor(m_buffer), m_buffer.CurrentSnapshot);
            m_failures.Clear();
            m_failures.AddRange(messages);
            var start = 0;
            var end = snapshot.Length;
            if (messages.Count > 0)
            {
                start = messages.Min(f => f.Span.Start);
                end = messages.Max(f => f.Span.End);
            }
            if (raise)
                TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, Span.FromBounds(start, end))));
        }
    }
}
