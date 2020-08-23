﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SocketMeister.Testing
{

    /// <summary>
    /// ITest Interface 
    /// </summary>
    internal interface ITestOnHarness : ITest
    {
        void Reset();

        void Start();

        void Stop();

        event EventHandler<EventArgs> ExecuteTest;
        event EventHandler<TestPercentCompleteChangedEventArgs> PercentCompleteChanged;
        event EventHandler<TraceEventArgs> TraceEventRaised;


    }
}
