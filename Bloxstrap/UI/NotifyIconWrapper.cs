﻿using System.Windows;

using Bloxstrap.Integrations;
using Bloxstrap.UI.Elements.ContextMenu;

namespace Bloxstrap.UI
{
    public class NotifyIconWrapper : IDisposable
    {
        // lol who needs properly structured mvvm and xaml when you have the absolute catastrophe that this is

        bool _disposed = false;

        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly MenuContainer _menuContainer = new();
        private RobloxActivity? _activityWatcher;

        private ServerInformation? _serverInformationWindow;

        public DiscordRichPresence? RichPresenceIntegration;
        
        EventHandler? _alertClickHandler;

        public NotifyIconWrapper()
        {
            App.Logger.WriteLine("[NotifyIconWrapper::NotifyIconWrapper] Initializing notification area icon");

            _notifyIcon = new()
            {
                Icon = Properties.Resources.IconBloxstrap,
                Text = App.ProjectName,
                Visible = true
            };

            _notifyIcon.MouseClick += MouseClickEventHandler;

            _menuContainer.Dispatcher.BeginInvoke(_menuContainer.ShowDialog);

            _menuContainer.ServerDetailsMenuItem.Click += (_, _) => ShowServerInformationWindow();

            _menuContainer.Closing += (_, _) => App.Logger.WriteLine("[NotifyIconWrapper::NotifyIconWrapper] Context menu container closed");
        }

        public void SetActivityWatcher(RobloxActivity activityWatcher)
        {
            if (_activityWatcher is not null)
                return;

            _activityWatcher = activityWatcher;
            _activityWatcher.OnGameJoin += (_, _) => Task.Run(OnGameJoin);
            _activityWatcher.OnGameLeave += OnGameLeave;
        }

        public async void OnGameJoin()
        {
            _menuContainer.Dispatcher.Invoke(() => _menuContainer.ServerDetailsMenuItem.Visibility = Visibility.Visible);

            if (App.Settings.Prop.ShowServerDetails)
            {
                string serverLocation = await _activityWatcher!.GetServerLocation();
                ShowAlert("Connnected to server", $"Location: {serverLocation}\nClick for more information", 10, (_, _) => ShowServerInformationWindow());
            }
        }

        public void OnGameLeave(object? sender, EventArgs e)
        {
            _menuContainer.Dispatcher.Invoke(() => _menuContainer.ServerDetailsMenuItem.Visibility = Visibility.Collapsed);

            if (_serverInformationWindow is not null && _serverInformationWindow.IsVisible)
                _serverInformationWindow.Dispatcher.Invoke(_serverInformationWindow.Close);

        }

        public void MouseClickEventHandler(object? sender, System.Windows.Forms.MouseEventArgs e) 
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Right)
                return;

            _menuContainer.Activate();
            _menuContainer.ContextMenu.IsOpen = true;
        }

        public void ShowServerInformationWindow()
        {
            if (_serverInformationWindow is null)
            {
                _serverInformationWindow = new ServerInformation(_activityWatcher!);
                _serverInformationWindow.Closed += (_, _) => _serverInformationWindow = null;
            }

            if (!_serverInformationWindow.IsVisible)
                _serverInformationWindow.Show();

            _serverInformationWindow.Activate();
        }

        public void ShowAlert(string caption, string message, int duration, EventHandler? clickHandler)
        {
            string id = Guid.NewGuid().ToString()[..8];

            App.Logger.WriteLine($"[NotifyIconWrapper::ShowAlert] [{id}] Showing alert for {duration} seconds (clickHandler={clickHandler is not null})");
            App.Logger.WriteLine($"[NotifyIconWrapper::ShowAlert] [{id}] {caption}: {message.Replace("\n", "\\n")}");

            _notifyIcon.BalloonTipTitle = caption;
            _notifyIcon.BalloonTipText = message;

            if (_alertClickHandler is not null)
            {
                App.Logger.WriteLine($"[NotifyIconWrapper::ShowAlert] [{id}] Previous alert still present, erasing click handler");
                _notifyIcon.BalloonTipClicked -= _alertClickHandler;
            }

            _alertClickHandler = clickHandler;
            _notifyIcon.BalloonTipClicked += clickHandler;

            _notifyIcon.ShowBalloonTip(duration);

            Task.Run(async () =>
            {
                await Task.Delay(duration * 1000);
             
                _notifyIcon.BalloonTipClicked -= clickHandler;

                App.Logger.WriteLine($"[NotifyIconWrapper::ShowAlert] [{id}] Duration over, erasing current click handler");

                if (_alertClickHandler == clickHandler)
                    _alertClickHandler = null;
                else
                    App.Logger.WriteLine($"[NotifyIconWrapper::ShowAlert] [{id}] Click handler has been overriden by another alert");
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            App.Logger.WriteLine($"[NotifyIconWrapper::Dispose] Disposing NotifyIcon");

            _menuContainer.Dispatcher.Invoke(_menuContainer.Close);
            _notifyIcon.Dispose();

            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}