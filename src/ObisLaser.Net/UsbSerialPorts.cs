using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace ObisLaser.Net
{
    // Reads the USB vendor ID of serial ports from the Windows device registry.
    static class UsbSerialPorts
    {
        const string UsbDevicesKey = @"SYSTEM\CurrentControlSet\Enum\USB";

        // Returns the names of the serial ports of USB devices with the given vendor ID, including
        // devices that are no longer connected. Returns an empty list on other operating systems.
        public static IReadOnlyList<string> GetPortNames(int vendorId)
        {
            var portNames = new List<string>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return portNames;

            try
            {
                using var usbDevices = Registry.LocalMachine.OpenSubKey(UsbDevicesKey);
                if (usbDevices is null) return portNames;

                foreach (var hardwareId in usbDevices.GetSubKeyNames())
                {
                    if (ParseVendorId(hardwareId) != vendorId) continue;
                    using var device = usbDevices.OpenSubKey(hardwareId);
                    if (device is null) continue;
                    foreach (var instance in device.GetSubKeyNames())
                    {
                        using var parameters = device.OpenSubKey(instance + @"\Device Parameters");
                        if (parameters?.GetValue("PortName") is string portName && portName.Length > 0)
                        {
                            portNames.Add(portName);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                // Without access to the registry, no port is known to be from this vendor.
            }

            return portNames;
        }

        // Parses the vendor ID of a USB hardware ID such as "VID_0D4D&PID_003B" or "VID_0D4D&PID_003B&MI_00".
        internal static int? ParseVendorId(string hardwareId)
        {
            const string Prefix = "VID_";
            if (!hardwareId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || hardwareId.Length < Prefix.Length + 4) return null;
            return int.TryParse(hardwareId.Substring(Prefix.Length, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vendorId)
                ? vendorId
                : null;
        }
    }
}
