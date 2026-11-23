using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Obis.Net
{
    /// <summary>
    /// Represents one OBIS laser.
    /// </summary>
    /// <remarks>
    /// Power is in watts, current in amperes and temperatures in degrees Celsius. Settings documented
    /// as persistent are saved in the laser's non-volatile memory. Every method has an asynchronous
    /// counterpart; requests run one at a time, in the order they are made.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var laser = ObisLaser.Open("COM3");
    /// Console.WriteLine(laser.GetInfo());
    /// laser.SetPower(0.010);
    /// laser.SetEmission(true);
    /// </code>
    /// </example>
    public sealed class ObisLaser : IDisposable
    {
        readonly bool ownsConnection;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObisLaser"/> class for the laser on
        /// <paramref name="channel"/> of an existing connection.
        /// </summary>
        /// <param name="connection">The connection to the laser or to its controller.</param>
        /// <param name="channel">0 for a laser connected directly; 1, 2, ... for the lasers connected
        /// to an OBIS Remote or LaserBox.</param>
        public ObisLaser(ObisConnection connection, int channel = 0)
            : this(connection, channel, ownsConnection: false)
        {
        }

        ObisLaser(ObisConnection connection, int channel, bool ownsConnection)
        {
            if (channel < 0 || channel >= ObisProtocol.BroadcastChannel)
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "The channel must be between 0 and 254.");
            }

            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Channel = channel;
            this.ownsConnection = ownsConnection;
        }

        /// <summary>Gets the connection used to communicate with the laser.</summary>
        public ObisConnection Connection { get; }

        /// <summary>Gets the channel of the laser on the connection.</summary>
        public int Channel { get; }

        /// <summary>
        /// Opens a serial port and returns the laser on it. Disposing the laser closes the port.
        /// </summary>
        /// <param name="portName">The name of the port, e.g. <c>COM3</c>.</param>
        /// <param name="channel">The channel of the laser on the port.</param>
        /// <param name="timeout">The maximum time to wait for a complete reply.</param>
        /// <returns>The laser.</returns>
        public static ObisLaser Open(string portName, int channel = 0, TimeSpan? timeout = null)
        {
            var connection = ObisConnection.Open(portName, timeout);
            try
            {
                return new ObisLaser(connection, channel, ownsConnection: true);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Closes the connection, if it was opened by <see cref="Open"/>. Emission is not changed.
        /// </summary>
        public void Dispose()
        {
            if (ownsConnection) Connection.Dispose();
        }

        /// <inheritdoc/>
        public override string ToString() => $"{nameof(ObisLaser)}(Channel={Channel})";

        /// <summary>Executes any request from <see cref="ObisProtocol"/> on this laser.</summary>
        /// <typeparam name="T">The type of the reply value.</typeparam>
        /// <param name="request">The request.</param>
        /// <returns>The reply value.</returns>
        public T Execute<T>(ObisRequest<T> request) => Connection.Execute(request, Channel);

        /// <summary>Executes any request from <see cref="ObisProtocol"/> on this laser.</summary>
        /// <typeparam name="T">The type of the reply value.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the reply value.</returns>
        public Task<T> ExecuteAsync<T>(ObisRequest<T> request, CancellationToken cancellationToken = default) =>
            Connection.ExecuteAsync(request, Channel, cancellationToken);

        /// <summary>Sends a raw command or query to this laser and returns the reply lines.</summary>
        /// <param name="command">The command text.</param>
        /// <returns>The reply lines, without the <c>OK</c> handshake.</returns>
        public IReadOnlyList<string> Query(string command) => Connection.Query(command, Channel);

        /// <summary>Sends a raw command or query to this laser and returns the reply lines.</summary>
        /// <param name="command">The command text.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the reply lines, without the <c>OK</c> handshake.</returns>
        public Task<IReadOnlyList<string>> QueryAsync(string command, CancellationToken cancellationToken = default) =>
            Connection.QueryAsync(command, Channel, cancellationToken);

        // Combined queries

        /// <summary>Gets the model, serial number, firmware, wavelength, power limits and other static information.</summary>
        /// <returns>The laser information.</returns>
        public DeviceInfo GetInfo() => new(
            Execute(ObisProtocol.GetModel()),
            Execute(ObisProtocol.GetSerialNumber()),
            Execute(ObisProtocol.GetPartNumber()),
            Execute(ObisProtocol.GetFirmwareVersion()),
            Execute(ObisProtocol.GetProtocolVersion()),
            Execute(ObisProtocol.GetDeviceType()),
            Execute(ObisProtocol.GetWavelength()),
            Execute(ObisProtocol.GetPowerRating()),
            Execute(ObisProtocol.GetNominalPower()),
            Execute(ObisProtocol.GetMinPower()),
            Execute(ObisProtocol.GetMaxPower()),
            Execute(ObisProtocol.GetManufactureDate()),
            Execute(ObisProtocol.GetCalibrationDate()));

        /// <summary>Gets the model, serial number, firmware, wavelength, power limits and other static information.</summary>
        /// <param name="cancellationToken">Cancels the remaining requests.</param>
        /// <returns>A task completing with the laser information.</returns>
        public async Task<DeviceInfo> GetInfoAsync(CancellationToken cancellationToken = default) => new(
            await ExecuteAsync(ObisProtocol.GetModel(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetSerialNumber(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetPartNumber(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetFirmwareVersion(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetProtocolVersion(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetDeviceType(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetWavelength(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetPowerRating(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetNominalPower(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetMinPower(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetMaxPower(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetManufactureDate(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetCalibrationDate(), cancellationToken).ConfigureAwait(false));

        /// <summary>Gets the emission, operating mode, power, current, base plate temperature, status and faults.</summary>
        /// <returns>The laser state.</returns>
        public LaserState GetState() => new(
            Execute(ObisProtocol.GetEmission()),
            Execute(ObisProtocol.GetOperatingMode()),
            Execute(ObisProtocol.GetPowerSetpoint()),
            Execute(ObisProtocol.GetPower()),
            Execute(ObisProtocol.GetCurrent()),
            Execute(ObisProtocol.GetBaseplateTemperature()),
            Execute(ObisProtocol.GetStatus()),
            Execute(ObisProtocol.GetFaults()));

        /// <summary>Gets the emission, operating mode, power, current, base plate temperature, status and faults.</summary>
        /// <param name="cancellationToken">Cancels the remaining requests.</param>
        /// <returns>A task completing with the laser state.</returns>
        public async Task<LaserState> GetStateAsync(CancellationToken cancellationToken = default) => new(
            await ExecuteAsync(ObisProtocol.GetEmission(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetOperatingMode(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetPowerSetpoint(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetPower(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetCurrent(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetBaseplateTemperature(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetStatus(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetFaults(), cancellationToken).ConfigureAwait(false));

        /// <summary>Gets the base plate temperature range outside of which the laser faults.</summary>
        /// <returns>The temperature limits, in degrees Celsius.</returns>
        public TemperatureLimits GetBaseplateTemperatureLimits() => new(
            Execute(ObisProtocol.GetBaseplateTemperatureLowLimit()),
            Execute(ObisProtocol.GetBaseplateTemperatureHighLimit()));

        /// <summary>Gets the base plate temperature range outside of which the laser faults.</summary>
        /// <param name="cancellationToken">Cancels the remaining requests.</param>
        /// <returns>A task completing with the temperature limits, in degrees Celsius.</returns>
        public async Task<TemperatureLimits> GetBaseplateTemperatureLimitsAsync(CancellationToken cancellationToken = default) => new(
            await ExecuteAsync(ObisProtocol.GetBaseplateTemperatureLowLimit(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetBaseplateTemperatureHighLimit(), cancellationToken).ConfigureAwait(false));

        /// <summary>Gets the diode temperature range outside of which the laser faults (OBIS LX).</summary>
        /// <returns>The temperature limits, in degrees Celsius.</returns>
        public TemperatureLimits GetDiodeTemperatureLimits() => new(
            Execute(ObisProtocol.GetDiodeTemperatureLowLimit()),
            Execute(ObisProtocol.GetDiodeTemperatureHighLimit()));

        /// <summary>Gets the diode temperature range outside of which the laser faults (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the remaining requests.</param>
        /// <returns>A task completing with the temperature limits, in degrees Celsius.</returns>
        public async Task<TemperatureLimits> GetDiodeTemperatureLimitsAsync(CancellationToken cancellationToken = default) => new(
            await ExecuteAsync(ObisProtocol.GetDiodeTemperatureLowLimit(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetDiodeTemperatureHighLimit(), cancellationToken).ConfigureAwait(false));

        /// <summary>Gets the internal temperature range outside of which the laser faults (OBIS LX).</summary>
        /// <returns>The temperature limits, in degrees Celsius.</returns>
        public TemperatureLimits GetInternalTemperatureLimits() => new(
            Execute(ObisProtocol.GetInternalTemperatureLowLimit()),
            Execute(ObisProtocol.GetInternalTemperatureHighLimit()));

        /// <summary>Gets the internal temperature range outside of which the laser faults (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the remaining requests.</param>
        /// <returns>A task completing with the temperature limits, in degrees Celsius.</returns>
        public async Task<TemperatureLimits> GetInternalTemperatureLimitsAsync(CancellationToken cancellationToken = default) => new(
            await ExecuteAsync(ObisProtocol.GetInternalTemperatureLowLimit(), cancellationToken).ConfigureAwait(false),
            await ExecuteAsync(ObisProtocol.GetInternalTemperatureHighLimit(), cancellationToken).ConfigureAwait(false));

        /// <summary>Reads and removes every record of the device error queue, oldest first.</summary>
        /// <returns>The error records.</returns>
        public IReadOnlyList<ErrorRecord> GetErrors()
        {
            var count = Execute(ObisProtocol.GetErrorCount());
            var errors = new List<ErrorRecord>(count);
            for (int i = 0; i < count; i++)
            {
                errors.Add(Execute(ObisProtocol.GetNextError()));
            }

            return errors;
        }

        /// <summary>Reads and removes every record of the device error queue, oldest first.</summary>
        /// <param name="cancellationToken">Cancels the remaining requests.</param>
        /// <returns>A task completing with the error records.</returns>
        public async Task<IReadOnlyList<ErrorRecord>> GetErrorsAsync(CancellationToken cancellationToken = default)
        {
            var count = await ExecuteAsync(ObisProtocol.GetErrorCount(), cancellationToken).ConfigureAwait(false);
            var errors = new List<ErrorRecord>(count);
            for (int i = 0; i < count; i++)
            {
                errors.Add(await ExecuteAsync(ObisProtocol.GetNextError(), cancellationToken).ConfigureAwait(false));
            }

            return errors;
        }

        // Information
        /// <summary>Gets the identification string (<c>*IDN?</c>).</summary>
        /// <returns>The value read from the device.</returns>
        public string Identify() => Execute(ObisProtocol.Identify());

        /// <summary>Gets the identification string (<c>*IDN?</c>).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<string> IdentifyAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.Identify(), cancellationToken);

        /// <summary>Gets the user-defined text stored at <paramref name="index"/> (0 to 3).</summary>
        /// <param name="index">The storage index, from 0 to 3.</param>
        /// <returns>The value read from the device.</returns>
        public string GetUserInfo(int index) => Execute(ObisProtocol.GetUserInfo(index));

        /// <summary>Gets the user-defined text stored at <paramref name="index"/> (0 to 3).</summary>
        /// <param name="index">The storage index, from 0 to 3.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<string> GetUserInfoAsync(int index, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetUserInfo(index), cancellationToken);

        /// <summary>Stores up to 31 characters of user-defined text at <paramref name="index"/> (0 to 3). Persistent.</summary>
        /// <param name="index">The storage index, from 0 to 3.</param>
        /// <param name="text">The text to store.</param>
        public void SetUserInfo(int index, string text) => Execute(ObisProtocol.SetUserInfo(index, text));

        /// <summary>Stores up to 31 characters of user-defined text at <paramref name="index"/> (0 to 3). Persistent.</summary>
        /// <param name="index">The storage index, from 0 to 3.</param>
        /// <param name="text">The text to store.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetUserInfoAsync(int index, string text, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetUserInfo(index, text), cancellationToken);

        /// <summary>Gets the date of the last field calibration.</summary>
        /// <returns>The value read from the device.</returns>
        public string GetFieldCalibrationDate() => Execute(ObisProtocol.GetFieldCalibrationDate());

        /// <summary>Gets the date of the last field calibration.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<string> GetFieldCalibrationDateAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetFieldCalibrationDate(), cancellationToken);

        /// <summary>Stores the date of the last field calibration (up to 31 characters). Persistent.</summary>
        /// <param name="text">The text to store.</param>
        public void SetFieldCalibrationDate(string text) => Execute(ObisProtocol.SetFieldCalibrationDate(text));

        /// <summary>Stores the date of the last field calibration (up to 31 characters). Persistent.</summary>
        /// <param name="text">The text to store.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetFieldCalibrationDateAsync(string text, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetFieldCalibrationDate(text), cancellationToken);

        // Control
        /// <summary>Gets whether emission is turned on.</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetEmission() => Execute(ObisProtocol.GetEmission());

        /// <summary>Gets whether emission is turned on.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetEmissionAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetEmission(), cancellationToken);

        /// <summary>Turns emission on or off. Emission may be delayed by warm-up and the CDRH delay.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetEmission(bool enabled) => Execute(ObisProtocol.SetEmission(enabled));

        /// <summary>Turns emission on or off. Emission may be delayed by warm-up and the CDRH delay.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetEmissionAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetEmission(enabled), cancellationToken);

        /// <summary>Gets the requested output power, in watts.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetPowerSetpoint() => Execute(ObisProtocol.GetPowerSetpoint());

        /// <summary>Gets the requested output power, in watts.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetPowerSetpointAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetPowerSetpoint(), cancellationToken);

        /// <summary>Sets the output power, in watts. Does not turn emission on.</summary>
        /// <param name="watts">The output power, in watts.</param>
        public void SetPower(double watts) => Execute(ObisProtocol.SetPower(watts));

        /// <summary>Sets the output power, in watts. Does not turn emission on.</summary>
        /// <param name="watts">The output power, in watts.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetPowerAsync(double watts, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetPower(watts), cancellationToken);

        /// <summary>Gets the measured output power, in watts.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetPower() => Execute(ObisProtocol.GetPower());

        /// <summary>Gets the measured output power, in watts.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetPowerAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetPower(), cancellationToken);

        /// <summary>Gets the laser diode current, in amperes.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetCurrent() => Execute(ObisProtocol.GetCurrent());

        /// <summary>Gets the laser diode current, in amperes.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetCurrentAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetCurrent(), cancellationToken);

        /// <summary>Gets the operating mode.</summary>
        /// <returns>The value read from the device.</returns>
        public OperatingMode GetOperatingMode() => Execute(ObisProtocol.GetOperatingMode());

        /// <summary>Gets the operating mode.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<OperatingMode> GetOperatingModeAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetOperatingMode(), cancellationToken);

        /// <summary>Sets the operating mode. Persistent. External modes require an OBIS Remote.</summary>
        /// <param name="mode">The operating mode.</param>
        public void SetOperatingMode(OperatingMode mode) => Execute(ObisProtocol.SetOperatingMode(mode));

        /// <summary>Sets the operating mode. Persistent. External modes require an OBIS Remote.</summary>
        /// <param name="mode">The operating mode.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetOperatingModeAsync(OperatingMode mode, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetOperatingMode(mode), cancellationToken);

        // State
        /// <summary>Gets the system status word.</summary>
        /// <returns>The value read from the device.</returns>
        public StatusFlags GetStatus() => Execute(ObisProtocol.GetStatus());

        /// <summary>Gets the system status word.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<StatusFlags> GetStatusAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetStatus(), cancellationToken);

        /// <summary>Gets the active faults. A reset or power cycle is needed to clear them.</summary>
        /// <returns>The value read from the device.</returns>
        public FaultFlags GetFaults() => Execute(ObisProtocol.GetFaults());

        /// <summary>Gets the active faults. A reset or power cycle is needed to clear them.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<FaultFlags> GetFaultsAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetFaults(), cancellationToken);

        /// <summary>Gets the base plate temperature, in degrees Celsius.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetBaseplateTemperature() => Execute(ObisProtocol.GetBaseplateTemperature());

        /// <summary>Gets the base plate temperature, in degrees Celsius.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetBaseplateTemperatureAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetBaseplateTemperature(), cancellationToken);

        /// <summary>Gets the laser diode temperature, in degrees Celsius (OBIS LX).</summary>
        /// <returns>The value read from the device.</returns>
        public double GetDiodeTemperature() => Execute(ObisProtocol.GetDiodeTemperature());

        /// <summary>Gets the laser diode temperature, in degrees Celsius (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetDiodeTemperatureAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetDiodeTemperature(), cancellationToken);

        /// <summary>Gets the TEC set point of the laser diode, in degrees Celsius (OBIS LX).</summary>
        /// <returns>The value read from the device.</returns>
        public double GetDiodeTemperatureSetpoint() => Execute(ObisProtocol.GetDiodeTemperatureSetpoint());

        /// <summary>Gets the TEC set point of the laser diode, in degrees Celsius (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetDiodeTemperatureSetpointAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetDiodeTemperatureSetpoint(), cancellationToken);

        /// <summary>Gets the internal (microprocessor) temperature, in degrees Celsius (OBIS LX).</summary>
        /// <returns>The value read from the device.</returns>
        public double GetInternalTemperature() => Execute(ObisProtocol.GetInternalTemperature());

        /// <summary>Gets the internal (microprocessor) temperature, in degrees Celsius (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetInternalTemperatureAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetInternalTemperature(), cancellationToken);

        /// <summary>Gets the laser diode threshold current, in amperes.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetThresholdCurrent() => Execute(ObisProtocol.GetThresholdCurrent());

        /// <summary>Gets the laser diode threshold current, in amperes.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetThresholdCurrentAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetThresholdCurrent(), cancellationToken);

        /// <summary>Gets the laser diode upper current limit, in amperes (OBIS LX, firmware 2.x or later).</summary>
        /// <returns>The value read from the device.</returns>
        public double GetMaxCurrent() => Execute(ObisProtocol.GetMaxCurrent());

        /// <summary>Gets the laser diode upper current limit, in amperes (OBIS LX, firmware 2.x or later).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetMaxCurrentAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetMaxCurrent(), cancellationToken);

        /// <summary>Gets the OBIS Remote interlock state: <see langword="true"/> when closed (emission allowed).</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetInterlock() => Execute(ObisProtocol.GetInterlock());

        /// <summary>Gets the OBIS Remote interlock state: <see langword="true"/> when closed (emission allowed).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetInterlockAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetInterlock(), cancellationToken);

        /// <summary>Gets the relative power noise level (constant power mode). Above 30 is considered noisy.</summary>
        /// <returns>The value read from the device.</returns>
        public int GetNoise() => Execute(ObisProtocol.GetNoise());

        /// <summary>Gets the relative power noise level (constant power mode). Above 30 is considered noisy.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<int> GetNoiseAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetNoise(), cancellationToken);

        /// <summary>Gets the number of times the device has been powered on.</summary>
        /// <returns>The value read from the device.</returns>
        public int GetPowerCycles() => Execute(ObisProtocol.GetPowerCycles());

        /// <summary>Gets the number of times the device has been powered on.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<int> GetPowerCyclesAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetPowerCycles(), cancellationToken);

        /// <summary>Gets the number of hours the device has been powered on.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetHours() => Execute(ObisProtocol.GetHours());

        /// <summary>Gets the number of hours the device has been powered on.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetHoursAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetHours(), cancellationToken);

        /// <summary>Gets the number of hours the laser diode has been emitting.</summary>
        /// <returns>The value read from the device.</returns>
        public double GetDiodeHours() => Execute(ObisProtocol.GetDiodeHours());

        /// <summary>Gets the number of hours the laser diode has been emitting.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<double> GetDiodeHoursAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetDiodeHours(), cancellationToken);

        // Error queue
        /// <summary>Clears the error queue.</summary>
        public void ClearErrors() => Execute(ObisProtocol.ClearErrors());

        /// <summary>Clears the error queue.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task ClearErrorsAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.ClearErrors(), cancellationToken);

        // Persistent settings
        /// <summary>Gets whether the laser starts emitting at power-up.</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetAutoStart() => Execute(ObisProtocol.GetAutoStart());

        /// <summary>Gets whether the laser starts emitting at power-up.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetAutoStartAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetAutoStart(), cancellationToken);

        /// <summary>Sets whether the laser starts emitting at power-up. Persistent.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetAutoStart(bool enabled) => Execute(ObisProtocol.SetAutoStart(enabled));

        /// <summary>Sets whether the laser starts emitting at power-up. Persistent.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetAutoStartAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetAutoStart(enabled), cancellationToken);

        /// <summary>Gets whether the 5 s CDRH emission delay is enabled.</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetCdrhDelay() => Execute(ObisProtocol.GetCdrhDelay());

        /// <summary>Gets whether the 5 s CDRH emission delay is enabled.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetCdrhDelayAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetCdrhDelay(), cancellationToken);

        /// <summary>Enables or disables the CDRH emission delay. Persistent. Disabling it makes the system non-CDRH compliant.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetCdrhDelay(bool enabled) => Execute(ObisProtocol.SetCdrhDelay(enabled));

        /// <summary>Enables or disables the CDRH emission delay. Persistent. Disabling it makes the system non-CDRH compliant.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetCdrhDelayAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetCdrhDelay(enabled), cancellationToken);

        /// <summary>Gets whether the TEC (LX) or thermal stabilization (LS) is enabled.</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetTecEnabled() => Execute(ObisProtocol.GetTecEnabled());

        /// <summary>Gets whether the TEC (LX) or thermal stabilization (LS) is enabled.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetTecEnabledAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetTecEnabled(), cancellationToken);

        /// <summary>Enables or disables the TEC (LX) or thermal stabilization (LS). Off puts an LX in sleep mode.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetTecEnabled(bool enabled) => Execute(ObisProtocol.SetTecEnabled(enabled));

        /// <summary>Enables or disables the TEC (LX) or thermal stabilization (LS). Off puts an LX in sleep mode.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetTecEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetTecEnabled(enabled), cancellationToken);

        /// <summary>Gets whether emission waits for the diode warm-up (OBIS LX).</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetDiodeWarmup() => Execute(ObisProtocol.GetDiodeWarmup());

        /// <summary>Gets whether emission waits for the diode warm-up (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetDiodeWarmupAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetDiodeWarmup(), cancellationToken);

        /// <summary>Sets whether emission waits for the diode warm-up (OBIS LX). Persistent.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetDiodeWarmup(bool enabled) => Execute(ObisProtocol.SetDiodeWarmup(enabled));

        /// <summary>Sets whether emission waits for the diode warm-up (OBIS LX). Persistent.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetDiodeWarmupAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetDiodeWarmup(enabled), cancellationToken);

        /// <summary>Gets whether the status LED is enabled.</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetStatusIndicator() => Execute(ObisProtocol.GetStatusIndicator());

        /// <summary>Gets whether the status LED is enabled.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetStatusIndicatorAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetStatusIndicator(), cancellationToken);

        /// <summary>Turns the status LED on or off; status bits are not affected. Persistent.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetStatusIndicator(bool enabled) => Execute(ObisProtocol.SetStatusIndicator(enabled));

        /// <summary>Turns the status LED on or off; status bits are not affected. Persistent.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetStatusIndicatorAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetStatusIndicator(enabled), cancellationToken);

        /// <summary>Gets whether blanking is enabled in analog modulation modes.</summary>
        /// <returns>The value read from the device.</returns>
        public bool GetAnalogBlanking() => Execute(ObisProtocol.GetAnalogBlanking());

        /// <summary>Gets whether blanking is enabled in analog modulation modes.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<bool> GetAnalogBlankingAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetAnalogBlanking(), cancellationToken);

        /// <summary>Enables or disables blanking in analog modulation modes.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        public void SetAnalogBlanking(bool enabled) => Execute(ObisProtocol.SetAnalogBlanking(enabled));

        /// <summary>Enables or disables blanking in analog modulation modes.</summary>
        /// <param name="enabled"><see langword="true"/> to enable; otherwise, <see langword="false"/>.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetAnalogBlankingAsync(bool enabled, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetAnalogBlanking(enabled), cancellationToken);

        /// <summary>Gets the OBIS Remote analog input impedance.</summary>
        /// <returns>The value read from the device.</returns>
        public AnalogImpedance GetAnalogImpedance() => Execute(ObisProtocol.GetAnalogImpedance());

        /// <summary>Gets the OBIS Remote analog input impedance.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<AnalogImpedance> GetAnalogImpedanceAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.GetAnalogImpedance(), cancellationToken);

        /// <summary>Selects the OBIS Remote analog input impedance. Persistent.</summary>
        /// <param name="impedance">The analog input impedance.</param>
        public void SetAnalogImpedance(AnalogImpedance impedance) => Execute(ObisProtocol.SetAnalogImpedance(impedance));

        /// <summary>Selects the OBIS Remote analog input impedance. Persistent.</summary>
        /// <param name="impedance">The analog input impedance.</param>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task SetAnalogImpedanceAsync(AnalogImpedance impedance, CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SetAnalogImpedance(impedance), cancellationToken);

        // Maintenance
        /// <summary>Warm-boots the device. Clears fault conditions.</summary>
        public void Reset() => Execute(ObisProtocol.Reset());

        /// <summary>Warm-boots the device. Clears fault conditions.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task ResetAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.Reset(), cancellationToken);

        /// <summary>Runs the self-test. <c>0xFFFFFFFF</c> means the self-test is not implemented.</summary>
        /// <returns>The value read from the device.</returns>
        public FaultFlags SelfTest() => Execute(ObisProtocol.SelfTest());

        /// <summary>Runs the self-test. <c>0xFFFFFFFF</c> means the self-test is not implemented.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing with the value read from the device.</returns>
        public Task<FaultFlags> SelfTestAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.SelfTest(), cancellationToken);

        /// <summary>Starts a power field calibration (OBIS LX). Takes minutes; do not cut power. Progress is reported by <see cref="StatusFlags.FieldCalibration"/>.</summary>
        public void StartFieldCalibration() => Execute(ObisProtocol.StartFieldCalibration());

        /// <summary>Starts a power field calibration (OBIS LX). Takes minutes; do not cut power. Progress is reported by <see cref="StatusFlags.FieldCalibration"/>.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task StartFieldCalibrationAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.StartFieldCalibration(), cancellationToken);

        /// <summary>Undoes the last field calibration (OBIS LX).</summary>
        public void UndoFieldCalibration() => Execute(ObisProtocol.UndoFieldCalibration());

        /// <summary>Undoes the last field calibration (OBIS LX).</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task UndoFieldCalibrationAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.UndoFieldCalibration(), cancellationToken);

        /// <summary>Recovers from a persistent checksum error, restoring factory defaults. Ask Coherent before using it.</summary>
        public void Recover() => Execute(ObisProtocol.Recover());

        /// <summary>Recovers from a persistent checksum error, restoring factory defaults. Ask Coherent before using it.</summary>
        /// <param name="cancellationToken">Cancels the request while it waits for its turn.</param>
        /// <returns>A task completing when the device acknowledges the command.</returns>
        public Task RecoverAsync(CancellationToken cancellationToken = default) => ExecuteAsync(ObisProtocol.Recover(), cancellationToken);
    }
}
