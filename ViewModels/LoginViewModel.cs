using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WorkshopTracker.Models;
using WorkshopTracker.Services;

namespace WorkshopTracker.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly UserService _userService;
        private readonly List<UserRecord> _users;

        private string _username = string.Empty;
        private string _password = string.Empty;
        private string? _selectedBranch;
        private bool _isBranchEnabled;
        private string? _message;

        public ObservableCollection<string> Branches { get; }

        public string Username
        {
            get => _username;
            set
            {
                if (_username == value) return;
                _username = value;
                OnPropertyChanged(nameof(Username));
                UpdateBranchForUser();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password == value) return;
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        public string? SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (_selectedBranch == value) return;
                _selectedBranch = value;
                OnPropertyChanged(nameof(SelectedBranch));
            }
        }

        public bool IsBranchEnabled
        {
            get => _isBranchEnabled;
            set
            {
                if (_isBranchEnabled == value) return;
                _isBranchEnabled = value;
                OnPropertyChanged(nameof(IsBranchEnabled));
            }
        }

        public string? Message
        {
            get => _message;
            set
            {
                if (_message == value) return;
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public LoginViewModel(UserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _users = _userService.LoadUsers() ?? new List<UserRecord>();

            var branches = _users
                .Select(u => u.Branch)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b)
                .ToList();

            Branches = new ObservableCollection<string>(branches);
            IsBranchEnabled = false;
        }

        private void UpdateBranchForUser()
        {
            Message = string.Empty;

            if (string.IsNullOrWhiteSpace(Username))
            {
                IsBranchEnabled = false;
                SelectedBranch = null;
                return;
            }

            if (string.Equals(Username, "root", StringComparison.OrdinalIgnoreCase))
            {
                // root can pick any branch
                IsBranchEnabled = true;
                if (SelectedBranch == null && Branches.Count > 0)
                    SelectedBranch = Branches[0];
                return;
            }

            var user = _users.FirstOrDefault(
                u => string.Equals(u.Username, Username, StringComparison.OrdinalIgnoreCase));

            if (user != null)
            {
                SelectedBranch = user.Branch;
                IsBranchEnabled = false;
            }
            else
            {
                // Unknown user – don't pre-select a branch
                IsBranchEnabled = false;
                SelectedBranch = null;
            }
        }

        public bool TryLogin(out string branch, out string username, out string errorMessage)
        {
            branch = string.Empty;
            username = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                errorMessage = "Please enter username and password.";
                Message = errorMessage;
                return false;
            }

            var user = _users.FirstOrDefault(
                u => string.Equals(u.Username, Username, StringComparison.OrdinalIgnoreCase));

            if (user == null || !string.Equals(user.Password, Password))
            {
                errorMessage = "Invalid username or password.";
                Message = errorMessage;
                return false;
            }

            if (string.Equals(user.Username, "root", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(SelectedBranch))
                {
                    errorMessage = "Please select a branch.";
                    Message = errorMessage;
                    return false;
                }
                branch = SelectedBranch!;
            }
            else
            {
                branch = user.Branch;
            }

            username = user.Username;
            Message = string.Empty;
            return true;
        }
    }
}
