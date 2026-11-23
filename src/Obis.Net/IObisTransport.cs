using System;
using System.IO.Ports;
using System.Text;

namespace Obis.Net
{
    /// <summary>
    /// Provides the line-based byte transport used by <see cref="ObisConnection"/>.
    /// </summary>
    public interface IObisTransport : IDisposable
    {
        /// <summary>Writes text to the device.</summary>
        /// <param name="text">The text to write, including its terminator.</param>
        void Write(string text);

        /// <summary>Reads the next line sent by the device, without its terminator.</summary>
        /// <returns>The line read.</returns>
        /// <exception cref="TimeoutException">No complete line was received in time.</exception>
        string ReadLine();

        /// <summary>Discards any received data that has not been read yet.</summary>
        void DiscardInBuffer();
    }

    /// <summary>
    /// Provides an <see cref="IObisTransport"/> over a serial port (USB or RS-232).
    /// </summary>
    public sealed class SerialPortTransport : IObisTransport
    {
        /// <summary>The baud rate of OBIS devices (manual Table 9-12).</summary>
        public const int BaudRate = 115200;

        readonly SerialPort port;

        SerialPortTransport(SerialPort port)
        {
            this.port = port;
        }

        /// <summary>
        /// Opens a serial port with the OBIS communication settings: 115200 baud, 8N1, no flow control.
        /// </summary>
        /// <param name="portName">The name of the port, e.g. <c>COM3</c>.</param>
        /// <param name="timeout">The read and write timeout.</param>
        /// <returns>The open transport.</returns>
        public static SerialPortTransport Open(string portName, TimeSpan timeout)
        {
            var milliseconds = (int)Math.Ceiling(timeout.TotalMilliseconds);
            var port = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                Encoding = Encoding.ASCII,
                NewLine = "\n",
                ReadTimeout = milliseconds,
                WriteTimeout = milliseconds,
                // Asserted like pyserial does by default; USB virtual COM ports may wait for DTR.
                DtrEnable = true,
                RtsEnable = true,
            };

            try
            {
                port.Open();
                return new SerialPortTransport(port);
            }
            catch
            {
                port.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        public void Write(string text) => port.Write(text);

        /// <inheritdoc/>
        public string ReadLine() => port.ReadLine().TrimEnd('\r');

        /// <inheritdoc/>
        public void DiscardInBuffer() => port.DiscardInBuffer();

        /// <inheritdoc/>
        public void Dispose() => port.Dispose();
    }
}
