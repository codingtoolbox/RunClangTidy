using CodingToolBox.ErrorWindow;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using VsUtil.Util;

namespace CodingToolBox.Service
{
    class FailureCollection
    {
        internal readonly MessageFactory Factory;

        public FailureCollection(MessageGeneratorService provider, string filePath, List<MessageText> failures)
        {
            this.Factory = new MessageFactory(new MessageSnapshot(filePath, failures, 0));
        }
    }

}
