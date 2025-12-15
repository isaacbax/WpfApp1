using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkshopTracker.Converters
{
    public class DateDueToBrushConverter : IValueConverter
    {
        // value: DateTime? (DATE DUE)
        // return: Brush (Red before today, Yellow today, Green after today)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.Transparent;

            if (value is DateTime dt)
            {
                var date = dt.Date;
                var today = DateTime.Today;

                if (date < today)
                    return Brushes.Red;
                if (date == today)
                    return Brushes.Yellow;
                return Brushes.LightGreen;
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not used
            return null!;
        }
    }
}
