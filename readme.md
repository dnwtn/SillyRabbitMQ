🐇 SillyRabbitMQ
A developer-centric WPF application that provides a real-time window into RabbitMQ clusters. SillyRabbitMQ allows engineers to safely eavesdrop on exchanges, inspect complex payloads, and manipulate traffic without disrupting production pipelines.

Unlike the standard RabbitMQ web UI, this tool is designed specifically for deep-dive debugging, payload analysis, and active traffic manipulation in distributed systems.

✨ Features
- **Multi-Tabbed Architecture:** Simultaneously run multiple independent eavesdropping sessions, each with its own connection, queue, and filter settings.
- **Traffic Eavesdropping ("Sniffer Mode"):** Dynamically bind temporary, auto-delete queues to any exchange and routing key to watch live traffic without stealing messages from production consumers.
- **Deep Payload Filtering (Regex):** Apply regular expressions directly to the message body to filter noise and only capture messages matching your exact criteria.
- **Intelligent Payload Inspection:** Automatically prettify, format, and syntax-highlight JSON payloads using AvalonEdit. 
- **Active Developer Operations:**
  - **Message Cloning & Re-Publishing:** Select an existing message, modify its JSON payload directly in the editor, and re-publish it instantly to any exchange.
  - **DLQ Rescue:** One-click requeueing to parse `x-death` headers and rescue failed messages from Dead Letter Queues back into their primary flows.
- **Real-Time Telemetry:** View live, high-performance graphs of message throughput (msgs/sec) using ScottPlot.
- **Secure Profile Management:** Save and encrypt your local, dev, and production connection strings securely using Windows DPAPI.
- **Dark/Light Mode Theming:** Seamlessly toggle between Material Design Dark and Light themes.

🛠️ Tech Stack
SillyRabbitMQ is built for high performance and responsiveness using modern .NET desktop technologies:

- **Framework:** C# / .NET 10.0
- **UI Presentation:** WPF (Windows Presentation Foundation)
- **Architecture:** MVVM powered by CommunityToolkit.Mvvm
- **Messaging Client:** RabbitMQ.Client (Official)
- **Message Parsing:** Newtonsoft.Json
- **Telemetry & Visuals:** ScottPlot
- **Syntax Highlighting:** AvalonEdit

🛡️ Safety First
SillyRabbitMQ is designed to be safe for production environments. By default, the application uses Passive Declarations (ExchangeDeclarePassive / QueueDeclarePassive) when interacting with existing infrastructure. This guarantees that the viewer cannot accidentally alter the durable/transient status or arguments of your live queues.

🚀 Getting Started
Prerequisites
.NET Desktop Runtime (Version 8.0 or higher)

Visual Studio 2022 (for development)

Installation & Build
Clone the repository:

Bash
git clone https://github.com/yourusername/SillyRabbitMQ.git
Open the solution SillyRabbitMQ.sln in Visual Studio.

Restore NuGet packages.

Build and Run the application (F5).

🤝 Contributing
Contributions, issues, and feature requests are welcome! Feel free to check the issues page.

📝 License
This project is licensed under the MIT License - see the LICENSE file for details.