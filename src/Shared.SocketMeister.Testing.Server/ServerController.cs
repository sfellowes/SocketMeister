﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace SocketMeister.Testing
{
    /// <summary>
    /// Controls a socket server
    /// </summary>
    public class ServerController
    {
        private readonly ControlBusClient _harnessControlBusClient;
        private readonly object _lock = new object();
        private readonly int _port;
        private SocketServer _socketServer = null;

        /// <summary>
        /// Triggered when connection could not be established with the HarnessController. This ClientController should now abort (close)
        /// </summary> 
        public event EventHandler<EventArgs> HarnessConnectionFailed;

        /// <summary>
        /// Raised when the status of the socket listener changes.
        /// </summary>
        public event EventHandler<SocketServer.SocketServerStatusChangedEventArgs> ListenerStateChanged;

        /// <summary>
        /// Raised when an trace log event has been raised.
        /// </summary>
        internal event EventHandler<TraceEventArgs> TraceEventRaised;

        /// <summary>
        /// Raised when a message is received from a client.
        /// </summary>
        internal event EventHandler<SocketServer.MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Raised when a request message is received from a client. A response can be provided which will be returned to the client.
        /// </summary>
        internal event EventHandler<SocketServer.RequestReceivedEventArgs> RequestReceived;

        /// <summary>
        /// Event raised when when there is a change to the clients connected to the socket server
        /// </summary>
        internal event EventHandler<SocketServer.ClientsChangedEventArgs> ClientsChanged;


        public ServerController(int Port, int HarnessControlBusClientId, string HarnessControlBusIPAddress)
        {
            //  CONNECT TO THE HarnessController
            _harnessControlBusClient = new ControlBusClient(ControlBusClientType.ClientController, HarnessControlBusClientId, HarnessControlBusIPAddress, Constants.HarnessControlBusPort);
            _harnessControlBusClient.ConnectionFailed += harnessControlBusClient_ConnectionFailed;
            _harnessControlBusClient.ControlBusSocketClient.MessageReceived += HarnessControlBusSocketClient_MessageReceived;

            _port = Port;
        }


        private void HarnessControlBusSocketClient_MessageReceived(object sender, SocketClient.MessageReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void harnessControlBusClient_ConnectionFailed(object sender, EventArgs e)
        {
            //  CONNECTION TO THE HarnessController COULD NOT BE ESTABLISHED. PARENT FORM (IF THERE IS ONE) SHOULD CLOSE
            HarnessConnectionFailed?.Invoke(this, e);
        }

        /// <summary>
        /// 
        /// </summary>
        public void StopAll()
        {
            _harnessControlBusClient.Stop();
            StopSocketServer();
        }

        public SocketServer SocketServer
        {
            get { return _socketServer; }
        }

        public int Port
        {
            get { return _port; }
        }


        private void SocketServer_ListenerStateChanged(object sender, SocketServer.SocketServerStatusChangedEventArgs e)
        {
            ListenerStateChanged?.Invoke(this, e);
        }


        internal void StartSocketServer()
        {
            //  START SOCKET SERVER IN BACHGROUND
            Thread bgStartSocketServer = new Thread(new ThreadStart(delegate
            {
                _socketServer = new SocketServer(_port, true);
                _socketServer.ClientsChanged += SocketServer_ClientsChanged;
                _socketServer.ListenerStateChanged += SocketServer_ListenerStateChanged;
                _socketServer.TraceEventRaised += SocketServer_TraceEventRaised;
                _socketServer.MessageReceived += SocketServer_MessageReceived;
                _socketServer.RequestReceived += SocketServer_RequestReceived;
                _socketServer.Start();
            }));
            bgStartSocketServer.IsBackground = true;
            bgStartSocketServer.Start();
        }

        internal void StopSocketServer()
        {
            if (_socketServer == null) return;

            //  UNREGISTER EVENTS
            _socketServer.ClientsChanged -= SocketServer_ClientsChanged;
            _socketServer.ListenerStateChanged -= SocketServer_ListenerStateChanged;
            _socketServer.TraceEventRaised -= SocketServer_TraceEventRaised;
            _socketServer.MessageReceived -= SocketServer_MessageReceived;
            _socketServer.RequestReceived -= SocketServer_RequestReceived;

            //  STOP SOCKET SERVER
            if (_socketServer.Status == SocketServerStatus.Started)
            {
                _socketServer.Stop();
            }
        }

        private void SocketServer_MessageReceived(object sender, SocketServer.MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(sender, e);
        }

        private void SocketServer_RequestReceived(object sender, SocketServer.RequestReceivedEventArgs e)
        {
            RequestReceived?.Invoke(sender, e);
        }

        private void SocketServer_ClientsChanged(object sender, SocketServer.ClientsChangedEventArgs e)
        {
            ClientsChanged?.Invoke(sender, e);
        }

        private void SocketServer_TraceEventRaised(object sender, TraceEventArgs e)
        {
            TraceEventRaised?.Invoke(sender, e);
        }



    }
}