using System;
using System.ComponentModel;

namespace WorkshopTracker.Models
{
    /// <summary>
    /// One row in the workshop CSV (plus IsGroupRow for date-divider rows).
    /// CSV columns:
    /// RETAIL, OE, CUSTOMER, SERIAL, DAY DUE, DATE DUE, STATUS,
    /// QTY, WHAT IS IT, PO, WHAT ARE WE DOING, PARTS, SHAFT, PRIORITY, LAST USER
    /// </summary>
    public class WorkRow : INotifyPropertyChanged
    {
        private bool _isGroupRow;
        private string _retail = string.Empty;
        private string _oe = string.Empty;
        private string _customer = string.Empty;
        private string _serial = string.Empty;
        private string _dayDue = string.Empty;
        private DateTime? _dateDue;
        private string _status = string.Empty;
        private string _qty = string.Empty;
        private string _whatIsIt = string.Empty;
        private string _po = string.Empty;
        private string _whatAreWeDoing = string.Empty;
        private string _parts = string.Empty;
        private string _shaft = string.Empty;
        private string _priority = string.Empty;
        private string _lastUser = string.Empty;

        public bool IsGroupRow
        {
            get => _isGroupRow;
            set { _isGroupRow = value; OnPropertyChanged(nameof(IsGroupRow)); }
        }

        public string Retail
        {
            get => _retail;
            set { _retail = value; OnPropertyChanged(nameof(Retail)); }
        }

        public string OE
        {
            get => _oe;
            set { _oe = value; OnPropertyChanged(nameof(OE)); }
        }

        public string Customer
        {
            get => _customer;
            set { _customer = value; OnPropertyChanged(nameof(Customer)); }
        }

        public string Serial
        {
            get => _serial;
            set { _serial = value; OnPropertyChanged(nameof(Serial)); }
        }

        public string DayDue
        {
            get => _dayDue;
            set { _dayDue = value; OnPropertyChanged(nameof(DayDue)); }
        }

        public DateTime? DateDue
        {
            get => _dateDue;
            set { _dateDue = value; OnPropertyChanged(nameof(DateDue)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string Qty
        {
            get => _qty;
            set { _qty = value; OnPropertyChanged(nameof(Qty)); }
        }

        public string WhatIsIt
        {
            get => _whatIsIt;
            set { _whatIsIt = value; OnPropertyChanged(nameof(WhatIsIt)); }
        }

        public string PO
        {
            get => _po;
            set { _po = value; OnPropertyChanged(nameof(PO)); }
        }

        public string WhatAreWeDoing
        {
            get => _whatAreWeDoing;
            set { _whatAreWeDoing = value; OnPropertyChanged(nameof(WhatAreWeDoing)); }
        }

        public string Parts
        {
            get => _parts;
            set { _parts = value; OnPropertyChanged(nameof(Parts)); }
        }

        public string Shaft
        {
            get => _shaft;
            set { _shaft = value; OnPropertyChanged(nameof(Shaft)); }
        }

        public string Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        public string LastUser
        {
            get => _lastUser;
            set { _lastUser = value; OnPropertyChanged(nameof(LastUser)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
