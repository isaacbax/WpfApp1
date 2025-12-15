using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkshopTracker.Converters
{
    /// <summary>
    /// Colours DATE DUE:
    ///   - Before today: Red
    ///   - Today: Yellow
    ///   - After today: LightGreen
    /// </summary>
    public class DateDueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.Transparent;

            DateTime date;

            if (value is DateTime dt)
            {
                date = dt;
            }
            else if (!DateTime.TryParse(value.ToString(), culture, DateTimeStyles.None, out date))
            {
                return Brushes.Transparent;
            }

            var today = DateTime.Today;

            if (date.Date < today)
                return Brushes.Red;

            if (date.Date == today)
                return Brushes.Yellow;

            return Brushes.LightGreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter only
            return Binding.DoNothing;
        }
    }
}
