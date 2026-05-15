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
        private string? _currentQueueName;

        [ObservableProperty]
        private ObservableCollection<ConnectionProfile> _profiles = new();

        [ObservableProperty]
        private ConnectionProfile? _selectedProfile;

        [ObservableProperty]
        private ObservableCollection<MessageItem> _messages = new();

        [ObservableProperty]
        private MessageItem? _selectedMessage;

        [ObservableProperty]
        private string _targetExchange = "amq.topic";

        [ObservableProperty]
        private string _targetRoutingKey = "#";

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isPaused;

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
                // Automatically connect when a profile is selected
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
            // Create a clone to edit
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
        private async Task ConnectAsync()
        {
            if (SelectedProfile == null) return;

            try
            {
                await _messageService.DisconnectAsync();
                await _messageService.ConnectAsync(SelectedProfile);
                IsConnected = _messageService.IsConnected;
                
                // Clear existing messages
                Messages.Clear();
                IsPaused = false;
                _currentQueueName = null;
                
                MessageBox.Show($"Connected to {SelectedProfile.HostName}!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                MessageBox.Show($"Failed to connect: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    // Resume
                    await _messageService.ResumeEavesdroppingAsync(_currentQueueName, OnMessageReceived);
                    IsPaused = false;
                }
                else
                {
                    // Pause
                    await _messageService.PauseEavesdroppingAsync(_currentQueueName);
                    IsPaused = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to toggle pause: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnMessageReceived(MessageItem message)
        {
            // Marshal back to UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Insert(0, message); // Add to top

                // Optional: enforce a max limit to prevent out of memory
                if (Messages.Count > 1000)
                {
                    Messages.RemoveAt(Messages.Count - 1);
                }
            });
        }
    }
}
