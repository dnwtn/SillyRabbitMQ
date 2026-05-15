using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SillyRabbitMQ.Core.Services;
using SillyRabbitMQ.UI.ViewModels;

namespace SillyRabbitMQ.UI
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<IMessageService, RabbitMQService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            return services.BuildServiceProvider();
        }
    }
}

