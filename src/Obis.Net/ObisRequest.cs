using System;
using System.Collections.Generic;

namespace Obis.Net
{
    /// <summary>
    /// Represents a single command or query, and how to interpret its reply.
    /// </summary>
    /// <typeparam name="T">The type of the value parsed from the reply.</typeparam>
    public sealed class ObisRequest<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObisRequest{T}"/> class.
        /// </summary>
        /// <param name="command">The command text sent to the device.</param>
        /// <param name="parse">A function converting the reply lines into a value.</param>
        public ObisRequest(string command, Func<IReadOnlyList<string>, T> parse)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Parse = parse ?? throw new ArgumentNullException(nameof(parse));
        }

        /// <summary>
        /// Gets the command text sent to the device, for example <c>SOUR:AM:STAT ON</c>.
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// Gets the function converting the reply lines (without the <c>OK</c> handshake) into a value.
        /// </summary>
        public Func<IReadOnlyList<string>, T> Parse { get; }

        /// <inheritdoc/>
        public override string ToString() => Command;
    }
}
