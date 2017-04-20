using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace LEDControllerUWP {
    public class DeviceConnection : IDisposable
    {
        private readonly DataReader _reader;
        private readonly DataWriter _writer;
        private readonly SerialDevice _device;

        private bool _inEditMode = false;

        private readonly object _mutex = new object();


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

        private bool EnterEditMode()
        {
            lock (_mutex)
            {
                if (_inEditMode) return true;
                Debug.WriteLine("Entering edit mode");
                _writer.WriteByte((byte) 'E');
                _writer.StoreAsync().AsTask().Wait();
                _reader.LoadAsync(1).AsTask().Wait();
                var val = _reader.ReadByte();
                Debug.WriteLine($"EnterEditMode Response: {val}");
                if (val == Ack)
                {
                    _inEditMode = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void ExitEditMode()
        {
            lock (_mutex)
            {
                if (!_inEditMode) return;
                //Debug.WriteLine("Exiting edit mode");
                _writer.WriteByte((byte) 'E');
                _writer.StoreAsync().AsTask().Wait();
                //Debug.WriteLine("Exited edit mode");
                _inEditMode = false;
            }
        }



        public bool SetBrightness(byte newBrightness)
        {
            lock(_mutex) {
                Debug.WriteLine($"Setting brightness to {newBrightness}");
                if (!EnterEditMode()) return false;
                Debug.WriteLine("Sending B and brightness");
                _writer.WriteByte((byte)'B');
                _writer.WriteByte(newBrightness);
                _writer.StoreAsync().AsTask().Wait();
                Debug.WriteLine("Reading response");
                _reader.LoadAsync(1).AsTask().Wait();
                var val = _reader.ReadByte();
                Debug.WriteLine($"SetBrightness Response: {val}");
                ExitEditMode();
                return val == Ack;
            }
        }

        public bool SetImmediateRgb(byte number, byte red, byte green, byte blue)
        {
            return SetImmediateInternal(number, red, green, blue, 'I');
        }

        public bool SetImmediateHsv(byte number, byte hue, byte saturation, byte value) {
            return SetImmediateInternal(number, hue, saturation, value, 'i');
        }

        private bool SetImmediateInternal(byte number, byte r, byte g, byte b, char command)
        {
            lock (_mutex) {
                if (!EnterEditMode()) return false;
                _writer.WriteByte((byte)command);
                _writer.WriteByte(number);
                _writer.WriteByte(r);
                _writer.WriteByte(g);
                _writer.WriteByte(b);
                _writer.StoreAsync().AsTask().Wait();
                _reader.LoadAsync(1).AsTask().Wait();
                var val = _reader.ReadByte();
                ExitEditMode();
                return val == Ack;
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
                _inEditMode = false;
                return true;
            }
        }

        public bool SetTranslationMode(TranslationTableSetting mode)
        {
            lock (_mutex) {
                if (!EnterEditMode()) return false;
                _writer.WriteByte((byte)'T');
                _writer.WriteByte((byte)mode);
                _writer.StoreAsync().AsTask().Wait();
                _reader.LoadAsync(1).AsTask().Wait();
                var val = _reader.ReadByte();
                ExitEditMode();
                return val == Ack;
            }
        }
        public bool PowerDown()
        {
            lock (_mutex)
            {
                if (!EnterEditMode()) return false;
                _writer.WriteByte((byte) 'P');
                _writer.StoreAsync().AsTask().Wait();
                _inEditMode = false;
                return true;
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
            _writer.Dispose();
        }


        private const byte Ack = 0x06;


    }
}
