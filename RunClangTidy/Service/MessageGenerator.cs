using CodingToolBox.ErrorWindow;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsUtil.Util;

namespace CodingToolBox.Service
{

    [Export(typeof(IMessageGenerator))]
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Analyzable)]
    class MessageGenerator : ITableDataSource, 
        IMessageGenerator, 
        IViewTaggerProvider
    {
        public void SetMessages(List<MessageText> col)
        {
            Dictionary<string, List<MessageText>> errorsByFile = new Dictionary<string, List<MessageText>> ();
            foreach (var f  in col)
            {
                List<MessageText> failures;
                if (errorsByFile.TryGetValue( f.m_file , out failures))
                {
                    failures.Add(f);
                }
                else
                {
                    failures = new List<MessageText>();
                    failures.Add(f);
                    errorsByFile.Add(f.m_file, failures);
                }
             }
            m_errorsByFile = errorsByFile;
            var oldFailures = _failures.ToArray();
            foreach (var f in oldFailures)
                RemoveFailureCollection(f);
            foreach (var e in errorsByFile)
            {
                MessageCollection f = new MessageCollection(this, e.Key, e.Value);
                AddFailureCollection(f);
            }
            UpdateAllSinks();
            FireTaggerChanges();
        }

        public List<EditorMessage> Filter(string filePath, ITextSnapshot snapshot)
        {
            if (snapshot == null)
                return new List<EditorMessage>();
            List<MessageText> errors; 
            if (m_errorsByFile == null)
                return new List<EditorMessage>();
            if (!m_errorsByFile.TryGetValue(filePath, out errors))
                return new List<EditorMessage>();
            List<EditorMessage> failureList = new List<EditorMessage>();
            foreach (var e in errors)
            {
                if (e.m_errorLine == null)
                    continue;

                var errorLine = snapshot.GetLineFromLineNumber(e.m_line-1);
                if (errorLine == null)
                    continue;
                var text = errorLine.GetText();
                if (text == null)
                    continue;
                text = text.Trim();

                if (text.Contains(e.m_errorLine.Trim()))
                {
                    EditorMessage f = new EditorMessage(errorLine.Extent, e);
                    failureList.Add(f);
                }
            }
            return failureList;
        }

        internal readonly ITableManager ErrorTableManager;
        internal readonly ITextDocumentFactoryService TextDocumentFactoryService;
        internal readonly IClassifierAggregatorService ClassifierAggregatorService;

        const string _datasource = "clang tidy";

        private readonly List<SinkManager> _managers = new List<SinkManager>();      // Also used for locks
        private readonly List<MessageCollection> _failures = new List<MessageCollection>();
        private Dictionary<string, List<MessageText>> m_errorsByFile;
        private List<MessageTagger> m_taggers = new List<MessageTagger>();

        [ImportingConstructor]
        internal MessageGenerator([Import]ITableManagerProvider provider, [Import] ITextDocumentFactoryService textDocumentFactoryService, [Import] IClassifierAggregatorService classifierAggregatorService)
        {
            this.ErrorTableManager = provider.GetTableManager(StandardTables.ErrorsTable);
            this.TextDocumentFactoryService = textDocumentFactoryService;

            this.ClassifierAggregatorService = classifierAggregatorService;

            this.ErrorTableManager.AddSource(this, StandardTableColumnDefinitions.DetailsExpander,
                                                   StandardTableColumnDefinitions.ErrorSeverity, StandardTableColumnDefinitions.ErrorCode,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.BuildTool,
                                                   StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.ErrorCategory,
                                                   StandardTableColumnDefinitions.Text, StandardTableColumnDefinitions.DocumentName, StandardTableColumnDefinitions.Line, StandardTableColumnDefinitions.Column);
        }

        public string GetFileNameFor(ITextBuffer buffer)
        {
            ITextDocument document;
            if (this.TextDocumentFactoryService.TryGetTextDocument(buffer, out document))
                return Path.GetFullPath(new Uri(document.FilePath).LocalPath); 
            return null;
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            ITagger<T> tagger = null;

            // Only attempt to spell check on the view's edit buffer (and multiple views could have that buffer open simultaneously so
            // only create one instance of the spell checker.
            if ((buffer == textView.TextBuffer) && (typeof(T) == typeof(IErrorTag)))
            {
                var filePath = GetFileNameFor(buffer);
                if (filePath != null)
                    tagger = new MessageTagger(filePath, this, buffer) as ITagger<T>;
                
            }

            return tagger;
        }
        

        #region ITableDataSource members
        public string DisplayName
        {
            get
            {
                return Resources.RunTidy;
            }
        }

        public string Identifier
        {
            get
            {
                return _datasource;
            }
        }

        public string SourceTypeIdentifier
        {
            get
            {
                return StandardTableDataSources.ErrorTableDataSource;
            }
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            // This method is called to each consumer interested in errors. In general, there will be only a single consumer (the error list tool window)
            // but it is always possible for 3rd parties to write code that will want to subscribe.
            return new SinkManager(this, sink);
        }
        #endregion

        public void AddSinkManager(SinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (_managers)
            {
                _managers.Add(manager);

                // Add the pre-existing spell checkers to the manager.
                foreach (var s in _failures)
                {
                    manager.AddFailureCollection(s);
                }
            }
        }

        public void RemoveSinkManager(SinkManager manager)
        {
            // This call can, in theory, happen from any thread so be appropriately thread safe.
            // In practice, it will probably be called only once from the UI thread (by the error list tool window).
            lock (_managers)
            {
                _managers.Remove(manager);
            }
        }

        public void AddFailureCollection(MessageCollection с)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding or removing the 1st/last tagger).
            lock (_managers)
            {
                _failures.Add(с);

                // Tell the preexisting managers about the new spell checker
                foreach (var manager in _managers)
                {
                    manager.AddFailureCollection(с);
                }
            }
        }

        public void RemoveFailureCollection(MessageCollection с)
        {
            // This call will always happen on the UI thread (it is a side-effect of adding or removing the 1st/last tagger).
            lock (_managers)
            {
                _failures.Remove(с);

                foreach (var manager in _managers)
                {
                    manager.RemoveFailureCollection(с);
                }
            }
        }

        public void UpdateAllSinks()
        {
            lock (_managers)
            {
                foreach (var manager in _managers)
                {
                    manager.UpdateSink();
                }
            }
        }

        internal void SubscribeTagger(MessageTagger failureTagger)
        {
            lock (m_taggers)
            {
                m_taggers.Add(failureTagger);
            }
            
        }

        internal void UnsubscribeTagger(MessageTagger failureTagger)
        {
            lock (m_taggers)
            {
                m_taggers.Remove(failureTagger);
            }
        }
        void FireTaggerChanges()
        {
            lock (m_taggers)
            {
                m_taggers.ForEach(tagger => tagger.Update());
            }
            
        }

    }

    class SinkManager : IDisposable
    {
        private readonly MessageGenerator _provider;
        private readonly ITableDataSink _sink;

        internal SinkManager(MessageGenerator p, ITableDataSink sink)
        {
            _provider = p;
            _sink = sink;

            _provider.AddSinkManager(this);
        }

        public void Dispose()
        {
            // Called when the person who subscribed to the data source disposes of the cookie (== this object) they were given.
            _provider.RemoveSinkManager(this);
        }

        internal void AddFailureCollection(MessageCollection c)
        {
            _sink.AddFactory(c.Factory);
        }

        internal void RemoveFailureCollection(MessageCollection c)
        {
            _sink.RemoveFactory(c.Factory);
        }

        internal void UpdateSink()
        {
            _sink.FactorySnapshotChanged(null);
        }
    }


    public interface IMessageGenerator
    {
        void SetMessages(List<MessageText> col);
    }
}
