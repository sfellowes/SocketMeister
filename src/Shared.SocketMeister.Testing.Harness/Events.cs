﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SocketMeister.Testing
{

    internal class HarnessClientEventArgs : EventArgs
    {
        private readonly HarnessControlBusClientSocketClient _client;

        internal HarnessClientEventArgs(HarnessControlBusClientSocketClient Client)
        {
            _client = Client;
        }

        public HarnessControlBusClientSocketClient Client
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
