using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace ObisLaser.Net
{
    /// <summary>
    /// Provides a method to find OBIS devices connected to serial ports.
    /// </summary>
    public static class ObisDiscovery
    {
        /// <summary>The default time to wait for each reply while probing.</summary>
        public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromMilliseconds(300);

        /// <summary>The maximum number of lasers searched for behind a controller.</summary>
        public const int MaxChannels = 8;

        /// <summary>
        /// Finds OBIS lasers and controllers by probing serial ports.
        /// </summary>
        /// <remarks>
        /// Each port is opened and sent an identification query (<c>*IDN?</c>); ports that reply like a
        /// Coherent device are then queried for their type, model and serial number. Lasers behind a
        /// controller (OBIS Remote, LaserBox) are listed with their channel. Probing writes to every port
        /// in <paramref name="portNames"/> (all serial ports by default), which may disturb other devices;
        /// use <paramref name="portNames"/> or <paramref name="excludedPortNames"/> to limit this.
        /// Ports that are already in use are skipped.
        /// </remarks>
        /// <param name="portNames">The ports to probe. Defaults to every serial port on the computer.</param>
        /// <param name="excludedPortNames">The ports never to probe, e.g. <c>COM1</c> (case-insensitive).</param>
        /// <param name="timeout">The time to wait for each reply.</param>
        /// <returns>The devices found.</returns>
        public static IReadOnlyList<DiscoveredDevice> FindAll(
            IEnumerable<string>? portNames = null,
            IEnumerable<string>? excludedPortNames = null,
            TimeSpan? timeout = null)
        {
            return FindAll(
                portNames ?? SerialPort.GetPortNames(),
                excludedPortNames ?? Array.Empty<string>(),
                timeout ?? DefaultProbeTimeout,
                SerialPortTransport.Open);
        }

        internal static IReadOnlyList<DiscoveredDevice> FindAll(
            IEnumerable<string> portNames,
            IEnumerable<string> excludedPortNames,
            TimeSpan timeout,
            Func<string, TimeSpan, IObisTransport> openTransport)
        {
            var excluded = new HashSet<string>(excludedPortNames, StringComparer.OrdinalIgnoreCase);
            var devices = new List<DiscoveredDevice>();
            foreach (var portName in portNames.Distinct().OrderBy(name => name, StringComparer.Ordinal))
            {
                if (excluded.Contains(portName)) continue;
                if (!RepliesLikeObis(portName, timeout, openTransport)) continue;
                try
                {
                    using var connection = new ObisConnection(openTransport(portName, timeout), timeout);
                    devices.AddRange(Describe(connection, portName));
                }
                catch (Exception ex) when (ex is ObisException or IOException or UnauthorizedAccessException or TimeoutException)
                {
                    // The device stopped responding or the port was taken in the meantime.
                }
            }

            return devices;
        }

        // Sends *IDN? without changing any device setting and checks the reply.
        static bool RepliesLikeObis(string portName, TimeSpan timeout, Func<string, TimeSpan, IObisTransport> openTransport)
        {
            try
            {
                using var transport = openTransport(portName, timeout);
                transport.DiscardInBuffer();
                transport.Write("*IDN?\r");
                var reply = transport.ReadLine().Trim().TrimStart('>').Trim();
                return reply.StartsWith("Coherent", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or InvalidOperationException)
            {
                return false;
            }
        }

        static IEnumerable<DiscoveredDevice> Describe(ObisConnection connection, string portName)
        {
            var root = DescribeChannel(connection, portName, 0);
            var devices = new List<DiscoveredDevice> { root };
            if (root.DeviceType is DeviceType.Mini or DeviceType.Master)
            {
                // Lasers behind a controller are numbered from 1; stop at the first missing one.
                for (int channel = 1; channel <= MaxChannels; channel++)
                {
                    try
                    {
                        devices.Add(DescribeChannel(connection, portName, channel));
                    }
                    catch (ObisException)
                    {
                        break;
                    }
                }
            }

            return devices;
        }

        static DiscoveredDevice DescribeChannel(ObisConnection connection, string portName, int channel)
        {
            return new DiscoveredDevice(
                portName,
                channel,
                connection.Execute(ObisProtocol.GetDeviceType(), channel),
                connection.Execute(ObisProtocol.GetModel(), channel),
                connection.Execute(ObisProtocol.GetSerialNumber(), channel));
        }
    }
}
