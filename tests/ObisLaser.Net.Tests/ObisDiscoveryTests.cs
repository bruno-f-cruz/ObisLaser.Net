using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ObisLaser.Net.Tests
{
    [TestClass]
    public class ObisDiscoveryTests
    {
        [TestMethod]
        public void FindAll_FindsLasersBehindController()
        {
            // COM1 has a LaserBox with two lasers, COM2 an unrelated silent device, COM3 is in use.
            var laserBox = new FakeObisTransport(new()
            {
                ["*IDN?"] = "Coherent, Inc-OBIS LaserBox-V1.0-20200101",
                ["SYST:INF:TYP?"] = "MASTER",
                ["SYST:INF:MOD?"] = "OBIS LaserBox",
                ["SYST:INF:SNUM?"] = "B1",
                ["SYST1:INF:TYP?"] = "DDL",
                ["SYST1:INF:MOD?"] = "OBIS 405nm LX 100mW",
                ["SYST1:INF:SNUM?"] = "L1",
                ["SYST2:INF:TYP?"] = "OPSL",
                ["SYST2:INF:MOD?"] = "OBIS 561nm LS 50mW",
                ["SYST2:INF:SNUM?"] = "L2",
            });
            var other = new FakeObisTransport { Silent = true };
            IObisTransport Open(string port, TimeSpan timeout) => port switch
            {
                "COM1" => laserBox,
                "COM2" => other,
                _ => throw new UnauthorizedAccessException(),
            };

            var found = ObisDiscovery.FindAll(new[] { "COM1", "COM2", "COM3" }, Array.Empty<string>(), TimeSpan.FromMilliseconds(50), Open);

            CollectionAssert.AreEqual(
                new[]
                {
                    new DiscoveredDevice("COM1", 0, DeviceType.Master, "OBIS LaserBox", "B1"),
                    new DiscoveredDevice("COM1", 1, DeviceType.Ddl, "OBIS 405nm LX 100mW", "L1"),
                    new DiscoveredDevice("COM1", 2, DeviceType.Opsl, "OBIS 561nm LS 50mW", "L2"),
                },
                found.ToList());
            CollectionAssert.AreEqual(new[] { "*IDN?" }, other.Written);
        }

        [TestMethod]
        public void FindAll_SkipsExcludedPorts()
        {
            var excluded = new FakeObisTransport();
            var probed = new FakeObisTransport { Silent = true };
            IObisTransport Open(string port, TimeSpan timeout) => port == "COM1" ? excluded : probed;

            var found = ObisDiscovery.FindAll(new[] { "COM1", "COM2" }, new[] { "com1" }, TimeSpan.FromMilliseconds(50), Open);

            Assert.AreEqual(0, found.Count);
            Assert.AreEqual(0, excluded.Written.Count);
            CollectionAssert.AreEqual(new[] { "*IDN?" }, probed.Written);
        }
    }
}
