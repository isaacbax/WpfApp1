using System.Windows;
using System.Windows.Controls;
using WorkshopTracker.Services;
using WorkshopTracker.ViewModels;

namespace WorkshopTracker.Views
{
    public partial class LoginWindow : Window
    {
        private readonly ConfigServices _config;
        private readonly UserService _userService;
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();

            _config = new ConfigServices();
            _userService = new UserService(_config);
            _viewModel = new LoginViewModel(_userService);

            DataContext = _viewModel;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LoginViewModel vm)
                return;

            if (vm.TryLogin(out var branch, out var username, out var error))
            {
                var main = new MainWindow(branch, username, _config);
                main.Show();
                Close();
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(error, "Login failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
