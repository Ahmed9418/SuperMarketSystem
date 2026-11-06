using System.Runtime.InteropServices;
using System.Text;

namespace Supermarket.POS.Printing;

/// <summary>
/// Minimal Win32 spooler interop used to push raw ESC/POS bytes to a thermal printer.
/// Raw mode is required because a thermal printer has no GDI driver: the application owns the
/// layout and the printer only executes the command stream.
/// </summary>
internal static class RawPrinterHelper
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

    /// <summary>True when the printer is installed on this machine.</summary>
    public static bool PrinterExists(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        return OpenPrinter(printerName, out var handle, IntPtr.Zero) && CloseAndReturn(handle);
    }

    private static bool CloseAndReturn(IntPtr handle)
    {
        ClosePrinter(handle);
        return true;
    }

    /// <summary>Sends the raw bytes to the spooler. Throws <see cref="Win32Exception"/>-like errors on failure.</summary>
    public static void SendBytes(string printerName, byte[] data, string documentName)
    {
        if (!OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
        {
            throw new InvalidOperationException($"تعذر فتح الطابعة ({printerName}). تأكد من توصيلها وتشغيلها.");
        }

        try
        {
            var docInfo = new DocInfo
            {
                DocumentName = documentName,
                DataType = "RAW"
            };

            if (!StartDocPrinter(printerHandle, 1, ref docInfo))
            {
                throw new InvalidOperationException($"تعذر بدء مهمة الطباعة على ({printerName}).");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    throw new InvalidOperationException("تعذر بدء صفحة الطباعة.");
                }

                try
                {
                    var unmanaged = Marshal.AllocHGlobal(data.Length);
                    try
                    {
                        Marshal.Copy(data, 0, unmanaged, data.Length);
                        if (!WritePrinter(printerHandle, unmanaged, data.Length, out var written) || written != data.Length)
                        {
                            throw new InvalidOperationException("لم يتم إرسال بيانات الإيصال بالكامل إلى الطابعة.");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(unmanaged);
                    }
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }
}
