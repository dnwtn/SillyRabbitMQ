using System;

namespace SillyRabbitMQ.Core.Models
{
    public class ConnectionProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "New Profile";
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = "guest";
        
        // This should be stored securely (e.g., using DPAPI)
        public string Password { get; set; } = "guest";
        
        public bool UseSsl { get; set; } = false;
        
        // Streams specific
        public int StreamPort { get; set; } = 5552;
    }
}
