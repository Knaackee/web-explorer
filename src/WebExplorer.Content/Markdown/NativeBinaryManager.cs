using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace WebExplorer.Content.Markdown;

internal static class NativeBinaryManager
{
    private static string? _binaryPath;
    private static readonly object Lock = new();

    static NativeBinaryManager()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
    }

    public static string BinaryPath
    {
        get
        {
            if (_binaryPath != null) return _binaryPath;
            lock (Lock)
            {
                return _binaryPath ??= ExtractBinary();
            }
        }
    }

    private static string ExtractBinary()
    {
        var (resourceName, fileName) = GetPlatformResource();
        var tempDir = Path.Combine(Path.GetTempPath(), $"web-explorer-html2md-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var targetPath = Path.Combine(tempDir, fileName);

        using var resourceStream = typeof(NativeBinaryManager).Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new PlatformNotSupportedException(
                $"Embedded resource '{resourceName}' not found for this platform.");

        using var gzipStream = new GZipStream(resourceStream, CompressionMode.Decompress);
        using var fileStream = File.Create(targetPath);
        gzipStream.CopyTo(fileStream);
        fileStream.Close();

        if (!OperatingSystem.IsWindows())
        {
            using var chmod = Process.Start(new ProcessStartInfo("chmod", ["+x", targetPath])
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            chmod?.WaitForExit();
        }

        return targetPath;
    }

    private static (string resourceName, string fileName) GetPlatformResource()
    {
        var arch = RuntimeInformation.OSArchitecture;

        return (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(), arch) switch
        {
            (true, _, _, Architecture.X64) =>
                ("html2markdown-windows-amd64.exe.gz", "html2markdown.exe"),
            (_, true, _, Architecture.X64) =>
                ("html2markdown-linux-amd64.gz", "html2markdown"),
            (_, true, _, Architecture.Arm64) =>
                ("html2markdown-linux-arm64.gz", "html2markdown"),
            (_, _, true, Architecture.X64) =>
                ("html2markdown-darwin-amd64.gz", "html2markdown"),
            (_, _, true, Architecture.Arm64) =>
                ("html2markdown-darwin-arm64.gz", "html2markdown"),
            _ => throw new PlatformNotSupportedException(
                $"No html2markdown binary available for {RuntimeInformation.OSDescription} ({arch}).")
        };
    }

    private static void Cleanup()
    {
        var path = _binaryPath;
        if (path == null) return;
        _binaryPath = null;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup on process exit
        }
    }
}
