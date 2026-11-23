using System;

namespace ObisLaser.Net
{
    /// <summary>
    /// Specifies the bits of the system status word (manual Table 9-5).
    /// </summary>
    [Flags]
    public enum StatusFlags : uint
    {
        /// <summary>No status bit is set.</summary>
        None = 0,
        /// <summary>A laser fault is active.</summary>
        LaserFault = 1u << 0,
        /// <summary>Laser emission is on.</summary>
        LaserEmission = 1u << 1,
        /// <summary>The laser is ready.</summary>
        LaserReady = 1u << 2,
        /// <summary>The laser is in standby.</summary>
        LaserStandby = 1u << 3,
        /// <summary>Emission is delayed by the CDRH delay.</summary>
        CdrhDelay = 1u << 4,
        /// <summary>A hardware fault is active.</summary>
        LaserHardwareFault = 1u << 5,
        /// <summary>An error is queued.</summary>
        LaserError = 1u << 6,
        /// <summary>Laser power is within factory calibration specification.</summary>
        LaserPowerCalibration = 1u << 7,
        /// <summary>The laser is warming up.</summary>
        LaserWarmUp = 1u << 8,
        /// <summary>The noise level is above 30.</summary>
        LaserNoise = 1u << 9,
        /// <summary>An external operating mode is selected.</summary>
        ExternalOperatingMode = 1u << 10,
        /// <summary>A field calibration is in progress.</summary>
        FieldCalibration = 1u << 11,
        /// <summary>The 12 V laser power voltage is present.</summary>
        LaserPowerVoltage = 1u << 12,
        /// <summary>The controller keyswitch is in the standby position.</summary>
        ControllerStandby = 1u << 25,
        /// <summary>The controller interlock is open.</summary>
        ControllerInterlock = 1u << 26,
        /// <summary>One or more lasers have been enumerated by the controller.</summary>
        ControllerEnumeration = 1u << 27,
        /// <summary>The controller error flag is set.</summary>
        ControllerError = 1u << 28,
        /// <summary>A controller fault is active.</summary>
        ControllerFault = 1u << 29,
        /// <summary>A remote host is connected to the controller.</summary>
        RemoteActive = 1u << 30,
        /// <summary>The status word comes from a controller.</summary>
        ControllerIndicator = 1u << 31,
    }
}
