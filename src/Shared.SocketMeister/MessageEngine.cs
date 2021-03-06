﻿#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0090 // Use 'new(...)'
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0063 // Use simple 'using' statement
#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA1805 // Do not initialize unnecessarily
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable CA1825 // Avoid zero-length array allocations.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SocketMeister.Messages
{
    /// <summary>
    /// This is the core engine for creating bytes to send down a socket and to receive bytes from a socket.
    /// </summary>
    internal sealed partial class MessageEngine
    {
        internal class Parse
        {
            public Parse(long MessageNumber, string MessageType, int MessageLength, int MessageLengthUncompressed)
            {
                this.MessageLength = MessageLength;
                this.MessageLengthUncompressed = MessageLengthUncompressed;
                this.MessageNumber = MessageNumber;
                this.MessageType = MessageType;
                ReceivedDateTime = DateTime.Now;
            }

            public long MessageNumber { get; set; }
            public string MessageType { get; set; }
            public int MessageLength { get; set; }
            public int MessageLengthUncompressed { get; set; }
            public bool MessageReceived { get; set; }
            public int SocketBufferLength { get; set; }
            public int SocketBytesRead { get; set; }
            public DateTime ReceivedDateTime { get; set; }
        }

        internal class ParseHistory
        {
            private readonly List<Parse> _items = new List<Parse>();

            public void Add(Parse message)
            {
                if (_items.Count > 19) _items.RemoveAt(0);
                _items.Add(message);
            }

            public string Text
            {
                get
                {
                    string T = Environment.NewLine + Environment.NewLine + "Receive History" + Environment.NewLine;
                    foreach (Parse m in _items)
                    {
                        T += "#" + m.MessageNumber + " " + m.ReceivedDateTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        T += ", MessageType: " + m.MessageType;
                        T += ", MessageLength: " + m.MessageLength + " (" + m.MessageLengthUncompressed + " uncompressed)";
                        T += ", SocketBytesRead: " + m.SocketBytesRead + "(BufSize: " + m.SocketBufferLength + ")";
                        if (m.MessageReceived == true) T += ", MsgComplete: Y";
                        else T += ", MsgComplete: N";
                        T += Environment.NewLine;
                    }
                    return T;
                }
            }
        }


        public const int HEADERLENGTH = 11;

        private readonly bool _enableCompression;
        private readonly ParseHistory _history = new ParseHistory();
        private bool _messageIsCompressed = false;
        private int _messageLength = 0;
        private int _messageLengthUncompressed = 0;
        private MessageEngineMessageType _messageType = MessageEngineMessageType.Unknown;
        private readonly byte[] _headerBuffer = new byte[11];
        private int _headerBufferPtr = 0;
        private bool _headerReceived = false;
        private byte[] _receiveBuffer = new byte[0];
        private int _receiveBufferPtr = 0;
        private long _statMessageNumber = 1;
        private bool _statMessageReceived;
        private int _statSocketBufferLength;
        private int _statSocketBytesRead;
        private byte[] _uncompressedBuffer = new byte[0];

        internal MessageEngine(bool EnableCompression)
        {
            _enableCompression = EnableCompression;
        }

        internal bool AddBytesFromSocketReceiveBuffer(int SocketBytesRead, byte[] SocketReceiveBuffer, ref int SocketReceiveBufferPtr)
        {
            _statSocketBufferLength = SocketReceiveBuffer.Length;
            _statSocketBytesRead = SocketBytesRead;
            _statMessageReceived = false;

            if (_headerReceived == false)
            {
                int bytesRequired = 0;
                int bytesPossible = 0;
                try
                {
                    bytesRequired = HEADERLENGTH - _headerBufferPtr;
                    bytesPossible = bytesRequired;
                    if ((SocketBytesRead - SocketReceiveBufferPtr) < bytesRequired) bytesPossible = (SocketBytesRead - SocketReceiveBufferPtr);
                    Buffer.BlockCopy(SocketReceiveBuffer, SocketReceiveBufferPtr, _headerBuffer, _headerBufferPtr, bytesPossible);
                    _headerBufferPtr += bytesPossible;
                    SocketReceiveBufferPtr += bytesPossible;

                    if (_headerBufferPtr == HEADERLENGTH)
                    {
                        //  SETUP TO RECEIVE THE ENTIRE MESSAGE
                        _headerReceived = true;

                        //  SETUP MESSAGE BODY BYTE ARRAY FOR THE EXPECTED BYTES
                        _messageType = (MessageEngineMessageType)Convert.ToInt16(_headerBuffer[0] | (_headerBuffer[1] << 8));
                        _messageIsCompressed = Convert.ToBoolean(_headerBuffer[2]);
                        _messageLength = _headerBuffer[3] | (_headerBuffer[4] << 8) | (_headerBuffer[5] << 16) | (_headerBuffer[6] << 24);
                        _messageLengthUncompressed = _headerBuffer[7] | (_headerBuffer[8] << 8) | (_headerBuffer[9] << 16) | (_headerBuffer[10] << 24);

                        if (Enum.IsDefined(typeof(MessageEngineMessageType), _messageType) == false)
                        {
                            throw new Exception("Invalid Message Type");
                        }

                        //  ONLY EXTEND THE MESSAGE BUFFER IF IT IS TOO SMALL FOR THE INCOMING MESSAGE.
                        //  THIS PROVIDES IMPROVED SPEED AND MUCH LESS WORK FOR THE GARBAGE COLLECTOR (BECAUSE WE ARE REUSING THE BUFFER)
                        if (_messageLength > _receiveBuffer.Length) _receiveBuffer = new byte[_messageLength];
                        // _receiveBuffer = new byte[_messageLength];

                        //  WE MAY NEED THIS
                        ////  WRITE THE MESSAGE LENGTH IN THE FIRST 4 BYTES (THIS LOOKS CONVOLUTED BUT ACTUALLY IS THE FASTEST WAY POSSIBLE)
                        //_sendBytes[0] = (byte)(_uncompressedLength >> 24);
                        //_sendBytes[1] = (byte)(_uncompressedLength >> 16);
                        //_sendBytes[2] = (byte)(_uncompressedLength >> 8);
                        //_sendBytes[3] = (byte)_uncompressedLength;
                    }
                }
                catch (Exception ex)
                {
                    string msg = "Error processing Message Body. _messageLength: " + _messageLength + "(" + _messageLengthUncompressed + " uncompressed), ReceivedByesCount: " + SocketBytesRead + ", SocketReceiveBuffer.Length: " + SocketReceiveBuffer.Length;
                    msg += ", SocketReceiveBufferPtr: " + SocketReceiveBufferPtr;
                    msg += ", _receiveBuffer.Length: " + _receiveBuffer.Length + ", _receiveBufferPtr: " + _receiveBufferPtr;
                    msg += ", bytesRequired: " + bytesRequired + ", BytesPossible: " + bytesPossible;
                    msg += ", _messageType: " + _messageType.ToString() + ", _messageIsCompressed: " + _messageIsCompressed.ToString(CultureInfo.InvariantCulture);
                    msg += ", MessageCount: " + _statMessageNumber;
                    msg += _history.Text;
                    msg += Environment.NewLine + Environment.NewLine;
                    msg += ex.Message;
                    throw new Exception(msg);
                }
            }
            if (_headerReceived == true && (SocketReceiveBufferPtr < SocketBytesRead || _messageLength == 0))
            {
                int bytesRequired = 0;
                int bytesPossible = 0;
                try
                {
                    bytesRequired = _messageLength - _receiveBufferPtr;
                    bytesPossible = bytesRequired;
                    if ((SocketBytesRead - SocketReceiveBufferPtr) < bytesRequired) bytesPossible = (SocketBytesRead - SocketReceiveBufferPtr);
                    Buffer.BlockCopy(SocketReceiveBuffer, SocketReceiveBufferPtr, _receiveBuffer, _receiveBufferPtr, bytesPossible);
                    _receiveBufferPtr += bytesPossible;
                    SocketReceiveBufferPtr += bytesPossible;

                    if (_receiveBufferPtr == _messageLength)
                    {
                        _statMessageReceived = true;
                        Reset();
                        AddParseAttemptDetails();
                        _statMessageNumber++;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    string msg = "Error processing Message Body. _messageLength: " + _messageLength + "(" + _messageLengthUncompressed + " uncompressed), ReceivedByesCount: " + SocketBytesRead + ", SocketReceiveBuffer.Length: " + SocketReceiveBuffer.Length;
                    msg += ", SocketReceiveBufferPtr: " + SocketReceiveBufferPtr;
                    msg += ", _receiveBuffer.Length: " + _receiveBuffer.Length + ", _receiveBufferPtr: " + _receiveBufferPtr;
                    msg += ", bytesRequired: " + bytesRequired + ", BytesPossible: " + bytesPossible;
                    msg += ", _messageType: " + _messageType.ToString() + ", _messageIsCompressed: " + _messageIsCompressed.ToString(CultureInfo.InvariantCulture);
                    msg += ", MessageCount: " + _statMessageNumber;
                    msg += _history.Text;
                    msg += Environment.NewLine + Environment.NewLine;
                    msg += ex.Message;
                    throw new Exception(msg);
                }

            }
            AddParseAttemptDetails();
            return false;
        }


        /// <summary>
        /// Generate send bytes for a transmit operation.
        /// </summary>
        /// <param name="SendObject">Object to be sent</param>
        /// <param name="Compress">Compress the data</param>
        /// <returns>Byte array of the data to be sent</returns>
        public static byte[] GenerateSendBytes(IMessage SendObject, bool Compress)
        {
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                //  WRITE HEADER
                writer.Write(Convert.ToInt16(SendObject.MessageType, CultureInfo.InvariantCulture));

                if (Compress == false)
                {
                    //  WRITE HEADER
                    writer.Write(false);    //  NOT COMPRESSED
                    writer.Write(0);   //  WILL BE USED TO WRITE _compressedLength
                    writer.Write(0);   //  WILL BE USED TO WRITE _uncompressedLength

                    //  WRITE USER BYTES
                    long position1 = writer.BaseStream.Position;
                    SendObject.AppendBytes(writer);
                    int messageLength = Convert.ToInt32(writer.BaseStream.Position - position1);
                    int messageLengthUncompressed = messageLength;

                    //  GO BACK AND FINISH HEADER
                    writer.BaseStream.Position = 3;
                    writer.Write(messageLength);
                    writer.Write(messageLengthUncompressed);
                }
                else
                {
                    byte[] uncompressedBytes = null;
                    using (BinaryWriter writer2 = new BinaryWriter(new MemoryStream()))
                    {
                        SendObject.AppendBytes(writer2);

                        using (BinaryReader reader2 = new BinaryReader(writer2.BaseStream))
                        {
                            reader2.BaseStream.Position = 0;
                            uncompressedBytes = reader2.ReadBytes(Convert.ToInt32(reader2.BaseStream.Length));

                        }
                    }

                    if (uncompressedBytes != null && uncompressedBytes.Length > 1024)
                    {
                        writer.Write(true);     //  COMPRESSED
                        byte[] compressedBytes = CLZF2.Compress(uncompressedBytes);
                        writer.Write(compressedBytes.Length);
                        writer.Write(uncompressedBytes.Length);
                        writer.Write(compressedBytes);
                    }
                    else
                    {
                        writer.Write(false);     //  UNCOMPRESSED
                        writer.Write(0);   //  WILL BE USED TO WRITE _compressedLength
                        writer.Write(0);   //  WILL BE USED TO WRITE _uncompressedLength

                        //  WRITE USER BYTES
                        long position1 = writer.BaseStream.Position;
                        SendObject.AppendBytes(writer);
                        int messageLength2 = Convert.ToInt32(writer.BaseStream.Position - position1);
                        int messageLengthUncompressed2 = messageLength2;

                        //  GO BACK AND FINISH HEADER
                        writer.BaseStream.Position = 3;
                        writer.Write(messageLength2);
                        writer.Write(messageLengthUncompressed2);
                    }


                }
                using (BinaryReader reader = new BinaryReader(writer.BaseStream))
                {
                    reader.BaseStream.Position = 0;
                    return reader.ReadBytes(Convert.ToInt32(reader.BaseStream.Length));

                }
            }
        }


        /// <summary>
        /// The type of message. 
        /// </summary>
        internal MessageEngineMessageType MessageType => _messageType;

        internal int MessageLength => _messageLength;

        internal int MessageLengthUncompressed => _messageLengthUncompressed;


        private void AddParseAttemptDetails()
        {
            Parse p = new Parse(_statMessageNumber, _messageType.ToString(), _messageLength, _messageLengthUncompressed)
            {
                MessageReceived = _statMessageReceived,
                SocketBytesRead = _statSocketBytesRead,
                SocketBufferLength = _statSocketBufferLength
            };
            _history.Add(p);
        }


        internal ServerStoppingNotificationV1 GetServerStoppingNotificationV1()
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(GetBuffer());
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream = null;
                    return new ServerStoppingNotificationV1(reader);
                }
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }


        internal MessageV1 GetMessageV1()
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(GetBuffer());
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream = null;
                    return new MessageV1(reader);
                }
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }

        internal BroadcastV1 GetBroadcastV1()
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(GetBuffer());
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream = null;
                    return new BroadcastV1(reader);
                }
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }



        internal TokenChangesRequestV1 GetSubscriptionChangesNotificationV1()
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(GetBuffer());
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream = null;
                    return new TokenChangesRequestV1(reader);
                }
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }


        internal TokenChangesResponseV1 GetSubscriptionChangesResponseV1()
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(GetBuffer());
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream = null;
                    return new TokenChangesResponseV1(reader);
                }
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }



        internal MessageResponseV1 GetMessageResponseV1()
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(GetBuffer());
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream = null;
                    return new MessageResponseV1(reader);
                }
            }
            finally
            {
                if (stream != null) stream.Dispose();
            }
        }

        private byte[] GetBuffer()
        {
            if (_messageIsCompressed == false) return _receiveBuffer;
            //  UNCOMPRESS
            if (_uncompressedBuffer.Length < _messageLengthUncompressed) _uncompressedBuffer = new byte[_messageLengthUncompressed];
            CLZF2.Decompress(_receiveBuffer, ref _uncompressedBuffer, _messageLength);
            return _uncompressedBuffer;
        }

        /// <summary>
        /// Resets the envelope for a fresh receive operation. Warning: should not be done in the middle of a receive operation.
        /// </summary>
        public void Reset()
        {
            _headerBufferPtr = 0;
            _receiveBufferPtr = 0;
            _headerReceived = false;
        }


    }
}

#pragma warning restore CA1825 // Avoid zero-length array allocations.
#pragma warning restore CA1031 // Do not catch general exception types
#pragma warning restore CA1805 // Do not initialize unnecessarily
#pragma warning restore CA1303 // Do not pass literals as localized parameters
#pragma warning restore IDE0063 // Use simple 'using' statement
#pragma warning restore IDE0052 // Remove unread private members
#pragma warning restore IDE0090 // Use 'new(...)'
#pragma warning restore IDE0079 // Remove unnecessary suppression

