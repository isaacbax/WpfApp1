using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkshopTracker
{
    public class DateDueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.Transparent;

            if (value is not DateTime date)
            {
                if (!DateTime.TryParse(value.ToString(), out date))
                    return Brushes.Transparent;
            }

            date = date.Date;
            var today = DateTime.Today;

            if (date < today)
                return Brushes.Red;
            if (date == today)
                return Brushes.Yellow;

            return Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter – no convert back
            return Binding.DoNothing;
        }
    }
}
