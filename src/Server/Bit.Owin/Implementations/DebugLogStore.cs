﻿#define DEBUG
using System;
using System.Threading.Tasks;
using Bit.Core.Contracts;
using Bit.Core.Models;
using System.Diagnostics;

namespace Bit.Owin.Implementations
{
    public class DebugLogStore : ILogStore
    {
        private readonly IContentFormatter _formatter;

        public DebugLogStore(IContentFormatter formatter)
        {
            _formatter = formatter;
        }

#if DEBUG
        protected DebugLogStore()
        {
        }
#endif

        public virtual void SaveLog(LogEntry logEntry)
        {
            if (Debugger.IsAttached)
                Debug.WriteLine(_formatter.Serialize(logEntry) + Environment.NewLine);
        }

        public virtual async Task SaveLogAsync(LogEntry logEntry)
        {
            if (Debugger.IsAttached)
                Debug.WriteLine(_formatter.Serialize(logEntry) + Environment.NewLine);
        }
    }
}
