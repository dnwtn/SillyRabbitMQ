using System.Windows;
using ICSharpCode.AvalonEdit.Folding;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using SillyRabbitMQ.UI.Helpers;
using SillyRabbitMQ.UI.ViewModels;

namespace SillyRabbitMQ.UI
{
    public partial class MainWindow : Window
    {
        private FoldingManager _foldingManager;
        private BraceFoldingStrategy _foldingStrategy;
        private System.Windows.Threading.DispatcherTimer _plotTimer;
        private ScottPlot.Plottables.Signal? _telemetrySignal;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            DataContext = viewModel;

            // Setup AvalonEdit Folding
            _foldingManager = FoldingManager.Install(PayloadEditor.TextArea);
            _foldingStrategy = new BraceFoldingStrategy();

            // Setup ScottPlot
            TelemetryPlot.Plot.Axes.Title.Label.Text = "Throughput (Messages / Second)";
            TelemetryPlot.Plot.Axes.Bottom.Label.Text = "Seconds";
            TelemetryPlot.Plot.Axes.SetLimitsX(0, 60);

            _plotTimer = new System.Windows.Threading.DispatcherTimer();
            _plotTimer.Interval = TimeSpan.FromMilliseconds(100);
            _plotTimer.Tick += PlotTimer_Tick;
            _plotTimer.Start();
        }

        private void PlotTimer_Tick(object? sender, EventArgs e)
        {
            if (_telemetrySignal != null)
            {
                var vm = DataContext as MainViewModel;
                if (vm?.SelectedSession != null)
                {
                    double maxVal = vm.SelectedSession.TelemetryHistory.Max();
                    TelemetryPlot.Plot.Axes.SetLimitsY(0, Math.Max(10, maxVal * 1.2));
                    TelemetryPlot.Refresh();
                }
            }
        }

        private void ListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Core.Models.MessageItem message)
            {
                UpdatePayloadEditor(message);
            }
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Only handle selection change for the TabControl itself (not inner elements like ListView)
            if (e.Source is System.Windows.Controls.TabControl)
            {
                var vm = DataContext as MainViewModel;
                
                TelemetryPlot.Plot.Clear();
                _telemetrySignal = null;

                if (vm?.SelectedSession != null)
                {
                    UpdatePayloadEditor(vm.SelectedSession.SelectedMessage);
                    _telemetrySignal = TelemetryPlot.Plot.Add.Signal(vm.SelectedSession.TelemetryHistory);
                }
                else
                {
                    UpdatePayloadEditor(null);
                }
                
                TelemetryPlot.Refresh();
            }
        }

        private void UpdatePayloadEditor(Core.Models.MessageItem? message)
        {
            PayloadEditor.Text = message?.FormattedPayloadString ?? string.Empty;
            _foldingStrategy.UpdateFoldings(_foldingManager, PayloadEditor.Document);
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

        private void RePublish_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.PublishEditedMessageCommand.CanExecute(PayloadEditor.Text) == true)
            {
                vm.PublishEditedMessageCommand.Execute(PayloadEditor.Text);
            }
        }

        private void DlqRescue_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.RescueDlqMessageCommand.CanExecute(PayloadEditor.Text) == true)
            {
                vm.RescueDlqMessageCommand.Execute(PayloadEditor.Text);
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(ThemeToggle.IsChecked == true ? MaterialDesignThemes.Wpf.BaseTheme.Dark : MaterialDesignThemes.Wpf.BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }
    }
}