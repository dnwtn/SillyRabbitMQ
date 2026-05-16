using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SillyRabbitMQ.Core.Models;
using SillyRabbitMQ.Core.Services;

namespace SillyRabbitMQ.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IMessageService _messageService;
        private readonly ProfileManager _profileManager;

        [ObservableProperty]
        private ObservableCollection<ConnectionProfile> _profiles = new();

        [ObservableProperty]
        private ConnectionProfile? _selectedProfile;

        [ObservableProperty]
        private ObservableCollection<EavesdropSession> _sessions = new();

        [ObservableProperty]
        private EavesdropSession? _selectedSession;

        [ObservableProperty]
        private bool _isConnected;

        // Dialog Properties
        [ObservableProperty]
        private bool _isDialogOpen;

        [ObservableProperty]
        private ConnectionProfile _editingProfile = new();

        public MainViewModel(IMessageService messageService, ProfileManager profileManager)
        {
            _messageService = messageService;
            _profileManager = profileManager;

            LoadProfiles();
            
            // Start with one default session
            AddSession();
        }

        private void LoadProfiles()
        {
            var loaded = _profileManager.LoadProfiles();
            if (loaded.Count == 0)
            {
                // Provide a default local profile
                loaded.Add(new ConnectionProfile { Name = "Local Dev", HostName = "localhost" });
                _profileManager.SaveProfiles(loaded);
            }

            foreach (var profile in loaded)
            {
                Profiles.Add(profile);
            }
        }

        partial void OnSelectedProfileChanged(ConnectionProfile? value)
        {
            if (value != null && !IsDialogOpen)
            {
                ConnectCommand.ExecuteAsync(null);
            }
        }

        // Profile Management Commands
        [RelayCommand]
        private void AddProfile()
        {
            EditingProfile = new ConnectionProfile { Name = "New Profile", HostName = "localhost" };
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void EditProfile(ConnectionProfile profile)
        {
            if (profile == null) return;
            EditingProfile = new ConnectionProfile
            {
                Id = profile.Id,
                Name = profile.Name,
                HostName = profile.HostName,
                Port = profile.Port,
                VirtualHost = profile.VirtualHost,
                Username = profile.Username,
                Password = profile.Password,
                UseSsl = profile.UseSsl
            };
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void DeleteProfile(ConnectionProfile profile)
        {
            if (profile != null)
            {
                Profiles.Remove(profile);
                _profileManager.SaveProfiles(Profiles.ToList());
            }
        }

        [RelayCommand]
        private void SaveProfile()
        {
            var existing = Profiles.FirstOrDefault(p => p.Id == EditingProfile.Id);
            if (existing != null)
            {
                var index = Profiles.IndexOf(existing);
                Profiles[index] = EditingProfile;
            }
            else
            {
                Profiles.Add(EditingProfile);
            }

            _profileManager.SaveProfiles(Profiles.ToList());
            IsDialogOpen = false;
        }

        [RelayCommand]
        private void CancelProfile()
        {
            IsDialogOpen = false;
        }

        [RelayCommand]
        private void AddSession()
        {
            var session = new EavesdropSession(_messageService);
            Sessions.Add(session);
            SelectedSession = session;
        }

        [RelayCommand]
        private async Task CloseSessionAsync(EavesdropSession session)
        {
            if (session != null)
            {
                await session.CleanupAsync();
                Sessions.Remove(session);
                if (Sessions.Count == 0)
                {
                    AddSession();
                }
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (SelectedProfile == null) return;

            // Reset statuses
            foreach (var p in Profiles) p.Status = ConnectionStatus.Disconnected;

            SelectedProfile.Status = ConnectionStatus.Connecting;

            try
            {
                await _messageService.DisconnectAsync();
                await _messageService.ConnectAsync(SelectedProfile);
                IsConnected = _messageService.IsConnected;
                SelectedProfile.Status = ConnectionStatus.Connected;
                
                // Clear messages in all sessions
                foreach (var session in Sessions)
                {
                    session.Messages.Clear();
                    session.IsPaused = false;
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                SelectedProfile.Status = ConnectionStatus.Failed;
                MessageBox.Show($"Failed to connect: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task BrowseServerEntitiesAsync()
        {
            if (!_messageService.IsConnected)
            {
                MessageBox.Show("Please connect to a profile first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var exchanges = await _messageService.GetExchangesAsync();
                var queues = await _messageService.GetQueuesAsync();
                
                var exchangeList = exchanges.Any() ? string.Join("\n", exchanges.Take(20)) + (exchanges.Count() > 20 ? "\n...and more" : "") : "None";
                var queueList = queues.Any() ? string.Join("\n", queues.Take(20)) + (queues.Count() > 20 ? "\n...and more" : "") : "None";

                var msg = $"--- EXCHANGES ---\n{exchangeList}\n\n--- QUEUES ---\n{queueList}";
                MessageBox.Show(msg, "Server Entities (Top 20)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch entities: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PublishEditedMessageAsync(string payload)
        {
            if (!_messageService.IsConnected || SelectedSession == null)
            {
                MessageBox.Show("Please connect and select a session.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var headers = SelectedSession.SelectedMessage?.Headers;
                await _messageService.PublishMessageAsync(SelectedSession.TargetExchange, SelectedSession.TargetRoutingKey, payload, headers);
                MessageBox.Show("Message published successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to publish: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RescueDlqMessageAsync(string payload)
        {
            if (!_messageService.IsConnected || SelectedSession?.SelectedMessage == null)
            {
                MessageBox.Show("Please select a message to rescue.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var msg = SelectedSession.SelectedMessage;
            
            // Look for x-death headers to find original exchange/routing key
            string? originalExchange = null;
            string? originalRoutingKey = null;

            if (msg.Headers != null && msg.Headers.TryGetValue("x-death", out var xDeathObj))
            {
                if (xDeathObj is System.Collections.Generic.List<object> xDeathList && xDeathList.Count > 0)
                {
                    if (xDeathList[0] is System.Collections.Generic.IDictionary<string, object> deathEntry)
                    {
                        if (deathEntry.TryGetValue("exchange", out var exObj) && exObj is byte[] exBytes)
                            originalExchange = System.Text.Encoding.UTF8.GetString(exBytes);
                        
                        if (deathEntry.TryGetValue("routing-keys", out var rkObj) && rkObj is System.Collections.Generic.List<object> rkList && rkList.Count > 0)
                            if (rkList[0] is byte[] rkBytes)
                                originalRoutingKey = System.Text.Encoding.UTF8.GetString(rkBytes);
                    }
                }
            }

            if (originalExchange == null || originalRoutingKey == null)
            {
                MessageBox.Show("Could not find x-death headers to determine original destination. Cannot automatically rescue.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await _messageService.PublishMessageAsync(originalExchange, originalRoutingKey, payload, msg.Headers);
                MessageBox.Show($"Rescued to Exchange '{originalExchange}' with Routing Key '{originalRoutingKey}'!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rescue: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
