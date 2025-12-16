using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WorkshopTracker.Models;

namespace WorkshopTracker.Views
{
    public partial class MainWindow : Window
    {
        private readonly string _username;
        private readonly string _branch;

        private const string BaseFolder = @"S:\Public\DesignData\";

        private ObservableCollection<WorkRow> _openRows = new();
        private ObservableCollection<WorkRow> _closedRows = new();

        private bool _isLoading;

        // Live-refresh
        private FileSystemWatcher? _openWatcher;
        private FileSystemWatcher? _closedWatcher;
        private bool _suppressWatcher;

        // Are we currently editing a row?
        private bool _isEditingRow;
        // Did a file change happen while we were editing?
        private bool _pendingReload;

        // Last known write time for each watched file (to ignore duplicate events)
        private readonly Dictionary<string, DateTime> _fileWriteTimes = new();

        // Debounce timer so we don't reload repeatedly
        private DispatcherTimer? _reloadDebounceTimer;

        // Drag & drop
        private Point _dragStart;
        private WorkRow? _draggedRow;
        private DataGrid? _dragSourceGrid;

        public MainWindow(string username, string branch)
        {
            InitializeComponent();

            _username = username;
            _branch = branch;

            Title = $"Workshop Tracker - {branch}";
            StatusTextBlock.Text = $"Logged in as {_username} ({_branch})";

            Loaded += MainWindow_Loaded;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _openWatcher?.Dispose();
            _closedWatcher?.Dispose();
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            LoadData();
            SetupFileWatchers();
        }

        // ---------- FILE WATCHERS (LIVE REFRESH) ----------

        private void SetupFileWatchers()
        {
            try
            {
                _openWatcher?.Dispose();
                _closedWatcher?.Dispose();

                string openFile = Path.Combine(BaseFolder, $"{_branch}open.csv");
                string closedFile = Path.Combine(BaseFolder, $"{_branch}closed.csv");

                if (File.Exists(openFile))
                    _openWatcher = CreateWatcher(BaseFolder, $"{_branch}open.csv");

                if (File.Exists(closedFile))
                    _closedWatcher = CreateWatcher(BaseFolder, $"{_branch}closed.csv");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting up file watchers:\n{ex.Message}",
                    "Watcher Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private FileSystemWatcher CreateWatcher(string folder, string fileName)
        {
            var watcher = new FileSystemWatcher(folder, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };

            watcher.Changed += (s, e) => OnCsvChanged(e);
            watcher.Created += (s, e) => OnCsvChanged(e);
            watcher.Renamed += (s, e) => OnCsvChanged(e);

            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private void OnCsvChanged(FileSystemEventArgs e)
        {
            // Always marshal back to UI thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var path = e.FullPath;

                // Get latest write time
                DateTime lastWrite;
                try
                {
                    lastWrite = File.GetLastWriteTimeUtc(path);
                }
                catch
                {
                    // File might be temporarily locked / removed
                    return;
                }

                // Ignore duplicate events for same write time
                if (_fileWriteTimes.TryGetValue(path, out var known) && lastWrite == known)
                    return;

                _fileWriteTimes[path] = lastWrite;

                // Ignore our own save cycle
                if (_suppressWatcher)
                    return;

                // If user is currently editing, queue a single reload for after they finish
                if (_isEditingRow)
                {
                    _pendingReload = true;
                    return;
                }

                ScheduleReload();
            }), DispatcherPriority.Background);
        }

        private void ScheduleReload()
        {
            if (_reloadDebounceTimer == null)
            {
                _reloadDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _reloadDebounceTimer.Tick += (s, e) =>
                {
                    _reloadDebounceTimer!.Stop();
                    StatusTextBlock.Text = "External changes detected, reloading…";
                    LoadData();
                };
            }

            _reloadDebounceTimer.Stop();
            _reloadDebounceTimer.Start();
        }

        // ---------- LOAD / SAVE & AUTO SORT ----------

        private void LoadData()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                string openPath = Path.Combine(BaseFolder, $"{_branch}open.csv");
                string closedPath = Path.Combine(BaseFolder, $"{_branch}closed.csv");

                var openCore = ReadCsv(openPath);
                var closedCore = ReadCsv(closedPath);

                // Auto-move picked up / cancelled from open -> closed
                var toMove = openCore
                    .Where(r =>
                        !r.IsGroupRow &&
                        (EqualsIgnoreCase(r.Status, "picked up") ||
                         EqualsIgnoreCase(r.Status, "cancelled")))
                    .ToList();

                foreach (var r in toMove)
                {
                    openCore.Remove(r);
                    closedCore.Add(r);
                }

                _openRows = new ObservableCollection<WorkRow>(InsertDayDividers(openCore));
                _closedRows = new ObservableCollection<WorkRow>(InsertDayDividers(closedCore));

                OpenGrid.ItemsSource = _openRows;
                ClosedGrid.ItemsSource = _closedRows;

                StatusTextBlock.Text =
                    $"Loaded Open: {openCore.Count} items | Closed: {closedCore.Count} items";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading CSV files for branch '{_branch}':\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private static bool EqualsIgnoreCase(string? a, string b) =>
            string.Equals(a ?? string.Empty, b, StringComparison.OrdinalIgnoreCase);

        private List<WorkRow> ReadCsv(string path)
        {
            var items = new List<WorkRow>();

            if (!File.Exists(path))
                return items;

            string[] lines;

            try
            {
                lines = ReadAllLinesShared(path);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show(
                    $"Could not read file:\n{path}\n\n{ioEx.Message}",
                    "Read Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return items;
            }

            if (lines.Length <= 1)
                return items;

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = SplitCsvLine(line);
                if (cols.Length < 14)
                    continue;

                var row = new WorkRow
                {
                    Retail = GetCol(cols, 0),
                    OE = GetCol(cols, 1),
                    Customer = GetCol(cols, 2),
                    Serial = GetCol(cols, 3),
                    DayDue = GetCol(cols, 4),
                    Status = GetCol(cols, 6),
                    Qty = GetCol(cols, 7),
                    WhatIsIt = GetCol(cols, 8),
                    PO = GetCol(cols, 9),
                    WhatAreWeDoing = GetCol(cols, 10),
                    Parts = GetCol(cols, 11),
                    Shaft = GetCol(cols, 12),
                    Priority = GetCol(cols, 13),
                    LastUser = cols.Length > 14 ? GetCol(cols, 14) : string.Empty,
                    IsGroupRow = false
                };

                var rawDate = GetCol(cols, 5);
                if (!string.IsNullOrWhiteSpace(rawDate))
                {
                    if (DateTime.TryParseExact(rawDate, "dd/MM/yyyy",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dd)
                        || DateTime.TryParse(rawDate, out dd))
                    {
                        row.DateDue = dd;
                        if (string.IsNullOrWhiteSpace(row.DayDue))
                            row.DayDue = dd.ToString("ddd", CultureInfo.InvariantCulture);
                    }
                }

                items.Add(row);
            }

            // Auto-sort:
            //   - All "paint shop" rows first
            //   - Then others sorted by DATE DUE
            var paintShop = items
                .Where(r => EqualsIgnoreCase(r.Status, "paint shop"))
                .ToList();

            var others = items
                .Where(r => !EqualsIgnoreCase(r.Status, "paint shop"))
                .OrderBy(r => r.DateDue ?? DateTime.MaxValue)
                .ToList();

            return paintShop.Concat(others).ToList();
        }

        private string[] ReadAllLinesShared(string path)
        {
            var lines = new List<string>();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                lines.Add(line);
            }
            return lines.ToArray();
        }

        private string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string current = "";

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }

            result.Add(current);
            return result.ToArray();
        }

        private string GetCol(string[] cols, int index) =>
            index < cols.Length ? cols[index].Trim() : string.Empty;

        private List<WorkRow> InsertDayDividers(List<WorkRow> source)
        {
            var items = source
                .Where(r => !r.IsGroupRow)
                .OrderBy(r => r.DateDue ?? DateTime.MaxValue)
                .ToList();

            var result = new List<WorkRow>();
            string? currentKey = null;

            foreach (var item in items)
            {
                string key;
                string caption;

                if (item.DateDue.HasValue)
                {
                    key = item.DateDue.Value.ToString("yyyy-MM-dd");
                    caption = item.DateDue.Value.ToString("dd/MM/yyyy");
                }
                else
                {
                    key = "NO_DATE";
                    caption = "No Date";
                }

                if (currentKey != key)
                {
                    currentKey = key;
                    result.Add(new WorkRow
                    {
                        IsGroupRow = true,
                        Customer = caption,
                        DateDue = item.DateDue
                    });
                }

                result.Add(item);
            }

            return result;
        }

        private void SaveCsv(string path, IEnumerable<WorkRow> rows)
        {
            var realRows = rows.Where(r => !r.IsGroupRow).ToList();

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs);

            sw.WriteLine("RETAIL,OE,CUSTOMER,SERIAL,DAY DUE,DATE DUE,STATUS,QTY,WHAT IS IT,PO,WHAT ARE WE DOING,PARTS,SHAFT,PRIORITY,LAST USER");

            foreach (var r in realRows)
            {
                var dateStr = r.DateDue.HasValue
                    ? r.DateDue.Value.ToString("dd/MM/yyyy")
                    : "";

                sw.WriteLine(string.Join(",",
                    Escape(r.Retail),
                    Escape(r.OE),
                    Escape(r.Customer),
                    Escape(r.Serial),
                    Escape(r.DayDue),
                    Escape(dateStr),
                    Escape(r.Status),
                    Escape(r.Qty),
                    Escape(r.WhatIsIt),
                    Escape(r.PO),
                    Escape(r.WhatAreWeDoing),
                    Escape(r.Parts),
                    Escape(r.Shaft),
                    Escape(r.Priority),
                    Escape(r.LastUser)));
            }
        }

        private string Escape(string value)
        {
            if (value == null)
                return "";
            if (value.Contains(",") || value.Contains("\""))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private void SaveAll()
        {
            try
            {
                _suppressWatcher = true;

                string openPath = Path.Combine(BaseFolder, $"{_branch}open.csv");
                string closedPath = Path.Combine(BaseFolder, $"{_branch}closed.csv");

                SaveCsv(openPath, _openRows);
                SaveCsv(closedPath, _closedRows);

                // Record our own write times so our watcher ignores this save
                try
                {
                    _fileWriteTimes[openPath] = File.GetLastWriteTimeUtc(openPath);
                }
                catch { }
                try
                {
                    _fileWriteTimes[closedPath] = File.GetLastWriteTimeUtc(closedPath);
                }
                catch { }

                StatusTextBlock.Text = $"Autosaved at {DateTime.Now:T}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving CSV:\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _suppressWatcher = false;
            }
        }

        // ---------- BUTTON HANDLERS ----------

        private void Reload_Click(object sender, RoutedEventArgs e) => LoadData();

        private void Save_Click(object sender, RoutedEventArgs e) => SaveAll();

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            if (grid.SelectedItem is not WorkRow row || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            collection.Remove(row);

            // Don't touch LastUser here – row is being deleted.
            SaveAll();
        }

        // ---------- SEARCH ----------

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchTextBox.Text?.Trim() ?? string.Empty;

            LoadData(); // reset to full list first

            if (string.IsNullOrEmpty(text))
                return;

            bool Matches(WorkRow r) =>
                (!string.IsNullOrEmpty(r.Customer) &&
                 r.Customer.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(r.Serial) &&
                 r.Serial.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(r.WhatIsIt) &&
                 r.WhatIsIt.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(r.PO) &&
                 r.PO.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(r.WhatAreWeDoing) &&
                 r.WhatAreWeDoing.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);

            var openCore = _openRows.Where(r => !r.IsGroupRow && Matches(r)).ToList();
            var closedCore = _closedRows.Where(r => !r.IsGroupRow && Matches(r)).ToList();

            _openRows = new ObservableCollection<WorkRow>(InsertDayDividers(openCore));
            _closedRows = new ObservableCollection<WorkRow>(InsertDayDividers(closedCore));

            OpenGrid.ItemsSource = _openRows;
            ClosedGrid.ItemsSource = _closedRows;
        }

        // ---------- FONT SIZE ----------

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OpenGrid == null || ClosedGrid == null)
                return;

            if (FontSizeComboBox.SelectedItem is not ComboBoxItem item)
                return;

            if (!int.TryParse(item.Tag?.ToString(), out var size))
                return;

            OpenGrid.FontSize = size;
            ClosedGrid.FontSize = size;
        }

        // ---------- DATE PICKER (auto DayDue + autosave) ----------

        private void DateDuePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DatePicker dp) return;
            if (dp.DataContext is not WorkRow row) return;
            if (row.IsGroupRow) return;

            if (row.DateDue.HasValue)
            {
                row.DayDue = row.DateDue.Value.ToString("ddd", CultureInfo.InvariantCulture);
            }

            // This specific row has changed
            row.LastUser = _username;
            SaveAll();
        }



        // ---------- STATUS CHANGES (move between open/closed + autosave) ----------

        private void StatusComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Used to ignore the first SelectionChanged that fires when the ComboBox is created/bound.
            if (sender is ComboBox cb && cb.Tag == null)
                cb.Tag = "INIT";
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (sender is not ComboBox cb) return;
            if (cb.DataContext is not WorkRow row || row.IsGroupRow) return;

            // WPF fires SelectionChanged once when the ComboBox first binds its SelectedItem.
            // Ignore that first event so the dropdown doesn't instantly close/move the row.
            if (cb.Tag as string != "READY")
            {
                cb.Tag = "READY";
                return;
            }

            // Let binding update row.Status first, then migrate if required.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var grid = FindVisualParent<DataGrid>(cb);
                if (grid == null) return;

                try
                {
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch
                {
                    // Don't block the UI if commit fails.
                }

                // Status changes can move the row before RowEditEnding fires,
                // so ensure we clear editing state here to keep live-refresh working.
                _isEditingRow = false;

                row.LastUser = _username;

                if (grid == OpenGrid && IsClosingStatus(row.Status))
                    MoveRowOpenToClosed(row);
                else if (grid == ClosedGrid && !IsClosingStatus(row.Status))
                    MoveRowClosedToOpen(row);

                // Always save on status change (so other PCs pick it up quickly).
                SaveAll();

                // If a file change happened while editing earlier, reload now.
                if (_pendingReload)
                {
                    _pendingReload = false;
                    StatusTextBlock.Text = "External changes detected, reloading…";
                    LoadData();
                }

            }), DispatcherPriority.Background);
        }

        private bool IsClosingStatus(string? status) =>
            EqualsIgnoreCase(status, "picked up") || EqualsIgnoreCase(status, "cancelled");

        private bool MoveRowOpenToClosed(WorkRow row)
        {
            var openCore = _openRows.Where(r => !r.IsGroupRow).ToList();
            var closedCore = _closedRows.Where(r => !r.IsGroupRow).ToList();

            if (!openCore.Remove(row))
                return false;

            closedCore.Add(row);

            _openRows = new ObservableCollection<WorkRow>(InsertDayDividers(openCore));
            _closedRows = new ObservableCollection<WorkRow>(InsertDayDividers(closedCore));

            OpenGrid.ItemsSource = _openRows;
            ClosedGrid.ItemsSource = _closedRows;

            return true;
        }

        private bool MoveRowClosedToOpen(WorkRow row)
        {
            var openCore = _openRows.Where(r => !r.IsGroupRow).ToList();
            var closedCore = _closedRows.Where(r => !r.IsGroupRow).ToList();

            if (!closedCore.Remove(row))
                return false;

            openCore.Add(row);

            _openRows = new ObservableCollection<WorkRow>(InsertDayDividers(openCore));
            _closedRows = new ObservableCollection<WorkRow>(InsertDayDividers(closedCore));

            OpenGrid.ItemsSource = _openRows;
            ClosedGrid.ItemsSource = _closedRows;

            return true;
        }

        // ---------- ROW EDIT AUTOSAVE & EDIT TRACKING ----------

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row?.Item is WorkRow row && !row.IsGroupRow)
            {
                _isEditingRow = true;
            }
        }

        private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                _isEditingRow = false;
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {

                if (e.Row.Item is WorkRow row && !row.IsGroupRow)
                {
                    row.LastUser = _username;

                    var grid = sender as DataGrid;
                    if (grid == OpenGrid && IsClosingStatus(row.Status))
                        MoveRowOpenToClosed(row);
                    else if (grid == ClosedGrid && !IsClosingStatus(row.Status))
                        MoveRowClosedToOpen(row);

                    SaveAll();
                }

                _isEditingRow = false;

                // If a file change happened while editing, reload now
                if (_pendingReload)
                {
                    _pendingReload = false;
                    StatusTextBlock.Text = "External changes detected, reloading…";
                    LoadData();
                }
            }), DispatcherPriority.Background);
        }

        // ---------- CONTEXT MENU ROW OPS ----------

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            if (grid.SelectedItem is not WorkRow row)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            int index = collection.IndexOf(row);
            if (index < 0) return;

            var newRow = CreateBlankRow();
            // creator is the first user to touch it
            newRow.LastUser = _username;

            collection.Insert(index, newRow);
            SaveAll();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            if (grid.SelectedItem is not WorkRow row)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            int index = collection.IndexOf(row);
            if (index < 0) return;

            var newRow = CreateBlankRow();
            newRow.LastUser = _username;

            collection.Insert(index + 1, newRow);
            SaveAll();
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            if (grid.SelectedItem is not WorkRow row || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            int index = collection.IndexOf(row);
            if (index < 0) return;

            var copy = new WorkRow
            {
                Retail = row.Retail,
                OE = row.OE,
                Customer = row.Customer,
                Serial = row.Serial,
                DayDue = row.DayDue,
                DateDue = row.DateDue,
                Status = row.Status,
                Qty = row.Qty,
                WhatIsIt = row.WhatIsIt,
                PO = row.PO,
                WhatAreWeDoing = row.WhatAreWeDoing,
                Parts = row.Parts,
                Shaft = row.Shaft,
                Priority = row.Priority,
                // The user doing the copy is the last user for this new row
                LastUser = _username,
                IsGroupRow = false
            };

            collection.Insert(index + 1, copy);
            SaveAll();
        }

        private WorkRow CreateBlankRow()
        {
            var today = DateTime.Today;
            return new WorkRow
            {
                DateDue = today,
                DayDue = today.ToString("ddd", CultureInfo.InvariantCulture),
                IsGroupRow = false
                // LastUser is set by caller
            };
        }

        // ---------- DRAG & DROP WITH LEFT HANDLE ONLY ----------

        private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _draggedRow = null;
            _dragSourceGrid = null;

            if (sender is FrameworkElement fe)
            {
                _draggedRow = fe.DataContext as WorkRow;
                _dragSourceGrid = FindVisualParent<DataGrid>(fe);
            }
        }

        private void DragHandle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (_draggedRow == null || _draggedRow.IsGroupRow || _dragSourceGrid == null)
                return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            DragDrop.DoDragDrop(_dragSourceGrid, _draggedRow, DragDropEffects.Move);
        }

        private void DragHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedRow = null;
            _dragSourceGrid = null;
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (_draggedRow == null || _draggedRow.IsGroupRow)
                return;

            if (sender is not DataGrid targetGrid)
                return;

            var sourceCollection = _dragSourceGrid == OpenGrid ? _openRows : _closedRows;
            var targetCollection = targetGrid == OpenGrid ? _openRows : _closedRows;

            var targetRowContainer = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
            if (targetRowContainer == null ||
                targetRowContainer.Item is not WorkRow targetRow ||
                targetRow.IsGroupRow)
            {
                _draggedRow = null;
                _dragSourceGrid = null;
                return;
            }

            int oldIndex = sourceCollection.IndexOf(_draggedRow);
            if (oldIndex < 0)
            {
                _draggedRow = null;
                _dragSourceGrid = null;
                return;
            }

            if (sourceCollection == targetCollection)
            {
                int newIndex = targetCollection.IndexOf(targetRow);
                if (newIndex >= 0 && oldIndex != newIndex)
                {
                    targetCollection.Move(oldIndex, newIndex);
                }
            }
            else
            {
                sourceCollection.Remove(_draggedRow);
                int newIndex = targetCollection.IndexOf(targetRow);
                if (newIndex < 0) newIndex = targetCollection.Count;
                targetCollection.Insert(newIndex, _draggedRow);
            }

            SaveAll();

            _draggedRow = null;
            _dragSourceGrid = null;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
