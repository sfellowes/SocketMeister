﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SocketMeister.Testing
{

    internal class HarnessClientEventArgs : EventArgs
    {
        private readonly ControlBusListenerClient _client;

        internal HarnessClientEventArgs(ControlBusListenerClient Client)
        {
            _client = Client;
        }

        public ControlBusListenerClient Client
        {
            get { return _client; }
        }

    }


    internal class HarnessTestStatusChangedEventArgs : EventArgs
    {
        private readonly TestStatus _status;
        private readonly ITestOnHarness _test;

        internal HarnessTestStatusChangedEventArgs(ITestOnHarness Test, TestStatus Status)
        {
            _test = Test;
            _status = Status;
        }

        public TestStatus Status
        {
            get { return _status; }
        }

        public ITestOnHarness Test
        {
            get { return _test; }
        }
    }


}
