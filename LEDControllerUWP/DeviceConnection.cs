using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace LEDControllerUWP {
    public class DeviceConnection : IDisposable
    {
        private readonly DataReader _reader;
        private readonly DataWriter _writer;
        private readonly SerialDevice _device;

        private readonly object _mutex = new object();

        private const int Timeout = 1000;


        public DeviceConnection(SerialDevice device)
        {
            _device = device;
            _device.BaudRate = 115200;
            _device.StopBits = SerialStopBitCount.One;
            _device.Parity = SerialParity.None;
            _device.DataBits = 8;
            _device.Handshake = SerialHandshake.None;
            _reader = new DataReader(device.InputStream);
            _writer = new DataWriter(device.OutputStream);
            Debug.WriteLine($"_reader.UncomsumedBufferLength = {_reader.UnconsumedBufferLength}");
            while (_reader.UnconsumedBufferLength > 0)
            {
                _reader.ReadByte();
            }
        }

        public DeviceConnectionSession BeginSession()
        {
            Monitor.Enter(_mutex);
            return EnterEditModeInternal() ? new DeviceConnectionSession(this) : null;
        }

        private bool EnterEditMode()
        {
            lock (_mutex)
            {
                return EnterEditModeInternal();
            }
        }

        private bool EnterEditModeInternal()
        {
            _writer.WriteByte((byte)'E');
            if (!_writer.StoreAsync().AsTask().Wait(Timeout)) return false;
            if (!_reader.LoadAsync(1).AsTask().Wait(Timeout)) return false;
            var val = _reader.ReadByte();
            return val == Ack;
        }


        public bool SetBrightness(byte newBrightness)
        {
            using (var session = BeginSession())
            {
                return session != null && session.SetBrightness(newBrightness);
            }
        }

        public bool SetImmediateRgb(byte number, byte red, byte green, byte blue)
        {
            using (var session = BeginSession())
            {
                return session != null && session.SetImmediateRgb(number, red, green, blue);
            }
        }

        public bool SetImmediateHsv(byte number, byte hue, byte saturation, byte value) {
            using (var session = BeginSession())
            {
                return session != null && session.SetImmediateHsv(number, hue, saturation, value);
            }
        }

        public bool ResetDevice()
        {
            lock (_mutex)
            {
                _device.IsDataTerminalReadyEnabled = false;
                Task.Delay(500).Wait();
                _device.IsDataTerminalReadyEnabled = true;
                Task.Delay(2000).Wait();
                return true;
            }
        }

        public bool SetTranslationMode(TranslationTableSetting mode)
        {
            using (var session = BeginSession())
            {
                return session != null && session.SetTranslationMode(mode);
            }
        }

        public bool PowerDown()
        {
            lock (_mutex)
            {
                return EnterEditMode() && PowerDownInternal();
            }
        }

        private bool PowerDownInternal()
        {
            _writer.WriteByte((byte)'P');
            _writer.StoreAsync().AsTask().Wait();
            return true;
        }

        public void Dispose()
        {
            _reader.Dispose();
            _writer.Dispose();
        }

        public bool VerifyDevice()
        {
            lock (_mutex)
            {
                _writer.WriteByte((byte) 'e');
                _writer.StoreAsync().AsTask().Wait();
                _reader.LoadAsync(1).AsTask().Wait();
                var val = _reader.ReadByte();
                return val == Ack;
            }
        }


        private const byte Ack = 0x06;


        /// <summary>
        /// This class represents an edit mode session on the device. 
        /// To open a new session, call DeviceConnection.BeginSession()
        /// Only one DeviceConnectionSession can be open at a time.
        /// Using a DeviceConnectionSession allows for multiple actions to be performed
        /// during the same edit mode session.  
        /// 
        /// </summary>
        public sealed class DeviceConnectionSession : IDisposable
        {
            private readonly DeviceConnection _connection;

            internal DeviceConnectionSession(DeviceConnection parent)
            {
                _connection = parent;
            }

            public void Dispose()
            {
                ExitEditMode();
                Monitor.Exit(_connection._mutex);
            }

            private bool ExitEditMode()
            {
                _connection._writer.WriteByte((byte)'e');
                return PushAndWaitForAck();
            }

            public bool SetTranslationMode(TranslationTableSetting mode)
            {
                _connection._writer.WriteByte((byte)'T');
                _connection._writer.WriteByte((byte)mode);
                return PushAndWaitForAck();
            }

            public bool SetImmediateRgb(byte number, byte red, byte green, byte blue) {
                _connection._writer.WriteByte((byte)'I');
                _connection._writer.WriteByte(number);
                _connection._writer.WriteByte(red);
                _connection._writer.WriteByte(green);
                _connection._writer.WriteByte(blue);
                return PushAndWaitForAck();
            }

            public bool SetImmediateHsv(byte number, byte hue, byte saturation, byte value) {
                _connection._writer.WriteByte((byte)'i');
                _connection._writer.WriteByte(number);
                _connection._writer.WriteByte(hue);
                _connection._writer.WriteByte(saturation);
                _connection._writer.WriteByte(value);
                return PushAndWaitForAck();
            }

            public bool SetBrightness(byte newBrightness)
            {
                _connection._writer.WriteByte((byte)'B');
                _connection._writer.WriteByte(newBrightness);
                return PushAndWaitForAck();
            }

            /// <summary>
            /// Writes any data pending in _connection._writer, with a timeout, and then waits for ACK
            /// </summary>
            /// <returns>True if both actions succeded, false otherwise</returns>
            private bool PushAndWaitForAck()
            {
                return _connection._writer.StoreAsync().AsTask().Wait(Timeout) && WaitForAck();
            }

            private bool WaitForAck()
            {
                _connection._reader.LoadAsync(1).AsTask().Wait();
                var val = _connection._reader.ReadByte();
                return val == Ack;
            }
        }
    }
}
