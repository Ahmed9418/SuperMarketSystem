using System.Printing;
using System.Runtime.InteropServices;
using System.Text;

namespace Supermarket.Wpf.Printing;

/// <summary>
/// Minimal Win32 spooler interop used to push raw ESC/POS bytes to a thermal printer.
/// Raw mode is required because a thermal printer has no standard GDI driver: the application
/// owns the layout and the printer executes the command stream.
/// </summary>
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr defaults);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr printerHandle, int level, ref DocInfo docInfo);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr printerHandle, IntPtr bytes, int count, out int written);

    [DllImport("winspool.drv", EntryPoint = "GetDefaultPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinter(StringBuilder? buffer, ref int size);

    /// <summary>Name of the Windows default printer, or null when none is installed.</summary>
    public static string? GetDefaultPrinterName()
    {
        var size = 0;
        GetDefaultPrinter(null, ref size);

        if (size <= 0)
        {
            return null;
        }

        var buffer = new StringBuilder(size);
        return GetDefaultPrinter(buffer, ref size) ? buffer.ToString() : null;
    }

    /// <summary>Returns the names of all printers installed in Windows.</summary>
    public static IReadOnlyList<string> GetInstalledPrinterNames()
    {
        var names = new List<string>();
        try
        {
            var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
            foreach (var pq in printQueues)
            {
                names.Add(pq.Name);
            }
        }
        catch
        {
            // Fallback to default printer if spooler enumeration is blocked
            var def = GetDefaultPrinterName();
            if (!string.IsNullOrWhiteSpace(def))
            {
                names.Add(def);
            }
        }

        return names;
    }

    /// <summary>True when the printer is installed on this machine.</summary>
    public static bool PrinterExists(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        return OpenPrinter(printerName, out var handle, IntPtr.Zero) && ClosePrinter(handle);
    }

    /// <summary>Sends raw bytes to the specified Windows printer.</summary>
    public static void SendBytes(string printerName, byte[] bytes, string documentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);
        ArgumentNullException.ThrowIfNull(bytes);

        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"تعذر فتح الطابعة '{printerName}'. رمز الخطأ: {err}");
        }

        var docInfo = new DocInfo
        {
            DocumentName = string.IsNullOrWhiteSpace(documentName) ? "SupermarketReceipt" : documentName,
            DataType = "RAW"
        };

        var unmanaged = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, unmanaged, bytes.Length);

        try
        {
            if (!StartDocPrinter(handle, 1, ref docInfo))
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"تعذر بدء مستند الطباعة. رمز الخطأ: {err}");
            }

            try
            {
                if (!StartPagePrinter(handle))
                {
                    var err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"تعذر بدء صفحة الطباعة. رمز الخطأ: {err}");
                }

                try
                {
                    if (!WritePrinter(handle, unmanaged, bytes.Length, out var written) || written != bytes.Length)
                    {
                        var err = Marshal.GetLastWin32Error();
                        throw new InvalidOperationException($"فشلت كتابة البيانات إلى الطابعة. رمز الخطأ: {err}");
                    }
                }
                finally
                {
                    EndPagePrinter(handle);
                }
            }
            finally
            {
                EndDocPrinter(handle);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanaged);
            ClosePrinter(handle);
        }
    }
}
