using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace ObisLaser.Net
{
    /// <summary>
    /// Represents a connection to an OBIS laser or controller (OBIS Remote, LaserBox).
    /// </summary>
    /// <remarks>
    /// Each command waits for the device to acknowledge it (<c>OK</c> or <c>ERR&lt;n&gt;</c>) before
    /// the next one is sent. Exchanges are serialized, so a connection can be shared between threads;
    /// asynchronous requests run in the order they are made.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var connection = ObisConnection.Open("COM3");
    /// var laser = connection.GetLaser();
    /// laser.SetPower(0.02);
    /// </code>
    /// </example>
    public sealed class ObisConnection : IDisposable
    {
        /// <summary>The default time to wait for a complete reply.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        readonly IObisTransport transport;
        readonly SemaphoreSlim gate = new(1, 1);
        bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObisConnection"/> class over an open transport.
        /// Most users should call <see cref="Open"/> instead.
        /// </summary>
        /// <param name="transport">The open transport. It is disposed with the connection.</param>
        /// <param name="timeout">The maximum time to wait for a complete reply.</param>
        public ObisConnection(IObisTransport transport, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Timeout = timeout ?? DefaultTimeout;
            EnableHandshaking();
        }

        /// <summary>
        /// Gets or sets the maximum time to wait for a complete reply.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Opens a serial port and connects to the OBIS device on it.
        /// </summary>
        /// <param name="portName">The name of the port, e.g. <c>COM3</c>.</param>
        /// <param name="timeout">The maximum time to wait for a complete reply.</param>
        /// <returns>The open connection.</returns>
        public static ObisConnection Open(string portName, TimeSpan? timeout = null)
        {
            var transport = SerialPortTransport.Open(portName, timeout ?? DefaultTimeout);
            try
            {
                return new ObisConnection(transport, timeout);
            }
            catch
            {
                transport.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets a laser on this connection.
        /// </summary>
        /// <param name="channel">0 for a laser connected directly; 1, 2, ... for the lasers connected
        /// to an OBIS Remote or LaserBox.</param>
        /// <returns>The laser.</returns>
        public Laser GetLaser(int channel = 0) => new(this, channel);

        /// <summary>
        /// Sends a request to the device on <paramref name="channel"/> and returns its parsed reply.
        /// </summary>
        /// <typeparam name="T">The type of the reply value.</typeparam>
        /// <param name="request">The request, typically created by <see cref="ObisProtocol"/>.</param>
        /// <param name="channel">The channel of the device.</param>
        /// <returns>The reply value.</returns>
        public T Execute<T>(ObisRequest<T> request, int channel = 0)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            return request.Parse(Send(AddressRequest(request.Command, channel)));
        }

        /// <summary>
        /// Sends a request to the device on <paramref name="channel"/> and returns its parsed reply.
        /// </summary>
        /// <typeparam name="T">The type of the reply value.</typeparam>
        /// <param name="request">The request, typically created by <see cref="ObisProtocol"/>.</param>
        /// <param name="channel">The channel of the device.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the reply value.</returns>
        public async Task<T> ExecuteAsync<T>(ObisRequest<T> request, int channel = 0, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var lines = await SendAsync(AddressRequest(request.Command, channel), cancellationToken).ConfigureAwait(false);
            return request.Parse(lines);
        }

        /// <summary>
        /// Sends any raw command or query and returns the reply lines, without the <c>OK</c> handshake.
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="channel">The channel of the device.</param>
        /// <returns>The reply lines.</returns>
        public IReadOnlyList<string> Query(string command, int channel = 0)
        {
            return Send(ObisProtocol.Address(command, channel));
        }

        /// <summary>
        /// Sends any raw command or query and returns the reply lines, without the <c>OK</c> handshake.
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="channel">The channel of the device.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the reply lines.</returns>
        public async Task<IReadOnlyList<string>> QueryAsync(string command, int channel = 0, CancellationToken cancellationToken = default)
        {
            return await SendAsync(ObisProtocol.Address(command, channel), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a command to every laser connected to a controller (OBIS Remote, LaserBox).
        /// </summary>
        /// <remarks>
        /// Useful to switch several lasers at once, e.g. <c>Broadcast("SOUR:AM:STAT ON")</c>.
        /// Queries cannot be broadcast. Lasers connected directly do not acknowledge broadcasts,
        /// so this only works through a controller.
        /// </remarks>
        /// <param name="command">The command text.</param>
        public void Broadcast(string command)
        {
            Send(ObisProtocol.Address(CheckBroadcast(command), ObisProtocol.BroadcastChannel));
        }

        /// <summary>
        /// Sends a command to every laser connected to a controller (OBIS Remote, LaserBox).
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the controller acknowledges the command.</returns>
        public Task BroadcastAsync(string command, CancellationToken cancellationToken = default)
        {
            return SendAsync(ObisProtocol.Address(CheckBroadcast(command), ObisProtocol.BroadcastChannel), cancellationToken);
        }

        /// <summary>
        /// Closes the connection. Laser emission is not changed.
        /// </summary>
        public void Dispose()
        {
            gate.Wait();
            try
            {
                if (disposed) return;
                disposed = true;
                transport.Dispose();
            }
            finally
            {
                gate.Release();
            }
        }

        static string AddressRequest(string command, int channel)
        {
            if (channel == ObisProtocol.BroadcastChannel)
            {
                throw new ArgumentException("Use Broadcast to send a command to every device.", nameof(channel));
            }

            return ObisProtocol.Address(command, channel);
        }

        static string CheckBroadcast(string command)
        {
            if (command is null) throw new ArgumentNullException(nameof(command));
            if (command.Contains("?")) throw new ArgumentException("Queries cannot be broadcast.", nameof(command));
            return command;
        }

        List<string> Send(string command)
        {
            gate.Wait();
            try
            {
                return Exchange(command);
            }
            finally
            {
                gate.Release();
            }
        }

        // Waiting for the gate starts synchronously and SemaphoreSlim releases waiting tasks
        // first-in first-out, so requests run in the order they are made.
        async Task<List<string>> SendAsync(string command, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => Exchange(command), CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        // Must be called while holding the gate.
        List<string> Exchange(string command)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ObisConnection));
            Write(command);
            return ReadReply(command);
        }

        void Write(string command)
        {
            // Drop anything left over (late replies, command prompts) so replies stay in sync.
            transport.DiscardInBuffer();
            transport.Write(command + "\r");
        }

        // Reads reply lines until the OK / ERR<n> handshake.
        List<string> ReadReply(string command)
        {
            var stopwatch = Stopwatch.StartNew();
            var lines = new List<string>();
            while (true)
            {
                var line = ReadLine(command, stopwatch);
                if (line == "OK") return lines;
                if (line.StartsWith("ERR", StringComparison.Ordinal))
                {
                    if (!int.TryParse(line.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                    {
                        throw new ObisProtocolException($"Malformed error reply '{line}'.");
                    }

                    throw new ObisDeviceException(code, command);
                }

                lines.Add(line);
            }
        }

        // Reads the next non-empty line, ignoring the optional '>' command prompt.
        string ReadLine(string command, Stopwatch stopwatch)
        {
            while (true)
            {
                string raw;
                try
                {
                    raw = transport.ReadLine();
                }
                catch (TimeoutException)
                {
                    throw new ObisTimeoutException($"No complete reply to '{command}' within {Timeout.TotalSeconds:0.###} s.");
                }

                if (stopwatch.Elapsed > Timeout)
                {
                    throw new ObisTimeoutException($"No complete reply to '{command}' within {Timeout.TotalSeconds:0.###} s.");
                }

                var line = raw.Trim().TrimStart('>').Trim();
                if (line.Length > 0) return line;
            }
        }

        // The client relies on the device acknowledging every message. The setting is
        // persistent, so it is only written when it is off.
        void EnableHandshaking()
        {
            var query = ObisProtocol.GetHandshaking().Command;
            Write(query);
            var reply = ReadLine(query, Stopwatch.StartNew());
            if (string.Equals(reply, "ON", StringComparison.OrdinalIgnoreCase))
            {
                ReadReply(query);
                return;
            }

            var enable = ObisProtocol.SetHandshaking(true).Command;
            Write(enable);
            try
            {
                ReadReply(enable);
            }
            catch (ObisTimeoutException)
            {
                // The manual does not say whether this very command is acknowledged.
            }
        }
    }
}
