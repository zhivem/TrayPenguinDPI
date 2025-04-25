using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Linq;
using System.Windows.Media.Imaging;

namespace TrayPenguinDPI
{
    public partial class NotificationWindow : Window
    {
        private readonly int _timeout;

        public NotificationWindow(string title, string message, int timeout = 2000)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            _timeout = timeout;

            try
            {
                string iconPath = System.IO.Path.Combine(App.AssetsImagesPath, "notif.png");
                if (System.IO.File.Exists(iconPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    AppIconRight.Source = bitmap;
                }
            }
            catch
            {
            }

            var themeDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme") == true);
            if (themeDict != null)
                Resources.MergedDictionaries.Add(themeDict);

            var screen = SystemParameters.WorkArea;
            Left = screen.Right - Width - 10;
            Top = screen.Bottom - Height - 50;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            BeginAnimation(OpacityProperty, fadeIn);

            var timer = new System.Timers.Timer(timeout);
            timer.Elapsed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
                    fadeOut.Completed += (s2, e2) => Close();
                    BeginAnimation(OpacityProperty, fadeOut);
                });
            };
            timer.Start();
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
            fadeOut.Completed += (s, args) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}