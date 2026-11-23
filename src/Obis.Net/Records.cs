namespace Obis.Net
{
    /// <summary>
    /// Represents static information describing a laser.
    /// </summary>
    /// <param name="Model">The model name.</param>
    /// <param name="SerialNumber">The serial number.</param>
    /// <param name="PartNumber">The manufacturer part number.</param>
    /// <param name="FirmwareVersion">The firmware version.</param>
    /// <param name="ProtocolVersion">The OBIS protocol version.</param>
    /// <param name="DeviceType">The device type.</param>
    /// <param name="Wavelength">The nominal wavelength, in nanometers.</param>
    /// <param name="PowerRating">The power rating, in watts.</param>
    /// <param name="NominalPower">The nominal CW output power, in watts.</param>
    /// <param name="MinPower">The minimum CW output power, in watts.</param>
    /// <param name="MaxPower">The maximum CW output power, in watts.</param>
    /// <param name="ManufactureDate">The manufacture date, as reported by the device (YYYYMMDD).</param>
    /// <param name="CalibrationDate">The calibration date, as reported by the device (YYYYMMDD).</param>
    public sealed record DeviceInfo(
        string Model,
        string SerialNumber,
        string PartNumber,
        string FirmwareVersion,
        string ProtocolVersion,
        DeviceType DeviceType,
        double Wavelength,
        double PowerRating,
        double NominalPower,
        double MinPower,
        double MaxPower,
        string ManufactureDate,
        string CalibrationDate);

    /// <summary>
    /// Represents a snapshot of the dynamic state of a laser.
    /// </summary>
    /// <param name="Emission">Whether emission is turned on.</param>
    /// <param name="OperatingMode">The operating mode.</param>
    /// <param name="PowerSetpoint">The requested output power, in watts.</param>
    /// <param name="Power">The measured output power, in watts.</param>
    /// <param name="Current">The laser diode current, in amperes.</param>
    /// <param name="BaseplateTemperature">The base plate temperature, in degrees Celsius.</param>
    /// <param name="Status">The system status word.</param>
    /// <param name="Faults">The system fault word.</param>
    public sealed record LaserState(
        bool Emission,
        OperatingMode OperatingMode,
        double PowerSetpoint,
        double Power,
        double Current,
        double BaseplateTemperature,
        StatusFlags Status,
        FaultFlags Faults);

    /// <summary>
    /// Represents a temperature range, in degrees Celsius, outside of which the device faults.
    /// </summary>
    /// <param name="Low">The lowest temperature allowed.</param>
    /// <param name="High">The highest temperature allowed.</param>
    public readonly record struct TemperatureLimits(double Low, double High);

    /// <summary>
    /// Represents an entry of the device error queue.
    /// </summary>
    /// <param name="Code">The error code.</param>
    /// <param name="Message">The error message.</param>
    public sealed record ErrorRecord(int Code, string Message);

    /// <summary>
    /// Represents an OBIS device found by <see cref="ObisDiscovery"/>.
    /// </summary>
    /// <param name="PortName">The serial port the device is connected to.</param>
    /// <param name="Channel">The channel of the device: 0 for the device on the port itself,
    /// 1 or more for lasers connected to a controller.</param>
    /// <param name="DeviceType">The device type.</param>
    /// <param name="Model">The model name.</param>
    /// <param name="SerialNumber">The serial number.</param>
    public sealed record DiscoveredDevice(
        string PortName,
        int Channel,
        DeviceType DeviceType,
        string Model,
        string SerialNumber);
}
