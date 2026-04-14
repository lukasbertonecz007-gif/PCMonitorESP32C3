using System.IO.Ports;
using System.Text;

namespace PcMonitorHost;

internal sealed class SerialService : IDisposable
{
    private SerialPort? _port;
    private readonly object _rxLock = new();
    private readonly StringBuilder _rxBuffer = new(256);

    public bool IsConnected => _port is { IsOpen: true };
    public event Action<string>? LineReceived;

    public static IReadOnlyList<string> GetPortNames()
    {
        return SerialPort.GetPortNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Connect(string portName, int baudRate)
    {
        Disconnect();

        _port = new SerialPort(portName, baudRate)
        {
            NewLine = "\n",
            ReadTimeout = 300,
            WriteTimeout = 300
        };

        _port.DataReceived += OnDataReceived;
        _port.Open();
    }

    public void SendLine(string line)
    {
        if (_port is null || !_port.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not connected.");
        }

        _port.WriteLine(line);
    }

    public void Disconnect()
    {
        if (_port is null)
        {
            return;
        }

        try
        {
            if (_port.IsOpen)
            {
                _port.DataReceived -= OnDataReceived;
                _port.Close();
            }
        }
        finally
        {
            _port.Dispose();
            _port = null;
            lock (_rxLock)
            {
                _rxBuffer.Clear();
            }
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort? port = _port;
        if (port is null)
        {
            return;
        }

        string chunk;
        try
        {
            chunk = port.ReadExisting();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        lock (_rxLock)
        {
            _rxBuffer.Append(chunk);
            while (true)
            {
                int newlineIndex = _rxBuffer.ToString().IndexOf('\n');
                if (newlineIndex < 0)
                {
                    break;
                }

                string line = _rxBuffer.ToString(0, newlineIndex).TrimEnd('\r');
                _rxBuffer.Remove(0, newlineIndex + 1);
                if (line.Length > 0)
                {
                    LineReceived?.Invoke(line);
                }
            }

            if (_rxBuffer.Length > 2048)
            {
                _rxBuffer.Clear();
            }
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}
