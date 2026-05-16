using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SillyRabbitMQ.Core.Models;
using SillyRabbitMQ.Core.Services;

namespace SillyRabbitMQ.UI.ViewModels
{
    public partial class EavesdropSession : ObservableObject
    {
        private readonly IMessageService _messageService;
        private string? _currentQueueName;

        private string _header = "New Session";
        private bool _isAutoUpdatingHeader = false;
        private bool _isCustomHeader = false;

        public string Header
        {
            get => _header;
            set
            {
                if (SetProperty(ref _header, value))
                {
                    if (!_isAutoUpdatingHeader)
                        _isCustomHeader = true;
                }
            }
        }

        [ObservableProperty]
        private string _targetExchange = "amq.topic";

        [ObservableProperty]
        private string _targetRoutingKey = "#";

        [ObservableProperty]
        private string _targetRegexFilter = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MessageItem> _messages = new();

        [ObservableProperty]
        private MessageItem? _selectedMessage;

        [ObservableProperty]
        private bool _isPaused;

        // Telemetry
        public double[] TelemetryHistory { get; } = new double[60];
        private int _messageCountThisSecond;
        private System.Timers.Timer _telemetryTimer;

        public EavesdropSession(IMessageService messageService)
        {
            _messageService = messageService;

            _telemetryTimer = new System.Timers.Timer(1000);
            _telemetryTimer.Elapsed += (s, e) =>
            {
                Array.Copy(TelemetryHistory, 1, TelemetryHistory, 0, TelemetryHistory.Length - 1);
                TelemetryHistory[^1] = System.Threading.Interlocked.Exchange(ref _messageCountThisSecond, 0);
            };
            _telemetryTimer.Start();
        }

        public async Task CleanupAsync()
        {
            if (!string.IsNullOrEmpty(_currentQueueName))
            {
                await _messageService.StopEavesdroppingAsync(_currentQueueName);
                _currentQueueName = null;
            }
            _telemetryTimer.Stop();
            _telemetryTimer.Dispose();
        }

        partial void OnTargetExchangeChanged(string value)
        {
            UpdateHeader();
        }

        partial void OnTargetRoutingKeyChanged(string value)
        {
            UpdateHeader();
        }

        private void UpdateHeader()
        {
            if (_isCustomHeader) return;

            _isAutoUpdatingHeader = true;
            if (string.IsNullOrWhiteSpace(TargetExchange) && string.IsNullOrWhiteSpace(TargetRoutingKey))
                Header = "New Session";
            else
                Header = $"{TargetExchange} -> {TargetRoutingKey}";
            _isAutoUpdatingHeader = false;
        }

        [RelayCommand]
        private async Task BindAsync()
        {
            if (!_messageService.IsConnected)
            {
                MessageBox.Show("Please connect to a profile first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(_currentQueueName))
                {
                    await _messageService.StopEavesdroppingAsync(_currentQueueName);
                }

                Messages.Clear();
                IsPaused = false;

                _currentQueueName = await _messageService.StartEavesdroppingAsync(TargetExchange, TargetRoutingKey, OnMessageReceived);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to bind: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task TogglePauseAsync()
        {
            if (string.IsNullOrEmpty(_currentQueueName)) return;

            try
            {
                if (IsPaused)
                {
                    await _messageService.ResumeEavesdroppingAsync(_currentQueueName, OnMessageReceived);
                    IsPaused = false;
                }
                else
                {
                    await _messageService.PauseEavesdroppingAsync(_currentQueueName);
                    IsPaused = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to toggle pause: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearMessages()
        {
            Messages.Clear();
        }

        [RelayCommand]
        private async Task ClearFilterAsync()
        {
            if (!string.IsNullOrEmpty(_currentQueueName))
            {
                await _messageService.StopEavesdroppingAsync(_currentQueueName);
                _currentQueueName = null;
            }
            TargetExchange = string.Empty;
            TargetRoutingKey = string.Empty;
            TargetRegexFilter = string.Empty;
            IsPaused = false;
        }


        private void OnMessageReceived(MessageItem message)
        {
            System.Threading.Interlocked.Increment(ref _messageCountThisSecond);

            if (!string.IsNullOrWhiteSpace(TargetRegexFilter))
            {
                try
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(message.PayloadString, TargetRegexFilter, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return; // Ignore message
                    }
                }
                catch
                {
                    // Invalid regex, ignore the filter or handle error. We'll just pass it through for now.
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Insert(0, message);
                if (Messages.Count > 1000)
                {
                    Messages.RemoveAt(Messages.Count - 1);
                }
            });
        }

    }
}
