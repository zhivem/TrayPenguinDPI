using System.Diagnostics;
using System.Threading;

namespace TrayPenguinDPI.Helpers
{
    public static class ProcessHelper
    {
        private static readonly object _processLock = new();

        public static async Task<int> RunProcessAsync(string fileName, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(processInfo);
            if (process == null) return -1;
            await Task.Run(() => process.WaitForExit());
            return process.ExitCode;
        }

        public static async Task<(int ExitCode, string Output)> RunProcessWithOutputAsync(string fileName, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(processInfo);
            if (process == null) return (-1, string.Empty);

            string output = await process.StandardOutput.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit());
            return (process.ExitCode, output);
        }

        public static async Task<bool> CleanupProcessesAndServicesAsync()
        {
            lock (_processLock)
            {
                foreach (var process in Process.GetProcessesByName("winws"))
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    process.Dispose();
                }
            }

            foreach (var (stopArgs, deleteArgs) in new[] {
                ("stop \"WinDivert\"", "delete \"WinDivert\""),
                ("stop \"WinDivert14\"", "delete \"WinDivert14\"")
            })
            {
                await RunProcessAsync("net.exe", stopArgs);
                await RunProcessAsync("sc.exe", deleteArgs);
            }
            return true;
        }

        public static Process StartProcess(string executable, string args)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = App.ZapretPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process? process = Process.Start(processInfo) ?? throw new InvalidOperationException($"Failed to start process: {executable}");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
    }
}