using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WorkshopTracker.Views;

namespace WorkshopTracker.Views
{
    public partial class LoginWindow : Window
    {
        private const string UsersCsvPath = @"S:\Public\DesignData\users.csv";

        private class UserRecord
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Branch { get; set; } = string.Empty;
        }

        private List<UserRecord> _users = new();

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }

        private void LoadUsers()
        {
            _users.Clear();

            if (!File.Exists(UsersCsvPath))
            {
                MessageBox.Show(
                    $"users.csv not found at:\n{UsersCsvPath}",
                    "Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var lines = File.ReadAllLines(UsersCsvPath);
            if (lines.Length <= 1)
                return; // header only

            // header: username,password,branch
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = line.Split(',');
                if (cols.Length < 3)
                    continue;

                _users.Add(new UserRecord
                {
                    Username = cols[0].Trim(),
                    Password = cols[1].Trim(),
                    Branch = cols[2].Trim()
                });
            }

            var branches = _users
                .Select(u => u.Branch)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b)
                .ToList();

            BranchComboBox.ItemsSource = branches;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password.Trim();
            var selectedBranch = BranchComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter username and password.",
                    "Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Root user: can choose any branch
            if (string.Equals(username, "root", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(password, "root", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(selectedBranch))
                {
                    MessageBox.Show("Please select a branch for root login.",
                        "Login", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                OpenMainWindow("root", selectedBranch);
                return;
            }

            // Normal user must be found in users.csv
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(u.Password, password, StringComparison.Ordinal));

            if (user == null)
            {
                MessageBox.Show("Invalid username or password.",
                    "Login", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(user.Branch))
            {
                MessageBox.Show("Your account does not have a branch assigned.",
                    "Login", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenMainWindow(user.Username, user.Branch);
        }

        private void OpenMainWindow(string username, string branch)
        {
            try
            {
                var main = new MainWindow(username, branch);
                main.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening main window:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
