namespace ObisLaser.Net
{
    /// <summary>
    /// Specifies the type of an OBIS device.
    /// </summary>
    public enum DeviceType
    {
        /// <summary>Any other device type (OTHER).</summary>
        Other,

        /// <summary>OBIS LX direct diode laser (DDL).</summary>
        Ddl,

        /// <summary>OBIS LS optically pumped semiconductor laser (OPSL).</summary>
        Opsl,

        /// <summary>OBIS Remote mini controller (MINI).</summary>
        Mini,

        /// <summary>Master controller, such as an OBIS LaserBox (MASTER).</summary>
        Master,
    }
}
