using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.UI.Composition.Interactions;

namespace LEDControllerUWP {
    public class DeviceConnection : IDisposable
    {
        private readonly DataReader _reader;
        private readonly DataWriter _writer;
        private readonly SerialDevice _device;

        private bool _inEditMode = false;

        private object _mutex = new object();


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
            if (_inEditMode) return true;
            Debug.WriteLine("Entering edit mode");
            _writer.WriteByte((byte)'E');
            _writer.StoreAsync().AsTask().Wait();
            _reader.LoadAsync(1).AsTask().Wait();
            var val = _reader.ReadByte();
            Debug.WriteLine($"EnterEditMode Response: {val}");
            if (val == Ack) {
                _inEditMode = true;
                return true;
            } else {
                return false;
            }
        }

        private void ExitEditMode()
        {
            lock (_mutex)
            {
                ExitEditModeInternal();
            }
        }

        private void ExitEditModeInternal()
        {
            if (!_inEditMode) return;
            _writer.WriteByte((byte)'E');
            _writer.StoreAsync().AsTask().Wait();
            _inEditMode = false;
        }


        public bool SetBrightness(byte newBrightness)
        {
            using (var session = BeginSession())
            {
                return session != null && session.SetBrightness(newBrightness);
            }
            //lock(_mutex) {
            //    if (!EnterEditMode()) return false;
            //    var val = SetBrightnessInternal(newBrightness);
            //    ExitEditMode();
            //    return val;
            //}
        }

        private bool SetBrightnessInternal(byte newBrightness)
        {
            _writer.WriteByte((byte)'B');
            _writer.WriteByte(newBrightness);
            _writer.StoreAsync().AsTask().Wait();
            _reader.LoadAsync(1).AsTask().Wait();
            var val = _reader.ReadByte();
            return val == Ack;
        }

        public bool SetImmediateRgb(byte number, byte red, byte green, byte blue)
        {
            using (var session = BeginSession())
            {
                return session != null && session.SetImmediateRgb(number, red, green, blue);
            }
            //return SetImmediate(number, red, green, blue, 'I');
        }

        public bool SetImmediateHsv(byte number, byte hue, byte saturation, byte value) {
            using (var session = BeginSession())
            {
                return session != null && session.SetImmediateHsv(number, hue, saturation, value);
            }
            //return SetImmediate(number, hue, saturation, value, 'i');
        }

        private bool SetImmediate(byte number, byte r, byte g, byte b, char command)
        {
            lock (_mutex) {
                if (!EnterEditMode()) return false;
                var val = SetImmediateInternal(number, r, g, b, command);
                ExitEditMode();
                return val;
            }
        }

        private bool SetImmediateInternal(byte number, byte r, byte g, byte b, char command)
        {
            _writer.WriteByte((byte)command);
            _writer.WriteByte(number);
            _writer.WriteByte(r);
            _writer.WriteByte(g);
            _writer.WriteByte(b);
            _writer.StoreAsync().AsTask().Wait();
            _reader.LoadAsync(1).AsTask().Wait();
            var val = _reader.ReadByte();
            return val == Ack;
        }

        public bool ResetDevice()
        {
            lock (_mutex)
            {
                _device.IsDataTerminalReadyEnabled = false;
                Task.Delay(500).Wait();
                _device.IsDataTerminalReadyEnabled = true;
                Task.Delay(2000).Wait();
                _inEditMode = false;
                return true;
            }
        }

        public bool SetTranslationMode(TranslationTableSetting mode)
        {
            using (var session = BeginSession())
            {
                return session != null && session.SetTranslationMode(mode);
            }
            //lock (_mutex) {
            //    if (!EnterEditMode()) return false;
            //    var response = SetTranslationModeInternal(mode);
            //    ExitEditMode();
            //    return response;
            //}
        }

        private bool SetTranslationModeInternal(TranslationTableSetting mode)
        {
            _writer.WriteByte((byte)'T');
            _writer.WriteByte((byte)mode);
            _writer.StoreAsync().AsTask().Wait();
            _reader.LoadAsync(1).AsTask().Wait();
            var val = _reader.ReadByte();
            return val == Ack;
        }

        public bool PowerDown()
        {
            lock (_mutex)
            {
                if (!EnterEditMode()) return false;
                return PowerDownInternal();
            }
        }

        private bool PowerDownInternal()
        {
            _writer.WriteByte((byte)'P');
            _writer.StoreAsync().AsTask().Wait();
            _inEditMode = false;
            return true;
        }

        public void Dispose()
        {
            _reader.Dispose();
            _writer.Dispose();
        }


        private const byte Ack = 0x06;


        public class DeviceConnectionSession : IDisposable
        {
            private readonly DeviceConnection _connection;

            internal DeviceConnectionSession(DeviceConnection parent)
            {
                _connection = parent;
            }

            public void Dispose()
            {
                _connection.ExitEditModeInternal();
                Monitor.Exit(_connection._mutex);
            }

            public bool SetTranslationMode(TranslationTableSetting mode)
            {
                return _connection.SetTranslationModeInternal(mode);
            }

            public bool SetImmediateRgb(byte number, byte red, byte green, byte blue) {
                return _connection.SetImmediateInternal(number, red, green, blue, 'I');
            }

            public bool SetImmediateHsv(byte number, byte hue, byte saturation, byte value) {
                return _connection.SetImmediateInternal(number, hue, saturation, value, 'i');
            }

            public bool SetBrightness(byte newBrightness)
            {
                return _connection.SetBrightnessInternal(newBrightness);
            }
        }
    }
}
