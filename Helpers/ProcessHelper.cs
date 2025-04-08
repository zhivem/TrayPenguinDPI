using System.Diagnostics;
using System.IO;

namespace TrayPenguinDPI.Helpers
{
    public static class ProcessHelper
    {
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

        public static async Task CleanupProcessesAndServices()
        {
            try
            {
                // Завершаем все процессы winws.exe
                foreach (var process in Process.GetProcessesByName("winws"))
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000); // Ждем до 5 секунд
                    }
                    process.Dispose();
                }

                // Останавливаем и удаляем службы WinDivert и WinDivert14
                foreach (var (stopArgs, deleteArgs) in new[] {
                    ("stop \"WinDivert\"", "delete \"WinDivert\""),
                    ("stop \"WinDivert14\"", "delete \"WinDivert14\"")
                })
                {
                    await RunProcessAsync("net.exe", stopArgs);
                    await RunProcessAsync("sc.exe", deleteArgs);
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки, чтобы не прерывать выполнение
            }
        }
    }
}