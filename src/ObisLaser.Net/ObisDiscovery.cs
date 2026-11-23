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

        /// <summary>The USB vendor ID of Coherent, which identifies OBIS lasers and controllers connected over USB.</summary>
        public const int CoherentVendorId = 0x0D4D;

        /// <summary>
        /// Lists the serial ports of OBIS lasers and controllers connected over USB, without opening any port.
        /// </summary>
        /// <remarks>
        /// Ports are recognized by Coherent's USB vendor ID. Devices connected over RS-232, for example
        /// through a USB adapter, are not listed: probe them with <see cref="FindAll(IEnumerable{string}, IEnumerable{string}, TimeSpan?)"/>. Only Windows
        /// reports the vendor ID of serial ports; on other operating systems the list is empty.
        /// </remarks>
        /// <returns>The port names, sorted.</returns>
        public static IReadOnlyList<string> GetCandidatePortNames()
        {
            var connected = new HashSet<string>(SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);
            return UsbSerialPorts.GetPortNames(CoherentVendorId)
                .Where(connected.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Finds OBIS lasers and controllers by probing serial ports.
        /// </summary>
        /// <remarks>
        /// Each port is opened and sent an identification query (<c>*IDN?</c>); ports that reply like a
        /// Coherent device are then queried for their type, model and serial number. Lasers behind a
        /// controller (OBIS Remote, LaserBox) are listed with their channel. Probing writes to every port
        /// in <paramref name="portNames"/>, which may disturb other devices; use <paramref name="portNames"/>
        /// or <paramref name="excludedPortNames"/> to limit this. Ports that are already in use are skipped.
        /// By default, only the ports of Coherent USB devices are probed (see <see cref="GetCandidatePortNames"/>),
        /// or every serial port if there are none. To also find devices connected over RS-232 while a
        /// Coherent USB device is connected, pass <see cref="SerialPort.GetPortNames"/>.
        /// </remarks>
        /// <param name="portNames">
        /// The ports to probe. Defaults to the ports of Coherent USB devices, or to every serial port if there are none.
        /// </param>
        /// <param name="excludedPortNames">The ports never to probe, e.g. <c>COM1</c> (case-insensitive).</param>
        /// <param name="timeout">The time to wait for each reply.</param>
        /// <returns>The devices found.</returns>
        public static IReadOnlyList<DiscoveredDevice> FindAll(
            IEnumerable<string>? portNames = null,
            IEnumerable<string>? excludedPortNames = null,
            TimeSpan? timeout = null)
        {
            return FindAll(
                portNames ?? DefaultPortNames(GetCandidatePortNames(), SerialPort.GetPortNames()),
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

        internal static IReadOnlyList<string> DefaultPortNames(IReadOnlyList<string> candidates, IReadOnlyList<string> allPortNames) =>
            candidates.Count > 0 ? candidates : allPortNames;

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
