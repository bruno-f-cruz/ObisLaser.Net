using System;
using System.Collections.Generic;

namespace ObisLaser.Net
{
    /// <summary>
    /// Represents an error raised while communicating with an OBIS device.
    /// </summary>
    public class ObisException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObisException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ObisException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Represents an error reply (<c>ERR&lt;n&gt;</c>) from an OBIS device.
    /// </summary>
    public class ObisDeviceException : ObisException
    {
        static readonly Dictionary<int, string> Descriptions = new()
        {
            [-400] = "Query unavailable (queries cannot be broadcast)",
            [-350] = "Queue overflow",
            [-321] = "Out of memory",
            [-310] = "System error",
            [-257] = "File to open not named",
            [-256] = "File does not exist",
            [-241] = "Device unavailable",
            [-221] = "Settings conflict (command not allowed in the current device state)",
            [-220] = "Invalid parameter",
            [-203] = "Command protected",
            [-200] = "Execution error (command out of order)",
            [-109] = "Parameter missing",
            [-102] = "Syntax error",
            [-100] = "Unrecognized command or query",
            [0] = "No error",
            [500] = "Coherent Connection bus (CCB) fault",
            [510] = "I2C bus fault",
            [520] = "Controller time out",
            [900] = "CCB message timed out",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ObisDeviceException"/> class.
        /// </summary>
        /// <param name="code">The error code returned by the device.</param>
        /// <param name="command">The command that caused the error.</param>
        public ObisDeviceException(int code, string command)
            : base($"'{command}' failed with ERR{code}: {Describe(code)}")
        {
            Code = code;
            Command = command;
            Description = Describe(code);
        }

        /// <summary>Gets the error code returned by the device (manual Table 9-7).</summary>
        public int Code { get; }

        /// <summary>Gets the command that caused the error.</summary>
        public string Command { get; }

        /// <summary>Gets the description of the error code.</summary>
        public string Description { get; }

        static string Describe(int code)
        {
            return Descriptions.TryGetValue(code, out var description) ? description : "Unknown error";
        }
    }

    /// <summary>
    /// Represents the error raised when an OBIS device does not reply in time.
    /// </summary>
    public class ObisTimeoutException : ObisException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObisTimeoutException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ObisTimeoutException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Represents the error raised when an OBIS device sends an unexpected reply.
    /// </summary>
    public class ObisProtocolException : ObisException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObisProtocolException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ObisProtocolException(string message)
            : base(message)
        {
        }
    }
}
