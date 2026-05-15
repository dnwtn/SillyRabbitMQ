using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SillyRabbitMQ.Core.Models;

namespace SillyRabbitMQ.Core.Services
{
    public interface IMessageService : IAsyncDisposable, IDisposable
    {
        bool IsConnected { get; }
        
        Task ConnectAsync(ConnectionProfile profile);
        Task DisconnectAsync();
        
        // Eavesdropping
        Task<string> StartEavesdroppingAsync(string exchange, string routingKey, Action<MessageItem> onMessageReceived);
        Task PauseEavesdroppingAsync(string queueName);
        Task ResumeEavesdroppingAsync(string queueName, Action<MessageItem> onMessageReceived);
        Task StopEavesdroppingAsync(string queueName);
        
        Task<string> StartStreamEavesdroppingAsync(string streamName, Action<MessageItem> onMessageReceived);
        
        // Operations
        Task PublishMessageAsync(string exchange, string routingKey, string payload, IDictionary<string, object>? headers = null);
        Task RequeueMessageAsync(MessageItem message, string targetExchange, string targetRoutingKey);
        
        // Infrastructure Querying
        Task<IEnumerable<string>> GetExchangesAsync();
        Task<IEnumerable<string>> GetQueuesAsync();
    }
}
