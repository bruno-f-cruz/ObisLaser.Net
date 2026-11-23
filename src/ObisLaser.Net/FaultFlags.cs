using System;

namespace ObisLaser.Net
{
    /// <summary>
    /// Specifies the bits of the system fault word (manual Tables 9-4 and 9-6).
    /// </summary>
    [Flags]
    public enum FaultFlags : uint
    {
        /// <summary>No fault is active.</summary>
        None = 0,
        /// <summary>Base plate temperature out of range.</summary>
        BaseplateTemperature = 1u << 0,
        /// <summary>Diode temperature out of range.</summary>
        DiodeTemperature = 1u << 1,
        /// <summary>Internal temperature out of range.</summary>
        InternalTemperature = 1u << 2,
        /// <summary>No electrical power to the laser diode.</summary>
        LaserPowerSupply = 1u << 3,
        /// <summary>Internal I2C bus error.</summary>
        I2CError = 1u << 4,
        /// <summary>Laser diode over current.</summary>
        OverCurrent = 1u << 5,
        /// <summary>Persistent memory checksum error.</summary>
        LaserChecksum = 1u << 6,
        /// <summary>Persistent memory was restored to default settings.</summary>
        ChecksumRecovery = 1u << 7,
        /// <summary>Message buffer overflow.</summary>
        BufferOverflow = 1u << 8,
        /// <summary>Warm-up time limit exceeded.</summary>
        WarmUpLimit = 1u << 9,
        /// <summary>Thermoelectric cooler driver failure.</summary>
        TecDriver = 1u << 10,
        /// <summary>Coherent Connection bus (RS-485) error.</summary>
        CcbError = 1u << 11,
        /// <summary>Diode temperature too far from its set point.</summary>
        DiodeTemperatureLimit = 1u << 12,
        /// <summary>The laser failed to emit at the requested power.</summary>
        LaserReady = 1u << 13,
        /// <summary>Negative photodiode reading.</summary>
        Photodiode = 1u << 14,
        /// <summary>Irrecoverable system failure.</summary>
        Fatal = 1u << 15,
        /// <summary>Errors during firmware start-up.</summary>
        Startup = 1u << 16,
        /// <summary>Firmware resumed from a watchdog reset.</summary>
        WatchdogReset = 1u << 17,
        /// <summary>Errors during field calibration.</summary>
        FieldCalibration = 1u << 18,
        /// <summary>Output power above limit.</summary>
        OverPower = 1u << 20,
        /// <summary>Controller checksum error.</summary>
        ControllerChecksum = 1u << 30,
        /// <summary>The fault word comes from a controller.</summary>
        ControllerStatus = 1u << 31,
    }
}
