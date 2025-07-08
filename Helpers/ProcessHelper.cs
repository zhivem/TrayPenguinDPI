using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TrayPenguinDPI.Helpers
{
    public static class ProcessHelper
    {
        private static readonly object _processLock = new();

        public static async Task<int> RunProcessAsync(string fileName, string arguments)
        {
            var result = await RunProcessInternalAsync(fileName, arguments, false, null)
                .ConfigureAwait(false);

            return result.ExitCode;
        }

        public static async Task<(int ExitCode, string Output)> RunProcessWithOutputAsync(string fileName, string arguments)
        {
            var result = await RunProcessInternalAsync(fileName, arguments, true, Encoding.UTF8)
                .ConfigureAwait(false);

            return (result.ExitCode, result.Output ?? string.Empty);
        }

        public static async Task<bool> CleanupProcessesAndServicesAsync()
        {
            lock (_processLock)
            {
                foreach (var process in Process.GetProcessesByName("winws"))
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            var commands = new[]
            {
                ("net.exe", "stop \"WinDivert\"", "sc.exe", "delete \"WinDivert\""),
                ("net.exe", "stop \"WinDivert14\"", "sc.exe", "delete \"WinDivert14\"")
            };

            foreach (var (netExe, stopArgs, scExe, deleteArgs) in commands)
            {
                await RunProcessAsync(netExe, stopArgs).ConfigureAwait(false);
                await RunProcessAsync(scExe, deleteArgs).ConfigureAwait(false);
            }

            return true;
        }

        public static Process StartProcess(string executable, string args)
        {
            try
            {
                if (!File.Exists(executable))
                {
                    throw new FileNotFoundException($"Executable not found: {executable}");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = App.ZapretPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Process? process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new InvalidOperationException($"Failed to start process: {executable}");
                }

                process.EnableRaisingEvents = true;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                return process;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private static async Task<(int ExitCode, string? Output)> RunProcessInternalAsync(
            string fileName,
            string arguments,
            bool captureOutput,
            Encoding? outputEncoding)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = captureOutput,
                StandardOutputEncoding = outputEncoding
            };

            using var process = new Process { StartInfo = processInfo };
            if (!process.Start())
                return (-1, null);

            Task<string> outputTask = captureOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);

            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);

            string? output = captureOutput ? await outputTask.ConfigureAwait(false) : null;
            return (process.ExitCode, output);
        }
    }
}