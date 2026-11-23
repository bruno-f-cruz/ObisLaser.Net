using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ObisLaser.Net.Tests
{
    [TestClass]
    public class LaserTests
    {
        static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

        static FakeObisTransport CreateDevice() => new(new Dictionary<string, string>
        {
            ["SYST:INF:MOD?"] = "OBIS 488nm LX 50mW",
            ["SYST:INF:SNUM?"] = "123456",
            ["SYST:INF:PNUM?"] = "1234567",
            ["SYST:INF:FVER?"] = "V3.0.5",
            ["SYST:INF:PVER?"] = "P1.0a",
            ["SYST:INF:TYP?"] = "DDL",
            ["SYST:INF:WAV?"] = "488",
            ["SYST:INF:POW?"] = "0.050",
            ["SOUR:POW:NOM?"] = "0.05000",
            ["SOUR:POW:LIM:LOW?"] = "0.00000",
            ["SOUR:POW:LIM:HIGH?"] = "0.05500",
            ["SYST:INF:MDAT?"] = "20200102",
            ["SYST:INF:CDAT?"] = "20200103",
            ["SOUR:AM:STAT?"] = "ON",
            ["SOUR:AM:SOUR?"] = "CWP",
            ["SOUR:POW:LEV:IMM:AMPL?"] = "0.02000",
            ["SOUR:POW:LEV?"] = "0.01998",
            ["SOUR:POW:CURR?"] = "0.06100",
            ["SOUR:TEMP:BAS?"] = "28.4C",
            ["SYST:STAT?"] = "00000006",
            ["SYST:FAUL?"] = "00000000",
        });

        static Laser CreateLaser(FakeObisTransport device, int channel = 0)
        {
            return new ObisConnection(device, Timeout).GetLaser(channel);
        }

        [TestMethod]
        public void Connecting_DoesNotRewriteHandshakingWhenOn()
        {
            var device = CreateDevice();
            using var connection = new ObisConnection(device, Timeout);
            CollectionAssert.AreEqual(new[] { "SYST:COMM:HAND?" }, device.Written);
        }

        [TestMethod]
        public void Connecting_EnablesHandshakingWhenOff()
        {
            var device = new FakeObisTransport(new() { ["SYST:COMM:HAND?"] = "OFF" }) { Handshaking = false };
            using var connection = new ObisConnection(device, Timeout);
            CollectionAssert.AreEqual(new[] { "SYST:COMM:HAND?", "SYST:COMM:HAND ON" }, device.Written);
            Assert.IsTrue(device.Handshaking);
        }

        [TestMethod]
        public void GetInfo_ReadsStaticInformation()
        {
            var info = CreateLaser(CreateDevice()).GetInfo();
            Assert.AreEqual("OBIS 488nm LX 50mW", info.Model);
            Assert.AreEqual(DeviceType.Ddl, info.DeviceType);
            Assert.AreEqual(488, info.Wavelength);
            Assert.AreEqual(0.055, info.MaxPower, 1e-9);
            Assert.AreEqual("20200103", info.CalibrationDate);
        }

        [TestMethod]
        public void GetState_ReadsDynamicState()
        {
            var state = CreateLaser(CreateDevice()).GetState();
            Assert.IsTrue(state.Emission);
            Assert.AreEqual(OperatingMode.Cwp, state.OperatingMode);
            Assert.AreEqual(0.01998, state.Power, 1e-9);
            Assert.AreEqual(28.4, state.BaseplateTemperature, 1e-9);
            Assert.AreEqual(StatusFlags.LaserEmission | StatusFlags.LaserReady, state.Status);
            Assert.AreEqual(FaultFlags.None, state.Faults);
        }

        [TestMethod]
        public void SetPowerAndEmission_SendCommands()
        {
            var device = CreateDevice();
            var laser = CreateLaser(device);
            laser.SetPower(0.02);
            laser.SetEmission(true);
            CollectionAssert.AreEqual(
                new[] { "SOUR:POW:LEV:IMM:AMPL 0.020000", "SOUR:AM:STAT ON" },
                device.Written.Skip(1).ToList());
        }

        [TestMethod]
        public void LaserBehindController_UsesItsChannel()
        {
            var device = CreateDevice();
            device.Replies["SOUR2:AM:STAT?"] = "OFF";
            var laser = CreateLaser(device, channel: 2);
            Assert.IsFalse(laser.GetEmission());
            laser.SetEmission(true);
            Assert.AreEqual("SOUR2:AM:STAT ON", device.Written.Last());
        }

        [TestMethod]
        public void DeviceErrors_AreThrown()
        {
            var device = CreateDevice();
            device.Errors["SOUR:POW:LEV:IMM:AMPL 1.000000"] = -220;
            var error = Assert.ThrowsExactly<ObisDeviceException>(() => CreateLaser(device).SetPower(1.0));
            Assert.AreEqual(-220, error.Code);
            Assert.AreEqual("Invalid parameter", error.Description);
        }

        [TestMethod]
        public void MissingReply_TimesOut()
        {
            var device = CreateDevice();
            var laser = CreateLaser(device);
            device.Silent = true;
            Assert.ThrowsExactly<ObisTimeoutException>(() => laser.GetEmission());
        }

        [TestMethod]
        public void LateCommandPrompt_IsIgnored()
        {
            var device = CreateDevice();
            var laser = CreateLaser(device);
            device.KeepInputOnDiscard = true;
            device.EnqueuePrompt();
            Assert.IsTrue(laser.GetEmission());
        }

        [TestMethod]
        public void GetErrors_ReadsTheWholeQueue()
        {
            var device = CreateDevice();
            device.Replies["SYST:ERR:COUN?"] = "2";
            device.Replies["SYST:ERR:NEXT?"] = "-102,\"Syntax error\"";
            var errors = CreateLaser(device).GetErrors();
            CollectionAssert.AreEqual(new[] { -102, -102 }, errors.Select(e => e.Code).ToList());
        }

        [TestMethod]
        public void Dispose_ClosesOwnedConnectionOnly()
        {
            var device = CreateDevice();
            using (var connection = new ObisConnection(device, Timeout))
            {
                connection.GetLaser().Dispose();
                Assert.IsFalse(device.Disposed);
            }

            Assert.IsTrue(device.Disposed);
        }

        [TestMethod]
        public async Task AsyncMethods_MirrorSyncApi()
        {
            var device = CreateDevice();
            var laser = CreateLaser(device);
            Assert.AreEqual(OperatingMode.Cwp, await laser.GetOperatingModeAsync());
            Assert.AreEqual(28.4, (await laser.GetStateAsync()).BaseplateTemperature, 1e-9);
            await laser.SetEmissionAsync(false);
            Assert.AreEqual("SOUR:AM:STAT OFF", device.Written.Last());
        }

        [TestMethod]
        public async Task ConcurrentAsyncRequests_RunInOrder()
        {
            var device = CreateDevice();
            var laser = CreateLaser(device);
            await Task.WhenAll(Enumerable.Range(0, 20).Select(i => laser.SetPowerAsync(i / 1000.0)));
            var expected = Enumerable.Range(0, 20).Select(i => $"SOUR:POW:LEV:IMM:AMPL {i / 1000.0:F6}").ToList();
            CollectionAssert.AreEqual(expected, device.Written.Skip(1).ToList());
        }

        [TestMethod]
        public void Connection_IsThreadSafe()
        {
            var laser = CreateLaser(CreateDevice());
            var results = new bool[200];
            Parallel.For(0, results.Length, i =>
            {
                results[i] = laser.GetEmission();
                laser.SetPower(0.01);
            });
            Assert.IsTrue(results.All(emission => emission));
        }
    }
}
