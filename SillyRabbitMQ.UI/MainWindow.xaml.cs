using System.Windows;
using ICSharpCode.AvalonEdit.Folding;
using Microsoft.Extensions.DependencyInjection;
using SillyRabbitMQ.UI.Helpers;
using SillyRabbitMQ.UI.ViewModels;

namespace SillyRabbitMQ.UI
{
    public partial class MainWindow : Window
    {
        private FoldingManager _foldingManager;
        private BraceFoldingStrategy _foldingStrategy;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            DataContext = viewModel;

            // Setup AvalonEdit Folding
            _foldingManager = FoldingManager.Install(PayloadEditor.TextArea);
            _foldingStrategy = new BraceFoldingStrategy();

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedMessage))
                {
                    // Use the newly added FormattedPayloadString
                    PayloadEditor.Text = viewModel.SelectedMessage?.FormattedPayloadString ?? string.Empty;
                    
                    // Update foldings for the new text
                    _foldingStrategy.UpdateFoldings(_foldingManager, PayloadEditor.Document);
                }
            };
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                if (!string.IsNullOrEmpty(PasswordBoxControl.Password))
                {
                    vm.EditingProfile.Password = PasswordBoxControl.Password;
                }
                PasswordBoxControl.Password = string.Empty; // clear for next time
            }
        }
    }
}