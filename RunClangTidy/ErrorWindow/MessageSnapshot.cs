using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Documents;
using VsUtil.Util;

namespace CodingToolBox.ErrorWindow
{
    class MessageSnapshot : WpfTableEntriesSnapshotBase
    {
        private readonly string _filePath;
        private readonly int _versionNumber;

        // We're not using an immutable list here but we cannot modify the list in any way once we've published the snapshot.
        public readonly List<MessageText> Messages;
        
        public MessageSnapshot NextSnapshot;

        public MessageSnapshot(string filePath,
            List<MessageText> messages, int version)
        {
            _filePath = filePath;
            _versionNumber = version;
            Messages = new List<MessageText>();
            Messages.AddRange(messages);
        }


        public override int Count => Messages.Count;
        public override int VersionNumber => _versionNumber;
        public override int IndexOf(int currentIndex, ITableEntriesSnapshot newerSnapshot)
        {
            var currentSnapshot = this;
            do
            {
                currentIndex = currentSnapshot.Messages[currentIndex].NextIndex;
                currentSnapshot = currentSnapshot.NextSnapshot;
            }
            while ((currentSnapshot != null) && (currentSnapshot != newerSnapshot) && (currentIndex >= 0));
            return currentIndex;
        }

        public override bool TryGetValue(int index, string columnName, out object content)
        {
            if ((index < 0) || index > this.Messages.Count)
            {
                content = null;
                return false;
            }

            switch( columnName)
            {
                case StandardTableKeyNames.DocumentName:
                    content = this.Messages[index].m_file;
                    return true;
                case StandardTableKeyNames.ErrorSource:
                    content = ErrorSource.Other;
                    return true;
                case StandardTableKeyNames.ErrorCategory:
                    content = "clang-tidy";
                    return true;
                case StandardTableKeyNames.Line:
                    content =  this.Messages[index].m_line - 1;
                    return true;
                case StandardTableKeyNames.Column:
                    content = this.Messages[index].m_column;
                    return true;
                case StandardTableKeyNames.Text:
                    content = string.Format(CultureInfo.InvariantCulture, this.Messages[index].m_description);
                    return true;
                case StandardTableKeyNames2.TextInlines:
                    {
                        var inlines = new List<Inline>();
                        inlines.Add(new Run(this.Messages[index].m_description));
                        content = inlines;
                    }
                    return true;
                case StandardTableKeyNames.ErrorSeverity:
                    if (this.Messages[index].Warning)
                        content = __VSERRORCATEGORY.EC_WARNING;
                    else if (this.Messages[index].Error)
                        content = __VSERRORCATEGORY.EC_ERROR;
                    else
                        content = __VSERRORCATEGORY.EC_MESSAGE;
                    return true;
                case StandardTableKeyNames.BuildTool:
                    content = "clang tidy";
                    return true;
                case StandardTableKeyNames.ErrorCode:
                    if (this.Messages[index].m_code == null)
                        break;
                    content = this.Messages[index].m_code;
                    return true;
                case StandardTableKeyNames.ErrorCodeToolTip:
                case StandardTableKeyNames.HelpLink:
                    content = string.Format(CultureInfo.InvariantCulture, "http://www.duckduckgo.com/{0}", System.Uri.EscapeUriString(this.Messages[index].m_description));
                    return true;
            }
            // We should also be providing values for StandardTableKeyNames.Project & StandardTableKeyNames.ProjectName but that is
            // beyond the scope of this sample.
            content = null;
            return false;
        }

        public override bool CanCreateDetailsContent(int index)
        {
            return this.Messages[index].m_errorLine.Length > 0;
        }

        public override bool TryCreateDetailsStringContent(int index, out string content)
        {
            string inlines = this.Messages[index].m_errorLine + "\n";
            //inlines.Add(new Run(this.Errors[index].m_columnMarker + "\n"));
            if (this.Messages[index].m_fixLine != null)
                inlines  += this.Messages[index].m_fixLine + "\n";
            content = inlines;
            return (content != null);
        }
    }
}
