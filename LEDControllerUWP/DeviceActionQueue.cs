using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LEDControllerUWP {
    public class DeviceActionQueue : IDisposable
    {
        private Node _root = null;

        private readonly object _lock = new object();

        private readonly AutoResetEvent _itemsInQueueEvent = new AutoResetEvent(false);

        private readonly CancellationTokenSource _tokenSource;
        private CancellationToken _token;

        private readonly DeviceConnection _connection;

        public int Count {get; private set;}

        public DeviceActionQueue(DeviceConnection connection)
        {
            lock (_lock)
            {
                _connection = connection;
                _tokenSource = new CancellationTokenSource();
                _token = _tokenSource.Token;
                var worker = new Task(WorkerThreadAction, _token, TaskCreationOptions.LongRunning);
                worker.Start();
                Count = 0;
            }
        }


        public void Enqueue(DeviceActionType type, DeviceActionParams args, bool overwriteSameType, Action<bool> completionCallback = null)
        {
            lock (_lock)
            {
                if (overwriteSameType)
                {
                    RemoveAllOfType(type);
                }
                Enqueue(new Node(new DeviceAction {ActionType = type, Params = args, CompletionCallback = completionCallback}));
            }
        }

        private void Enqueue(Node node)
        {
            lock (_lock)
            {
                Count++;
                if (_root == null)
                {
                    _root = node;
                    _itemsInQueueEvent.Set();
                }
                else
                {
                    var cur = _root;
                    while (cur.Next != null) cur = cur.Next;
                    cur.Next = node;
                    _itemsInQueueEvent.Set();
                }
            }
        }

        private void RemoveAllOfType(DeviceActionType type)
        {
            lock (_lock)
            {
                var cur = _root;
                Node prev = null;
                while (cur != null)
                {
                    if (cur.Action.ActionType == type)
                    {
                        Count--;
                        if (prev != null)
                        {
                            prev.Next = cur.Next;
                        }
                        else //we are removing the root
                        {
                            _root = cur.Next;
                        }
                    }
                    else
                    {
                        prev = cur;
                    }
                    cur = cur.Next;
                }
                if (_root == null) _itemsInQueueEvent.Reset();
            }
        }

        private DeviceAction? Peek()
        {
            lock (_lock)
            {
                return _root?.Action;
            }
        }

        private DeviceAction? Dequeue()
        {
            lock (_lock)
            {
                var retVal = Peek();
                _root = _root?.Next;
                if (_root == null) _itemsInQueueEvent.Reset();
                Count--;
                return retVal;
            }
        }

        private bool HasNext()
        {
            lock (_lock)
            {
                return _root != null;
            }
        }

        private class Node
        {
            public Node Next;
            public DeviceAction Action;

            public Node(DeviceAction action, Node next = null)
            {
                Action = action;
                Next = next;
            }
        }


        private struct DeviceAction
        {
            public DeviceActionType ActionType;
            public DeviceActionParams Params;
            public Action<bool> CompletionCallback;
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
            _itemsInQueueEvent.Set();//allows the worker to continue and check the token if needed
            _tokenSource.Dispose();
        }

        /// <summary>
        /// the entry point of the worker thread
        /// </summary>
        private void WorkerThreadAction()
        {
            while (!_token.IsCancellationRequested)
            {
                _itemsInQueueEvent.WaitOne();
                if (_token.IsCancellationRequested) return;
                using (var session = _connection.BeginSession())
                {
                    Debug.Assert(session != null, "session is null");
                    while (HasNext())
                    {
                        if (_token.IsCancellationRequested) return;
                        var nextAction = Dequeue();
                        Debug.Assert(nextAction != null, "nextAction is null!");
                        var success = false;
                        try
                        {
                            success = ExecuteAction(nextAction.Value, session);
                        }
                        finally
                        {
                            nextAction.Value.CompletionCallback?.Invoke(success);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Executes the action, called from the worker thread
        /// </summary>
        /// <param name="action"></param>
        /// <param name="session"></param>
        private bool ExecuteAction(DeviceAction action, DeviceConnection.DeviceConnectionSession session)
        {
            switch (action.ActionType)
            {
                case DeviceActionType.SetBrightness:
                    return session.SetBrightness(action.Params.Brightness);
                case DeviceActionType.Reset:
                    return _connection.ResetDevice();
                case DeviceActionType.SetRgb:
                    return session.SetImmediateRgb(action.Params.Number, action.Params.Red, action.Params.Green,
                        action.Params.Blue);
                case DeviceActionType.SetHsv:
                    return session.SetImmediateRgb(action.Params.Number, action.Params.Hue, action.Params.Saturation,
                        action.Params.Value);
                case DeviceActionType.SetTranslation:
                    return session.SetTranslationMode(action.Params.TranslationTableSetting);
                case DeviceActionType.SetColorMode:
                    break;
                case DeviceActionType.PowerDown:
                    return _connection.PowerDown();
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return false;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DeviceActionParams
    {
        [FieldOffset(0)] public byte Brightness;
        [FieldOffset(0)] public byte Number;
        [FieldOffset(0)] public ColorMode ColorMode;
        [FieldOffset(0)] public TranslationTableSetting TranslationTableSetting;

        [FieldOffset(1)] public byte Red;
        [FieldOffset(1)] public byte Hue;


        [FieldOffset(2)] public byte Green;
        [FieldOffset(2)] public byte Saturation;

        [FieldOffset(3)] public byte Blue;
        [FieldOffset(3)] public byte Value;

        [FieldOffset(1)] public int RGB;
        [FieldOffset(1)] public int HSV;
    }

    public enum DeviceActionType
    {
        SetBrightness,
        Reset,
        SetRgb,
        SetHsv,
        SetTranslation,
        SetColorMode,
        PowerDown
    }
}
