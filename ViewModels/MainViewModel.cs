namespace WorkshopTracker.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private string _branch;
        private string _currentUser;

        public string Branch
        {
            get => _branch;
            set
            {
                if (_branch == value) return;
                _branch = value;
                OnPropertyChanged(nameof(Branch));
            }
        }

        public string CurrentUser
        {
            get => _currentUser;
            set
            {
                if (_currentUser == value) return;
                _currentUser = value;
                OnPropertyChanged(nameof(CurrentUser));
            }
        }

        public MainViewModel(string branch, string currentUser)
        {
            _branch = branch;
            _currentUser = currentUser;
        }
    }
}
