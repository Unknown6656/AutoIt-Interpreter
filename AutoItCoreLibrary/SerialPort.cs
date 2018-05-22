using System.Runtime.InteropServices;
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

        private SerialStream stream;
        private byte[] _inbuffer = new byte[defaultBufferSize];
        private char[] _onechar = new char[1];
        private char[] _singlecharbuffer;
        private int _readpos;
        private int _readlen;


        public event EventHandler<SerialData> DataReceived;
        public event EventHandler<SerialPinChange> PinChanged;
        public event EventHandler<SerialError> ErrorReceived;


        public Stream BaseStream => AssertOpen(stream);

        public int BaudRate
        {
            get => _rate;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(BaudRate));
                else if (IsOpen)
                    stream.BaudRate = value;
                else
                    _rate = value;
            }
        }

        public bool BreakState
        {
            get => AssertOpen(stream.BreakState);
            set => AssertOpen(() => stream.BreakState = value);
        }

        public int BytesToWrite => AssertOpen(stream.BytesToWrite);

        public int BytesToRead => AssertOpen(stream.BytesToRead + CachedBytesToRead);

        private int CachedBytesToRead => _readlen - _readpos;

        public bool CDHolding => AssertOpen(stream.CDHolding);

        public bool CtsHolding => AssertOpen(stream.CtsHolding);

        public int DataBits
        {
            get => _databits;
            set
            {
                if (value < MIN_DATABITS || value > MAX_DATABITS)
                    throw new ArgumentOutOfRangeException(nameof(DataBits));

                if (IsOpen)
                    stream.DataBits = value;
                else
                    _databits = value;
            }
        }

        public bool DiscardNull
        {
            get => _discardnull;
            set
            {
                if (IsOpen)
                    stream.DiscardNull = value;
                else
                    _discardnull = value;
            }
        }

        public bool DsrHolding => AssertOpen(stream.DsrHolding);

        public bool DtrEnable
        {
            get
            {
                if (IsOpen)
                    _dtrenable = stream.DtrEnable;

                return _dtrenable;
            }
            set
            {
                if (IsOpen)
                    stream.DtrEnable = value;
                else
                    _dtrenable = value;
            }
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
                else if (IsOpen)
                    stream.Handshake = value;
                else
                    _handshake = value;
            }
        }

        public bool IsOpen => stream?.IsOpen ?? false;

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
                else if (IsOpen)
                    stream.Parity = value;
                else
                    _parity = value;
            }
        }

        public byte ParityReplace
        {
            get => _parityreplace;
            set
            {
                if (IsOpen)
                    stream.ParityReplace = value;
                else
                    _parityreplace = value;
            }
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
                else if (IsOpen)
                    stream.ReadTimeout = value;
                else
                    _rimeout = value;
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
                    CatchReceivedEvents(this, SerialData.Chars);
            }
        }

        public bool RtsEnable
        {
            get
            {
                if (IsOpen)
                    _rtsenable = stream.RtsEnable;

                return _rtsenable;
            }
            set
            {
                if (IsOpen)
                    stream.RtsEnable = value;
                else
                    _rtsenable = value;
            }
        }

        public StopBits StopBits
        {
            get => _stopbits;
            set
            {
                if (value < StopBits.One || value > StopBits.OnePointFive)
                    throw new ArgumentOutOfRangeException(nameof(StopBits));
                else if (IsOpen)
                    stream.StopBits = value;
                else
                    _stopbits = value;
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
                else if (IsOpen)
                    stream.WriteTimeout = value;
                else
                    _wtimeout = value;
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
                stream?.Flush();
                stream?.Close();
                stream = null;
            }

            base.Dispose(disposing);
        }

        public void DiscardInBuffer() => AssertOpen(() =>
        {
            stream.DiscardInBuffer();
            _readpos = _readlen = 0;
        });

        public void DiscardOutBuffer() => AssertOpen(() => stream.DiscardOutBuffer());

        public static string[] GetPortNames() => new string[0];

        public void Open()
        {
            if (IsOpen)
                throw new InvalidOperationException("Port is already open");

            stream = new SerialStream(_name, _rate, _parity, _databits, _stopbits, _rimeout, _wtimeout, _handshake, _dtrenable, _rtsenable, _discardnull, _parityreplace);
            stream.SetBufferSizes(_readbuffersize, _writebuffersize);
            stream.ErrorReceived += new EventHandler<SerialError>(CatchErrorEvents);
            stream.PinChanged += new EventHandler<SerialPinChange>(CatchPinChangedEvents);
            stream.DataReceived += new EventHandler<SerialData>(CatchReceivedEvents);
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

            bytesReadToBuffer += stream.Read(buffer, offset + bytesReadToBuffer, bytesLeftToRead);

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
                    int bytesInStream = stream.BytesToRead;

                    if (bytesInStream == 0)
                        bytesInStream = 1;

                    MaybeResizeBuffer(bytesInStream);
                    _readlen += stream.Read(_inbuffer, _readlen, bytesInStream);

                    return ReadBufferIntoChars(_onechar, 0, 1, false) != 0 ? _onechar[0] : throw new TimeoutException();
                }

                int startTicks = Environment.TickCount;

                do
                {
                    if (timeout == InfiniteTimeout)
                        nextByte = stream.ReadByte(); // InfiniteTimeout
                    else if (timeout - timeUsed >= 0)
                    {
                        nextByte = stream.ReadByte();// timeout - timeUsed
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
                throw new ArgumentException("Invalid ofset length.");
            else
                return InternalRead(buffer, offset, count, _rimeout, false);
        });

        private int InternalRead(char[] buffer, int offset, int count, int timeout, bool countMultiByteCharsAsOne)
        {
            if (count == 0)
                return 0;

            int startTicks = Environment.TickCount;
            int bytesInStream = stream.BytesToRead;

            MaybeResizeBuffer(bytesInStream);

            _readlen += stream.Read(_inbuffer, _readlen, bytesInStream);

            if (_decoder.GetCharCount(_inbuffer, _readpos, CachedBytesToRead) > 0)
                return ReadBufferIntoChars(buffer, offset, count, countMultiByteCharsAsOne);
            else if (timeout == 0)
                throw new TimeoutException();

            int justRead;
            int maxReadSize = Encoding.GetMaxByteCount(count);

            do
            {
                MaybeResizeBuffer(maxReadSize);

                _readlen += stream.Read(_inbuffer, _readlen, maxReadSize);
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

                return stream.ReadByte();
            }
        });

        public string ReadExisting() => AssertOpen(() =>
        {
            byte[] bytesReceived = new byte[BytesToRead];

            if (_readpos < _readlen)
                Buffer.BlockCopy(_inbuffer, _readpos, bytesReceived, 0, CachedBytesToRead);

            stream.Read(bytesReceived, CachedBytesToRead, bytesReceived.Length - (CachedBytesToRead));

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
            int bytesInStream = stream.BytesToRead;

            MaybeResizeBuffer(bytesInStream);

            _readlen += stream.Read(_inbuffer, _readlen, bytesInStream);

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

                stream.Write(bytesToWrite, 0, bytesToWrite.Length, _wtimeout);
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
                stream.Write(buffer, offset, count, _wtimeout);
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

            if ((eventHandler != null) && (stream != null))
                lock (stream)
                    if (stream.IsOpen)
                        eventHandler(this, e);
        }

        private void CatchPinChangedEvents(object src, SerialPinChange e)
        {
            EventHandler<SerialPinChange> eventHandler = PinChanged;

            if ((eventHandler != null) && (stream != null))
                lock (stream)
                    if (stream.IsOpen)
                        eventHandler(this, e);
        }

        private void CatchReceivedEvents(object src, SerialData e)
        {
            EventHandler<SerialData> eventHandler = DataReceived;
            bool raise = false;

            if ((eventHandler != null) && (stream != null))
                lock (stream)
                    try
                    {
                        raise = stream.IsOpen && ((e == SerialData.Eof) || (BytesToRead >= _receivedbytesthreshold));
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

    internal sealed unsafe class SerialStream
        : Stream
    {
        private const int INF_TIMEOUT = -2;

        private static readonly IOCompletionCallback _iocallback = new IOCompletionCallback(AsyncFSCallback);

        private readonly EventLoopRunner _evtrunner;
        private COMMTIMEOUTS _com_timeout;
        private COMMPROP _com_prop;
        private COMSTAT _com_stat;
        private DCB _dcb;

        private Handshake _handshake;
        private byte _parityreplace = (byte)'?';
        private bool _rtsenable;
        private bool _inbreak;
        private byte[] _tmpbuf;


        internal event EventHandler<SerialPinChange> PinChanged;
        internal event EventHandler<SerialError> ErrorReceived;
        internal event EventHandler<SerialData> DataReceived;


        public string Name { get; }

        public bool IsAsync { get; } = true;

        public SafeFileHandle Handle { get; private set; }

        public override bool CanRead => Handle != null;

        public override bool CanSeek => false;

        public override bool CanTimeout => Handle != null;

        public override bool CanWrite => Handle != null;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        internal int BaudRate
        {
            get => (int)_dcb.BaudRate;
            set
            {
                if (value <= 0 || (value > _com_prop.dwMaxBaud && _com_prop.dwMaxBaud > 0))
                    throw new ArgumentOutOfRangeException(nameof(BaudRate));
                else if (value != _dcb.BaudRate)
                {
                    int baudRateOld = (int)_dcb.BaudRate;

                    _dcb.BaudRate = (uint)value;

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        _dcb.BaudRate = (uint)baudRateOld;

                        Win32.WinIOError();
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
                    if (!Win32.SetCommBreak(Handle))
                        Win32.WinIOError();
                }
                else if (!Win32.ClearCommBreak(Handle))
                    Win32.WinIOError();

                _inbreak = value;
            }
        }

        internal int DataBits
        {
            get => _dcb.ByteSize;
            set
            {
                if (value != _dcb.ByteSize)
                {
                    byte byteSizeOld = _dcb.ByteSize;

                    _dcb.ByteSize = (byte)value;

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        _dcb.ByteSize = byteSizeOld;
                        Win32.WinIOError();
                    }
                }
            }
        }

        internal bool DiscardNull
        {
            get => GetDcbFlag(Win32.FNULL) == 1;
            set
            {
                int fNullFlag = GetDcbFlag(Win32.FNULL);

                if ((value ? 0 : 1) == fNullFlag)
                {
                    int fNullOld = fNullFlag;

                    SetDcbFlag(Win32.FNULL, value ? 1 : 0);

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        SetDcbFlag(Win32.FNULL, fNullOld);

                        Win32.WinIOError();
                    }
                }
            }
        }

        internal bool DtrEnable
        {
            get => GetDcbFlag(Win32.FDTRCONTROL) == Win32.DTR_CONTROL_ENABLE;
            set
            {
                int fDtrControlOld = GetDcbFlag(Win32.FDTRCONTROL);

                SetDcbFlag(Win32.FDTRCONTROL, value ? Win32.DTR_CONTROL_ENABLE : Win32.DTR_CONTROL_DISABLE);

                if (!Win32.SetCommState(Handle, ref _dcb))
                {
                    SetDcbFlag(Win32.FDTRCONTROL, fDtrControlOld);

                    Win32.WinIOError();
                }

                if (!Win32.EscapeCommFunction(Handle, value ? Win32.SETDTR : Win32.CLRDTR))
                    Win32.WinIOError();
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
                    int fInOutXOld = GetDcbFlag(Win32.FINX);
                    int fOutxCtsFlowOld = GetDcbFlag(Win32.FOUTXCTSFLOW);
                    int fRtsControlOld = GetDcbFlag(Win32.FRTSCONTROL);

                    _handshake = value;

                    int fInXOutXFlag = (_handshake == Handshake.XOnXOff || _handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0;

                    SetDcbFlag(Win32.FINX, fInXOutXFlag);
                    SetDcbFlag(Win32.FOUTX, fInXOutXFlag);
                    SetDcbFlag(Win32.FOUTXCTSFLOW, (_handshake == Handshake.RequestToSend || _handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);

                    if ((_handshake == Handshake.RequestToSend || _handshake == Handshake.RequestToSendXOnXOff))
                        SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_HANDSHAKE);
                    else if (_rtsenable)
                        SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_ENABLE);
                    else
                        SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_DISABLE);

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        _handshake = handshakeOld;

                        SetDcbFlag(Win32.FINX, fInOutXOld);
                        SetDcbFlag(Win32.FOUTX, fInOutXOld);
                        SetDcbFlag(Win32.FOUTXCTSFLOW, fOutxCtsFlowOld);
                        SetDcbFlag(Win32.FRTSCONTROL, fRtsControlOld);

                        Win32.WinIOError();
                    }
                }
            }
        }

        internal bool IsOpen => Handle != null && !_evtrunner.ShutdownLoop;

        internal Parity Parity
        {
            get => (Parity)_dcb.Parity;
            set
            {
                if ((byte)value != _dcb.Parity)
                {
                    byte parityOld = _dcb.Parity;
                    int fParityOld = GetDcbFlag(Win32.FPARITY);
                    byte ErrorCharOld = _dcb.ErrorChar;
                    int fErrorCharOld = GetDcbFlag(Win32.FERRORCHAR);

                    _dcb.Parity = (byte)value;

                    int parityFlag = (_dcb.Parity == (byte)Parity.None) ? 0 : 1;

                    SetDcbFlag(Win32.FPARITY, parityFlag);

                    if (parityFlag == 1)
                    {
                        SetDcbFlag(Win32.FERRORCHAR, (_parityreplace != '\0') ? 1 : 0);
                        _dcb.ErrorChar = _parityreplace;
                    }
                    else
                    {
                        SetDcbFlag(Win32.FERRORCHAR, 0);
                        _dcb.ErrorChar = (byte)'\0';
                    }
                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        _dcb.Parity = parityOld;
                        SetDcbFlag(Win32.FPARITY, fParityOld);

                        _dcb.ErrorChar = ErrorCharOld;
                        SetDcbFlag(Win32.FERRORCHAR, fErrorCharOld);

                        Win32.WinIOError();
                    }
                }
            }
        }

        internal byte ParityReplace
        {
            get => _parityreplace;
            set
            {
                if (value != _parityreplace)
                {
                    byte parityReplaceOld = _parityreplace;
                    byte errorCharOld = _dcb.ErrorChar;
                    int fErrorCharOld = GetDcbFlag(Win32.FERRORCHAR);

                    _parityreplace = value;

                    if (GetDcbFlag(Win32.FPARITY) == 1)
                    {
                        SetDcbFlag(Win32.FERRORCHAR, (_parityreplace != '\0') ? 1 : 0);

                        _dcb.ErrorChar = _parityreplace;
                    }
                    else
                    {
                        SetDcbFlag(Win32.FERRORCHAR, 0);

                        _dcb.ErrorChar = (byte)'\0';
                    }

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        _parityreplace = parityReplaceOld;

                        SetDcbFlag(Win32.FERRORCHAR, fErrorCharOld);

                        _dcb.ErrorChar = errorCharOld;

                        Win32.WinIOError();
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
            get => _com_timeout.ReadTotalTimeoutConstant == INF_TIMEOUT ? SerialPort.InfiniteTimeout : _com_timeout.ReadTotalTimeoutConstant;
            set => AssertHandle(() =>
            {
                if (value < 0 && value != SerialPort.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(ReadTimeout));

                int oldReadConstant = _com_timeout.ReadTotalTimeoutConstant;
                int oldReadInterval = _com_timeout.ReadIntervalTimeout;
                int oldReadMultipler = _com_timeout.ReadTotalTimeoutMultiplier;

                if (value == 0)
                {
                    _com_timeout.ReadTotalTimeoutConstant = 0;
                    _com_timeout.ReadTotalTimeoutMultiplier = 0;
                    _com_timeout.ReadIntervalTimeout = -1;
                }
                else if (value == SerialPort.InfiniteTimeout)
                {
                    _com_timeout.ReadTotalTimeoutConstant = INF_TIMEOUT;
                    _com_timeout.ReadTotalTimeoutMultiplier = -1;
                    _com_timeout.ReadIntervalTimeout = -1;
                }
                else
                {
                    _com_timeout.ReadTotalTimeoutConstant = value;
                    _com_timeout.ReadTotalTimeoutMultiplier = -1;
                    _com_timeout.ReadIntervalTimeout = -1;
                }

                if (!Win32.SetCommTimeouts(Handle, ref _com_timeout))
                {
                    _com_timeout.ReadTotalTimeoutConstant = oldReadConstant;
                    _com_timeout.ReadTotalTimeoutMultiplier = oldReadMultipler;
                    _com_timeout.ReadIntervalTimeout = oldReadInterval;

                    Win32.WinIOError();
                }
            });
        }

        internal bool RtsEnable
        {
            get
            {
                int fRtsControl = GetDcbFlag(Win32.FRTSCONTROL);

                if (fRtsControl == Win32.RTS_CONTROL_HANDSHAKE)
                    throw new InvalidOperationException();

                return fRtsControl == Win32.RTS_CONTROL_ENABLE;
            }
            set
            {
                if ((_handshake == Handshake.RequestToSend) || (_handshake == Handshake.RequestToSendXOnXOff))
                    throw new InvalidOperationException();

                if (value != _rtsenable)
                {
                    int fRtsControlOld = GetDcbFlag(Win32.FRTSCONTROL);

                    _rtsenable = value;

                    if (value)
                        SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_ENABLE);
                    else
                        SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_DISABLE);

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        SetDcbFlag(Win32.FRTSCONTROL, fRtsControlOld);

                        _rtsenable = !_rtsenable;

                        Win32.WinIOError();
                    }

                    if (!Win32.EscapeCommFunction(Handle, value ? Win32.SETRTS : Win32.CLRRTS))
                        Win32.WinIOError();
                }
            }
        }

        internal StopBits StopBits
        {
            get
            {
                switch (_dcb.StopBits)
                {
                    case Win32.ONE5STOPBITS:
                        return StopBits.OnePointFive;
                    case Win32.TWOSTOPBITS:
                        return StopBits.Two;
                    default:
                        return StopBits.One;
                }
            }
            set
            {
                byte nativeValue = Win32.TWOSTOPBITS;

                if (value == StopBits.One)
                    nativeValue = Win32.ONESTOPBIT;
                else if (value == StopBits.OnePointFive)
                    nativeValue = Win32.ONE5STOPBITS;

                if (nativeValue != _dcb.StopBits)
                {
                    byte stopBitsOld = _dcb.StopBits;

                    _dcb.StopBits = nativeValue;

                    if (!Win32.SetCommState(Handle, ref _dcb))
                    {
                        _dcb.StopBits = stopBitsOld;

                        Win32.WinIOError();
                    }
                }
            }
        }

        public override int WriteTimeout
        {
            get => _com_timeout.WriteTotalTimeoutConstant == INF_TIMEOUT ? SerialPort.InfiniteTimeout : _com_timeout.WriteTotalTimeoutConstant;
            set => AssertHandle(() =>
            {
                if (value <= 0 && value != SerialPort.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(WriteTimeout));

                int oldWriteConstant = _com_timeout.WriteTotalTimeoutConstant;

                _com_timeout.WriteTotalTimeoutConstant = ((value == SerialPort.InfiniteTimeout) ? 0 : value);

                if (!Win32.SetCommTimeouts(Handle, ref _com_timeout))
                {
                    _com_timeout.WriteTotalTimeoutConstant = oldWriteConstant;

                    Win32.WinIOError();
                }
            });
        }

        internal bool CDHolding
        {
            get
            {
                int pinStatus = 0;

                if (!Win32.GetCommModemStatus(Handle, ref pinStatus))
                    Win32.WinIOError();

                return (Win32.MS_RLSD_ON & pinStatus) != 0;
            }
        }

        internal bool CtsHolding
        {
            get
            {
                int pinStatus = 0;

                if (!Win32.GetCommModemStatus(Handle, ref pinStatus))
                    Win32.WinIOError();

                return (Win32.MS_CTS_ON & pinStatus) != 0;
            }
        }

        internal bool DsrHolding
        {
            get
            {
                int pinStatus = 0;

                if (!Win32.GetCommModemStatus(Handle, ref pinStatus))
                    Win32.WinIOError();

                return (Win32.MS_DSR_ON & pinStatus) != 0;
            }
        }

        internal int BytesToRead
        {
            get
            {
                int errorCode = 0;

                if (!Win32.ClearCommError(Handle, ref errorCode, ref _com_stat))
                    Win32.WinIOError();

                return (int)_com_stat.cbInQue;
            }
        }

        internal int BytesToWrite
        {
            get
            {
                int errorCode = 0;

                if (!Win32.ClearCommError(Handle, ref errorCode, ref _com_stat))
                    Win32.WinIOError();

                return (int)_com_stat.cbOutQue;
            }
        }

        internal SerialStream(string name, int bauds, Parity parity, int dataBits, StopBits stopBits, int readTimeout, int writeTimeout, Handshake handshake, bool dtrEnable, bool rtsEnable, bool discardNull, byte parityReplace)
        {
            int flags = Win32.FILE_FLAG_OVERLAPPED;

            if (Environment.OSVersion.Platform == PlatformID.Win32Windows)
            {
                flags = Win32.FILE_ATTRIBUTE_NORMAL;
                IsAsync = false;
            }

            if (name?.StartsWith("COM", StringComparison.OrdinalIgnoreCase) != true)
                throw new ArgumentException("Invalid serial port", nameof(name));

            SafeFileHandle tempHandle = new SafeFileHandle((IntPtr)Win32.CreateFile($@"\\.\{name}", unchecked((uint)(Win32.GENERIC_READ | Win32.GENERIC_WRITE)), 0, null, 3, (uint)flags, null), true);

            if (tempHandle.IsInvalid)
                Win32.WinIOError(name);

            try
            {
                int fileType = Win32.GetFileType(tempHandle);

                if ((fileType != Win32.FILE_TYPE_CHAR) && (fileType != Win32.FILE_TYPE_UNKNOWN))
                    throw new ArgumentException("Invalid serial port", nameof(name));

                Handle = tempHandle;
                Name = name;
                _handshake = handshake;
                _parityreplace = parityReplace;
                _tmpbuf = new byte[1];
                _com_prop = new COMMPROP();

                int pinStatus = 0;

                if (!Win32.GetCommProperties(Handle, ref _com_prop) || !Win32.GetCommModemStatus(Handle, ref pinStatus))
                {
                    int errorCode = Marshal.GetLastWin32Error();

                    if ((errorCode == Win32.ERROR_INVALID_PARAMETER) || (errorCode == Win32.ERROR_INVALID_HANDLE))
                        throw new ArgumentException("Invalid serial port", nameof(name));
                    else
                        Win32.WinIOError(errorCode, string.Empty);
                }
                if (_com_prop.dwMaxBaud != 0 && bauds > _com_prop.dwMaxBaud)
                    throw new ArgumentOutOfRangeException(nameof(bauds));

                _com_stat = new COMSTAT();
                _dcb = new DCB();

                InitializeDCB(bauds, parity, dataBits, stopBits, discardNull);

                DtrEnable = dtrEnable;
                this._rtsenable = (GetDcbFlag(Win32.FRTSCONTROL) == Win32.RTS_CONTROL_ENABLE);

                if ((handshake != Handshake.RequestToSend) && (handshake != Handshake.RequestToSendXOnXOff))
                    RtsEnable = rtsEnable;

                // NOTE: this logic should match what is in the ReadTimeout property
                if (readTimeout == 0)
                {
                    _com_timeout.ReadTotalTimeoutConstant = 0;
                    _com_timeout.ReadTotalTimeoutMultiplier = 0;
                    _com_timeout.ReadIntervalTimeout = -1;
                }
                else if (readTimeout == SerialPort.InfiniteTimeout)
                {
                    // SetCommTimeouts doesn't like a value of -1 for some reason, so
                    // we'll use -2(infiniteTimeoutConst) to represent infinite. 
                    _com_timeout.ReadTotalTimeoutConstant = INF_TIMEOUT;
                    _com_timeout.ReadTotalTimeoutMultiplier = -1;
                    _com_timeout.ReadIntervalTimeout = -1;
                }
                else
                {
                    _com_timeout.ReadTotalTimeoutConstant = readTimeout;
                    _com_timeout.ReadTotalTimeoutMultiplier = -1;
                    _com_timeout.ReadIntervalTimeout = -1;
                }

                _com_timeout.WriteTotalTimeoutMultiplier = 0;
                _com_timeout.WriteTotalTimeoutConstant = (writeTimeout == SerialPort.InfiniteTimeout) ? 0 : writeTimeout;

                if (!Win32.SetCommTimeouts(Handle, ref _com_timeout))
                    Win32.WinIOError();

                if (IsAsync && !ThreadPool.BindHandle(Handle))
                    throw new IOException();

                Win32.SetCommMask(Handle, Win32.ALL_EVENTS);

                _evtrunner = new EventLoopRunner(this);

                Thread thrd = new Thread(new ThreadStart(_evtrunner.WaitForCommEvent))
                {
                    IsBackground = true
                };
                thrd.Start();
            }
            catch
            {
                tempHandle.Close();
                Handle = null;

                throw;
            }
        }

        ~SerialStream() => Dispose(false);

        protected override void Dispose(bool disposing)
        {
            if (Handle != null && !Handle.IsInvalid)
                try
                {
                    _evtrunner._shutdown = true;

                    Thread.MemoryBarrier();

                    bool skipSPAccess = false;

                    Win32.SetCommMask(Handle, 0);

                    if (!Win32.EscapeCommFunction(Handle, Win32.CLRDTR))
                    {
                        int hr = Marshal.GetLastWin32Error();
                        const int ERROR_DEVICE_REMOVED = 1617;

                        if ((hr == Win32.ERROR_ACCESS_DENIED || hr == Win32.ERROR_BAD_COMMAND || hr == ERROR_DEVICE_REMOVED) && !disposing)
                            skipSPAccess = true;
                        else if (disposing)
                            Win32.WinIOError();
                    }

                    if (!skipSPAccess && !Handle.IsClosed)
                    {
                        Flush();
                    }

                    _evtrunner._evt_comwait.Set();

                    if (!skipSPAccess)
                    {
                        DiscardInBuffer();
                        DiscardOutBuffer();
                    }

                    if (disposing)
                    {
                        _evtrunner?._evt_loopend?.WaitOne();
                        _evtrunner?._evt_loopend?.Close();
                        _evtrunner?._evt_comwait?.Close();
                    }
                }
                finally
                {
                    if (disposing)
                        lock (this)
                        {
                            Handle.Close();
                            Handle = null;
                        }
                    else
                    {
                        Handle.Close();
                        Handle = null;
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
                if (!IsAsync)
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
                if (!IsAsync)
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
            if (!Win32.PurgeComm(Handle, Win32.PURGE_RXCLEAR | Win32.PURGE_RXABORT))
                Win32.WinIOError();
        }

        internal void DiscardOutBuffer()
        {
            if (!Win32.PurgeComm(Handle, Win32.PURGE_TXCLEAR | Win32.PURGE_TXABORT))
                Win32.WinIOError();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (!IsAsync)
                return base.EndRead(asyncResult);
            else if (asyncResult is null)
                throw new ArgumentNullException(nameof(asyncResult));

            SerialStreamAsyncResult afsar = asyncResult as SerialStreamAsyncResult;

            if (afsar?._isWrite != false)
                throw new ArgumentException("Invalid async result", nameof(asyncResult));

            if (1 == Interlocked.CompareExchange(ref afsar._EndXxxCalled, 1, 0))
                throw new InvalidOperationException("Called twice");

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
                Win32.WinIOError(afsar._errorCode, Name);
            else if (failed)
                throw new IOException();

            return afsar._numBytes;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (!IsAsync)
                base.EndWrite(asyncResult);
            else
            {
                if (_inbreak)
                    throw new InvalidOperationException();
                else if (asyncResult == null)
                    throw new ArgumentNullException(nameof(asyncResult));

                SerialStreamAsyncResult afsar = asyncResult as SerialStreamAsyncResult;

                if (afsar == null || !afsar._isWrite)
                    throw new ArgumentException("Invalid async result", nameof(asyncResult));
                if (1 == Interlocked.CompareExchange(ref afsar._EndXxxCalled, 1, 0))
                    throw new InvalidOperationException("Called twice");

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
                    Win32.WinIOError(afsar._errorCode, Name);
            }
        }

        public override void Flush() => AssertHandle(() => Win32.FlushFileBuffers(Handle));

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer, offset, count, ReadTimeout);

        internal int Read(byte[] array, int offset, int count, int timeout) => AssertHandle(() =>
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

            if (IsAsync)
                numBytes = EndRead(BeginReadCore(array, offset, count, null, null));
            else
            {
                numBytes = ReadFileNative(array, offset, count, null, out int hr);

                if (numBytes == -1)
                    Win32.WinIOError();
            }

            if (numBytes == 0)
                throw new TimeoutException();

            return numBytes;
        });

        public override int ReadByte() => AssertHandle(() =>
        {
            int numBytes = 0;

            if (IsAsync)
                numBytes = EndRead(BeginReadCore(_tmpbuf, 0, 1, null, null));
            else
            {
                numBytes = ReadFileNative(_tmpbuf, 0, 1, null, out int hr);

                if (numBytes == -1)
                    Win32.WinIOError();
            }

            if (numBytes == 0)
                throw new TimeoutException();
            else
                return _tmpbuf[0];
        });

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        internal void SetBufferSizes(int readBufferSize, int writeBufferSize) => AssertHandle(() =>
        {
            if (!Win32.SetupComm(Handle, readBufferSize, writeBufferSize))
                Win32.WinIOError();
        });

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer, offset, count, WriteTimeout);

        internal void Write(byte[] array, int offset, int count, int timeout) => AssertHandle(() =>
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
                return;
            else if (array.Length - offset < count)
                throw new ArgumentException("Invalid offset length.");

            int numBytes;

            if (IsAsync)
            {
                IAsyncResult result = BeginWriteCore(array, offset, count, null, null);
                EndWrite(result);

                SerialStreamAsyncResult afsar = result as SerialStreamAsyncResult;

                numBytes = afsar._numBytes;
            }
            else
            {
                numBytes = WriteFileNative(array, offset, count, null, out int hr);

                if (numBytes == -1)
                {
                    if (hr == Win32.ERROR_COUNTER_TIMEOUT)
                        throw new TimeoutException();

                    Win32.WinIOError();
                }
            }

            if (numBytes == 0)
                throw new TimeoutException();
        });

        public override void WriteByte(byte value) => WriteByte(value, WriteTimeout);

        internal void WriteByte(byte value, int timeout) => AssertHandle(() =>
        {
            if (_inbreak)
                throw new InvalidOperationException();

            _tmpbuf[0] = value;

            int numBytes;

            if (IsAsync)
            {
                IAsyncResult result = BeginWriteCore(_tmpbuf, 0, 1, null, null);

                EndWrite(result);

                numBytes = (result as SerialStreamAsyncResult)?._numBytes ?? 0;
            }
            else
            {
                numBytes = WriteFileNative(_tmpbuf, 0, 1, null, out int hr);

                if (numBytes == -1)
                    if (Marshal.GetLastWin32Error() == Win32.ERROR_COUNTER_TIMEOUT)
                        throw new TimeoutException();
                    else
                        Win32.WinIOError();
            }

            if (numBytes == 0)
                throw new TimeoutException();
        });

        private void InitializeDCB(int baudRate, Parity parity, int dataBits, StopBits stopBits, bool discardNull)
        {
            if (!Win32.GetCommState(Handle, ref _dcb))
                Win32.WinIOError();

            _dcb.DCBlength = (uint)sizeof(DCB);
            _dcb.BaudRate = (uint)baudRate;
            _dcb.ByteSize = (byte)dataBits;

            switch (stopBits)
            {
                case StopBits.One:
                    _dcb.StopBits = Win32.ONESTOPBIT;

                    break;
                case StopBits.OnePointFive:
                    _dcb.StopBits = Win32.ONE5STOPBITS;

                    break;
                case StopBits.Two:
                    _dcb.StopBits = Win32.TWOSTOPBITS;

                    break;
            }

            _dcb.Parity = (byte)parity;

            SetDcbFlag(Win32.FPARITY, (parity == Parity.None) ? 0 : 1);
            SetDcbFlag(Win32.FBINARY, 1);
            SetDcbFlag(Win32.FOUTXCTSFLOW, (_handshake == Handshake.RequestToSend) || (_handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);
            // SetDcbFlag(Win32.FOUTXDSRFLOW, (dsrTimeout != 0L) ? 1 : 0);
            SetDcbFlag(Win32.FOUTXDSRFLOW, 0);
            SetDcbFlag(Win32.FDTRCONTROL, Win32.DTR_CONTROL_DISABLE);
            SetDcbFlag(Win32.FDSRSENSITIVITY, 0);
            SetDcbFlag(Win32.FINX, (_handshake == Handshake.XOnXOff) || (_handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);
            SetDcbFlag(Win32.FOUTX, (_handshake == Handshake.XOnXOff) || (_handshake == Handshake.RequestToSendXOnXOff) ? 1 : 0);

            if (parity != Parity.None)
            {
                SetDcbFlag(Win32.FERRORCHAR, (_parityreplace != '\0') ? 1 : 0);

                _dcb.ErrorChar = _parityreplace;
            }
            else
            {
                SetDcbFlag(Win32.FERRORCHAR, 0);

                _dcb.ErrorChar = (byte)'\0';
            }

            SetDcbFlag(Win32.FNULL, discardNull ? 1 : 0);

            if ((_handshake == Handshake.RequestToSend) || (_handshake == Handshake.RequestToSendXOnXOff))
                SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_HANDSHAKE);
            else if (GetDcbFlag(Win32.FRTSCONTROL) == Win32.RTS_CONTROL_HANDSHAKE)
                SetDcbFlag(Win32.FRTSCONTROL, Win32.RTS_CONTROL_DISABLE);

            _dcb.XonChar = Win32.DEFAULTXONCHAR;
            _dcb.XoffChar = Win32.DEFAULTXOFFCHAR;

            _dcb.XonLim = _dcb.XoffLim = (ushort)(_com_prop.dwCurrentRxQueue / 4);
            _dcb.EofChar = Win32.EOFCHAR;
            // dcb.EvtChar = (byte)'\0';
            _dcb.EvtChar = Win32.EOFCHAR;

            if (!Win32.SetCommState(Handle, ref _dcb))
                Win32.WinIOError();
        }

        internal int GetDcbFlag(int whichFlag)
        {
            uint mask = 0x1;

            if (whichFlag == Win32.FDTRCONTROL || whichFlag == Win32.FRTSCONTROL)
                mask = 0x3;
            else if (whichFlag == Win32.FDUMMY2)
                mask = 0x1FFFF;

            return (int)((_dcb.Flags & (mask << whichFlag)) >> whichFlag);
        }

        internal void SetDcbFlag(int whichFlag, int setting)
        {
            uint mask = 0x1;

            setting <<= whichFlag;

            if (whichFlag == Win32.FDTRCONTROL || whichFlag == Win32.FRTSCONTROL)
                mask = 0x3;
            else if (whichFlag == Win32.FDUMMY2)
                mask = 0x1FFFF;

            _dcb.Flags &= ~(mask << whichFlag);
            _dcb.Flags |= ((uint)setting);
        }

        private SerialStreamAsyncResult BeginReadCore(byte[] array, int offset, int numBytes, AsyncCallback userCallback, Object stateObject)
        {
            SerialStreamAsyncResult asyncResult = new SerialStreamAsyncResult
            {
                _userCallback = userCallback,
                _userStateObject = stateObject,
                _isWrite = false,
                _waitHandle = new ManualResetEvent(false)
            };
            Overlapped overlapped = new Overlapped(0, 0, IntPtr.Zero, asyncResult);
            NativeOverlapped* intOverlapped = overlapped.Pack(_iocallback, array);

            asyncResult._overlapped = intOverlapped;

            if ((ReadFileNative(array, offset, numBytes, intOverlapped, out int hr) == -1) && (hr != Win32.ERROR_IO_PENDING))
                if (hr == Win32.ERROR_HANDLE_EOF)
                    throw new EndOfStreamException();
                else
                    Win32.WinIOError(hr, "");

            return asyncResult;
        }

        private SerialStreamAsyncResult BeginWriteCore(byte[] array, int offset, int numBytes, AsyncCallback userCallback, object stateObject)
        {
            SerialStreamAsyncResult asyncResult = new SerialStreamAsyncResult
            {
                _userCallback = userCallback,
                _userStateObject = stateObject,
                _isWrite = true,
                _waitHandle = new ManualResetEvent(false)
            };
            Overlapped overlapped = new Overlapped(0, 0, IntPtr.Zero, asyncResult);
            NativeOverlapped* intOverlapped = overlapped.Pack(_iocallback, array);

            asyncResult._overlapped = intOverlapped;

            if ((WriteFileNative(array, offset, numBytes, intOverlapped, out int hr) == -1) && (hr != Win32.ERROR_IO_PENDING))
                if (hr == Win32.ERROR_HANDLE_EOF)
                    throw new EndOfStreamException();
                else
                    Win32.WinIOError(hr, "");

            return asyncResult;
        }

        private int ReadFileNative(byte[] bytes, int offset, int count, NativeOverlapped* overlapped, out int hr)
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
                if (IsAsync)
                    r = Win32.ReadFile(Handle, p + offset, count, null, overlapped);
                else
                    r = Win32.ReadFile(Handle, p + offset, count, out numBytesRead, null);

            if (r == 0)
            {
                hr = Marshal.GetLastWin32Error();

                if (hr == Win32.ERROR_INVALID_HANDLE)
                    Handle.SetHandleAsInvalid();

                return -1;
            }
            else
                hr = 0;

            return numBytesRead;
        }

        private int WriteFileNative(byte[] bytes, int offset, int count, NativeOverlapped* overlapped, out int hr)
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
                if (IsAsync)
                    r = Win32.WriteFile(Handle, p + offset, count, null, overlapped);
                else
                    r = Win32.WriteFile(Handle, p + offset, count, out numBytesWritten, null);

            if (r == 0)
            {
                hr = Marshal.GetLastWin32Error();

                if (hr == Win32.ERROR_INVALID_HANDLE)
                    Handle.SetHandleAsInvalid();

                return -1;
            }
            else
                hr = 0;

            return numBytesWritten;
        }

        private static void AsyncFSCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            Overlapped overlapped = Overlapped.Unpack(pOverlapped);
            SerialStreamAsyncResult asyncResult = (SerialStreamAsyncResult)overlapped.AsyncResult;

            asyncResult._numBytes = (int)numBytes;
            asyncResult._errorCode = (int)errorCode;
            asyncResult._completedSynchronously = false;
            asyncResult._isComplete = true;

            ManualResetEvent wh = asyncResult._waitHandle;

            if (wh?.Set() == false)
                Win32.WinIOError();

            asyncResult._userCallback?.Invoke(asyncResult);
        }

        private void AssertHandle(Action f)
        {
            if (Handle is null)
                throw new ObjectDisposedException(null);
            else
                f();
        }

        private T AssertHandle<T>(Func<T> f) => Handle is null ? throw new ObjectDisposedException(null) : f();

        internal void RaisePinChanged(SerialPinChange e) => PinChanged?.Invoke(this, e);

        internal void RaiseErrorReceived(SerialError e) => ErrorReceived?.Invoke(this, e);

        internal void RaiseDataReceived(SerialData e) => DataReceived?.Invoke(this, e);
    }

    internal sealed unsafe class EventLoopRunner
    {
        private const int EVT_ERROR = (int)(SerialError.Frame | SerialError.Overrun | SerialError.RXOver | SerialError.RXParity | SerialError.TXFull);
        private const int EVT_RECV = (int)(SerialData.Chars | SerialData.Eof);
        private const int EVT_PINCH = (int)(SerialPinChange.Break | SerialPinChange.CDChanged | SerialPinChange.CtsChanged | SerialPinChange.Ring | SerialPinChange.DsrChanged);

        private readonly IOCompletionCallback _freenativeoverlappedcallback;
        private readonly WaitCallback _callreceiveevents;
        private readonly WaitCallback _callerrorevents;
        private readonly WaitCallback _callpinevents;

        private WeakReference StreamWeakReference { get; }

        public SafeFileHandle Handle { get; }
        public bool IsAsync { get; }
        public string Name { get; }


        internal ManualResetEvent _evt_loopend = new ManualResetEvent(false);
        internal ManualResetEvent _evt_comwait = new ManualResetEvent(false);
        private readonly int _vtocc;
        internal bool _shutdown;


        internal EventLoopRunner(SerialStream stream)
        {
            Name = stream.Name;
            Handle = stream.Handle;
            IsAsync = stream.IsAsync;
            StreamWeakReference = new WeakReference(stream);

            _callerrorevents = new WaitCallback(CallErrorEvents);
            _callreceiveevents = new WaitCallback(CallReceiveEvents);
            _callpinevents = new WaitCallback(CallPinEvents);
            _freenativeoverlappedcallback = new IOCompletionCallback(FreeNativeOverlappedCallback);
        }

        internal bool ShutdownLoop => _shutdown;

        internal void WaitForCommEvent()
        {
            int unused = 0;
            bool doCleanup = false;
            NativeOverlapped* intOverlapped = null;

            while (!ShutdownLoop)
            {
                SerialStreamAsyncResult asyncResult = null;

                if (IsAsync)
                {
                    asyncResult = new SerialStreamAsyncResult
                    {
                        _userCallback = null,
                        _userStateObject = null,
                        _isWrite = false,
                        _numBytes = 2,
                        _waitHandle = _evt_comwait
                    };

                    _evt_comwait.Reset();

                    Overlapped overlapped = new Overlapped(0, 0, _evt_comwait.SafeWaitHandle.DangerousGetHandle(), asyncResult);

                    intOverlapped = overlapped.Pack(_freenativeoverlappedcallback, null);
                }

                fixed (int* eventsOccurredPtr = &_vtocc)
                    if (!Win32.WaitCommEvent(Handle, eventsOccurredPtr, intOverlapped))
                    {
                        int hr = Marshal.GetLastWin32Error();
                        const int ERROR_DEVICE_REMOVED = 1617;

                        if (hr == Win32.ERROR_ACCESS_DENIED || hr == Win32.ERROR_BAD_COMMAND || hr == ERROR_DEVICE_REMOVED)
                        {
                            doCleanup = true;

                            break;
                        }
                        if (hr == Win32.ERROR_IO_PENDING)
                        {
                            int error;
                            bool success = _evt_comwait.WaitOne();

                            do
                            {
                                success = Win32.GetOverlappedResult(Handle, intOverlapped, ref unused, false);
                                error = Marshal.GetLastWin32Error();
                            }
                            while (error == Win32.ERROR_IO_INCOMPLETE && !ShutdownLoop && !success);
                        }
                    }

                if (!ShutdownLoop)
                    CallEvents(_vtocc);

                if (IsAsync && Interlocked.Decrement(ref asyncResult._numBytes) == 0)
                    Overlapped.Free(intOverlapped);
            }

            if (doCleanup)
            {
                _shutdown = true;

                Overlapped.Free(intOverlapped);
            }

            _evt_loopend.Set();
        }

        private void FreeNativeOverlappedCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            Overlapped overlapped = Overlapped.Unpack(pOverlapped);
            SerialStreamAsyncResult asyncResult = (SerialStreamAsyncResult)overlapped.AsyncResult;

            if (Interlocked.Decrement(ref asyncResult._numBytes) == 0)
                Overlapped.Free(pOverlapped);
        }

        private void CallEvents(int nativeEvents)
        {
            if ((nativeEvents & (Win32.EV_ERR | Win32.EV_RXCHAR)) != 0)
            {
                int errors = 0;

                if (!Win32.ClearCommError(Handle, ref errors, IntPtr.Zero))
                {
                    _shutdown = true;

                    Thread.MemoryBarrier();

                    return;
                }

                errors &= EVT_ERROR;

                if (errors != 0)
                    ThreadPool.QueueUserWorkItem(_callerrorevents, errors);
            }

            if ((nativeEvents & EVT_PINCH) != 0)
                ThreadPool.QueueUserWorkItem(_callpinevents, nativeEvents);

            if ((nativeEvents & EVT_RECV) != 0)
                ThreadPool.QueueUserWorkItem(_callreceiveevents, nativeEvents);
        }

        private void CallErrorEvents(object state)
        {
            int errors = (int)state;
            SerialStream stream = (SerialStream)StreamWeakReference.Target;

            if (stream == null)
                return;

            if ((errors & (int)SerialError.TXFull) != 0)
                stream.RaiseErrorReceived(SerialError.TXFull);
            if ((errors & (int)SerialError.RXOver) != 0)
                stream.RaiseErrorReceived(SerialError.RXOver);
            if ((errors & (int)SerialError.Overrun) != 0)
                stream.RaiseErrorReceived(SerialError.Overrun);
            if ((errors & (int)SerialError.RXParity) != 0)
                stream.RaiseErrorReceived(SerialError.RXParity);
            if ((errors & (int)SerialError.Frame) != 0)
                stream.RaiseErrorReceived(SerialError.Frame);

            stream = null;
        }

        private void CallReceiveEvents(object state)
        {
            int nativeEvents = (int)state;
            SerialStream stream = (SerialStream)StreamWeakReference.Target;

            if (stream == null)
                return;

            if ((nativeEvents & (int)SerialData.Chars) != 0)
                stream.RaiseDataReceived(SerialData.Chars);
            if ((nativeEvents & (int)SerialData.Eof) != 0)
                stream.RaiseDataReceived(SerialData.Eof);

            stream = null;
        }

        private void CallPinEvents(object state)
        {
            int nativeEvents = (int)state;
            SerialStream stream = (SerialStream)StreamWeakReference.Target;

            if (stream == null)
                return;

            if ((nativeEvents & (int)SerialPinChange.CtsChanged) != 0)
                stream.RaisePinChanged(SerialPinChange.CtsChanged);
            if ((nativeEvents & (int)SerialPinChange.DsrChanged) != 0)
                stream.RaisePinChanged(SerialPinChange.DsrChanged);
            if ((nativeEvents & (int)SerialPinChange.CDChanged) != 0)
                stream.RaisePinChanged(SerialPinChange.CDChanged);
            if ((nativeEvents & (int)SerialPinChange.Ring) != 0)
                stream.RaisePinChanged(SerialPinChange.Ring);
            if ((nativeEvents & (int)SerialPinChange.Break) != 0)
                stream.RaisePinChanged(SerialPinChange.Break);

            stream = null;
        }
    }

    internal sealed unsafe class SerialStreamAsyncResult
        : IAsyncResult
    {
        public AsyncCallback _userCallback;
        public object _userStateObject;
        public bool _isWrite;
        public bool _isComplete;
        public bool _completedSynchronously;
        public ManualResetEvent _waitHandle;
        public int _EndXxxCalled;
        public int _numBytes;
        public int _errorCode;
        public NativeOverlapped* _overlapped;


        public object AsyncState => _userStateObject;

        public WaitHandle AsyncWaitHandle => _waitHandle;

        public bool CompletedSynchronously => _completedSynchronously;

        public bool IsCompleted => _isComplete;
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
    }

    public enum StopBits
    {
        None = 0,
        One = 1,
        Two = 2,
        OnePointFive = 3
    }

    public enum Handshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    }
}
