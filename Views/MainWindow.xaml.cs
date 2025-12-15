using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WorkshopTracker.Models;
using WorkshopTracker.Services;

namespace WorkshopTracker.Views
{
    public partial class MainWindow : Window
    {
        private readonly string _branch;
        private readonly string _currentUser;
        private readonly ConfigServices _config;

        private static readonly string[] DateFormats =
        {
            "dd/MM/yyyy", "d/M/yyyy", "d/MM/yyyy", "dd/M/yyyy"
        };

        private ObservableCollection<WorkRow> _openRows = new();
        private ObservableCollection<WorkRow> _closedRows = new();

        private ICollectionView? _openView;
        private ICollectionView? _closedView;

        private Point _dragStartPoint;
        private DataGrid? _dragSourceGrid;
        private WorkRow? _draggedRow;

        private FileSystemWatcher? _watcher;
        private bool _isReloading;

        private string BaseFolder => _config.BaseFolder;

        public MainWindow(string branch, string currentUser, ConfigServices config)
        {
            InitializeComponent();

            _branch = branch;
            _currentUser = currentUser;
            _config = config;

            DataContext = this;

            OpenGrid.CellEditEnding += WorkGrid_CellEditEnding;
            ClosedGrid.CellEditEnding += WorkGrid_CellEditEnding;

            Loaded += MainWindow_Loaded;

            InitFileWatcher();
        }

        #region Paths & IO

        private string GetOpenPath() =>
            Path.Combine(BaseFolder, $"{_branch}open.csv");

        private string GetClosedPath() =>
            Path.Combine(BaseFolder, $"{_branch}closed.csv");

        private void LoadAll()
        {
            try
            {
                _openRows = new ObservableCollection<WorkRow>(
                    File.Exists(GetOpenPath()) ? LoadFromCsv(GetOpenPath()) : new List<WorkRow>());

                _closedRows = new ObservableCollection<WorkRow>(
                    File.Exists(GetClosedPath()) ? LoadFromCsv(GetClosedPath()) : new List<WorkRow>());
            }
            catch (IOException ex)
            {
                StatusTextBlock.Text = $"Error loading CSVs: {ex.Message}";
            }
        }

        private void SaveAll()
        {
            try
            {
                if (_watcher != null)
                    _watcher.EnableRaisingEvents = false;

                SaveToCsv(GetOpenPath(), _openRows);
                SaveToCsv(GetClosedPath(), _closedRows);

                StatusTextBlock.Text = $"Saved at {DateTime.Now:T}";
            }
            catch (IOException ex)
            {
                StatusTextBlock.Text = $"Error saving CSVs: {ex.Message}";
            }
            finally
            {
                if (_watcher != null)
                    _watcher.EnableRaisingEvents = true;
            }
        }

        private static List<WorkRow> LoadFromCsv(string path)
        {
            var result = new List<WorkRow>();

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
                return result;

            // first line is header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 15)
                    continue;

                var row = new WorkRow
                {
                    Retail = parts[0],
                    OE = parts[1],
                    Customer = parts[2],
                    Serial = parts[3],
                    DayDue = parts[4],
                    DateDue = ParseNullableDate(parts[5]),
                    Status = parts[6],
                    Qty = ParseInt(parts[7]),
                    WhatIsIt = parts[8],
                    PO = parts[9],
                    WhatAreWeDoing = parts[10],
                    Parts = parts[11],
                    Shaft = parts[12],
                    Priority = parts[13],
                    LastUser = parts[14]
                };

                // Auto-fill DayDue from DateDue if missing
                if (string.IsNullOrWhiteSpace(row.DayDue) && row.DateDue.HasValue)
                {
                    row.DayDue = DayOfWeekToShortName(row.DateDue.Value);
                }

                result.Add(row);
            }

            return result;
        }

        private static void SaveToCsv(string path, IEnumerable<WorkRow> rows)
        {
            var list = rows.ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var writer = new StreamWriter(path, false);

            writer.WriteLine("RETAIL,OE,CUSTOMER,SERIAL,DAY DUE,DATE DUE,STATUS,QTY,WHAT IS IT,PO,WHAT ARE WE DOING,PARTS,SHAFT,PRIORITY,LAST USER");

            foreach (var r in list)
            {
                if (r.IsGroupRow)
                    continue;

                var fields = new[]
                {
                    EscapeCsv(r.Retail),
                    EscapeCsv(r.OE),
                    EscapeCsv(r.Customer),
                    EscapeCsv(r.Serial),
                    EscapeCsv(r.DayDue),
                    r.DateDue?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                    EscapeCsv(r.Status),
                    r.Qty.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(r.WhatIsIt),
                    EscapeCsv(r.PO),
                    EscapeCsv(r.WhatAreWeDoing),
                    EscapeCsv(r.Parts),
                    EscapeCsv(r.Shaft),
                    EscapeCsv(r.Priority),
                    EscapeCsv(r.LastUser)
                };

                writer.WriteLine(string.Join(",", fields));
            }
        }

        private static int ParseInt(string s)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value;
            return 0;
        }

        private static DateTime? ParseNullableDate(string s)
        {
            if (DateTime.TryParseExact(
                    s,
                    DateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return dt.Date;
            }
            return null;
        }

        private static string DayOfWeekToShortName(DateTime date)
        {
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Monday: return "Mon";
                case DayOfWeek.Tuesday: return "Tues";
                case DayOfWeek.Wednesday: return "Wed";
                case DayOfWeek.Thursday: return "Thur";
                case DayOfWeek.Friday: return "Fri";
                default: return string.Empty; // weekend
            }
        }

        private static string EscapeCsv(string? s)
        {
            s ??= "";
            if (s.Contains(",") || s.Contains("\""))
            {
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }
            return s;
        }

        #endregion

        #region File watcher

        private void InitFileWatcher()
        {
            try
            {
                if (!Directory.Exists(BaseFolder))
                    return;

                _watcher = new FileSystemWatcher(BaseFolder, "*.csv");
                _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
                _watcher.Changed += Watcher_OnChanged;
                _watcher.Created += Watcher_OnChanged;
                _watcher.Renamed += Watcher_OnChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
            }
        }

        private void Watcher_OnChanged(object sender, FileSystemEventArgs e)
        {
            string openPath = GetOpenPath();
            string closedPath = GetClosedPath();

            if (!string.Equals(e.FullPath, openPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(e.FullPath, closedPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (_isReloading)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _isReloading = true;
                    ReloadFromDisk();
                    StatusTextBlock.Text = $"Auto-reloaded at {DateTime.Now:T}";
                }
                catch (IOException ex)
                {
                    StatusTextBlock.Text = $"Auto-reload error: {ex.Message}";
                }
                finally
                {
                    _isReloading = false;
                }
            }), DispatcherPriority.Background);
        }

        #endregion

        #region Lifecycle & status rules

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadFromDisk();
        }

        private void ReloadFromDisk()
        {
            LoadAll();

            OpenGrid.ItemsSource = _openRows;
            ClosedGrid.ItemsSource = _closedRows;

            _openView = CollectionViewSource.GetDefaultView(OpenGrid.ItemsSource);
            _closedView = CollectionViewSource.GetDefaultView(ClosedGrid.ItemsSource);

            if (_openView != null)
                _openView.Filter = OpenFilter;

            if (_closedView != null)
                _closedView.Filter = ClosedFilter;

            MoveExistingClosedStatusesToClosed();
            RebuildOpenWithDividers();
            RefreshViews();
        }

        private static bool IsClosedStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim().ToLowerInvariant();
            return status == "picked up" || status == "cancelled";
        }

        private static bool IsPaintShopStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim().ToLowerInvariant();
            return status == "paint shop";
        }

        private void WorkGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            if (sender is not DataGrid grid)
                return;

            if (e.Row?.Item is not WorkRow rowItem)
                return;

            if (rowItem.IsGroupRow)
                return;

            bool isStatusColumn =
                e.Column?.Header != null &&
                string.Equals(e.Column.Header.ToString(), "STATUS", StringComparison.OrdinalIgnoreCase);

            bool isDateDueColumn =
                e.Column?.Header != null &&
                string.Equals(e.Column.Header.ToString(), "DATE DUE", StringComparison.OrdinalIgnoreCase);

            string? newStatus = null;
            DateTime? newDateDue = null;

            if (isStatusColumn)
            {
                if (e.EditingElement is ComboBox cb)
                    newStatus = cb.Text;
                else if (e.EditingElement is TextBox tb)
                    newStatus = tb.Text;

                if (string.IsNullOrWhiteSpace(newStatus))
                    newStatus = null;
                else
                    newStatus = newStatus.Trim();
            }

            if (isDateDueColumn)
            {
                if (e.EditingElement is DatePicker dp)
                {
                    newDateDue = dp.SelectedDate?.Date;
                }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // If DATE DUE was edited, update DateDue + DayDue first
                if (isDateDueColumn && newDateDue.HasValue)
                {
                    rowItem.DateDue = newDateDue.Value;
                    rowItem.DayDue = DayOfWeekToShortName(newDateDue.Value);
                    // Rebuild dividers/date order whenever date changes
                    if (grid == OpenGrid)
                        RebuildOpenWithDividers();
                }
                else
                {
                    // If other columns (not DATE DUE) edited in open grid, still keep dividers
                    if (grid == OpenGrid)
                        RebuildOpenWithDividers();
                }

                // If STATUS changed, apply status rules (move to Closed, paint shop ordering)
                if (isStatusColumn && newStatus != null)
                {
                    ApplyStatusRules(grid, rowItem, newStatus);
                }

                // Update last user on any edit
                rowItem.LastUser = _currentUser;

                SaveAll();
                RefreshViews();
            }), DispatcherPriority.Background);
        }

        private void ApplyStatusRules(DataGrid editingGrid, WorkRow rowItem, string newStatus)
        {
            rowItem.Status = newStatus;

            // picked up / cancelled -> Closed
            if (IsClosedStatus(newStatus))
            {
                MoveRowBetweenCollections(_openRows, _closedRows, rowItem);
            }

            // Ensure Open grid is in the right order
            RebuildOpenWithDividers();
        }

        private void MoveExistingClosedStatusesToClosed()
        {
            var toMove = _openRows
                .Where(r => !r.IsGroupRow && IsClosedStatus(r.Status))
                .ToList();

            foreach (var r in toMove)
            {
                _openRows.Remove(r);
                _closedRows.Add(r);
            }
        }

        private void MoveRowBetweenCollections(ObservableCollection<WorkRow> from,
                                               ObservableCollection<WorkRow> to,
                                               WorkRow row)
        {
            if (from.Contains(row))
            {
                from.Remove(row);
                to.Add(row);
            }
        }

        /// <summary>
        /// Rebuilds Open Works:
        ///  • paint shop rows first (sorted by DateDue)
        ///  • then all other rows sorted by DateDue
        ///  • inserts one uneditable group row per date.
        /// </summary>
        private void RebuildOpenWithDividers()
        {
            var baseRows = _openRows.Where(r => !r.IsGroupRow).ToList();

            var paintRows = baseRows
                .Where(r => IsPaintShopStatus(r.Status))
                .OrderBy(r => r.DateDue ?? DateTime.MaxValue)
                .ToList();

            var otherRows = baseRows
                .Where(r => !IsPaintShopStatus(r.Status))
                .OrderBy(r => r.DateDue ?? DateTime.MaxValue)
                .ToList();

            var ordered = new List<WorkRow>();
            ordered.AddRange(paintRows);
            ordered.AddRange(otherRows);

            _openRows.Clear();

            DateTime? currentDate = null;
            bool first = true;

            foreach (var row in ordered)
            {
                var date = row.DateDue?.Date;

                if (date.HasValue && (first || currentDate == null || date.Value != currentDate.Value))
                {
                    currentDate = date.Value;
                    first = false;

                    _openRows.Add(new WorkRow
                    {
                        IsGroupRow = true,
                        Customer = currentDate.Value.ToString("dd/MM/yyyy"),
                        DateDue = currentDate.Value
                    });
                }

                _openRows.Add(row);
            }
        }

        #endregion

        #region Toolbar handlers

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            ReloadFromDisk();
            StatusTextBlock.Text = "Reloaded";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveAll();
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            if (grid.SelectedItem is WorkRow row && !row.IsGroupRow)
            {
                var collection = grid == OpenGrid ? _openRows : _closedRows;
                collection.Remove(row);

                if (grid == OpenGrid)
                    RebuildOpenWithDividers();

                SaveAll();
                RefreshViews();
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            if (FontSizeComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                double.TryParse(tag, out double size))
            {
                if (OpenGrid != null)
                    OpenGrid.FontSize = size;

                if (ClosedGrid != null)
                    ClosedGrid.FontSize = size;
            }
        }

        #endregion

        #region Search filtering

        private bool OpenFilter(object obj)
        {
            if (obj is not WorkRow row) return false;
            return FilterRow(row);
        }

        private bool ClosedFilter(object obj)
        {
            if (obj is not WorkRow row) return false;
            return FilterRow(row);
        }

        private bool FilterRow(WorkRow row)
        {
            if (row.IsGroupRow)
                return true;

            var text = SearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            text = text.Trim().ToLowerInvariant();

            bool Match(string? s) =>
                !string.IsNullOrEmpty(s) &&
                s.ToLowerInvariant().Contains(text);

            return Match(row.Retail) ||
                   Match(row.OE) ||
                   Match(row.Customer) ||
                   Match(row.Serial) ||
                   Match(row.DayDue) ||
                   (row.DateDue?.ToString("dd/MM/yyyy")?.ToLowerInvariant().Contains(text) ?? false) ||
                   Match(row.Status) ||
                   row.Qty.ToString(CultureInfo.InvariantCulture).Contains(text) ||
                   Match(row.WhatIsIt) ||
                   Match(row.PO) ||
                   Match(row.WhatAreWeDoing) ||
                   Match(row.Parts) ||
                   Match(row.Shaft) ||
                   Match(row.Priority) ||
                   Match(row.LastUser);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshViews();
        }

        private void RefreshViews()
        {
            _openView?.Refresh();
            _closedView?.Refresh();
        }

        #endregion

        #region Drag & drop reordering

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _dragSourceGrid = sender as DataGrid;
            _draggedRow = null;

            if (_dragSourceGrid == null)
                return;

            var row = GetRowUnderMouse(_dragSourceGrid, e.GetPosition(_dragSourceGrid));
            _draggedRow = row;
        }

        private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (_dragSourceGrid == null || _draggedRow == null)
                return;

            var position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (_draggedRow.IsGroupRow)
                return;

            DragDrop.DoDragDrop(_dragSourceGrid, _draggedRow, DragDropEffects.Move);
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (_draggedRow == null)
                return;

            var targetGrid = sender as DataGrid;
            if (targetGrid == null || targetGrid != _dragSourceGrid)
                return;

            var point = e.GetPosition(targetGrid);
            var targetRow = GetRowUnderMouse(targetGrid, point);

            var collection = targetGrid == OpenGrid ? _openRows : _closedRows;
            var oldIndex = collection.IndexOf(_draggedRow);

            if (oldIndex < 0)
                return;

            int newIndex;

            if (targetRow == null)
            {
                newIndex = collection.Count - 1;
            }
            else
            {
                newIndex = collection.IndexOf(targetRow);
                if (newIndex < 0)
                    newIndex = collection.Count - 1;
            }

            if (newIndex == oldIndex)
                return;

            collection.RemoveAt(oldIndex);
            collection.Insert(newIndex, _draggedRow);

            if (targetGrid == OpenGrid)
                RebuildOpenWithDividers();

            _draggedRow.LastUser = _currentUser;

            SaveAll();
            RefreshViews();
        }

        private static WorkRow? GetRowUnderMouse(DataGrid grid, Point position)
        {
            var element = grid.InputHitTest(position) as DependencyObject;
            while (element != null && element is not DataGridRow)
            {
                element = VisualTreeHelper.GetParent(element);
            }

            return (element as DataGridRow)?.Item as WorkRow;
        }

        #endregion

        #region Context menu

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var (grid, row) = GetContextMenuTarget(sender);
            if (grid == null || row == null || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            var index = collection.IndexOf(row);
            if (index < 0) index = 0;

            collection.Insert(index, new WorkRow { LastUser = _currentUser });

            if (grid == OpenGrid)
                RebuildOpenWithDividers();

            SaveAll();
            RefreshViews();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var (grid, row) = GetContextMenuTarget(sender);
            if (grid == null || row == null || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            var index = collection.IndexOf(row);
            if (index < 0) index = collection.Count - 1;

            collection.Insert(index + 1, new WorkRow { LastUser = _currentUser });

            if (grid == OpenGrid)
                RebuildOpenWithDividers();

            SaveAll();
            RefreshViews();
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            var (grid, row) = GetContextMenuTarget(sender);
            if (grid == null || row == null || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            var index = collection.IndexOf(row);
            if (index < 0) index = collection.Count - 1;

            var copy = row.Clone();
            copy.LastUser = _currentUser;
            collection.Insert(index + 1, copy);

            if (grid == OpenGrid)
                RebuildOpenWithDividers();

            SaveAll();
            RefreshViews();
        }

        private (DataGrid? grid, WorkRow? row) GetContextMenuTarget(object sender)
        {
            if (sender is not MenuItem menuItem)
                return (null, null);

            if (menuItem.Parent is not ContextMenu ctx)
                return (null, null);

            var grid = ctx.PlacementTarget as DataGrid;
            var row = grid?.SelectedItem as WorkRow;
            return (grid, row);
        }

        #endregion
    }
}
