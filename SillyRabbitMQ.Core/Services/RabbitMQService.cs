using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SillyRabbitMQ.Core.Models;

namespace SillyRabbitMQ.Core.Services
{
    public class RabbitMQService : IMessageService
    {
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly Dictionary<string, string> _queueConsumers = new(); // QueueName -> ConsumerTag

        public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true;

        /// <summary>
        /// Invoked when the connection drops unexpectedly (network loss, broker restart, etc.).
        /// The string argument is the shutdown reason message.
        /// </summary>
        public Action<string>? OnConnectionInterrupted { get; set; }

        private HttpClient? _httpClient;
        
        public async Task ConnectAsync(ConnectionProfile profile)
        {
            var factory = new ConnectionFactory
            {
                HostName = profile.HostName,
                Port = profile.Port,
                VirtualHost = profile.VirtualHost,
                UserName = profile.Username,
                Password = profile.Password
            };

            if (profile.UseSsl)
            {
                factory.Ssl = new SslOption { Enabled = true, ServerName = profile.HostName };
            }

            _connection = await factory.CreateConnectionAsync();

            // Detect unexpected disconnections
            _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;

            _channel = await _connection.CreateChannelAsync();

            var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
            var scheme = profile.UseSsl ? "https" : "http";
            _httpClient.BaseAddress = new Uri($"{scheme}://{profile.HostName}:15672/");
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{profile.Username}:{profile.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
        }

        private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
        {
            // Only fire the callback for unexpected shutdowns (not clean user-initiated closes)
            if (e.Initiator != ShutdownInitiator.Application)
            {
                OnConnectionInterrupted?.Invoke(e.ReplyText ?? "Connection lost");
            }
            return Task.CompletedTask;
        }

        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            }
            if (_channel != null && _channel.IsOpen)
            {
                await _channel.CloseAsync();
            }
            if (_connection != null && _connection.IsOpen)
            {
                await _connection.CloseAsync();
            }
            _httpClient?.Dispose();
            _httpClient = null;
        }

        public async Task<string> StartEavesdroppingAsync(string exchange, string routingKey, Action<MessageItem> onMessageReceived)
        {
            if (!IsConnected || _channel == null) throw new InvalidOperationException("Not connected to RabbitMQ.");

            // Create an exclusive temporary queue (autoDelete: false so it survives Pause/BasicCancel)
            var queueDeclareResult = await _channel.QueueDeclareAsync(queue: "", durable: false, exclusive: true, autoDelete: false);
            var queueName = queueDeclareResult.QueueName;
            
            // Bind to the specified exchange
            await _channel.QueueBindAsync(queue: queueName, exchange: exchange, routingKey: routingKey);

            await ResumeEavesdroppingAsync(queueName, onMessageReceived);

            return queueName;
        }

        public async Task PauseEavesdroppingAsync(string queueName)
        {
            if (!IsConnected || _channel == null) return;

            if (_queueConsumers.TryGetValue(queueName, out var consumerTag))
            {
                await _channel.BasicCancelAsync(consumerTag);
                _queueConsumers.Remove(queueName);
            }
        }

        public async Task ResumeEavesdroppingAsync(string queueName, Action<MessageItem> onMessageReceived)
        {
            if (!IsConnected || _channel == null) throw new InvalidOperationException("Not connected to RabbitMQ.");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var message = new MessageItem
                {
                    MessageId = ea.BasicProperties.MessageId ?? string.Empty,
                    CorrelationId = ea.BasicProperties.CorrelationId ?? string.Empty,
                    RoutingKey = ea.RoutingKey,
                    Exchange = ea.Exchange,
                    Timestamp = ea.BasicProperties.Timestamp.UnixTime > 0 
                        ? DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime).UtcDateTime 
                        : DateTime.UtcNow,
                    Body = ea.Body.ToArray(),
                    ContentType = ea.BasicProperties.ContentType ?? string.Empty,
                    Headers = ea.BasicProperties.Headers as IDictionary<string, object>,
                    Redelivered = ea.Redelivered,
                    DeliveryTag = ea.DeliveryTag
                };

                onMessageReceived?.Invoke(message);
                await Task.Yield();
            };

            var consumerTag = await _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);
            _queueConsumers[queueName] = consumerTag;
        }

        public async Task StopEavesdroppingAsync(string queueName)
        {
            if (!IsConnected || _channel == null) return;

            await PauseEavesdroppingAsync(queueName);
            await _channel.QueueDeleteAsync(queueName);
        }

        public Task<string> StartStreamEavesdroppingAsync(string streamName, Action<MessageItem> onMessageReceived)
        {
            // TODO: Implement actual RabbitMQ.Stream.Client logic.
            // Requires initializing StreamSystem and creating a Consumer.
            throw new NotImplementedException("RabbitMQ Streams support is not yet fully implemented.");
        }

        public async Task PublishMessageAsync(string exchange, string routingKey, string payload, IDictionary<string, object>? headers = null)
        {
            if (!IsConnected || _channel == null) throw new InvalidOperationException("Not connected to RabbitMQ.");

            var body = Encoding.UTF8.GetBytes(payload);
            var properties = new BasicProperties();
            
            if (headers != null)
            {
                properties.Headers = new Dictionary<string, object?>(headers!);
            }

            await _channel.BasicPublishAsync(exchange: exchange, routingKey: routingKey, basicProperties: properties, body: body, mandatory: false);
        }

        public Task RequeueMessageAsync(MessageItem message, string targetExchange, string targetRoutingKey)
        {
            return PublishMessageAsync(targetExchange, targetRoutingKey, message.PayloadString, message.Headers);
        }

        private class EntityDto
        {
            public string name { get; set; } = string.Empty;
        }

        public async Task<IEnumerable<string>> GetExchangesAsync()
        {
            if (_httpClient == null) return new List<string>();
            try
            {
                var response = await _httpClient.GetAsync("api/exchanges");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var list = System.Text.Json.JsonSerializer.Deserialize<List<EntityDto>>(json);
                return list?.Select(e => e.name).Where(n => !string.IsNullOrEmpty(n)) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<IEnumerable<string>> GetQueuesAsync()
        {
            if (_httpClient == null) return new List<string>();
            try
            {
                var response = await _httpClient.GetAsync("api/queues");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var list = System.Text.Json.JsonSerializer.Deserialize<List<EntityDto>>(json);
                return list?.Select(e => e.name).Where(n => !string.IsNullOrEmpty(n)) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            if (_channel != null) await _channel.DisposeAsync();
            if (_connection != null) await _connection.DisposeAsync();
        }
    }
}
