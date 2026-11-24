using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ObisLaser.Net.Tests
{
    [TestClass]
    public class ObisProtocolTests
    {
        [TestMethod]
        [DataRow("SOUR:AM:STAT ON", 0, "SOUR:AM:STAT ON")]
        [DataRow("SOUR:AM:STAT ON", 2, "SOUR2:AM:STAT ON")]
        [DataRow("*IDN?", 1, "*IDN1?")]
        [DataRow("SYST:INF:USER? 0", 3, "SYST3:INF:USER? 0")]
        [DataRow("*RST", 1, "*RST1")]
        [DataRow("SOUR:AM:STAT OFF", 255, "SOUR255:AM:STAT OFF")]
        public void Address_AppendsChannelToFirstWord(string command, int channel, string expected)
        {
            Assert.AreEqual(expected, ObisProtocol.Address(command, channel));
        }

        [TestMethod]
        [DataRow(-1)]
        [DataRow(256)]
        public void Address_RejectsInvalidChannel(int channel)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ObisProtocol.Address("*IDN?", channel));
        }

        [TestMethod]
        public void SetPower_FormatsWattsWithInvariantCulture()
        {
            Assert.AreEqual("SOUR:POW:LEV:IMM:AMPL 0.025000", ObisProtocol.SetPower(0.025).Command);
        }

        [TestMethod]
        [DataRow(-0.001)]
        [DataRow(double.NaN)]
        [DataRow(double.PositiveInfinity)]
        public void SetPower_RejectsInvalidValues(double watts)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ObisProtocol.SetPower(watts));
        }

        [TestMethod]
        [DataRow(OperatingMode.Cwp, "SOUR:AM:INT CWP")]
        [DataRow(OperatingMode.Cwc, "SOUR:AM:INT CWC")]
        [DataRow(OperatingMode.Digital, "SOUR:AM:EXT DIGITAL")]
        [DataRow(OperatingMode.Mixso, "SOUR:AM:EXT MIXSO")]
        public void SetOperatingMode_SelectsInternalOrExternal(OperatingMode mode, string command)
        {
            Assert.AreEqual(command, ObisProtocol.SetOperatingMode(mode).Command);
        }

        [TestMethod]
        [DataRow("25.1C", 25.1)]
        [DataRow("25.1", 25.1)]
        [DataRow("77.0F", 25.0)]
        [DataRow("-3.5c", -3.5)]
        public void Temperatures_AreParsedToCelsius(string reply, double celsius)
        {
            Assert.AreEqual(celsius, ObisProtocol.GetBaseplateTemperature().Parse(new[] { reply }), 1e-9);
        }

        [TestMethod]
        public void StatusWord_IsParsedFromHex()
        {
            var status = ObisProtocol.GetStatus().Parse(new[] { "00000012" });
            Assert.AreEqual(StatusFlags.LaserEmission | StatusFlags.CdrhDelay, status);
        }

        [TestMethod]
        public void UnknownFaultBits_AreKept()
        {
            var faults = ObisProtocol.GetFaults().Parse(new[] { "0x00080003" });
            Assert.IsTrue(faults.HasFlag(FaultFlags.BaseplateTemperature));
            Assert.AreEqual(0x00080003u, (uint)faults);
        }

        [TestMethod]
        public void ErrorRecord_IsParsed()
        {
            var record = ObisProtocol.GetNextError().Parse(new[] { "-220,\"Invalid parameter\"" });
            Assert.AreEqual(new ErrorRecord(-220, "Invalid parameter"), record);
        }

        [TestMethod]
        public void OnOff_RejectsOtherValues()
        {
            Assert.ThrowsExactly<ObisProtocolException>(() => ObisProtocol.GetEmission().Parse(new[] { "MAYBE" }));
        }

        [TestMethod]
        public void SingleLineReplies_RejectExtraLines()
        {
            Assert.ThrowsExactly<ObisProtocolException>(() => ObisProtocol.GetModel().Parse(new[] { "a", "b" }));
        }

        [TestMethod]
        [DataRow("DDL", DeviceType.Ddl)]
        [DataRow("OPSL", DeviceType.Opsl)]
        [DataRow("MINI", DeviceType.Mini)]
        [DataRow("MASTER", DeviceType.Master)]
        [DataRow("MULTI", DeviceType.Multi)]
        public void DeviceType_IsParsed(string reply, DeviceType expected)
        {
            Assert.AreEqual(expected, ObisProtocol.GetDeviceType().Parse(new[] { reply }));
        }

        [TestMethod]
        public void UnknownDeviceType_IsOther()
        {
            Assert.AreEqual(DeviceType.Other, ObisProtocol.GetDeviceType().Parse(new[] { "GALAXY" }));
        }

        [TestMethod]
        public void UserInfo_IsValidated()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ObisProtocol.SetUserInfo(4, "ok"));
            Assert.ThrowsExactly<ArgumentException>(() => ObisProtocol.SetUserInfo(0, new string('x', 32)));
            Assert.ThrowsExactly<ArgumentException>(() => ObisProtocol.SetUserInfo(0, "two\nlines"));
        }
    }
}
