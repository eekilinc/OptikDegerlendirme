using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OptikFormApp.Models;

namespace OptikFormApp.Services
{
    public interface INotificationService
    {
        void Show(string message, string title = "", ToastType type = ToastType.Info, TimeSpan? duration = null);
        void ShowSuccess(string message, string title = "Başarılı");
        void ShowError(string message, string title = "Hata");
        void ShowWarning(string message, string title = "Uyarı");
        void ShowInfo(string message, string title = "Bilgi");
        void ClearAll();
        void Dismiss(string notificationId);
        ObservableCollection<ToastNotificationModel> ActiveNotifications { get; }
    }

    public class NotificationService : INotificationService
    {
        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<ToastNotificationModel> _notifications;
        private readonly System.Collections.Generic.Dictionary<string, CancellationTokenSource> _autoDismissTokens;

        public ObservableCollection<ToastNotificationModel> ActiveNotifications => _notifications;

        private readonly object _syncLock = new();

        public NotificationService()
        {
            _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _notifications = new ObservableCollection<ToastNotificationModel>();
            _autoDismissTokens = new System.Collections.Generic.Dictionary<string, CancellationTokenSource>();
        }

        private void RunOnUiThread(Action action)
        {
            var appDispatcher = System.Windows.Application.Current?.Dispatcher;
            if (appDispatcher != null && !appDispatcher.HasShutdownStarted && !appDispatcher.HasShutdownFinished)
            {
                if (appDispatcher.CheckAccess())
                    action();
                else
                    appDispatcher.Invoke(action);
                return;
            }

            if (_dispatcher.CheckAccess() && !_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }

            lock (_syncLock)
            {
                action();
            }
        }

        public void Show(string message, string title = "", ToastType type = ToastType.Info, TimeSpan? duration = null)
        {
            var notification = new ToastNotificationModel
            {
                Title = title,
                Message = message,
                Type = type,
                Duration = duration ?? TimeSpan.FromSeconds(type == ToastType.Error ? 6 : 4),
                IsPersistent = type == ToastType.Error
            };

            RunOnUiThread(() =>
            {
                _notifications.Add(notification);

                if (_notifications.Count > 5)
                {
                    var oldest = _notifications.FirstOrDefault(n => !n.IsPersistent);
                    if (oldest != null)
                    {
                        Dismiss(oldest.Id);
                    }
                }
            });

            if (!notification.IsPersistent && System.Windows.Application.Current != null)
            {
                StartAutoDismiss(notification);
            }
        }

        public void ShowSuccess(string message, string title = "Başarılı")
        {
            Show(message, title, ToastType.Success);
        }

        public void ShowError(string message, string title = "Hata")
        {
            Show(message, title, ToastType.Error);
        }

        public void ShowWarning(string message, string title = "Uyarı")
        {
            Show(message, title, ToastType.Warning);
        }

        public void ShowInfo(string message, string title = "Bilgi")
        {
            Show(message, title, ToastType.Info);
        }

        public void Dismiss(string notificationId)
        {
            RunOnUiThread(() =>
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    _notifications.Remove(notification);
                }

                if (_autoDismissTokens.TryGetValue(notificationId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _autoDismissTokens.Remove(notificationId);
                }
            });
        }

        public void ClearAll()
        {
            RunOnUiThread(() =>
            {
                _notifications.Clear();

                foreach (var cts in _autoDismissTokens.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                _autoDismissTokens.Clear();
            });
        }

        private void StartAutoDismiss(ToastNotificationModel notification)
        {
            var cts = new CancellationTokenSource();
            _autoDismissTokens[notification.Id] = cts;

            Task.Run(async () =>
            {
                try
                {
                    var totalMs = notification.Duration.TotalMilliseconds;
                    var checkInterval = 50;
                    var elapsed = 0;

                    while (elapsed < totalMs && !cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(checkInterval, cts.Token);
                        elapsed += checkInterval;

                        var progress = 100 - (elapsed / totalMs * 100);
                        RunOnUiThread(() =>
                        {
                            notification.Progress = progress;
                        });
                    }

                    if (!cts.Token.IsCancellationRequested)
                    {
                        Dismiss(notification.Id);
                    }
                }
                catch (TaskCanceledException)
                {
                }
            }, cts.Token);
        }
    }
}
