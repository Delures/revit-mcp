using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCP.Plugin.UI
{
    public partial class AgentActivityDockablePane : UserControl, IDockablePaneProvider
    {
        public static readonly Guid PaneId = new Guid("D4A8E24E-721D-40EF-8A35-1317A3EE3691");
        public static readonly DockablePaneId DockablePaneId = new DockablePaneId(PaneId);

        private readonly ObservableCollection<ActivityItemViewModel> _items = new ObservableCollection<ActivityItemViewModel>();
        private UIApplication? _uiApp;

        public AgentActivityDockablePane()
        {
            InitializeComponent();
            ActivityListBox.ItemsSource = _items;
            _items.CollectionChanged += (s, e) =>
            {
                ItemCountTextBlock.Text = _items.Count.ToString();
            };
        }

        public void Initialize(UIApplication uiApp)
        {
            _uiApp = uiApp;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }

        public string BeginActivity(string agentName, string summary)
        {
            string id = Guid.NewGuid().ToString("N");
            Dispatcher.Invoke(() =>
            {
                var item = new ActivityItemViewModel
                {
                    Id = id,
                    AgentName = string.IsNullOrWhiteSpace(agentName) ? "Agent" : agentName,
                    Summary = summary,
                    Status = "Running",
                    Timestamp = DateTime.Now
                };

                _items.Insert(0, item);
                if (_items.Count > 100)
                {
                    _items.RemoveAt(_items.Count - 1);
                }
            });
            return id;
        }

        public void CompleteActivity(
            string activityId,
            string status,
            double durationMs = 0,
            List<long>? affectedElementIds = null,
            string? errorMessage = null)
        {
            Dispatcher.Invoke(() =>
            {
                var item = _items.FirstOrDefault(x => x.Id == activityId);
                if (item != null)
                {
                    item.DurationMs = durationMs;
                    item.ErrorMessage = errorMessage;
                    if (affectedElementIds != null && affectedElementIds.Count > 0)
                    {
                        item.AffectedElementIds = affectedElementIds;
                    }
                    item.Status = status;
                }
                else
                {
                    AddActivity("Agent", "Action", status, affectedElementIds, durationMs, errorMessage);
                }
            });
        }

        public void AddActivity(
            string agentName,
            string summary,
            string status,
            List<long>? affectedElementIds = null,
            double durationMs = 0,
            string? errorMessage = null)
        {
            Dispatcher.Invoke(() =>
            {
                var item = new ActivityItemViewModel
                {
                    Id = Guid.NewGuid().ToString("N"),
                    AgentName = string.IsNullOrWhiteSpace(agentName) ? "Agent" : agentName,
                    Summary = summary,
                    Status = status,
                    Timestamp = DateTime.Now,
                    DurationMs = durationMs,
                    AffectedElementIds = affectedElementIds ?? new List<long>(),
                    ErrorMessage = errorMessage
                };

                _items.Insert(0, item);
                if (_items.Count > 100)
                {
                    _items.RemoveAt(_items.Count - 1);
                }
            });
        }

        public void UpdateUiState(string state, string? docTitle = null)
        {
            Dispatcher.Invoke(() =>
            {
                UiStateTextBlock.Text = state == "Ready" ? "Ready" : state;
                if (state == "Ready")
                {
                    StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EC9B0"));
                }
                else if (state == "InSketchMode")
                {
                    StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CE9178"));
                }
                else
                {
                    StatusDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44747"));
                }

                if (docTitle != null)
                {
                    DocNameTextBlock.Text = docTitle;
                }
            });
        }

        public void UpdatePipeStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                PipeStatusTextBlock.Text = status;
            });
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            _items.Clear();
        }

        private void OnSelectElementsClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ActivityItemViewModel item)
            {
                if (item.AffectedElementIds.Count > 0)
                {
                    SelectElementsInRevit(item.AffectedElementIds);
                }
            }
        }

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ActivityListBox.SelectedItem is ActivityItemViewModel item && item.AffectedElementIds.Count > 0)
            {
                SelectElementsInRevit(item.AffectedElementIds);
            }
        }

        private void SelectElementsInRevit(List<long> elementIds)
        {
            if (_uiApp?.ActiveUIDocument == null) return;

            try
            {
                var doc = _uiApp.ActiveUIDocument.Document;
                var list = new List<ElementId>();
                foreach (long id in elementIds)
                {
#if REVIT_2019 || REVIT_2020 || REVIT_2021 || REVIT_2022 || REVIT_2023
                    list.Add(new ElementId((int)id));
#else
                    list.Add(new ElementId(id));
#endif
                }

                _uiApp.ActiveUIDocument.Selection.SetElementIds(list);
            }
            catch
            {
                // Ignore selection error if elements were deleted
            }
        }
    }

    public class ActivityItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _status = "Running";
        private double _durationMs;
        private List<long> _affectedElementIds = new List<long>();
        private string? _errorMessage;
        private string _summary = string.Empty;
        private string _agentName = "Agent";

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string AgentName
        {
            get => _agentName;
            set { _agentName = value; OnPropertyChanged(); }
        }

        public string Summary
        {
            get => _summary;
            set { _summary = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(IsRunning));
                }
            }
        }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public double DurationMs
        {
            get => _durationMs;
            set
            {
                _durationMs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationText));
                OnPropertyChanged(nameof(HasDurationVisibility));
            }
        }

        public List<long> AffectedElementIds
        {
            get => _affectedElementIds;
            set
            {
                _affectedElementIds = value ?? new List<long>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasElementsVisibility));
                OnPropertyChanged(nameof(ElementCountText));
                OnPropertyChanged(nameof(ButtonSelectText));
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasErrorVisibility));
            }
        }

        public string TimestampText => Timestamp.ToString("HH:mm:ss");

        public string DurationText => DurationMs > 0 ? $"⚡ {DurationMs:N0} ms" : string.Empty;
        public System.Windows.Visibility HasDurationVisibility => DurationMs > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public bool IsRunning => Status == "Running";

        public string StatusText => Status switch
        {
            "Success" => "✅ Done",
            "Error" => "❌ Error",
            "Running" => "⏳ Running...",
            _ => Status
        };

        public Brush StatusColor => Status switch
        {
            "Success" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EC9B0")),
            "Error" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44747")),
            "Running" => new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CE9178")),
            _ => Brushes.Gray
        };

        public System.Windows.Visibility HasElementsVisibility => AffectedElementIds.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public string ElementCountText => $"{AffectedElementIds.Count} items";
        public string ButtonSelectText => $"🎯 Select ({AffectedElementIds.Count})";

        public System.Windows.Visibility HasErrorVisibility => !string.IsNullOrEmpty(ErrorMessage) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
