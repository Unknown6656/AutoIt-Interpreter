using System.Security.Permissions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Text;
using System.IO;
using System;

using Microsoft.Win32.SafeHandles;

// CLONED AND REFACTORED FROM https://referencesource.microsoft.com/#System/sys/system/io/ports/SerialPort.cs,bd9a96fe323cfa70
//                   AND FROM https://referencesource.microsoft.com/#System/sys/system/io/ports/SerialStream.cs,b120632fda7c01ba

namespace AutoItCoreLibrary
{
    public sealed class SerialPort
        : Component
    {
        public const int InfiniteTimeout = -1;


        private const int defaultDataBits = 8;
        private const Parity defaultParity = Parity.None;
        private const StopBits defaultStopBits = StopBits.One;
        private const int defaultBufferSize = 1024;
        private const int MAX_DATABITS = 8;
        private const int MIN_DATABITS = 5;

        private int _rate = 9600;
        private int _databits = 8;
        private Parity _parity;
        private StopBits _stopbits = StopBits.One;
        private string _name = "COM1";
        private string _newline = "\n";
        private Encoding _encoding = Encoding.ASCII;
        private Decoder _decoder = Encoding.ASCII.GetDecoder();
        private int _maxbytecountforsinglechar = Encoding.ASCII.GetMaxByteCount(1);
        private Handshake _handshake;
        private int _rimeout = InfiniteTimeout;
        private int _wtimeout = InfiniteTimeout;
        private int _receivedbytesthreshold = 1;
        private bool _discardnull;
        private bool _dtrenable;
        private bool _rtsenable;
        private byte _parityreplace = (byte)'?';
        private int _readbuffersize = 4096;
        private int _writebuffersize = 2048;

        private SerialStream _internalserialstream;
        private byte[] _inbuffer = new byte[defaultBufferSize];
        private char[] _onechar = new char[1];
        private char[] _singlecharbuffer;
        private int _readpos;
        private int _readlen;


        public event EventHandler<SerialData> DataReceived;
        public event EventHandler<SerialPinChange> PinChanged;
        public event EventHandler<SerialError> ErrorReceived;


        public Stream BaseStream => AssertOpen(_internalserialstream);

        public int BaudRate
        {
            get => _rate;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(BaudRate));

                (IsOpen ? ref _internalserialstream.BaudRate : ref _rate) = value;
            }
        }

        public bool BreakState
        {
            get => AssertOpen(_internalserialstream.BreakState);
            set => AssertOpen(() => _internalserialstream.BreakState = value);
        }

        public int BytesToWrite => AssertOpen(_internalserialstream.BytesToWrite);

        public int BytesToRead => AssertOpen(_internalserialstream.BytesToRead + CachedBytesToRead);

        private int CachedBytesToRead => _readlen - _readpos;

        public bool CDHolding => AssertOpen(_internalserialstream.CDHolding);

        public bool CtsHolding => AssertOpen(_internalserialstream.CtsHolding);

        public int DataBits
        {
            get => _databits;
            set
            {
                if (value < MIN_DATABITS || value > MAX_DATABITS)
                    throw new ArgumentOutOfRangeException(nameof(DataBits));

                (IsOpen ? ref _internalserialstream.DataBits : ref _databits) = value;
            }
        }

        public bool DiscardNull
        {
            get => _discardnull;
            set => (IsOpen ? ref _internalserialstream.DiscardNull : ref _discardnull) = value;
        }

        public bool DsrHolding => AssertOpen(_internalserialstream.DsrHolding);

        public bool DtrEnable
        {
            get
            {
                if (IsOpen)
                    _dtrenable = _internalserialstream.DtrEnable;

                return _dtrenable;
            }
            set => (IsOpen ? ref _internalserialstream.DtrEnable : ref _dtrenable) = value;
        }

        public Encoding Encoding
        {
            get => _encoding;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(Encoding));

                if (!(value is ASCIIEncoding || value is UTF8Encoding || value is UnicodeEncoding || value is UTF32Encoding || ((value.CodePage < 50000 || value.CodePage == 54936) && value.GetType().Assembly == typeof(string).Assembly)))
                    throw new ArgumentException("Invalid encoding", nameof(value));

                _encoding = value;
                _decoder = _encoding.GetDecoder();
                _maxbytecountforsinglechar = _encoding.GetMaxByteCount(1);
                _singlecharbuffer = null;
            }
        }

        public Handshake Handshake
        {
            get => _handshake;
            set
            {
                if (value < Handshake.None || value > Handshake.RequestToSendXOnXOff)
                    throw new ArgumentOutOfRangeException(nameof(Handshake));

                (IsOpen ? ref _internalserialstream.Handshake : ref _handshake) = value;
            }
        }

        public bool IsOpen => _internalserialstream?.IsOpen ?? false;

        public string NewLine
        {
            get => _newline;
            set => _newline = (value?.Length ?? 0) == 0 ? throw new ArgumentException("Invalid argument", nameof(NewLine)) : value;
        }

        public Parity Parity
        {
            get => _parity;
            set
            {
                if (value < Parity.None || value > Parity.Space)
                    throw new ArgumentOutOfRangeException(nameof(Parity));

                (IsOpen ? ref _internalserialstream.Parity : ref _parity) = value;
            }
        }

        public byte ParityReplace
        {
            get => _parityreplace;
            set => (IsOpen ? ref _internalserialstream.ParityReplace : ref _parityreplace) = value;
        }

        public string PortName
        {
            get => _name;
            set
            {
                if ((value?.Length ?? 0) == 0)
                    throw new ArgumentException("Port name cannot be empty", nameof(PortName));
                else if (IsOpen)
                    throw new InvalidOperationException("Port name cannot be set when open");
                else
                    _name = value;
            }
        }

        public int ReadBufferSize
        {
            get => _readbuffersize;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(ReadBufferSize));
                else if (IsOpen)
                    throw new InvalidOperationException("Port read buffer size cannot be set when open");
                else
                    _readbuffersize = value;
            }
        }

        public int ReadTimeout
        {
            get => _rimeout;
            set
            {
                if ((value < 0) && (value != InfiniteTimeout))
                    throw new ArgumentOutOfRangeException(nameof(ReadTimeout));
                else
                    (IsOpen ? ref _internalserialstream.ReadTimeout : ref _rimeout) = value;
            }
        }

        public int ReceivedBytesThreshold
        {
            get => _receivedbytesthreshold;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(ReceivedBytesThreshold));

                _receivedbytesthreshold = value;

                if (IsOpen)
                {
                    SerialDataReceivedEventArgs args = new SerialDataReceivedEventArgs(SerialData.Chars);
                    CatchReceivedEvents(this, args);
                }
            }
        }

        public bool RtsEnable
        {
            get
            {
                if (IsOpen)
                    _rtsenable = _internalserialstream.RtsEnable;

                return _rtsenable;
            }
            set => (IsOpen ? ref _internalserialstream.RtsEnable : ref _rtsenable) = value;
        }

        public StopBits StopBits
        {
            get => _stopbits;
            set
            {
                if (value < StopBits.One || value > StopBits.OnePointFive)
                    throw new ArgumentOutOfRangeException(nameof(StopBits));

                (IsOpen ? ref _internalserialstream.StopBits : ref _stopbits) = value;
            }
        }

        public int WriteBufferSize
        {
            get => _writebuffersize;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(WriteBufferSize));
                else if (IsOpen)
                    throw new InvalidOperationException("Port write buffer size cannot be set when open");
                else
                    _writebuffersize = value;
            }
        }

        public int WriteTimeout
        {
            get => _wtimeout;
            set
            {
                if ((value < 0) && (value != InfiniteTimeout))
                    throw new ArgumentOutOfRangeException(nameof(_wtimeout));
                else
                    (IsOpen ? ref _internalserialstream.WriteTimeout : ref _wtimeout) = value;
            }
        }


        public SerialPort()
        {
        }

        public SerialPort(string portName)
            : this(portName, 9600, defaultParity, defaultDataBits, defaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate)
            : this(portName, baudRate, defaultParity, defaultDataBits, defaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate, Parity parity)
            : this(portName, baudRate, parity, defaultDataBits, defaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate, Parity parity, int dataBits)
            : this(portName, baudRate, parity, dataBits, defaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits) =>
            (PortName, BaudRate, Parity, DataBits, StopBits) = (portName, baudRate, parity, dataBits, stopBits);

        private T AssertOpen<T>(T f) => IsOpen ? f : throw new InvalidOperationException();

        private T AssertOpen<T>(Func<T> f) => IsOpen ? f() : throw new InvalidOperationException();

        private void AssertOpen(Action f)
        {
            if (IsOpen)
                f();
            else
                throw new InvalidOperationException();
        }

        public void Close() => Dispose();

        protected override void Dispose(bool disposing)
        {
            if (disposing && IsOpen)
            {
                _internalserialstream?.Flush();
                _internalserialstream?.Close();
                _internalserialstream = null;
            }

            base.Dispose(disposing);
        }

        public void DiscardInBuffer() => AssertOpen(() =>
        {
            _internalserialstream.DiscardInBuffer();
            _readpos = _readlen = 0;
        });

        public void DiscardOutBuffer() => AssertOpen(() => _internalserialstream.DiscardOutBuffer());

        public static string[] GetPortNames() => new string[0];

#if NYT
        private static unsafe char[] CallQueryDosDevice(string name, out int dataSize) {
            char[] buffer = new char[1024];
 
            fixed (char *bufferPtr = buffer) {
                dataSize =  UnsafeNativeMethods.QueryDosDevice(name, buffer, buffer.Length);
                while (dataSize <= 0) {
                    int lastError = Marshal.GetLastWin32Error();
                    if (lastError == NativeMethods.ERROR_INSUFFICIENT_BUFFER || lastError == NativeMethods.ERROR_MORE_DATA) {
                        buffer = new char[buffer.Length * 2];
                        dataSize = UnsafeNativeMethods.QueryDosDevice(null, buffer, buffer.Length);
                    }
                    else {
                        throw new Win32Exception();
                    }
                }
            }
            return buffer;
        }
#endif

        public void Open()
        {
            if (IsOpen)
                throw new InvalidOperationException("Port is already open");

            _internalserialstream = new SerialStream(_name, _rate, _parity, _databits, _stopbits, _rimeout,
                _wtimeout, _handshake, _dtrenable, _rtsenable, _discardnull, _parityreplace);

            _internalserialstream.SetBufferSizes(_readbuffersize, _writebuffersize);
            _internalserialstream.ErrorReceived += new EventHandler(CatchErrorEvents);
            _internalserialstream.PinChanged += new EventHandler(CatchPinChangedEvents);
            _internalserialstream.DataReceived += new EventHandler(CatchReceivedEvents);
        }

        // Read Design pattern:
        //  : ReadChar() returns the first available full char if found before, throws TimeoutExc if timeout.
        //  : Read(byte[] buffer..., int count) returns all data available before read timeout expires up to *count* bytes
        //  : Read(char[] buffer..., int count) returns all data available before read timeout expires up to *count* chars.
        //  :                                   Note, this does not return "half-characters".
        //  : ReadByte() is the binary analogue of the first one.
        //  : ReadLine(): returns null string on timeout, saves received data in buffer
        //  : ReadAvailable(): returns all full characters which are IMMEDIATELY available.

        public int Read(byte[] buffer, int offset, int count) => AssertOpen(() =>
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
                throw new ArgumentException("Invalid offset length");

            int bytesReadToBuffer = 0;

            if (CachedBytesToRead >= 1)
            {
                bytesReadToBuffer = Math.Min(CachedBytesToRead, count);

                Buffer.BlockCopy(_inbuffer, _readpos, buffer, offset, bytesReadToBuffer);

                _readpos += bytesReadToBuffer;

                if (bytesReadToBuffer == count)
                {
                    if (_readpos == _readlen)
                        _readpos = _readlen = 0;

                    return count;
                }

                if (BytesToRead == 0)
                    return bytesReadToBuffer;
            }

            _readlen = _readpos = 0;

            int bytesLeftToRead = count - bytesReadToBuffer;

            bytesReadToBuffer += _internalserialstream.Read(buffer, offset + bytesReadToBuffer, bytesLeftToRead);

            _decoder.Reset();

            return bytesReadToBuffer;
        });

        public int ReadChar() => AssertOpen(() => ReadOneChar(_rimeout));

        private int ReadOneChar(int timeout)
        {
            int nextByte;
            int timeUsed = 0;

            if (_decoder.GetCharCount(_inbuffer, _readpos, CachedBytesToRead) != 0)
            {
                int beginReadPos = _readpos;

                do
                    _readpos++;
                while (_decoder.GetCharCount(_inbuffer, beginReadPos, _readpos - beginReadPos) < 1);

                try
                {
                    _decoder.GetChars(_inbuffer, beginReadPos, _readpos - beginReadPos, _onechar, 0);
                }
                catch
                {
                    _readpos = beginReadPos;

                    throw;
                }

                return _onechar[0];
            }
            else
            {
                if (timeout == 0)
                {
                    int bytesInStream = _internalserialstream.BytesToRead;

                    if (bytesInStream == 0)
                        bytesInStream = 1;

                    MaybeResizeBuffer(bytesInStream);
                    _readlen += _internalserialstream.Read(_inbuffer, _readlen, bytesInStream);

                    return ReadBufferIntoChars(_onechar, 0, 1, false) != 0 ? _onechar[0] : throw new TimeoutException();
                }

                int startTicks = Environment.TickCount;

                do
                {
                    if (timeout == InfiniteTimeout)
                        nextByte = _internalserialstream.ReadByte(InfiniteTimeout);
                    else if (timeout - timeUsed >= 0)
                    {
                        nextByte = _internalserialstream.ReadByte(timeout - timeUsed);
                        timeUsed = Environment.TickCount - startTicks;
                    }
                    else
                        throw new TimeoutException();

                    MaybeResizeBuffer(1);
                    _inbuffer[_readlen++] = (byte)nextByte;
                }
                while (_decoder.GetCharCount(_inbuffer, _readpos, _readlen - _readpos) < 1);
            }

            _decoder.GetChars(_inbuffer, _readpos, _readlen - _readpos, _onechar, 0);
            _readlen = _readpos = 0;

            return _onechar[0];
        }

        public int Read(char[] buffer, int offset, int count) => AssertOpen(() =>
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (buffer.Length - offset < count)
                throw new ArgumentException(SR.GetString(SR.Argument_InvalidOffLen));
            else
                return InternalRead(buffer, offset, count, _rimeout, false);
        });

        private int InternalRead(char[] buffer, int offset, int count, int timeout, bool countMultiByteCharsAsOne)
        {
            if (count == 0)
                return 0;

            int startTicks = Environment.TickCount;
            int bytesInStream = _internalserialstream.BytesToRead;

            MaybeResizeBuffer(bytesInStream);

            _readlen += _internalserialstream.Read(_inbuffer, _readlen, bytesInStream);

            if (_decoder.GetCharCount(_inbuffer, _readpos, CachedBytesToRead) > 0)
                return ReadBufferIntoChars(buffer, offset, count, countMultiByteCharsAsOne);
            else if (timeout == 0)
                throw new TimeoutException();

            int justRead;
            int maxReadSize = Encoding.GetMaxByteCount(count);

            do
            {
                MaybeResizeBuffer(maxReadSize);

                _readlen += _internalserialstream.Read(_inbuffer, _readlen, maxReadSize);
                justRead = ReadBufferIntoChars(buffer, offset, count, countMultiByteCharsAsOne);

                if (justRead > 0)
                    return justRead;
            }
            while (timeout == InfiniteTimeout || (timeout - GetElapsedTime(Environment.TickCount, startTicks) > 0));

            throw new TimeoutException();
        }

        private int ReadBufferIntoChars(char[] buffer, int offset, int count, bool countMultiByteCharsAsOne)
        {
            int bytesToRead = Math.Min(count, CachedBytesToRead);
            DecoderReplacementFallback fallback = _encoding.DecoderFallback as DecoderReplacementFallback;

            if (_encoding.IsSingleByte && _encoding.GetMaxCharCount(bytesToRead) == bytesToRead && fallback?.MaxCharCount == 1)
            {
                _decoder.GetChars(_inbuffer, _readpos, bytesToRead, buffer, offset);
                _readpos += bytesToRead;

                if (_readpos == _readlen) _readpos = _readlen = 0;
                    return bytesToRead;
            }
            else
            {
                int totalBytesExamined = 0; // total number of Bytes in inBuffer we've looked at
                int totalCharsFound = 0;     // total number of chars we've found in inBuffer, totalCharsFound <= totalBytesExamined
                int currentBytesToExamine; // the number of additional bytes to examine for characters
                int currentCharsFound; // the number of additional chars found after examining currentBytesToExamine extra bytes
                int lastFullCharPos = _readpos; // first index AFTER last full char read, capped at ReadLen.

                do
                {
                    currentBytesToExamine = Math.Min(count - totalCharsFound, _readlen - _readpos - totalBytesExamined);

                    if (currentBytesToExamine <= 0)
                        break;

                    totalBytesExamined += currentBytesToExamine;
                    currentBytesToExamine = _readpos + totalBytesExamined - lastFullCharPos;
                    currentCharsFound = _decoder.GetCharCount(_inbuffer, lastFullCharPos, currentBytesToExamine);

                    if (currentCharsFound > 0)
                    {
                        if ((totalCharsFound + currentCharsFound) > count && !countMultiByteCharsAsOne)
                            break;

                        int foundCharsByteLength = currentBytesToExamine;

                        do
                            foundCharsByteLength--;
                        while (_decoder.GetCharCount(_inbuffer, lastFullCharPos, foundCharsByteLength) == currentCharsFound);

                        _decoder.GetChars(_inbuffer, lastFullCharPos, foundCharsByteLength + 1, buffer, offset + totalCharsFound);
                        lastFullCharPos = lastFullCharPos + foundCharsByteLength + 1;
                    }

                    totalCharsFound += currentCharsFound;
                }
                while ((totalCharsFound < count) && (totalBytesExamined < CachedBytesToRead));

                _readpos = lastFullCharPos;

                if (_readpos == _readlen)
                    _readpos = _readlen = 0;

                return totalCharsFound;
            }
        }

        public int ReadByte() => AssertOpen(() =>
        {
            if (_readlen != _readpos)
                return _inbuffer[_readpos++];
            else
            {
                _decoder.Reset();

                return _internalserialstream.ReadByte();
            }
        });

        public string ReadExisting() => AssertOpen(() =>
        {
            byte[] bytesReceived = new byte[BytesToRead];

            if (_readpos < _readlen)
                Buffer.BlockCopy(_inbuffer, _readpos, bytesReceived, 0, CachedBytesToRead);

            _internalserialstream.Read(bytesReceived, CachedBytesToRead, bytesReceived.Length - (CachedBytesToRead));

            Decoder localDecoder = Encoding.GetDecoder();
            int numCharsReceived = localDecoder.GetCharCount(bytesReceived, 0, bytesReceived.Length);
            int lastFullCharIndex = bytesReceived.Length;

            if (numCharsReceived == 0)
            {
                Buffer.BlockCopy(bytesReceived, 0, _inbuffer, 0, bytesReceived.Length);

                _readpos = 0;
                _readlen = bytesReceived.Length;

                return "";
            }

            do
            {
                localDecoder.Reset();
                lastFullCharIndex--;
            }
            while (localDecoder.GetCharCount(bytesReceived, 0, lastFullCharIndex) == numCharsReceived);

            _readpos = 0;
            _readlen = bytesReceived.Length - (lastFullCharIndex + 1);

            Buffer.BlockCopy(bytesReceived, lastFullCharIndex + 1, _inbuffer, 0, bytesReceived.Length - (lastFullCharIndex + 1));

            return Encoding.GetString(bytesReceived, 0, lastFullCharIndex + 1);
        });

        public string ReadLine() => ReadTo(NewLine);

        public string ReadTo(string value) => AssertOpen(() =>
        {
            if ((value ?? "").Length == 0)
                throw new ArgumentException("Value must not be empty");

            int startTicks = Environment.TickCount;
            int numCharsRead;
            int timeUsed = 0;
            int timeNow;
            StringBuilder currentLine = new StringBuilder();
            char lastValueChar = value[value.Length - 1];
            int bytesInStream = _internalserialstream.BytesToRead;

            MaybeResizeBuffer(bytesInStream);

            _readlen += _internalserialstream.Read(_inbuffer, _readlen, bytesInStream);

            int beginReadPos = _readpos;

            if (_singlecharbuffer == null)
                _singlecharbuffer = new char[_maxbytecountforsinglechar];

            try
            {
                while (true)
                {
                    if (_rimeout == InfiniteTimeout)
                        numCharsRead = InternalRead(_singlecharbuffer, 0, 1, _rimeout, true);
                    else if (_rimeout - timeUsed >= 0)
                    {
                        timeNow = Environment.TickCount;
                        numCharsRead = InternalRead(_singlecharbuffer, 0, 1, _rimeout - timeUsed, true);
                        timeUsed += Environment.TickCount - timeNow;
                    }
                    else
                        throw new TimeoutException();

                    currentLine.Append(_singlecharbuffer, 0, numCharsRead);

                    if (lastValueChar == _singlecharbuffer[numCharsRead - 1] && (currentLine.Length >= value.Length))
                    {
                        bool found = true;

                        for (int i = 2; i <= value.Length; i++)
                            if (value[value.Length - i] != currentLine[currentLine.Length - i])
                            {
                                found = false;

                                break;
                            }

                        if (found)
                        {
                            // we found the search string.  Exclude it from the return string.
                            string ret = currentLine.ToString(0, currentLine.Length - value.Length);

                            if (_readpos == _readlen)
                                _readpos = _readlen = 0;

                            return ret;
                        }
                    }
                }
            }
            catch
            {
                byte[] readBuffer = _encoding.GetBytes(currentLine.ToString());

                if (readBuffer.Length > 0)
                {
                    int bytesToSave = CachedBytesToRead;
                    byte[] savBuffer = new byte[bytesToSave];

                    if (bytesToSave > 0)
                        Buffer.BlockCopy(_inbuffer, _readpos, savBuffer, 0, bytesToSave);

                    _readpos = 0;
                    _readlen = 0;

                    MaybeResizeBuffer(readBuffer.Length + bytesToSave);

                    Buffer.BlockCopy(readBuffer, 0, _inbuffer, _readlen, readBuffer.Length);

                    _readlen += readBuffer.Length;

                    if (bytesToSave > 0)
                    {
                        Buffer.BlockCopy(savBuffer, 0, _inbuffer, _readlen, bytesToSave);

                        _readlen += bytesToSave;
                    }
                }

                throw;
            }
        });

        public void Write(string text) => AssertOpen(() =>
        {
            text = text ?? "";

            if (text.Length > 0)
            {
                byte[] bytesToWrite = _encoding.GetBytes(text);

                _internalserialstream.Write(bytesToWrite, 0, bytesToWrite.Length, _wtimeout);
            }
        });

        public void Write(char[] buffer, int offset, int count) => AssertOpen(() =>
        {
            if (AssertParams(buffer, offset, count))
            {
                byte[] byteArray = Encoding.GetBytes(buffer, offset, count);

                Write(byteArray, 0, byteArray.Length);
            }
        });

        public void Write(byte[] buffer, int offset, int count)
        {
            if (AssertParams(buffer, offset, count))
                _internalserialstream.Write(buffer, offset, count, _wtimeout);
        }

        public void WriteLine(string text) => Write(text + NewLine);

        private bool AssertParams<T>(T[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (buffer.Length - offset < count)
                throw new ArgumentException("Invalid offset length");
            else if (buffer.Length == 0)
                return false;

            return true;
        }

        private void CatchErrorEvents(object src, SerialError e)
        {
            EventHandler<SerialError> eventHandler = ErrorReceived;
            SerialStream stream = _internalserialstream;

            if ((eventHandler != null) && (stream != null))
                lock (stream)
                    if (stream.IsOpen)
                        eventHandler(this, e);
        }

        private void CatchPinChangedEvents(object src, SerialPinChange e)
        {
            EventHandler<SerialPinChange> eventHandler = PinChanged;
            SerialStream stream = _internalserialstream;

            if ((eventHandler != null) && (stream != null))
                lock (stream)
                    if (stream.IsOpen)
                        eventHandler(this, e);
        }

        private void CatchReceivedEvents(object src, SerialData e)
        {
            EventHandler<SerialData> eventHandler = DataReceived;
            SerialStream stream = _internalserialstream;
            bool raise = false;

            if ((eventHandler != null) && (stream != null))
                lock (stream)
                    try
                    {
                        raise = stream.IsOpen && (SerialData.Eof == e.EventType || BytesToRead >= _receivedbytesthreshold);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (raise)
                            eventHandler(this, e);
                    }
        }

        private void CompactBuffer()
        {
            Buffer.BlockCopy(_inbuffer, _readpos, _inbuffer, 0, CachedBytesToRead);

            _readlen = CachedBytesToRead;
            _readpos = 0;
        }

        private void MaybeResizeBuffer(int additionalByteLength)
        {
            if (additionalByteLength + _readlen <= _inbuffer.Length)
                return;
            else if (CachedBytesToRead + additionalByteLength <= _inbuffer.Length / 2)
                CompactBuffer();
            else
            {
                int newLength = Math.Max(CachedBytesToRead + additionalByteLength, _inbuffer.Length * 2);
                byte[] newBuffer = new byte[newLength];

                Buffer.BlockCopy(_inbuffer, _readpos, newBuffer, 0, CachedBytesToRead);

                _readlen = CachedBytesToRead;
                _readpos = 0;
                _inbuffer = newBuffer;
            }
        }

        private static int GetElapsedTime(int currentTickCount, int startTickCount)
        {
            int elapsed = unchecked(currentTickCount - startTickCount);

            return elapsed >= 0 ? elapsed : int.MaxValue;
        }
    }

    internal sealed class SerialStream
        : Stream
    {
        private const int EVT_ERROR = (int)(SerialError.Frame | SerialError.Overrun | SerialError.RXOver | SerialError.RXParity | SerialError.TXFull);
        private const int EVT_RECV = (int)(SerialData.Chars | SerialData.Eof);
        private const int EVT_PINCH = (int)(SerialPinChange.Break | SerialPinChange.CDChanged | SerialPinChange.CtsChanged | SerialPinChange.Ring | SerialPinChange.DsrChanged);
        private const int INF_TIMEOUT = -2;

        private DCB dcb;
        private COMSTAT comStat;
        private COMMPROP commProp;
        private COMMTIMEOUTS commTimeouts;

        private string _name;
        private byte _parityreplace = (byte)'?';
        private bool _isasync = true;
        private bool _rtsenable;
        private bool _inbreak;
        private Handshake _handshake;


        internal SafeFileHandle _handle;
        internal EventLoopRunner eventRunner;

        private byte[] tempBuf;                 // used to avoid multiple array allocations in ReadByte()

        // called whenever any async i/o operation completes.
        private unsafe static readonly IOCompletionCallback IOCallback = new IOCompletionCallback(SerialStream.AsyncFSCallback);

        internal event EventHandler<SerialPinChange> PinChanged;
        internal event EventHandler<SerialError> ErrorReceived;
        internal event EventHandler<SerialData> DataReceived;


        public override bool CanRead => _handle != null;

        public override bool CanSeek => false;

        public override bool CanTimeout => _handle != null;

        public override bool CanWrite => _handle != null;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        internal int BaudRate
        {
            get => (int)dcb.BaudRate;
            set
            {
                if (value <= 0 || (value > commProp.dwMaxBaud && commProp.dwMaxBaud > 0))
                    throw new ArgumentOutOfRangeException(nameof(BaudRate));

                if (value != dcb.BaudRate)
                {
                    int baudRateOld = (int)dcb.BaudRate;

                    dcb.BaudRate = (uint)value;

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        dcb.BaudRate = (uint)baudRateOld;
                        InternalResources.WinIOError();
                    }
                }
            }
        }

        public bool BreakState
        {
            get => _inbreak;
            set
            {
                if (value)
                {
                    if (!UnsafeNativeMethods.SetCommBreak(_handle))
                        InternalResources.WinIOError();
                }
                else if (!UnsafeNativeMethods.ClearCommBreak(_handle))
                    InternalResources.WinIOError();

                _inbreak = value;
            }
        }

        internal int DataBits
        {
            get => dcb.ByteSize;
            set
            {
                if (value != dcb.ByteSize)
                {
                    byte byteSizeOld = dcb.ByteSize;

                    dcb.ByteSize = (byte)value;

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        dcb.ByteSize = byteSizeOld;
                        InternalResources.WinIOError();
                    }
                }
            }
        }


        internal bool DiscardNull
        {
            get => GetDcbFlag(NativeMethods.FNULL) == 1;
            set
            {
                int fNullFlag = GetDcbFlag(NativeMethods.FNULL);

                if ((value ? 0 : 1) == fNullFlag)
                {
                    int fNullOld = fNullFlag;

                    SetDcbFlag(NativeMethods.FNULL, value ? 1 : 0);

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        SetDcbFlag(NativeMethods.FNULL, fNullOld);
                        InternalResources.WinIOError();
                    }
                }
            }
        }

        internal bool DtrEnable
        {
            get => GetDcbFlag(NativeMethods.FDTRCONTROL) == NativeMethods.DTR_CONTROL_ENABLE;
            set
            {
                int fDtrControlOld = GetDcbFlag(NativeMethods.FDTRCONTROL);

                SetDcbFlag(NativeMethods.FDTRCONTROL, value ? NativeMethods.DTR_CONTROL_ENABLE : NativeMethods.DTR_CONTROL_DISABLE);

                if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                {
                    SetDcbFlag(NativeMethods.FDTRCONTROL, fDtrControlOld);
                    InternalResources.WinIOError();
                }

                if (!UnsafeNativeMethods.EscapeCommFunction(_handle, value ? NativeMethods.SETDTR : NativeMethods.CLRDTR))
                    InternalResources.WinIOError();
            }
        }

        internal Handshake Handshake
        {
            get => _handshake;
            set
            {
                if (value != _handshake)
                {
                    Handshake handshakeOld = _handshake;
                    int fInOutXOld = GetDcbFlag(NativeMethods.FINX);
                    int fOutxCtsFlowOld = GetDcbFlag(NativeMethods.FOUTXCTSFLOW);
                    int fRtsControlOld = GetDcbFlag(NativeMethods.FRTSCONTROL);

                    _handshake = value;

                    int fInXOutXFlag = (_handshake == Handshake.XOnXOff || _handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0;

                    SetDcbFlag(NativeMethods.FINX, fInXOutXFlag);
                    SetDcbFlag(NativeMethods.FOUTX, fInXOutXFlag);
                    SetDcbFlag(NativeMethods.FOUTXCTSFLOW, (_handshake == Handshake.RequestToSend || _handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);

                    if ((_handshake == Handshake.RequestToSend || _handshake == Handshake.RequestToSendXOnXOff))
                        SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_HANDSHAKE);
                    else if (_rtsenable)
                        SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_ENABLE);
                    else
                        SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_DISABLE);

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        _handshake = handshakeOld;

                        SetDcbFlag(NativeMethods.FINX, fInOutXOld);
                        SetDcbFlag(NativeMethods.FOUTX, fInOutXOld);
                        SetDcbFlag(NativeMethods.FOUTXCTSFLOW, fOutxCtsFlowOld);
                        SetDcbFlag(NativeMethods.FRTSCONTROL, fRtsControlOld);

                        InternalResources.WinIOError();
                    }
                }
            }
        }

        internal bool IsOpen => _handle != null && !eventRunner.ShutdownLoop;

        internal Parity Parity
        {
            get => (Parity)dcb.Parity;
            set
            {
                if ((byte)value != dcb.Parity)
                {
                    byte parityOld = dcb.Parity;
                    int fParityOld = GetDcbFlag(NativeMethods.FPARITY);
                    byte ErrorCharOld = dcb.ErrorChar;
                    int fErrorCharOld = GetDcbFlag(NativeMethods.FERRORCHAR);

                    dcb.Parity = (byte)value;

                    int parityFlag = (dcb.Parity == (byte)Parity.None) ? 0 : 1;

                    SetDcbFlag(NativeMethods.FPARITY, parityFlag);

                    if (parityFlag == 1)
                    {
                        SetDcbFlag(NativeMethods.FERRORCHAR, (_parityreplace != '\0') ? 1 : 0);
                        dcb.ErrorChar = _parityreplace;
                    }
                    else
                    {
                        SetDcbFlag(NativeMethods.FERRORCHAR, 0);
                        dcb.ErrorChar = (byte)'\0';
                    }
                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        dcb.Parity = parityOld;
                        SetDcbFlag(NativeMethods.FPARITY, fParityOld);

                        dcb.ErrorChar = ErrorCharOld;
                        SetDcbFlag(NativeMethods.FERRORCHAR, fErrorCharOld);

                        InternalResources.WinIOError();
                    }
                }
            }
        }

        internal byte ParityReplace
        {
            get => parityReplace;
            set
            {
                if (value != _parityreplace)
                {
                    byte parityReplaceOld = _parityreplace;
                    byte errorCharOld = dcb.ErrorChar;
                    int fErrorCharOld = GetDcbFlag(NativeMethods.FERRORCHAR);

                    _parityreplace = value;

                    if (GetDcbFlag(NativeMethods.FPARITY) == 1)
                    {
                        SetDcbFlag(NativeMethods.FERRORCHAR, (_parityreplace != '\0') ? 1 : 0);
                        dcb.ErrorChar = _parityreplace;
                    }
                    else
                    {
                        SetDcbFlag(NativeMethods.FERRORCHAR, 0);
                        dcb.ErrorChar = (byte)'\0';
                    }

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        _parityreplace = parityReplaceOld;
                        SetDcbFlag(NativeMethods.FERRORCHAR, fErrorCharOld);
                        dcb.ErrorChar = errorCharOld;
                        InternalResources.WinIOError();
                    }
                }
            }
        }

        // Timeouts are considered to be TOTAL time for the Read/Write operation and to be in milliseconds.
        // Timeouts are translated into DCB structure as follows:
        // Desired timeout      =>  ReadTotalTimeoutConstant    ReadTotalTimeoutMultiplier  ReadIntervalTimeout
        //  0                                   0                           0               MAXDWORD
        //  0 < n < infinity                    n                       MAXDWORD            MAXDWORD
        // infinity                             infiniteTimeoutConst    MAXDWORD            MAXDWORD
        //
        // rationale for "infinity": There does not exist in the COMMTIMEOUTS structure a way to
        // *wait indefinitely for any byte, return when found*.  Instead, if we set ReadTimeout
        // to infinity, SerialStream's EndRead loops if infiniteTimeoutConst mills have elapsed
        // without a byte received.  Note that this is approximately 24 days, so essentially
        // most practical purposes effectively equate 24 days with an infinite amount of time
        // on a serial port connection.
        public override int ReadTimeout
        {
            get => commTimeouts.ReadTotalTimeoutConstant == INF_TIMEOUT ? SerialPort.InfiniteTimeout : commTimeouts.ReadTotalTimeoutConstant;
            set => AssertHandle(() =>
            {
                if (value < 0 && value != SerialPort.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(ReadTimeout));

                int oldReadConstant = commTimeouts.ReadTotalTimeoutConstant;
                int oldReadInterval = commTimeouts.ReadIntervalTimeout;
                int oldReadMultipler = commTimeouts.ReadTotalTimeoutMultiplier;

                if (value == 0)
                {
                    commTimeouts.ReadTotalTimeoutConstant = 0;
                    commTimeouts.ReadTotalTimeoutMultiplier = 0;
                    commTimeouts.ReadIntervalTimeout = NativeMethods.MAXDWORD;
                }
                else if (value == SerialPort.InfiniteTimeout)
                {
                    commTimeouts.ReadTotalTimeoutConstant = INF_TIMEOUT;
                    commTimeouts.ReadTotalTimeoutMultiplier = NativeMethods.MAXDWORD;
                    commTimeouts.ReadIntervalTimeout = NativeMethods.MAXDWORD;
                }
                else
                {
                    commTimeouts.ReadTotalTimeoutConstant = value;
                    commTimeouts.ReadTotalTimeoutMultiplier = NativeMethods.MAXDWORD;
                    commTimeouts.ReadIntervalTimeout = NativeMethods.MAXDWORD;
                }

                if (!UnsafeNativeMethods.SetCommTimeouts(_handle, ref commTimeouts))
                {
                    commTimeouts.ReadTotalTimeoutConstant = oldReadConstant;
                    commTimeouts.ReadTotalTimeoutMultiplier = oldReadMultipler;
                    commTimeouts.ReadIntervalTimeout = oldReadInterval;

                    InternalResources.WinIOError();
                }
            });
        }

        internal bool RtsEnable
        {
            get
            {
                int fRtsControl = GetDcbFlag(NativeMethods.FRTSCONTROL);

                if (fRtsControl == NativeMethods.RTS_CONTROL_HANDSHAKE)
                    throw new InvalidOperationException();

                return fRtsControl == NativeMethods.RTS_CONTROL_ENABLE;
            }
            set
            {
                if ((_handshake == Handshake.RequestToSend) || (_handshake == Handshake.RequestToSendXOnXOff))
                    throw new InvalidOperationException();

                if (value != _rtsenable)
                {
                    int fRtsControlOld = GetDcbFlag(NativeMethods.FRTSCONTROL);

                    _rtsenable = value;

                    if (value)
                        SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_ENABLE);
                    else
                        SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_DISABLE);

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        SetDcbFlag(NativeMethods.FRTSCONTROL, fRtsControlOld);

                        _rtsenable = !_rtsenable;

                        InternalResources.WinIOError();
                    }

                    if (!UnsafeNativeMethods.EscapeCommFunction(_handle, value ? NativeMethods.SETRTS : NativeMethods.CLRRTS))
                        InternalResources.WinIOError();
                }
            }
        }

        internal StopBits StopBits
        {
            get
            {
                switch(dcb.StopBits)
                {
                    case NativeMethods.ONE5STOPBITS:
                        return StopBits.OnePointFive;
                    case NativeMethods.TWOSTOPBITS:
                        return StopBits.Two;
                    default:
                        return StopBits.One;
                }
            }
            set
            {
                byte nativeValue = (byte)NativeMethods.TWOSTOPBITS;

                if (value == StopBits.One)
                    nativeValue = (byte)NativeMethods.ONESTOPBIT;
                else if (value == StopBits.OnePointFive)
                    nativeValue = (byte)NativeMethods.ONE5STOPBITS;

                if (nativeValue != dcb.StopBits)
                {
                    byte stopBitsOld = dcb.StopBits;

                    dcb.StopBits = nativeValue;

                    if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                    {
                        dcb.StopBits = stopBitsOld;
                        InternalResources.WinIOError();
                    }
                }
            }
        }

        public override int WriteTimeout
        {
            get => commTimeouts.WriteTotalTimeoutConstant == INF_TIMEOUT ? SerialPort.InfiniteTimeout : commTimeouts.WriteTotalTimeoutConstant;
            set => AssertHandle(() =>
            {
                if (value <= 0 && value != SerialPort.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(WriteTimeout));

                int oldWriteConstant = commTimeouts.WriteTotalTimeoutConstant;

                commTimeouts.WriteTotalTimeoutConstant = ((value == SerialPort.InfiniteTimeout) ? 0 : value);

                if (!UnsafeNativeMethods.SetCommTimeouts(_handle, ref commTimeouts))
                {
                    commTimeouts.WriteTotalTimeoutConstant = oldWriteConstant;
                    InternalResources.WinIOError();
                }
            });
        }

        internal bool CDHolding
        {
            get
            {
                int pinStatus = 0;

                if (!UnsafeNativeMethods.GetCommModemStatus(_handle, ref pinStatus))
                    InternalResources.WinIOError();

                return (NativeMethods.MS_RLSD_ON & pinStatus) != 0;
            }
        }

        internal bool CtsHolding
        {
            get
            {
                int pinStatus = 0;

                if (!UnsafeNativeMethods.GetCommModemStatus(_handle, ref pinStatus))
                    InternalResources.WinIOError();

                return (NativeMethods.MS_CTS_ON & pinStatus) != 0;
            }
        }

        internal bool DsrHolding
        {
            get
            {
                int pinStatus = 0;

                if (!UnsafeNativeMethods.GetCommModemStatus(_handle, ref pinStatus))
                    InternalResources.WinIOError();

                return (NativeMethods.MS_DSR_ON & pinStatus) != 0;
            }
        }

        internal int BytesToRead
        {
            get
            {
                int errorCode = 0;

                if (!UnsafeNativeMethods.ClearCommError(_handle, ref errorCode, ref comStat))
                    InternalResources.WinIOError();

                return (int)comStat.cbInQue;
            }
        }

        internal int BytesToWrite
        {
            get
            {
                int errorCode = 0;

                if (!UnsafeNativeMethods.ClearCommError(_handle, ref errorCode, ref comStat))
                    InternalResources.WinIOError();

                return (int)comStat.cbOutQue;
            }
        }

        internal SerialStream(string name, int bauds, Parity parity, int dataBits, StopBits stopBits, int readTimeout, int writeTimeout, Handshake handshake, bool dtrEnable, bool rtsEnable, bool discardNull, byte parityReplace)
        {
            int flags = UnsafeNativeMethods.FILE_FLAG_OVERLAPPED;

            if (Environment.OSVersion.Platform == PlatformID.Win32Windows)
            {
                flags = UnsafeNativeMethods.FILE_ATTRIBUTE_NORMAL;

                _isasync = false;
            }

            if (name?.StartsWith("COM", StringComparison.OrdinalIgnoreCase) != true)
                throw new ArgumentException("Invalid serial port", nameof(name));

            SafeFileHandle tempHandle = UnsafeNativeMethods.CreateFile($@"\\.\{name}", NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE, 0, IntPtr.Zero, UnsafeNativeMethods.OPEN_EXISTING, flags, IntPtr.Zero);

            if (tempHandle.IsInvalid)
                InternalResources.WinIOError(name);

            try
            {
                int fileType = UnsafeNativeMethods.GetFileType(tempHandle);

                if ((fileType != UnsafeNativeMethods.FILE_TYPE_CHAR) && (fileType != UnsafeNativeMethods.FILE_TYPE_UNKNOWN))
                    throw new ArgumentException("Invalid serial port", nameof(name));

                _handle = tempHandle;

                this._name = name;
                this._handshake = handshake;
                this._parityreplace = parityReplace;

                tempBuf = new byte[1];
                commProp = new UnsafeNativeMethods.COMMPROP();

                int pinStatus = 0;

                if (!UnsafeNativeMethods.GetCommProperties(_handle, ref commProp) || !UnsafeNativeMethods.GetCommModemStatus(_handle, ref pinStatus))
                {
                    int errorCode = Marshal.GetLastWin32Error();

                    if ((errorCode == NativeMethods.ERROR_INVALID_PARAMETER) || (errorCode == NativeMethods.ERROR_INVALID_HANDLE))
                        throw new ArgumentException("Invalid serial port", nameof(name));
                    else
                        InternalResources.WinIOError(errorCode, string.Empty);
                }
                if (commProp.dwMaxBaud != 0 && bauds > commProp.dwMaxBaud)
                    throw new ArgumentOutOfRangeException(nameof(bauds));

                comStat = new UnsafeNativeMethods.COMSTAT();
                dcb = new UnsafeNativeMethods.DCB();

                InitializeDCB(bauds, parity, dataBits, stopBits, discardNull);

                DtrEnable = dtrEnable;
                this._rtsenable = (GetDcbFlag(NativeMethods.FRTSCONTROL) == NativeMethods.RTS_CONTROL_ENABLE);

                if ((handshake != Handshake.RequestToSend) && (handshake != Handshake.RequestToSendXOnXOff))
                    RtsEnable = rtsEnable;

                // NOTE: this logic should match what is in the ReadTimeout property
                if (readTimeout == 0)
                {
                    commTimeouts.ReadTotalTimeoutConstant = 0;
                    commTimeouts.ReadTotalTimeoutMultiplier = 0;
                    commTimeouts.ReadIntervalTimeout = NativeMethods.MAXDWORD;
                }
                else if (readTimeout == SerialPort.InfiniteTimeout)
                {
                    // SetCommTimeouts doesn't like a value of -1 for some reason, so
                    // we'll use -2(infiniteTimeoutConst) to represent infinite. 
                    commTimeouts.ReadTotalTimeoutConstant = INF_TIMEOUT;
                    commTimeouts.ReadTotalTimeoutMultiplier = NativeMethods.MAXDWORD;
                    commTimeouts.ReadIntervalTimeout = NativeMethods.MAXDWORD;
                }
                else
                {
                    commTimeouts.ReadTotalTimeoutConstant = readTimeout;
                    commTimeouts.ReadTotalTimeoutMultiplier = NativeMethods.MAXDWORD;
                    commTimeouts.ReadIntervalTimeout = NativeMethods.MAXDWORD;
                }

                commTimeouts.WriteTotalTimeoutMultiplier = 0;
                commTimeouts.WriteTotalTimeoutConstant = (writeTimeout == SerialPort.InfiniteTimeout) ? 0 : writeTimeout;

                if (!UnsafeNativeMethods.SetCommTimeouts(_handle, ref commTimeouts))
                    InternalResources.WinIOError();

                if (_isasync && !ThreadPool.BindHandle(_handle))
                    throw new IOException();

                UnsafeNativeMethods.SetCommMask(_handle, NativeMethods.ALL_EVENTS);
                eventRunner = new EventLoopRunner(this);

                Thread eventLoopThread = LocalAppContextSwitches.DoNotCatchSerialStreamThreadExceptions
                    ? new Thread(new ThreadStart(eventRunner.WaitForCommEvent))
                    : new Thread(new ThreadStart(eventRunner.SafelyWaitForCommEvent));

                eventLoopThread.IsBackground = true;
                eventLoopThread.Start();
            }
            catch
            {
                tempHandle.Close();
                _handle = null;

                throw;
            }
        }

        ~SerialStream() => Dispose(false);

        protected override void Dispose(bool disposing)
        {
            if (_handle != null && !_handle.IsInvalid)
                try
                {
                    eventRunner.endEventLoop = true;

                    Thread.MemoryBarrier();

                    bool skipSPAccess = false;

                    UnsafeNativeMethods.SetCommMask(_handle, 0);

                    if (!UnsafeNativeMethods.EscapeCommFunction(_handle, NativeMethods.CLRDTR))
                    {
                        int hr = Marshal.GetLastWin32Error();
                        const int ERROR_DEVICE_REMOVED = 1617;

                        if ((hr == NativeMethods.ERROR_ACCESS_DENIED || hr == NativeMethods.ERROR_BAD_COMMAND || hr == ERROR_DEVICE_REMOVED) && !disposing)
                            skipSPAccess = true;
                        else if (disposing)
                            InternalResources.WinIOError();
                    }

                    if (!skipSPAccess && !_handle.IsClosed)
                    {
                        Flush();
                    }

                    eventRunner.waitCommEventWaitHandle.Set();

                    if (!skipSPAccess)
                    {
                        DiscardInBuffer();
                        DiscardOutBuffer();
                    }

                    if (disposing && eventRunner != null)
                    {
                        eventRunner.eventLoopEndedSignal.WaitOne();
                        eventRunner.eventLoopEndedSignal.Close();
                        eventRunner.waitCommEventWaitHandle.Close();
                    }
                }
                finally
                {
                    if (disposing)
                        lock (this)
                        {
                            _handle.Close();
                            _handle = null;
                        }
                    else
                    {
                        _handle.Close();
                        _handle = null;
                    }

                    base.Dispose(disposing);
                }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => AssertHandle(() =>
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (buffer.Length - offset < count)
                throw new ArgumentException();

            int oldtimeout = ReadTimeout;
            IAsyncResult result;

            ReadTimeout = SerialPort.InfiniteTimeout;

            try
            {
                if (!_isasync)
                    result = base.BeginRead(buffer, offset, count, callback, state);
                else
                    result = BeginReadCore(buffer, offset, count, callback, state);
            }
            finally
            {
                ReadTimeout = oldtimeout;
            }

            return result;
        });

        private void AssertHandle(Action f)
        {
            if (_handle is null)
                throw new ObjectDisposedException(null);
            else
                f();
        }

        private T AssertHandle<T>(T f) => _handle is null ? throw new ObjectDisposedException(null) : f;

        private T AssertHandle<T>(Func<T> f) => _handle is null ? throw new ObjectDisposedException(null) : f();

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => AssertHandle(() =>
        {
            if (_inbreak)
                throw new InvalidOperationException();
            else if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (buffer.Length - offset < count)
                throw new ArgumentException("Invalid offset length.");

            int oldtimeout = WriteTimeout;
            IAsyncResult result;

            WriteTimeout = SerialPort.InfiniteTimeout;

            try
            {
                if (!_isasync)
                    result = base.BeginWrite(buffer, offset, count, callback, state);
                else
                    result = BeginWriteCore(buffer, offset, count, callback, state);
            }
            finally
            {
                WriteTimeout = oldtimeout;
            }

            return result;
        });

        internal void DiscardInBuffer()
        {
            if (!UnsafeNativeMethods.PurgeComm(_handle, NativeMethods.PURGE_RXCLEAR | NativeMethods.PURGE_RXABORT))
                InternalResources.WinIOError();
        }

        internal void DiscardOutBuffer()
        {
            if (!UnsafeNativeMethods.PurgeComm(_handle, NativeMethods.PURGE_TXCLEAR | NativeMethods.PURGE_TXABORT))
                InternalResources.WinIOError();
        }

        public unsafe override int EndRead(IAsyncResult asyncResult)
        {
            if (!_isasync)
                return base.EndRead(asyncResult);
            else if (asyncResult is null)
                throw new ArgumentNullException(nameof(asyncResult));

            SerialStreamAsyncResult afsar = asyncResult as SerialStreamAsyncResult;

            if (afsar?._isWrite != false)
                InternalResources.WrongAsyncResult();

            if (1 == Interlocked.CompareExchange(ref afsar._EndXxxCalled, 1, 0))
                InternalResources.EndReadCalledTwice();

            bool failed = false;
            WaitHandle wh = afsar._waitHandle;

            if (wh != null)
                try
                {
                    wh.WaitOne();

                    if ((afsar._numBytes == 0) && (ReadTimeout == SerialPort.InfiniteTimeout) && (afsar._errorCode == 0))
                        failed = true;
                }
                finally
                {
                    wh.Close();
                }

            NativeOverlapped* overlappedPtr = afsar._overlapped;

            if (overlappedPtr != null)
                Overlapped.Free(overlappedPtr);

            if (afsar._errorCode != 0)
                InternalResources.WinIOError(afsar._errorCode, _name);
            else if (failed)
                throw new IOException();
            else
                return afsar._numBytes;
        }

        public unsafe override void EndWrite(IAsyncResult asyncResult)
        {
            if (!_isasync)
                base.EndWrite(asyncResult);
            else
            {
                if (_inbreak)
                    throw new InvalidOperationException();
                else if (asyncResult == null)
                    throw new ArgumentNullException(nameof(asyncResult));

                SerialStreamAsyncResult afsar = asyncResult as SerialStreamAsyncResult;

                if (afsar == null || !afsar._isWrite)
                    InternalResources.WrongAsyncResult();
                if (1 == Interlocked.CompareExchange(ref afsar._EndXxxCalled, 1, 0))
                    InternalResources.EndWriteCalledTwice();

                WaitHandle wh = afsar._waitHandle;

                if (wh != null)
                    try
                    {
                        wh.WaitOne();
                    }
                    finally
                    {
                        wh.Close();
                    }

                NativeOverlapped* overlappedPtr = afsar._overlapped;

                if (overlappedPtr != null)
                    Overlapped.Free(overlappedPtr);

                if (afsar._errorCode != 0)
                    InternalResources.WinIOError(afsar._errorCode, _name);
            }
        }

        public override void Flush() => AssertHandle(() => UnsafeNativeMethods.FlushFileBuffers(_handle));

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer, offset, count, ReadTimeout);

        internal unsafe int Read(byte[] array, int offset, int count, int timeout) => AssertHandle(() =>
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (count == 0)
                return 0;
            else if (array.Length - offset < count)
                throw new ArgumentException("Invalid offset length.");

            int numBytes = 0;

            if (_isasync)
                numBytes = EndRead(BeginReadCore(array, offset, count, null, null));
            else
            {
                numBytes = ReadFileNative(array, offset, count, null, out int hr);

                if (numBytes == -1)
                    InternalResources.WinIOError();
            }

            if (numBytes == 0)
                throw new TimeoutException();

            return numBytes;
        });

        public override int ReadByte() => AssertHandle(() =>
        {
            int numBytes = 0;

            if (_isasync)
                numBytes = EndRead(BeginReadCore(tempBuf, 0, 1, null, null));
            else
            {
                numBytes = ReadFileNative(tempBuf, 0, 1, null, out int hr);

                if (numBytes == -1)
                    InternalResources.WinIOError();
            }

            if (numBytes == 0)
                throw new TimeoutException();
            else
                return tempBuf[0];
        });

        internal static void WinIOError() => WinIOError(Marshal.GetLastWin32Error(), "");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        internal void SetBufferSizes(int readBufferSize, int writeBufferSize) => AssertHandle(() =>
        {
            if (!UnsafeNativeMethods.SetupComm(_handle, readBufferSize, writeBufferSize))
                InternalResources.WinIOError();
        });

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer, offset, count, WriteTimeout);

        internal unsafe void Write(byte[] array, int offset, int count, int timeout) => AssertHandle(() =>
        {

            if (_inbreak)
                throw new InvalidOperationException();
            else if (array == null)
                throw new ArgumentNullException(nameof(array));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (count == 0)
                return 0;
            else if (array.Length - offset < count)
                throw new ArgumentException("Invalid offset length.");

            int numBytes;
            int hr;

            if (_isasync)
            {
                IAsyncResult result = BeginWriteCore(array, offset, count, null, null);
                EndWrite(result);

                SerialStreamAsyncResult afsar = result as SerialStreamAsyncResult;

                numBytes = afsar._numBytes;
            }
            else
            {
                numBytes = WriteFileNative(array, offset, count, null, out hr);

                if (numBytes == -1)
                {
                    if (hr == NativeMethods.ERROR_COUNTER_TIMEOUT)
                        throw new TimeoutException();

                    InternalResources.WinIOError();
                }
            }

            if (numBytes == 0)
                throw new TimeoutException();
        });

        public override void WriteByte(byte value) => WriteByte(value, WriteTimeout);

        internal unsafe void WriteByte(byte value, int timeout) => AssertHandle(() =>
        {
            if (_inbreak)
                throw new InvalidOperationException();

            tempBuf[0] = value;

            int numBytes;

            if (_isasync)
            {
                IAsyncResult result = BeginWriteCore(tempBuf, 0, 1, null, null);

                EndWrite(result);

                numBytes = (result as SerialStreamAsyncResult)?._numBytes;
            }
            else
            {
                numBytes = WriteFileNative(tempBuf, 0, 1, null, out int hr);

                if (numBytes == -1)
                {
                    // This is how writes timeout on Win9x. 
                    if (Marshal.GetLastWin32Error() == NativeMethods.ERROR_COUNTER_TIMEOUT)
                        throw new TimeoutException();

                    InternalResources.WinIOError();
                }
            }

            if (numBytes == 0)
                throw new TimeoutException();
        });

        private void InitializeDCB(int baudRate, Parity parity, int dataBits, StopBits stopBits, bool discardNull)
        {
            if (!UnsafeNativeMethods.GetCommState(_handle, ref dcb))
                InternalResources.WinIOError();

            dcb.DCBlength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(dcb);
            dcb.BaudRate = (uint)baudRate;
            dcb.ByteSize = (byte)dataBits;

            switch (stopBits)
            {
                case StopBits.One:
                    dcb.StopBits = NativeMethods.ONESTOPBIT;

                    break;
                case StopBits.OnePointFive:
                    dcb.StopBits = NativeMethods.ONE5STOPBITS;

                    break;
                case StopBits.Two:
                    dcb.StopBits = NativeMethods.TWOSTOPBITS;

                    break;
            }

            dcb.Parity = (byte)parity;

            SetDcbFlag(NativeMethods.FPARITY, (parity == Parity.None) ? 0 : 1);
            SetDcbFlag(NativeMethods.FBINARY, 1);
            SetDcbFlag(NativeMethods.FOUTXCTSFLOW, (_handshake == Handshake.RequestToSend) || (_handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);
            // SetDcbFlag(NativeMethods.FOUTXDSRFLOW, (dsrTimeout != 0L) ? 1 : 0);
            SetDcbFlag(NativeMethods.FOUTXDSRFLOW, 0);
            SetDcbFlag(NativeMethods.FDTRCONTROL, NativeMethods.DTR_CONTROL_DISABLE);
            SetDcbFlag(NativeMethods.FDSRSENSITIVITY, 0);
            SetDcbFlag(NativeMethods.FINX, (_handshake == Handshake.XOnXOff) || (_handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);
            SetDcbFlag(NativeMethods.FOUTX, (_handshake == Handshake.XOnXOff) || (_handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);

            if (parity != Parity.None)
            {
                SetDcbFlag(NativeMethods.FERRORCHAR, (_parityreplace != '\0') ? 1 : 0);

                dcb.ErrorChar = _parityreplace;
            }
            else
            {
                SetDcbFlag(NativeMethods.FERRORCHAR, 0);

                dcb.ErrorChar = (byte)'\0';
            }

            SetDcbFlag(NativeMethods.FNULL, discardNull ? 1 : 0);

            if ((_handshake == Handshake.RequestToSend) || (_handshake == Handshake.RequestToSendXOnXOff))
                SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_HANDSHAKE);
            else if (GetDcbFlag(NativeMethods.FRTSCONTROL) == NativeMethods.RTS_CONTROL_HANDSHAKE)
                SetDcbFlag(NativeMethods.FRTSCONTROL, NativeMethods.RTS_CONTROL_DISABLE);

            dcb.XonChar = NativeMethods.DEFAULTXONCHAR;
            dcb.XoffChar = NativeMethods.DEFAULTXOFFCHAR;

            dcb.XonLim = dcb.XoffLim = (ushort)(commProp.dwCurrentRxQueue / 4);
            dcb.EofChar = NativeMethods.EOFCHAR;
            // dcb.EvtChar = (byte) 0;
            dcb.EvtChar = NativeMethods.EOFCHAR;

            if (!UnsafeNativeMethods.SetCommState(_handle, ref dcb))
                InternalResources.WinIOError();
        }

        internal int GetDcbFlag(int whichFlag)
        {
            uint mask = 0x1;

            if (whichFlag == NativeMethods.FDTRCONTROL || whichFlag == NativeMethods.FRTSCONTROL)
                mask = 0x3;
            else if (whichFlag == NativeMethods.FDUMMY2)
                mask = 0x1FFFF;

            return (int)((dcb.Flags & (mask << whichFlag)) >> whichFlag);
        }

        internal void SetDcbFlag(int whichFlag, int setting)
        {
            uint mask = 0x1;

            setting <<= whichFlag;

            if (whichFlag == NativeMethods.FDTRCONTROL || whichFlag == NativeMethods.FRTSCONTROL)
                mask = 0x3;
            else if (whichFlag == NativeMethods.FDUMMY2)
                mask = 0x1FFFF;

            dcb.Flags &= ~(mask << whichFlag);
            dcb.Flags |= ((uint)setting);
        }

        unsafe private SerialStreamAsyncResult BeginReadCore(byte[] array, int offset, int numBytes, AsyncCallback userCallback, Object stateObject)
        {
            SerialStreamAsyncResult asyncResult = new SerialStreamAsyncResult
            {
                _userCallback = userCallback,
                _userStateObject = stateObject,
                _isWrite = false,
                _waitHandle = new ManualResetEvent(false)
            };
            Overlapped overlapped = new Overlapped(0, 0, IntPtr.Zero, asyncResult);
            NativeOverlapped* intOverlapped = overlapped.Pack(IOCallback, array);

            asyncResult._overlapped = intOverlapped;

            if ((ReadFileNative(array, offset, numBytes, intOverlapped, out int hr) == -1) && (hr != NativeMethods.ERROR_IO_PENDING))
                if (hr == NativeMethods.ERROR_HANDLE_EOF)
                    InternalResources.EndOfFile();
                else
                    InternalResources.WinIOError(hr, "");

            return asyncResult;
        }

        unsafe private SerialStreamAsyncResult BeginWriteCore(byte[] array, int offset, int numBytes, AsyncCallback userCallback, object stateObject)
        {
            SerialStreamAsyncResult asyncResult = new SerialStreamAsyncResult
            {
                _userCallback = userCallback,
                _userStateObject = stateObject,
                _isWrite = true,
                _waitHandle = new ManualResetEvent(false)
            };
            Overlapped overlapped = new Overlapped(0, 0, IntPtr.Zero, asyncResult);
            NativeOverlapped* intOverlapped = overlapped.Pack(IOCallback, array);

            asyncResult._overlapped = intOverlapped;

            if ((WriteFileNative(array, offset, numBytes, intOverlapped, out int hr) == -1) && (hr != NativeMethods.ERROR_IO_PENDING))
                if (hr == NativeMethods.ERROR_HANDLE_EOF)
                    InternalResources.EndOfFile();
                else
                    InternalResources.WinIOError(hr, "");

            return asyncResult;
        }

        private unsafe int ReadFileNative(byte[] bytes, int offset, int count, NativeOverlapped* overlapped, out int hr)
        {
            if (bytes.Length - offset < count)
                throw new IndexOutOfRangeException();

            if (bytes.Length == 0)
            {
                hr = 0;

                return 0;
            }

            int r = 0;
            int numBytesRead = 0;

            fixed (byte* p = bytes)
                if (_isasync)
                    r = UnsafeNativeMethods.ReadFile(_handle, p + offset, count, IntPtr.Zero, overlapped);
                else
                    r = UnsafeNativeMethods.ReadFile(_handle, p + offset, count, out numBytesRead, IntPtr.Zero);

            if (r == 0)
            {
                hr = Marshal.GetLastWin32Error();

                if (hr == NativeMethods.ERROR_INVALID_HANDLE)
                    _handle.SetHandleAsInvalid();

                return -1;
            }
            else
                hr = 0;

            return numBytesRead;
        }

        private unsafe int WriteFileNative(byte[] bytes, int offset, int count, NativeOverlapped* overlapped, out int hr)
        {
            if (bytes.Length - offset < count)
                throw new IndexOutOfRangeException();

            if (bytes.Length == 0)
            {
                hr = 0;

                return 0;
            }

            int numBytesWritten = 0;
            int r = 0;

            fixed (byte* p = bytes)
                if (_isasync)
                    r = UnsafeNativeMethods.WriteFile(_handle, p + offset, count, IntPtr.Zero, overlapped);
                else
                    r = UnsafeNativeMethods.WriteFile(_handle, p + offset, count, out numBytesWritten, IntPtr.Zero);

            if (r == 0)
            {
                hr = Marshal.GetLastWin32Error();

                if (hr == NativeMethods.ERROR_INVALID_HANDLE)
                    _handle.SetHandleAsInvalid();

                return -1;
            }
            else
                hr = 0;

            return numBytesWritten;
        }

        unsafe private static void AsyncFSCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            Overlapped overlapped = Overlapped.Unpack(pOverlapped);
            SerialStreamAsyncResult asyncResult = (SerialStreamAsyncResult)overlapped.AsyncResult;

            asyncResult._numBytes = (int)numBytes;
            asyncResult._errorCode = (int)errorCode;
            asyncResult._completedSynchronously = false;
            asyncResult._isComplete = true;

            ManualResetEvent wh = asyncResult._waitHandle;

            if (wh?.Set() == false)
                InternalResources.WinIOError();

            asyncResult._userCallback?.Invoke(asyncResult);
        }





        internal sealed class EventLoopRunner
        {
            private WeakReference streamWeakReference;
            internal ManualResetEvent eventLoopEndedSignal = new ManualResetEvent(false);
            internal ManualResetEvent waitCommEventWaitHandle = new ManualResetEvent(false);
            private SafeFileHandle handle;
            private readonly string portName;
            private bool isAsync;
            internal bool endEventLoop;
            private int eventsOccurred;

            WaitCallback callErrorEvents;
            WaitCallback callReceiveEvents;
            WaitCallback callPinEvents;
            IOCompletionCallback freeNativeOverlappedCallback;

            internal unsafe EventLoopRunner(SerialStream stream)
            {
                handle = stream._handle;
                streamWeakReference = new WeakReference(stream);

                callErrorEvents = new WaitCallback(CallErrorEvents);
                callReceiveEvents = new WaitCallback(CallReceiveEvents);
                callPinEvents = new WaitCallback(CallPinEvents);
                freeNativeOverlappedCallback = new IOCompletionCallback(FreeNativeOverlappedCallback);
                isAsync = stream._isasync;
                portName = stream._name;
            }

            internal bool ShutdownLoop => endEventLoop;

            internal unsafe void WaitForCommEvent()
            {
                int unused = 0;
                bool doCleanup = false;
                NativeOverlapped* intOverlapped = null;

                while (!ShutdownLoop)
                {
                    SerialStreamAsyncResult asyncResult = null;
                    if (isAsync)
                    {
                        asyncResult = new SerialStreamAsyncResult();
                        asyncResult._userCallback = null;
                        asyncResult._userStateObject = null;
                        asyncResult._isWrite = false;

                        // we're going to use _numBytes for something different in this loop.  In this case, both 
                        // freeNativeOverlappedCallback and this thread will decrement that value.  Whichever one decrements it
                        // to zero will be the one to free the native overlapped.  This guarantees the overlapped gets freed
                        // after both the callback and GetOverlappedResult have had a chance to use it. 
                        asyncResult._numBytes = 2;
                        asyncResult._waitHandle = waitCommEventWaitHandle;

                        waitCommEventWaitHandle.Reset();
                        Overlapped overlapped = new Overlapped(0, 0, waitCommEventWaitHandle.SafeWaitHandle.DangerousGetHandle(), asyncResult);
                        // Pack the Overlapped class, and store it in the async result
                        intOverlapped = overlapped.Pack(freeNativeOverlappedCallback, null);
                    }

                    fixed (int* eventsOccurredPtr = &eventsOccurred)
                    {

                        if (!UnsafeNativeMethods.WaitCommEvent(handle, eventsOccurredPtr, intOverlapped))
                        {
                            int hr = Marshal.GetLastWin32Error();
                            // When a device is disconnected unexpectedly from a serial port, there appear to be
                            // at least three error codes Windows or drivers may return.
                            const int ERROR_DEVICE_REMOVED = 1617;
                            if (hr == NativeMethods.ERROR_ACCESS_DENIED || hr == NativeMethods.ERROR_BAD_COMMAND || hr == ERROR_DEVICE_REMOVED)
                            {
                                doCleanup = true;
                                break;
                            }
                            if (hr == NativeMethods.ERROR_IO_PENDING)
                            {
                                Debug.Assert(isAsync, "The port is not open for async, so we should not get ERROR_IO_PENDING from WaitCommEvent");
                                int error;

                                // if we get IO pending, MSDN says we should wait on the WaitHandle, then call GetOverlappedResult
                                // to get the results of WaitCommEvent. 
                                bool success = waitCommEventWaitHandle.WaitOne();
                                Debug.Assert(success, "waitCommEventWaitHandle.WaitOne() returned error " + Marshal.GetLastWin32Error());

                                do
                                {
                                    // NOTE: GetOverlappedResult will modify the original pointer passed into WaitCommEvent.
                                    success = UnsafeNativeMethods.GetOverlappedResult(handle, intOverlapped, ref unused, false);
                                    error = Marshal.GetLastWin32Error();
                                }
                                while (error == NativeMethods.ERROR_IO_INCOMPLETE && !ShutdownLoop && !success);

                                if (!success)
                                {
                                    // Ignore ERROR_IO_INCOMPLETE and ERROR_INVALID_PARAMETER, because there's a chance we'll get
                                    // one of those while shutting down 
                                    if (!((error == NativeMethods.ERROR_IO_INCOMPLETE || error == NativeMethods.ERROR_INVALID_PARAMETER) && ShutdownLoop))
                                        Debug.Assert(false, "GetOverlappedResult returned error, we might leak intOverlapped memory" + error.ToString(CultureInfo.InvariantCulture));
                                }
                            }
                            else if (hr != NativeMethods.ERROR_INVALID_PARAMETER)
                            {
                                // ignore ERROR_INVALID_PARAMETER errors.  WaitCommError seems to return this
                                // when SetCommMask is changed while it's blocking (like we do in Dispose())
                                Debug.Assert(false, "WaitCommEvent returned error " + hr);
                            }
                        }
                    }

                    if (!ShutdownLoop)
                        CallEvents(eventsOccurred);

                    if (isAsync)
                    {
                        if (Interlocked.Decrement(ref asyncResult._numBytes) == 0)
                            Overlapped.Free(intOverlapped);
                    }
                } // while (!ShutdownLoop)

                if (doCleanup)
                {
                    // the rest will be handled in Dispose()
                    endEventLoop = true;
                    Overlapped.Free(intOverlapped);
                }
                eventLoopEndedSignal.Set();
            }

            private unsafe void FreeNativeOverlappedCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
            {
                Overlapped overlapped = Overlapped.Unpack(pOverlapped);
                SerialStreamAsyncResult asyncResult = (SerialStreamAsyncResult)overlapped.AsyncResult;

                if (Interlocked.Decrement(ref asyncResult._numBytes) == 0)
                    Overlapped.Free(pOverlapped);
            }

            private void CallEvents(int nativeEvents)
            {
                if ((nativeEvents & (NativeMethods.EV_ERR | NativeMethods.EV_RXCHAR)) != 0)
                {
                    int errors = 0;

                    if (!UnsafeNativeMethods.ClearCommError(handle, ref errors, IntPtr.Zero))
                    {
                        endEventLoop = true;

                        Thread.MemoryBarrier();

                        return;
                    }

                    errors &= EVT_ERROR;

                    if (errors != 0)
                        ThreadPool.QueueUserWorkItem(callErrorEvents, errors);
                }

                if ((nativeEvents & EVT_PINCH) != 0)
                    ThreadPool.QueueUserWorkItem(callPinEvents, nativeEvents);

                if ((nativeEvents & EVT_RECV) != 0)
                    ThreadPool.QueueUserWorkItem(callReceiveEvents, nativeEvents);
            }

            private void CallErrorEvents(object state)
            {
                int errors = (int)state;
                SerialStream stream = (SerialStream)streamWeakReference.Target;

                if (stream == null)
                    return;

                if (stream.ErrorReceived != null)
                {
                    if ((errors & (int)SerialError.TXFull) != 0)
                        stream.ErrorReceived(stream, SerialError.TXFull);
                    if ((errors & (int)SerialError.RXOver) != 0)
                        stream.ErrorReceived(stream, SerialError.RXOver);
                    if ((errors & (int)SerialError.Overrun) != 0)
                        stream.ErrorReceived(stream, SerialError.Overrun);
                    if ((errors & (int)SerialError.RXParity) != 0)
                        stream.ErrorReceived(stream, SerialError.RXParity);
                    if ((errors & (int)SerialError.Frame) != 0)
                        stream.ErrorReceived(stream, SerialError.Frame);
                }

                stream = null;
            }

            private void CallReceiveEvents(object state)
            {
                int nativeEvents = (int)state;
                SerialStream stream = (SerialStream)streamWeakReference.Target;

                if (stream == null)
                    return;

                if (stream.DataReceived != null)
                {
                    if ((nativeEvents & (int)SerialData.Chars) != 0)
                        stream.DataReceived(stream, SerialData.Chars);
                    if ((nativeEvents & (int)SerialData.Eof) != 0)
                        stream.DataReceived(stream, SerialData.Eof);
                }

                stream = null;
            }

            private void CallPinEvents(object state)
            {
                int nativeEvents = (int)state;

                SerialStream stream = (SerialStream)streamWeakReference.Target;
                if (stream == null)
                    return;

                if (stream.PinChanged != null)
                {
                    if ((nativeEvents & (int)SerialPinChange.CtsChanged) != 0)
                        stream.PinChanged(stream, SerialPinChange.CtsChanged);
                    if ((nativeEvents & (int)SerialPinChange.DsrChanged) != 0)
                        stream.PinChanged(stream, SerialPinChange.DsrChanged);
                    if ((nativeEvents & (int)SerialPinChange.CDChanged) != 0)
                        stream.PinChanged(stream, SerialPinChange.CDChanged);
                    if ((nativeEvents & (int)SerialPinChange.Ring) != 0)
                        stream.PinChanged(stream, SerialPinChange.Ring);
                    if ((nativeEvents & (int)SerialPinChange.Break) != 0)
                        stream.PinChanged(stream, SerialPinChange.Break);
                }

                stream = null;
            }
        }

        unsafe internal sealed class SerialStreamAsyncResult
            : IAsyncResult
        {
            internal AsyncCallback _userCallback;
            internal object _userStateObject;
            internal bool _isWrite;
            internal bool _isComplete;
            internal bool _completedSynchronously;
            internal ManualResetEvent _waitHandle;
            internal int _EndXxxCalled;
            internal int _numBytes;
            internal int _errorCode;
            internal NativeOverlapped* _overlapped;


            public object AsyncState => _userStateObject;

            public bool IsCompleted => _isComplete;

            public WaitHandle AsyncWaitHandle => _waitHandle;

            public bool CompletedSynchronously => _completedSynchronously;
        }
    }

    internal struct DCB
    {
        public uint DCBlength;
        public uint BaudRate;
        public uint Flags;
        public ushort wReserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EvtChar;
        public ushort wReserved1;
    }

    internal struct COMSTAT
    {
        public uint Flags;
        public uint cbInQue;
        public uint cbOutQue;
    }

    internal struct COMMTIMEOUTS
    {
        public int ReadIntervalTimeout;
        public int ReadTotalTimeoutMultiplier;
        public int ReadTotalTimeoutConstant;
        public int WriteTotalTimeoutMultiplier;
        public int WriteTotalTimeoutConstant;
    }

    internal struct COMMPROP
    {
        public ushort wPacketLength;
        public ushort wPacketVersion;
        public int dwServiceMask;
        public int dwReserved1;
        public int dwMaxTxQueue;
        public int dwMaxRxQueue;
        public int dwMaxBaud;
        public int dwProvSubType;
        public int dwProvCapabilities;
        public int dwSettableParams;
        public int dwSettableBaud;
        public ushort wSettableData;
        public ushort wSettableStopParity;
        public int dwCurrentTxQueue;
        public int dwCurrentRxQueue;
        public int dwProvSpec1;
        public int dwProvSpec2;
        public char wcProvChar;
    }

    public enum SerialPinChange
    {
        CtsChanged = 0x08,
        DsrChanged = 0x10,
        CDChanged = 0x20,
        Ring = 0x100,
        Break = 0x40
    }

    public enum SerialData
    {
        Chars = 1,
        Eof = 2
    }

    public enum SerialError
    {
        TXFull = 256,
        RXOver = 1,
        Overrun = 2,
        RXParity = 4,
        Frame = 8,
    }

    public enum Parity
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4
    };

    public enum StopBits
    {
        None = 0,
        One = 1,
        Two = 2,
        OnePointFive = 3

    };

    public enum Handshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    };
}
