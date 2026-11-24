# ObisLaser.Net

.NET client for Coherent OBIS LX/LS lasers, over USB or RS-232. It covers the whole host
command set from the OBIS LX/LS Operator's Manual: device metadata, emission and power
control, operating modes, temperatures, status and fault words, the error queue and
persistent settings. Every method has a synchronous and an asynchronous (`...Async`) version.

Targets `net472` and `net8.0`.

> OBIS and Coherent are trademarks of Coherent Corp. This project is not affiliated
> with or endorsed by Coherent.

## Install

```sh
dotnet add package ObisLaser.Net
```

## Quick start

```csharp
using ObisLaser.Net;

using var laser = Laser.Open("COM3");

var info = laser.GetInfo();
Console.WriteLine($"{info.Model} (S/N {info.SerialNumber}), {info.Wavelength} nm");

laser.SetOperatingMode(OperatingMode.Cwp);
laser.SetPower(0.010);                     // watts
laser.SetEmission(true);
Console.WriteLine(await laser.GetStateAsync());
laser.SetEmission(false);
```

Units are SI as used by the device: power in **watts**, current in **amperes**,
temperatures in **degrees Celsius**.

### Several lasers behind an OBIS Remote, LaserBox or 5-Laser Box

Channel 0 is the controller; lasers are numbered from 1. Discovery lists every laser behind a controller, including
those after an empty slot.

```csharp
using var connection = ObisConnection.Open("COM3");
var blue = connection.GetLaser(1);
var green = connection.GetLaser(2);
blue.SetPower(0.02);
connection.Broadcast("SOUR:AM:STAT ON");   // every laser at once
```

### Finding lasers

```csharp
foreach (var device in ObisDiscovery.FindAll(excludedPortNames: new[] { "COM1" }))
{
    Console.WriteLine($"{device.PortName} channel {device.Channel}: {device.Model} ({device.SerialNumber})");
}
```

OBIS lasers and controllers connected over USB are recognized by Coherent's USB vendor ID
(`0x0D4D`) without opening any port: `ObisDiscovery.GetCandidatePortNames()` lists them (on
Windows only). `FindAll` probes these ports, or every serial port if there are none, for
example when the laser is connected over RS-232 through a USB adapter.

Probing sends `*IDN?` to each port, so it may disturb other serial devices (it does not change
any setting on them). Pass `portNames` to choose the ports to probe, for example
`SerialPort.GetPortNames()` to probe them all, or `excludedPortNames` to skip ports.

### Anything else

Every command is available as a request in `ObisProtocol`, and raw commands can be sent:

```csharp
double wavelength = laser.Execute(ObisProtocol.GetWavelength());
IReadOnlyList<string> reply = laser.Query("SYST:INF:FVER?");
```

## Online control

- Each call is a single request/reply exchange (a few milliseconds at 115200 baud).
  `GetInfo()` and `GetState()` combine several exchanges; for fast loops, call the
  specific getters you need, e.g. `GetPower()` or `GetStatus()`.
- Connections are thread-safe: exchanges are serialized, and asynchronous requests run
  in the order they are made. Cancellation applies while a request waits for its turn;
  once sent, a request always completes so replies stay in sync.
- Device errors throw `ObisDeviceException` (with `Code` and `Description`), missing replies
  throw `ObisTimeoutException`. All exceptions derive from `ObisException`.
- On connection, the client enables message handshaking (`OK`/`ERR` replies) if it is off.
  This setting is stored in the laser.

## Repository layout

```
src/ObisLaser.Net/          library (net472; net8.0)
tests/ObisLaser.Net.Tests/  unit tests against a simulated device (no hardware needed)
```

## Development

```sh
dotnet build
dotnet test
dotnet pack -c Release    # -> artifacts/package/release/
```

Requires the .NET 8 SDK or later. Running the `net472` tests requires Windows.

### Testing with a real laser

The `Hardware` test category runs against a real device. It is skipped unless `OBIS_PORT`
is set, and always skipped in CI (when `CI` is set). Only settings that are restored to
their original value are written.

```powershell
$env:OBIS_PORT = "COM3"
$env:OBIS_CHANNEL = "0"           # optional: laser behind an OBIS Remote / LaserBox
$env:OBIS_TEST_EMISSION = "1"     # optional: also turn emission ON (5 % of max power)
dotnet test -f net8.0 --filter TestCategory=Hardware --logger "console;verbosity=detailed"
```

Block the beam before enabling `OBIS_TEST_EMISSION`.

## License

MIT
