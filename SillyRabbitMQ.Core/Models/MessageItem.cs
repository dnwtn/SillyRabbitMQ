using System;
using System.Collections.Generic;

namespace SillyRabbitMQ.Core.Models
{
    public class MessageItem
    {
        public string? MessageId { get; set; }
        public string? CorrelationId { get; set; }
        public string? RoutingKey { get; set; }
        public string? Exchange { get; set; }
        public DateTime Timestamp { get; set; }
        public byte[]? Body { get; set; }
        public string? ContentType { get; set; }
        public IDictionary<string, object>? Headers { get; set; }
        public bool Redelivered { get; set; }
        public ulong DeliveryTag { get; set; }
        
        public string PayloadString => Body != null ? System.Text.Encoding.UTF8.GetString(Body) : string.Empty;

        public string FormattedPayloadString
        {
            get
            {
                var raw = PayloadString;
                if (string.IsNullOrWhiteSpace(raw)) return raw;

                try
                {
                    // Attempt to parse and format as JSON
                    var parsedJson = Newtonsoft.Json.Linq.JToken.Parse(raw);
                    return parsedJson.ToString(Newtonsoft.Json.Formatting.Indented);
                }
                catch
                {
                    // If it's not valid JSON, just return the raw string
                    return raw;
                }
            }
        }
    }
}
