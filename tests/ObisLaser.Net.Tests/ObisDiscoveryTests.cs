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
        public void FindAll_FindsLasersBehindMultiLaserBoxAcrossEmptySlots()
        {
            // Replies of an OBIS 5-Laser Box (TYP? MULTI): slot 2 is empty and replies ERR-241, slot 3 has a laser.
            var box = new FakeObisTransport(new()
            {
                ["*IDN?"] = "Coherent, Inc. - OBIS 5-LASER BOX - v1.0.5 - 20130109",
                ["SYST:INF:TYP?"] = "MULTI",
                ["SYST:INF:MOD?"] = "OBIS 5-LASER BOX",
                ["SYST:INF:SNUM?"] = "C230823001",
                ["SYST1:INF:TYP?"] = "OPSL",
                ["SYST1:INF:MOD?"] = "OBIS LS 561-150",
                ["SYST1:INF:SNUM?"] = "466789",
                ["SYST3:INF:TYP?"] = "DDL",
                ["SYST3:INF:MOD?"] = "514-50FP",
                ["SYST3:INF:SNUM?"] = "FS230321024",
            });
            foreach (var query in new[] { "TYP", "MOD", "SNUM" })
            {
                for (int channel = 2; channel <= ObisDiscovery.MaxChannels; channel += channel == 2 ? 2 : 1)
                {
                    box.Errors[string.Format("SYST{0}:INF:{1}?", channel, query)] = -241;
                }
            }

            var found = ObisDiscovery.FindAll(new[] { "COM6" }, Array.Empty<string>(), TimeSpan.FromMilliseconds(50), (port, timeout) => box);

            CollectionAssert.AreEqual(
                new[]
                {
                    new DiscoveredDevice("COM6", 0, DeviceType.Multi, "OBIS 5-LASER BOX", "C230823001"),
                    new DiscoveredDevice("COM6", 1, DeviceType.Opsl, "OBIS LS 561-150", "466789"),
                    new DiscoveredDevice("COM6", 3, DeviceType.Ddl, "514-50FP", "FS230321024"),
                },
                found.ToList());
        }

        [TestMethod]
        public void FindAll_StopsAtASlotThatDoesNotReply()
        {
            // A controller that stops replying at slot 2: the search ends there instead of waiting on every slot.
            var controller = new FakeObisTransport(new()
            {
                ["*IDN?"] = "Coherent, Inc-OBIS Remote-V1.0-20200101",
                ["SYST:INF:TYP?"] = "MINI",
                ["SYST:INF:MOD?"] = "OBIS Remote",
                ["SYST:INF:SNUM?"] = "R1",
                ["SYST1:INF:TYP?"] = "DDL",
                ["SYST1:INF:MOD?"] = "OBIS 405nm LX 100mW",
                ["SYST1:INF:SNUM?"] = "L1",
            })
            { SilentFrom = "SYST2:INF:TYP?" };

            var found = ObisDiscovery.FindAll(new[] { "COM1" }, Array.Empty<string>(), TimeSpan.FromMilliseconds(50), (port, timeout) => controller);

            Assert.AreEqual(2, found.Count);
            Assert.IsFalse(controller.Written.Any(command => command.StartsWith("SYST3", StringComparison.Ordinal)));
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

        [TestMethod]
        [DataRow("VID_0D4D&PID_003B", 0x0D4D)]
        [DataRow("vid_0d4d&pid_003b&MI_00", 0x0D4D)]
        [DataRow("VID_067B&PID_23A3", 0x067B)]
        [DataRow("ROOT_HUB30", null)]
        [DataRow("VID_XYZ", null)]
        public void ParseVendorId_ReadsTheVendorOfAUsbHardwareId(string hardwareId, int? expected)
        {
            Assert.AreEqual(expected, UsbSerialPorts.ParseVendorId(hardwareId));
        }

        [TestMethod]
        public void DefaultPortNames_PrefersCoherentPorts()
        {
            var all = new[] { "COM1", "COM10", "COM12" };
            CollectionAssert.AreEqual(new[] { "COM12" }, ObisDiscovery.DefaultPortNames(new[] { "COM12" }, all).ToArray());
            CollectionAssert.AreEqual(all, ObisDiscovery.DefaultPortNames(Array.Empty<string>(), all).ToArray());
        }

        [TestMethod]
        public void GetCandidatePortNames_OnlyListsConnectedPorts()
        {
            var connected = System.IO.Ports.SerialPort.GetPortNames();
            foreach (var portName in ObisDiscovery.GetCandidatePortNames())
            {
                CollectionAssert.Contains(connected, portName);
            }
        }
    }
}
