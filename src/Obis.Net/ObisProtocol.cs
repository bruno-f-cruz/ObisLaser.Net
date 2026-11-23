using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Obis.Net
{
    /// <summary>
    /// Provides every command and query of the OBIS host protocol, independent of any I/O.
    /// </summary>
    /// <remarks>
    /// Each method returns an <see cref="ObisRequest{T}"/> pairing the command text with a
    /// parser for its reply. <see cref="ObisLaser"/> executes these requests, and they can also
    /// be executed directly with <see cref="ObisConnection.Execute{T}"/>. Command syntax follows
    /// chapter 9 ("Host Interface") of the OBIS LX/LS Operator's Manual. Some commands only exist
    /// on some device types (LX, LS or OBIS Remote); other devices reply with an error, raised
    /// as <see cref="ObisDeviceException"/>.
    /// </remarks>
    public static class ObisProtocol
    {
        /// <summary>The channel used to broadcast a command to every device on the bus.</summary>
        public const int BroadcastChannel = 255;

        /// <summary>The maximum number of characters of user information and field calibration dates.</summary>
        public const int MaxUserTextLength = 31;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>
        /// Routes a command to a device by appending <paramref name="channel"/> to its first word.
        /// </summary>
        /// <remarks>
        /// Channel 0 is the first connected device: the laser itself when connected directly, or the
        /// controller (OBIS Remote, LaserBox) otherwise. Lasers behind a controller are numbered from 1
        /// (manual section 9.2.5). For example, <c>SOUR:AM:STAT ON</c> on channel 2 becomes
        /// <c>SOUR2:AM:STAT ON</c>.
        /// </remarks>
        /// <param name="command">The command text.</param>
        /// <param name="channel">The channel of the device, from 0 to 255.</param>
        /// <returns>The command text addressed to the device.</returns>
        public static string Address(string command, int channel)
        {
            if (channel < 0 || channel > BroadcastChannel)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "The channel must be between 0 and 255.");
            }

            if (channel == 0) return command;
            var end = command.IndexOfAny(new[] { ':', '?', ' ' });
            if (end < 0) end = command.Length;
            return command.Substring(0, end) + channel.ToString(Invariant) + command.Substring(end);
        }

        // IEEE-488.2

        /// <summary>Gets the identification string, e.g. <c>Coherent, Inc-OBIS 405nm 50mW C-V1.3-20090630</c>.</summary>
        public static ObisRequest<string> Identify() => Query("*IDN?", ParseText);

        /// <summary>Warm-boots the device. Clears fault conditions.</summary>
        public static ObisRequest<bool> Reset() => Command("*RST");

        /// <summary>Runs the self-test. Returns <c>0xFFFFFFFF</c> if the device does not implement it.</summary>
        public static ObisRequest<FaultFlags> SelfTest() => Query("*TST?", lines => (FaultFlags)ParseHex(lines));

        // Session control

        /// <summary>Gets whether message handshaking is enabled.</summary>
        public static ObisRequest<bool> GetHandshaking() => Query("SYST:COMM:HAND?", ParseOnOff);

        /// <summary>Enables or disables message handshaking. Persistent. The clients in this package require it.</summary>
        public static ObisRequest<bool> SetHandshaking(bool enabled) => Command("SYST:COMM:HAND " + OnOff(enabled));

        /// <summary>Gets whether the <c>&gt;</c> command prompt is enabled.</summary>
        public static ObisRequest<bool> GetPrompt() => Query("SYST:COMM:PROM?", ParseOnOff);

        /// <summary>Enables or disables the <c>&gt;</c> command prompt used by terminal programs. Persistent.</summary>
        public static ObisRequest<bool> SetPrompt(bool enabled) => Command("SYST:COMM:PROM " + OnOff(enabled));

        // System information

        /// <summary>Gets the model name.</summary>
        public static ObisRequest<string> GetModel() => Query("SYST:INF:MOD?", ParseText);

        /// <summary>Gets the manufacture date (YYYYMMDD).</summary>
        public static ObisRequest<string> GetManufactureDate() => Query("SYST:INF:MDAT?", ParseText);

        /// <summary>Gets the calibration date (YYYYMMDD).</summary>
        public static ObisRequest<string> GetCalibrationDate() => Query("SYST:INF:CDAT?", ParseText);

        /// <summary>Gets the serial number.</summary>
        public static ObisRequest<string> GetSerialNumber() => Query("SYST:INF:SNUM?", ParseText);

        /// <summary>Gets the manufacturer part number.</summary>
        public static ObisRequest<string> GetPartNumber() => Query("SYST:INF:PNUM?", ParseText);

        /// <summary>Gets the firmware version.</summary>
        public static ObisRequest<string> GetFirmwareVersion() => Query("SYST:INF:FVER?", ParseText);

        /// <summary>Gets the OBIS protocol version.</summary>
        public static ObisRequest<string> GetProtocolVersion() => Query("SYST:INF:PVER?", ParseText);

        /// <summary>Gets the nominal wavelength, in nanometers.</summary>
        public static ObisRequest<double> GetWavelength() => Query("SYST:INF:WAV?", ParseDouble);

        /// <summary>Gets the power rating, in watts.</summary>
        public static ObisRequest<double> GetPowerRating() => Query("SYST:INF:POW?", ParseDouble);

        /// <summary>Gets the device type.</summary>
        public static ObisRequest<DeviceType> GetDeviceType() => Query("SYST:INF:TYP?", ParseDeviceType);

        /// <summary>Gets the nominal CW output power, in watts.</summary>
        public static ObisRequest<double> GetNominalPower() => Query("SOUR:POW:NOM?", ParseDouble);

        /// <summary>Gets the minimum CW output power, in watts.</summary>
        public static ObisRequest<double> GetMinPower() => Query("SOUR:POW:LIM:LOW?", ParseDouble);

        /// <summary>Gets the maximum CW output power, in watts.</summary>
        public static ObisRequest<double> GetMaxPower() => Query("SOUR:POW:LIM:HIGH?", ParseDouble);

        /// <summary>Gets the user-defined text stored at <paramref name="index"/> (0 to 3).</summary>
        public static ObisRequest<string> GetUserInfo(int index)
        {
            CheckUserIndex(index);
            return Query("SYST:INF:USER? " + index.ToString(Invariant), ParseText);
        }

        /// <summary>Stores up to 31 characters of user-defined text at <paramref name="index"/> (0 to 3). Persistent.</summary>
        public static ObisRequest<bool> SetUserInfo(int index, string text)
        {
            CheckUserIndex(index);
            CheckUserText(text);
            return Command("SYST:INF:USER " + index.ToString(Invariant) + "," + text);
        }

        /// <summary>Gets the date of the last field calibration.</summary>
        public static ObisRequest<string> GetFieldCalibrationDate() => Query("SYST:INF:FCD?", ParseText);

        /// <summary>Stores the date of the last field calibration (up to 31 characters). Persistent.</summary>
        public static ObisRequest<bool> SetFieldCalibrationDate(string text)
        {
            CheckUserText(text);
            return Command("SYST:INF:FCD " + text);
        }

        // System state

        /// <summary>Gets the system status word.</summary>
        public static ObisRequest<StatusFlags> GetStatus() => Query("SYST:STAT?", lines => (StatusFlags)ParseHex(lines));

        /// <summary>Gets the system fault word.</summary>
        public static ObisRequest<FaultFlags> GetFaults() => Query("SYST:FAUL?", lines => (FaultFlags)ParseHex(lines));

        /// <summary>Gets the number of times the device has been powered on.</summary>
        public static ObisRequest<int> GetPowerCycles() => Query("SYST:CYCL?", ParseInt);

        /// <summary>Gets the number of hours the device has been powered on.</summary>
        public static ObisRequest<double> GetHours() => Query("SYST:HOUR?", ParseDouble);

        /// <summary>Gets the number of hours the laser diode has been emitting.</summary>
        public static ObisRequest<double> GetDiodeHours() => Query("SYST:DIOD:HOUR?", ParseDouble);

        /// <summary>Gets the measured output power, in watts.</summary>
        public static ObisRequest<double> GetPower() => Query("SOUR:POW:LEV?", ParseDouble);

        /// <summary>Gets the laser diode current, in amperes.</summary>
        public static ObisRequest<double> GetCurrent() => Query("SOUR:POW:CURR?", ParseDouble);

        /// <summary>Gets the base plate temperature, in degrees Celsius.</summary>
        public static ObisRequest<double> GetBaseplateTemperature() => Query("SOUR:TEMP:BAS?", ParseCelsius);

        /// <summary>Gets the OBIS Remote interlock state: <see langword="true"/> when closed (emission allowed).</summary>
        public static ObisRequest<bool> GetInterlock() => Query("SYST:LOCK?", ParseOnOff);

        /// <summary>Gets the relative power noise level (constant power mode). Above 30 is considered noisy.</summary>
        public static ObisRequest<int> GetNoise() => Query("SYST:NOIS?", ParseInt);

        /// <summary>Gets the laser diode threshold current, in amperes.</summary>
        public static ObisRequest<double> GetThresholdCurrent() => Query("SOUR:CURR:LIM:LOW?", ParseDouble);

        /// <summary>Gets the laser diode upper current limit, in amperes (OBIS LX, firmware 2.x or later).</summary>
        public static ObisRequest<double> GetMaxCurrent() => Query("SOUR:CURR:LIM:HIGH?", ParseDouble);

        /// <summary>Gets the lowest base plate temperature allowed, in degrees Celsius.</summary>
        public static ObisRequest<double> GetBaseplateTemperatureLowLimit() => Query("SOUR:TEMP:PROT:BAS:LOW?", ParseCelsius);

        /// <summary>Gets the highest base plate temperature allowed, in degrees Celsius.</summary>
        public static ObisRequest<double> GetBaseplateTemperatureHighLimit() => Query("SOUR:TEMP:PROT:BAS:HIGH?", ParseCelsius);

        /// <summary>Gets the lowest diode temperature allowed, in degrees Celsius (OBIS LX).</summary>
        public static ObisRequest<double> GetDiodeTemperatureLowLimit() => Query("SOUR:TEMP:PROT:DIOD:LOW?", ParseCelsius);

        /// <summary>Gets the highest diode temperature allowed, in degrees Celsius (OBIS LX).</summary>
        public static ObisRequest<double> GetDiodeTemperatureHighLimit() => Query("SOUR:TEMP:PROT:DIOD:HIGH?", ParseCelsius);

        /// <summary>Gets the lowest internal temperature allowed, in degrees Celsius (OBIS LX).</summary>
        public static ObisRequest<double> GetInternalTemperatureLowLimit() => Query("SOUR:TEMP:PROT:INT:LOW?", ParseCelsius);

        /// <summary>Gets the highest internal temperature allowed, in degrees Celsius (OBIS LX).</summary>
        public static ObisRequest<double> GetInternalTemperatureHighLimit() => Query("SOUR:TEMP:PROT:INT:HIGH?", ParseCelsius);

        /// <summary>Gets the laser diode temperature, in degrees Celsius (OBIS LX).</summary>
        public static ObisRequest<double> GetDiodeTemperature() => Query("SOUR:TEMP:DIOD?", ParseCelsius);

        /// <summary>Gets the TEC set point of the laser diode, in degrees Celsius (OBIS LX).</summary>
        /// <remarks>
        /// The manual lists both <c>SOURce:TEMPerature:DSETpoint?</c> and
        /// <c>SOURce:TEMPerature:DIODe:DSETpoint?</c>; the former is used here.
        /// </remarks>
        public static ObisRequest<double> GetDiodeTemperatureSetpoint() => Query("SOUR:TEMP:DSET?", ParseCelsius);

        /// <summary>Gets the internal (microprocessor) temperature, in degrees Celsius (OBIS LX).</summary>
        public static ObisRequest<double> GetInternalTemperature() => Query("SOUR:TEMP:INT?", ParseCelsius);

        // Error queue

        /// <summary>Gets the number of records in the error queue.</summary>
        public static ObisRequest<int> GetErrorCount() => Query("SYST:ERR:COUN?", ParseInt);

        /// <summary>Removes and returns the oldest record of the error queue. Only valid if the queue is not empty.</summary>
        public static ObisRequest<ErrorRecord> GetNextError() => Query("SYST:ERR:NEXT?", ParseErrorRecord);

        /// <summary>Clears the error queue.</summary>
        public static ObisRequest<bool> ClearErrors() => Command("SYST:ERR:CLE");

        // Operation

        /// <summary>Gets the operating mode.</summary>
        public static ObisRequest<OperatingMode> GetOperatingMode() => Query("SOUR:AM:SOUR?", ParseOperatingMode);

        /// <summary>Sets the operating mode. Persistent. External modes require an OBIS Remote.</summary>
        public static ObisRequest<bool> SetOperatingMode(OperatingMode mode)
        {
            var selector = mode is OperatingMode.Cwp or OperatingMode.Cwc ? "INT" : "EXT";
            return Command("SOUR:AM:" + selector + " " + FormatOperatingMode(mode));
        }

        /// <summary>Gets the requested output power, in watts.</summary>
        public static ObisRequest<double> GetPowerSetpoint() => Query("SOUR:POW:LEV:IMM:AMPL?", ParseDouble);

        /// <summary>Sets the output power, in watts. Does not turn emission on.</summary>
        public static ObisRequest<bool> SetPower(double watts)
        {
            if (double.IsNaN(watts) || double.IsInfinity(watts) || watts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(watts), watts, "The power must be a finite, non-negative number of watts.");
            }

            return Command("SOUR:POW:LEV:IMM:AMPL " + watts.ToString("F6", Invariant));
        }

        /// <summary>Gets whether emission is turned on.</summary>
        public static ObisRequest<bool> GetEmission() => Query("SOUR:AM:STAT?", ParseOnOff);

        /// <summary>Turns emission on or off. Emission may be delayed by warm-up and the CDRH delay.</summary>
        public static ObisRequest<bool> SetEmission(bool enabled) => Command("SOUR:AM:STAT " + OnOff(enabled));

        // Persistent settings

        /// <summary>Gets whether the laser starts emitting at power-up.</summary>
        public static ObisRequest<bool> GetAutoStart() => Query("SYST:AUT?", ParseOnOff);

        /// <summary>Sets whether the laser starts emitting at power-up. Persistent.</summary>
        public static ObisRequest<bool> SetAutoStart(bool enabled) => Command("SYST:AUT " + OnOff(enabled));

        /// <summary>Gets whether the 5 s CDRH emission delay is enabled.</summary>
        public static ObisRequest<bool> GetCdrhDelay() => Query("SYST:CDRH?", ParseOnOff);

        /// <summary>Enables or disables the CDRH emission delay. Persistent. Disabling it makes the system non-CDRH compliant.</summary>
        public static ObisRequest<bool> SetCdrhDelay(bool enabled) => Command("SYST:CDRH " + OnOff(enabled));

        /// <summary>Gets whether the TEC (LX) or thermal stabilization (LS) is enabled.</summary>
        public static ObisRequest<bool> GetTecEnabled() => Query("SOUR:TEMP:APR?", ParseOnOff);

        /// <summary>Enables or disables the TEC (LX) or thermal stabilization (LS). Off puts an LX in sleep mode.</summary>
        public static ObisRequest<bool> SetTecEnabled(bool enabled) => Command("SOUR:TEMP:APR " + OnOff(enabled));

        /// <summary>Gets whether emission waits for the diode warm-up (OBIS LX).</summary>
        public static ObisRequest<bool> GetDiodeWarmup() => Query("SYST:DIOD:WARM?", ParseOnOff);

        /// <summary>Sets whether emission waits for the diode warm-up (OBIS LX). Persistent.</summary>
        public static ObisRequest<bool> SetDiodeWarmup(bool enabled) => Command("SYST:DIOD:WARM " + OnOff(enabled));

        /// <summary>Gets whether the status LED is enabled.</summary>
        public static ObisRequest<bool> GetStatusIndicator() => Query("SYST:IND:LAS?", ParseOnOff);

        /// <summary>Turns the status LED on or off; status bits are not affected. Persistent.</summary>
        public static ObisRequest<bool> SetStatusIndicator(bool enabled) => Command("SYST:IND:LAS " + OnOff(enabled));

        /// <summary>Gets whether blanking is enabled in analog modulation modes.</summary>
        public static ObisRequest<bool> GetAnalogBlanking() => Query("SOUR:AM:BLAN?", ParseOnOff);

        /// <summary>Enables or disables blanking in analog modulation modes.</summary>
        public static ObisRequest<bool> SetAnalogBlanking(bool enabled) => Command("SOUR:AM:BLAN " + OnOff(enabled));

        /// <summary>Gets the OBIS Remote analog input impedance.</summary>
        public static ObisRequest<AnalogImpedance> GetAnalogImpedance() => Query("SYST:INF:AMOD:TYP?", lines => (AnalogImpedance)ParseInt(lines));

        /// <summary>Selects the OBIS Remote analog input impedance. Persistent.</summary>
        public static ObisRequest<bool> SetAnalogImpedance(AnalogImpedance impedance) =>
            Command("SYST:INF:AMOD:TYP " + ((int)impedance).ToString(Invariant));

        // Maintenance

        /// <summary>Starts a power field calibration (OBIS LX). Takes minutes; do not cut power.</summary>
        /// <remarks>Progress is reported by <see cref="StatusFlags.FieldCalibration"/>.</remarks>
        public static ObisRequest<bool> StartFieldCalibration() => Command("SOUR:POW:CAL");

        /// <summary>Undoes the last field calibration (OBIS LX).</summary>
        public static ObisRequest<bool> UndoFieldCalibration() => Command("SOUR:POW:UNC");

        /// <summary>Recovers from a persistent checksum error, restoring factory default settings.</summary>
        /// <remarks>The manual advises contacting Coherent before using this command.</remarks>
        public static ObisRequest<bool> Recover() => Command("SYST:REC");

        // Helpers

        static ObisRequest<T> Query<T>(string command, Func<IReadOnlyList<string>, T> parse) => new(command, parse);

        // Commands have no reply value; true is returned so that every request has a result.
        static ObisRequest<bool> Command(string command) => new(command, _ => true);

        static string OnOff(bool enabled) => enabled ? "ON" : "OFF";

        static string ParseText(IReadOnlyList<string> lines)
        {
            if (lines.Count != 1)
            {
                throw new ObisProtocolException($"Expected a single reply line, got {lines.Count}: '{string.Join("|", lines)}'.");
            }

            return lines[0];
        }

        static double ParseDouble(IReadOnlyList<string> lines) => ParseNumber(ParseText(lines));

        static int ParseInt(IReadOnlyList<string> lines)
        {
            var text = ParseText(lines);
            if (!int.TryParse(text, NumberStyles.Integer, Invariant, out var value))
            {
                throw new ObisProtocolException($"Expected an integer, got '{text}'.");
            }

            return value;
        }

        static uint ParseHex(IReadOnlyList<string> lines)
        {
            var text = ParseText(lines);
            var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text;
            if (!uint.TryParse(digits, NumberStyles.AllowHexSpecifier, Invariant, out var value))
            {
                throw new ObisProtocolException($"Expected a hexadecimal number, got '{text}'.");
            }

            return value;
        }

        static bool ParseOnOff(IReadOnlyList<string> lines)
        {
            var text = ParseText(lines).ToUpperInvariant();
            return text switch
            {
                "ON" => true,
                "OFF" => false,
                _ => throw new ObisProtocolException($"Expected ON or OFF, got '{text}'."),
            };
        }

        // Parses a temperature such as "25.1C" (or "77.2F") into degrees Celsius.
        static double ParseCelsius(IReadOnlyList<string> lines)
        {
            var text = ParseText(lines).Trim();
            var unit = char.ToUpperInvariant(text.LastOrDefault());
            if (unit == 'F') return (ParseNumber(text.Substring(0, text.Length - 1)) - 32.0) * 5.0 / 9.0;
            if (unit == 'C') return ParseNumber(text.Substring(0, text.Length - 1));
            return ParseNumber(text);
        }

        static double ParseNumber(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, Invariant, out var value))
            {
                throw new ObisProtocolException($"Expected a number, got '{text}'.");
            }

            return value;
        }

        static ErrorRecord ParseErrorRecord(IReadOnlyList<string> lines)
        {
            var text = ParseText(lines);
            var comma = text.IndexOf(',');
            var code = comma < 0 ? text : text.Substring(0, comma);
            var message = comma < 0 ? string.Empty : text.Substring(comma + 1).Trim().Trim('"');
            if (!int.TryParse(code, NumberStyles.Integer, Invariant, out var value))
            {
                throw new ObisProtocolException($"Malformed error record '{text}'.");
            }

            return new ErrorRecord(value, message);
        }

        static DeviceType ParseDeviceType(IReadOnlyList<string> lines)
        {
            return ParseText(lines).ToUpperInvariant() switch
            {
                "DDL" => DeviceType.Ddl,
                "OPSL" => DeviceType.Opsl,
                "MINI" => DeviceType.Mini,
                "MASTER" => DeviceType.Master,
                _ => DeviceType.Other,
            };
        }

        static OperatingMode ParseOperatingMode(IReadOnlyList<string> lines)
        {
            var text = ParseText(lines).ToUpperInvariant();
            return text switch
            {
                "CWP" => OperatingMode.Cwp,
                "CWC" => OperatingMode.Cwc,
                "DIGITAL" => OperatingMode.Digital,
                "ANALOG" => OperatingMode.Analog,
                "MIXED" => OperatingMode.Mixed,
                "DIGSO" => OperatingMode.Digso,
                "MIXSO" => OperatingMode.Mixso,
                _ => throw new ObisProtocolException($"Unknown operating mode '{text}'."),
            };
        }

        static string FormatOperatingMode(OperatingMode mode)
        {
            return mode switch
            {
                OperatingMode.Cwp => "CWP",
                OperatingMode.Cwc => "CWC",
                OperatingMode.Digital => "DIGITAL",
                OperatingMode.Analog => "ANALOG",
                OperatingMode.Mixed => "MIXED",
                OperatingMode.Digso => "DIGSO",
                OperatingMode.Mixso => "MIXSO",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown operating mode."),
            };
        }

        static void CheckUserIndex(int index)
        {
            if (index < 0 || index > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The user information index must be between 0 and 3.");
            }
        }

        static void CheckUserText(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (text.Length > MaxUserTextLength || text.Any(c => c > 127 || c == '\r' || c == '\n'))
            {
                throw new ArgumentException($"The text must be a single line of at most {MaxUserTextLength} ASCII characters.", nameof(text));
            }
        }
    }
}
