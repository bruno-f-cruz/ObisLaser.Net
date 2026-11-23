using System;
using System.Collections.Generic;

namespace ObisLaser.Net.Tests
{
    /// <summary>
    /// Replies to commands like an OBIS device, from a table of canned query replies.
    /// Queries listed in <see cref="Replies"/> return that line followed by <c>OK</c>; unknown
    /// queries, and commands listed in <see cref="Errors"/>, return <c>ERR&lt;code&gt;</c>;
    /// other commands return <c>OK</c>.
    /// </summary>
    sealed class FakeObisTransport : IObisTransport
    {
        readonly Queue<string> output = new();

        public FakeObisTransport(Dictionary<string, string>? replies = null)
        {
            Replies = new Dictionary<string, string>(replies ?? new()) { ["SYST:COMM:HAND?"] = "ON" };
            if (replies is not null && replies.TryGetValue("SYST:COMM:HAND?", out var handshaking))
            {
                Replies["SYST:COMM:HAND?"] = handshaking;
            }
        }

        public Dictionary<string, string> Replies { get; }

        public Dictionary<string, int> Errors { get; } = new();

        public List<string> Written { get; } = new();

        public bool Handshaking { get; set; } = true;

        public bool Silent { get; set; }

        public bool KeepInputOnDiscard { get; set; }

        public bool Disposed { get; private set; }

        public void Write(string text)
        {
            if (!text.EndsWith("\r", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Commands must be terminated by a carriage return.");
            }

            var command = text.Substring(0, text.Length - 1);
            Written.Add(command);
            if (!Silent) Respond(command);
        }

        public string ReadLine()
        {
            if (output.Count == 0) throw new TimeoutException();
            return output.Dequeue();
        }

        public void DiscardInBuffer()
        {
            if (!KeepInputOnDiscard) output.Clear();
        }

        public void Dispose() => Disposed = true;

        // Simulates the "> " command prompt arriving before the next reply.
        public void EnqueuePrompt() => output.Enqueue("> ");

        void Respond(string command)
        {
            if (Errors.TryGetValue(command, out var code))
            {
                output.Enqueue("ERR" + code);
            }
            else if (command.Contains("?"))
            {
                if (!Replies.TryGetValue(command, out var reply))
                {
                    output.Enqueue("ERR-100");
                    return;
                }

                output.Enqueue(reply);
                if (Handshaking) output.Enqueue("OK");
            }
            else
            {
                if (command == "SYST:COMM:HAND ON") Handshaking = true;
                if (Handshaking) output.Enqueue("OK");
            }
        }
    }
}
