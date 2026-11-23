namespace Obis.Net
{
    /// <summary>
    /// Specifies the laser operating mode (manual section 9.5.1).
    /// </summary>
    public enum OperatingMode
    {
        /// <summary>Continuous wave, constant power (CWP).</summary>
        Cwp,

        /// <summary>Continuous wave, constant current (CWC).</summary>
        Cwc,

        /// <summary>External digital modulation, current feedback (DIGITAL).</summary>
        Digital,

        /// <summary>External analog modulation, power feedback (ANALOG).</summary>
        Analog,

        /// <summary>External digital and analog modulation, current feedback (MIXED).</summary>
        Mixed,

        /// <summary>External digital modulation, power feedback (DIGSO). Not supported by all models.</summary>
        Digso,

        /// <summary>External digital and analog modulation, power feedback (MIXSO). Not supported by all models.</summary>
        Mixso,
    }
}
