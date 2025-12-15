using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using WorkshopTracker.Models;      // Adjust if UserRecord is in a different namespace

namespace WorkshopTracker.Views
{
    public partial class LoginWindow : Window
    {
        // Folder containing users.csv, headofficeopen.csv, etc.
        private const string BaseFolder = @"S:\Public\DesignData\";

        private readonly List<UserRecord> _users = new();

        public LoginWindow()
        {
            InitializeComponent();

            // Try to apply window icon, but never crash if it fails
            TrySetWindowIcon();

            Loaded += LoginWindow_Loaded;
        }

        private void TrySetWindowIcon()
        {
            try
            {
                // expects getsitelogo.ico added to project under Assets/ with Build Action = Resource
                var uri = new Uri("pack://application:,,,/Assets/getsitelogo.ico", UriKind.Absolute);
                Icon = BitmapFrame.Create(uri);
            }
            catch
            {
                // If the icon is missing/invalid, just ignore – app should still run
            }
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }

        private void LoadUsers()
        {
            string usersPath = Path.Combine(BaseFolder, "users.csv");

            _users.Clear();
            BranchComboBox.Items.Clear();

            if (!File.Exists(usersPath))
            {
                MessageBox.Show(
                    $"users.csv not found at:\n{usersPath}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            string[] lines;
            try
            {
                // users.csv is small, simple ReadAllLines is fine
                lines = File.ReadAllLines(usersPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to read users.csv:\n{ex.Message}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (lines.Length <= 1)
                return;

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = line.Split(',');
                if (cols.Length < 3)
                    continue;

                var user = new UserRecord
                {
                    Username = cols[0].Trim(),
                    Password = cols[1].Trim(),
                    Branch = cols[2].Trim()
                };

                _users.Add(user);
            }

            // Distinct list of branches for the root dropdown
            var branches = _users
                .Select(u => u.Branch)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b)
                .ToList();

            foreach (var b in branches)
                BranchComboBox.Items.Add(b);

            if (branches.Count > 0)
                BranchComboBox.SelectedIndex = 0;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter a username and password.",
                    "Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == password);

            if (user == null)
            {
                MessageBox.Show("Invalid username or password.",
                    "Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string branch;

            // root user can choose any branch from the ComboBox
            if (string.Equals(user.Username, "root", StringComparison.OrdinalIgnoreCase))
            {
                if (BranchComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a branch.",
                        "Login",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                branch = BranchComboBox.SelectedItem.ToString()!;
            }
            else
            {
                // normal users always use their branch from users.csv
                branch = user.Branch;
            }

            var main = new MainWindow(user.Username, branch);
            main.Show();
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
