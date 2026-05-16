using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SillyRabbitMQ.Core.Models
{
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }

    public partial class ConnectionProfile : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _name = "New Profile";
        
        [ObservableProperty]
        private string _hostName = "localhost";

        [ObservableProperty]
        private int _port = 5672;

        [ObservableProperty]
        private string _virtualHost = "/";

        [ObservableProperty]
        private string _username = "guest";

        public string Password { get; set; } = "guest";
        
        [ObservableProperty]
        private bool _useSsl = false;

        [ObservableProperty]
        [System.Text.Json.Serialization.JsonIgnore]
        private ConnectionStatus _status = ConnectionStatus.Disconnected;
    }
}
