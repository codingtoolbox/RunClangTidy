using CodingToolBox.Service;
using Microsoft.VisualStudio.Shell.TableManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsUtil.Util;

namespace CodingToolBox.ErrorWindow
{
    class MessageFactory : TableEntriesSnapshotFactoryBase
    {
        public MessageSnapshot CurrentSnapshot { get; private set; }

        public MessageFactory(MessageSnapshot s)
        {
            this.CurrentSnapshot = s;
        }

        internal void UpdateErrors(MessageSnapshot errors)
        {
            this.CurrentSnapshot.NextSnapshot = errors;
            this.CurrentSnapshot = errors;
        }
        public override int CurrentVersionNumber => this.CurrentSnapshot.VersionNumber;

        public override void Dispose()
        {
        }

        public override ITableEntriesSnapshot GetCurrentSnapshot()
        {
            return this.CurrentSnapshot;
        }

        public override ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            // In theory the snapshot could change in the middle of the return statement so snap the snapshot just to be safe.
            var snapshot = this.CurrentSnapshot;
            return (versionNumber == snapshot.VersionNumber) ? snapshot : null;
        }
    }
}
