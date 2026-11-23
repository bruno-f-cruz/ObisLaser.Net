using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ObisLaser.Net.Tests
{
    /// <summary>
    /// Tests against a real OBIS laser. Skipped unless <c>OBIS_PORT</c> is set, and always skipped
    /// in CI (<c>CI</c> is set).
    /// </summary>
    /// <remarks>
    /// <code>
    /// $env:OBIS_PORT = "COM3"
    /// dotnet test --filter TestCategory=Hardware --logger "console;verbosity=detailed"
    /// </code>
    /// Optional environment variables: <c>OBIS_CHANNEL</c>, the channel of the laser behind an
    /// OBIS Remote or LaserBox (default 0); <c>OBIS_TEST_EMISSION=1</c>, to also run the test that
    /// turns emission ON at 5 % of the maximum power. Make sure the beam is safely blocked first.
    /// Only settings that are restored to their original value are written.
    /// </remarks>
    [TestClass]
    [TestCategory("Hardware")]
    [DoNotParallelize]
    public class HardwareTests
    {
        static readonly string? Port = Environment.GetEnvironmentVariable("OBIS_PORT");
        static readonly int Channel = int.TryParse(Environment.GetEnvironmentVariable("OBIS_CHANNEL"), out var channel) ? channel : 0;
        static readonly bool TestEmission = Environment.GetEnvironmentVariable("OBIS_TEST_EMISSION") == "1";

        Laser laser = null!;

        [TestInitialize]
        public void Initialize()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            {
                Assert.Inconclusive("Hardware tests never run in CI.");
            }

            if (string.IsNullOrEmpty(Port))
            {
                Assert.Inconclusive("Set OBIS_PORT to test with a real laser.");
            }

            laser = Laser.Open(Port!, Channel);
        }

        [TestCleanup]
        public void Cleanup() => laser?.Dispose();

        [TestMethod]
        public void Identify_ReturnsCoherentIdentity()
        {
            var identity = laser.Identify();
            Console.WriteLine(identity);
            Assert.IsTrue(identity.StartsWith("Coherent", StringComparison.Ordinal));
        }

        [TestMethod]
        public void GetInfo_IsConsistent()
        {
            var info = laser.GetInfo();
            Console.WriteLine(info);
            Assert.IsTrue(info.DeviceType is DeviceType.Ddl or DeviceType.Opsl);
            Assert.IsTrue(info.Wavelength is > 200 and < 2000);
            Assert.IsTrue(info.MinPower >= 0 && info.MinPower <= info.NominalPower && info.NominalPower <= info.MaxPower);
            Assert.IsFalse(string.IsNullOrEmpty(info.SerialNumber));
        }

        [TestMethod]
        public void GetState_IsReadable()
        {
            var state = laser.GetState();
            Console.WriteLine(state);
            Assert.IsTrue(state.BaseplateTemperature is > 0 and < 60);
            Assert.IsTrue(state.Power >= 0);
            Assert.IsTrue(state.Current >= 0);
        }

        [TestMethod]
        public void Settings_AreReadable()
        {
            Console.WriteLine($"AutoStart={laser.GetAutoStart()} CdrhDelay={laser.GetCdrhDelay()} " +
                $"TecEnabled={laser.GetTecEnabled()} StatusIndicator={laser.GetStatusIndicator()} " +
                $"Hours={laser.GetHours()} BaseplateLimits={laser.GetBaseplateTemperatureLimits()}");
            Assert.IsTrue(laser.GetPowerCycles() > 0);
        }

        [TestMethod]
        public void DeviceErrors_AreReported()
        {
            laser.ClearErrors();
            Assert.ThrowsExactly<ObisDeviceException>(() => laser.Query("NOT:A:REAL:COMMAND?"));
            laser.GetEmission(); // the connection is still in sync
            laser.ClearErrors();
            Assert.AreEqual(0, laser.GetErrors().Count);
        }

        [TestMethod]
        public void PowerSetpoint_RoundTrips()
        {
            var original = laser.GetPowerSetpoint();
            var target = laser.GetInfo().MaxPower * 0.05;
            try
            {
                laser.SetPower(target);
                Assert.AreEqual(target, laser.GetPowerSetpoint(), Math.Max(target * 0.01, 1e-4));
            }
            finally
            {
                laser.SetPower(original);
            }

            Assert.AreEqual(original, laser.GetPowerSetpoint(), Math.Max(original * 0.01, 1e-4));
        }

        [TestMethod]
        public void OperatingMode_RoundTrips()
        {
            var mode = laser.GetOperatingMode();
            laser.SetOperatingMode(mode);
            Assert.AreEqual(mode, laser.GetOperatingMode());
        }

        [TestMethod]
        public void ExchangeLatency_IsSmall()
        {
            const int Count = 50;
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < Count; i++)
            {
                laser.GetPower();
            }

            var average = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / Count);
            Console.WriteLine($"Average exchange: {average.TotalMilliseconds:F2} ms");
            Assert.IsTrue(average < TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public async Task AsyncMethods_Work()
        {
            var stateTask = laser.GetStateAsync();
            var infoTask = laser.GetInfoAsync();
            var state = await stateTask;
            var info = await infoTask;
            Assert.IsTrue(info.MaxPower > 0);
            Assert.IsTrue(state.Power >= 0);
        }

        [TestMethod]
        public void Discovery_FindsTheLaser()
        {
            laser.Dispose(); // free the port so that discovery can open it
            var devices = ObisDiscovery.FindAll(portNames: new[] { Port! });
            foreach (var device in devices) Console.WriteLine(device);
            Assert.IsTrue(devices.Any(device => device.Channel == Channel));
        }

        [TestMethod]
        public void Discovery_ProbesCoherentUsbPortsByDefault()
        {
            laser.Dispose(); // free the port so that discovery can open it
            var candidates = ObisDiscovery.GetCandidatePortNames();
            Console.WriteLine($"Coherent USB ports: {string.Join(", ", candidates)}");
            if (!candidates.Contains(Port!, StringComparer.OrdinalIgnoreCase))
            {
                Assert.Inconclusive($"{Port} is not a Coherent USB port (e.g. RS-232 through an adapter).");
            }

            var devices = ObisDiscovery.FindAll();
            foreach (var device in devices) Console.WriteLine(device);
            Assert.IsTrue(devices.All(device => candidates.Contains(device.PortName)));
            Assert.IsTrue(devices.Any(device => device.PortName == Port && device.Channel == Channel));
        }

        [TestMethod]
        public void Emission_ReachesRequestedPower()
        {
            if (!TestEmission)
            {
                Assert.Inconclusive("Set OBIS_TEST_EMISSION=1 to run tests that turn emission on.");
            }

            var originalMode = laser.GetOperatingMode();
            var originalPower = laser.GetPowerSetpoint();
            var target = laser.GetInfo().MaxPower * 0.05;
            try
            {
                laser.SetOperatingMode(OperatingMode.Cwp);
                laser.SetPower(target);
                laser.SetEmission(true);
                Assert.IsTrue(laser.GetEmission());

                // Emission can be delayed by warm-up and the 5 s CDRH delay.
                var stopwatch = Stopwatch.StartNew();
                while (!laser.GetStatus().HasFlag(StatusFlags.LaserReady))
                {
                    Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(30), "The laser did not become ready within 30 s.");
                    Thread.Sleep(200);
                }

                Thread.Sleep(1000);
                var power = laser.GetPower();
                Console.WriteLine($"Set {target * 1000:F2} mW, measured {power * 1000:F2} mW");
                Assert.AreEqual(target, power, Math.Max(target * 0.1, 1e-3));
            }
            finally
            {
                laser.SetEmission(false);
                laser.SetPower(originalPower);
                laser.SetOperatingMode(originalMode);
            }

            Assert.IsFalse(laser.GetEmission());
        }
    }
}
