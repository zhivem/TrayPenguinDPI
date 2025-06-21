private async void UninstallButton_Click(object sender, RoutedEventArgs e)
{
    var result = AdonisUI.Controls.MessageBox.Show(
        (string)FindResource("UninstallConfirmationMessage"),
        (string)FindResource("UninstallConfirmationTitle"),
        AdonisUI.Controls.MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

    if (result == MessageBoxResult.Yes)
    {
        try
        {
            await ProcessHelper.CleanupProcessesAndServicesAsync();
            RegistrySettings.ClearAllSettings();

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string? appPath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(appPath))
            {
                App.ShowErrorMessage("Failed to determine executable path.");
                return;
            }

            string batchUrl = "https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/update/uninstall.bat"; 
            string batchPath = Path.Combine(appDir, "uninstall.bat");

            // Скачиваем bat-файл
            using var client = new HttpClient();
            var response = await client.GetAsync(batchUrl);
            response.EnsureSuccessStatusCode();

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(batchPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }

            // Запуск bat-файла с правами администратора
            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                Verb = "runas", // Запуск от имени администратора
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = appDir
            });

            // Небольшая пауза, чтобы процесс точно стартовал
            await Task.Delay(500);
            
            // Дополнительно можно скрыть главное окно или освободить ресурсы
            // Но полное завершение оставляем батнику
        }
        catch (Exception ex)
        {
            App.ShowErrorMessage(string.Format((string)FindResource("UninstallErrorMessage"), ex.Message));
        }
    }
}
