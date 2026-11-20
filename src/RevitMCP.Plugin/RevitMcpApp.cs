using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitMCP.Plugin.Core;
using RevitMCP.Plugin.Ipc;
using RevitMCP.Plugin.UI;
using RevitMCP.Shared;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Session;

namespace RevitMCP.Plugin
{
    public class RevitMcpApp : IExternalApplication
    {
        static RevitMcpApp()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

#if NET8_0_OR_GREATER
            // Revit 2025 (.NET 8) loads add-ins in a custom AssemblyLoadContext; the
            // native SQLite library (e_sqlite3.dll) is NOT resolved through
            // AppDomain.AssemblyResolve, so hook unmanaged-dll resolution on both the
            // default ALC and this assembly's own ALC.
            var ownAlc = AssemblyLoadContext.GetLoadContext(typeof(RevitMcpApp).Assembly);
            var defaultAlc = AssemblyLoadContext.Default;
            if (ownAlc != null && ownAlc != defaultAlc)
            {
                ownAlc.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;
            }
            defaultAlc.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;
#endif
        }

#if NET8_0_OR_GREATER
        private static IntPtr OnResolvingUnmanagedDll(Assembly assembly, string unmanagedDllName)
        {
            try
            {
                var folder = Path.GetDirectoryName(typeof(RevitMcpApp).Assembly.Location);
                if (string.IsNullOrEmpty(folder)) return IntPtr.Zero;

                var libName = unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? unmanagedDllName
                    : unmanagedDllName + ".dll";

                var rid = RuntimeInformation.RuntimeIdentifier;
                var candidates = new[]
                {
                    Path.Combine(folder, "runtimes", rid, "native", libName),
                    Path.Combine(folder, libName),
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return NativeLibrary.Load(candidate);
                    }
                }
            }
            catch
            {
                // Fall through to the default native resolution
            }
            return IntPtr.Zero;
        }
#endif

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(assemblyName)) return null;

                var folder = Path.GetDirectoryName(typeof(RevitMcpApp).Assembly.Location);
                if (folder != null)
                {
                    var targetDll = Path.Combine(folder, assemblyName + ".dll");
                    if (File.Exists(targetDll))
                    {
                        return Assembly.LoadFrom(targetDll);
                    }
                }
            }
            catch
            {
                // Fallback to default resolver
            }
            return null;
        }

        private static RevitMcpApp? _instance;
        public static RevitMcpApp Instance => _instance ?? throw new InvalidOperationException("RevitMcpApp is not initialized.");

        private UIControlledApplication? _uiApp;
        private RevitTaskDispatcher? _taskDispatcher;
        private NamedPipeIpcServer? _pipeServer;
        private AgentActivityDockablePane? _dockablePane;

        private int _processId;
        private string _revitVersion = string.Empty;
        private string _pipeName = string.Empty;
        private string _uiState = RevitUiStates.Ready;
        private string? _activeDocTitle;
        private string? _activeDocPath;
        private bool _isDocOpen;
        private DateTime _lastHeartbeatUtc = DateTime.UtcNow;

        public Result OnStartup(UIControlledApplication application)
        {
            _instance = this;
            _uiApp = application;

            try
            {
                McpStorageContext.Initialize();

                _processId = Process.GetCurrentProcess().Id;
                _revitVersion = application.ControlledApplication.VersionNumber;
                _pipeName = NamedPipeUtils.GetPipeName(_revitVersion, _processId);

                // 1. Initialize Task Dispatcher
                _taskDispatcher = new RevitTaskDispatcher();

                // 2. Register Dockable Pane
                _dockablePane = new AgentActivityDockablePane();
                application.RegisterDockablePane(
                    AgentActivityDockablePane.DockablePaneId,
                    "Revit MCP Agent Activity",
                    _dockablePane);

                // 3. Register Ribbon Panel & Button to show Dockable Pane
                CreateRibbon(application);

                // 4. Start Named Pipe IPC Server
                _pipeServer = new NamedPipeIpcServer(
                    _pipeName,
                    _taskDispatcher,
                    _dockablePane,
                    GetCurrentSessionInfo);
                _pipeServer.Start();

                // 5. Subscribe to application events
                application.ControlledApplication.DocumentOpened += OnDocumentOpened;
                application.ControlledApplication.DocumentCreated += OnDocumentCreated;
                application.ControlledApplication.DocumentClosed += OnDocumentClosed;
                application.Idling += OnIdling;

                // 6. Write session file
                UpdateSessionRegistry();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit MCP Error", "Startup error: " + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _pipeServer?.Stop();
                SessionRegistry.UnregisterSession(_revitVersion, _processId);

                if (_uiApp != null)
                {
                    _uiApp.ControlledApplication.DocumentOpened -= OnDocumentOpened;
                    _uiApp.ControlledApplication.DocumentCreated -= OnDocumentCreated;
                    _uiApp.ControlledApplication.DocumentClosed -= OnDocumentClosed;
                    _uiApp.Idling -= OnIdling;
                }

                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }

        private void CreateRibbon(UIControlledApplication application)
        {
            string tabName = "Revit MCP";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab may already exist
            }

            var panel = application.CreateRibbonPanel(tabName, "Agent Control");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            var showPaneButton = new PushButtonData(
                "ShowAgentActivityPane",
                "Agent\nActivity",
                assemblyPath,
                typeof(ShowAgentPaneCommand).FullName)
            {
                ToolTip = "Open the Revit MCP agent activity pane",
                LargeImage = LoadRibbonImage("icon32.png"),
                Image = LoadRibbonImage("icon16.png")
            };

            panel.AddItem(showPaneButton);
        }

        private static System.Windows.Media.ImageSource? LoadRibbonImage(string fileName)
        {
            try
            {
                // 1. Try Pack URI from WPF assembly resources
                string uriStr = $"pack://application:,,,/RevitMCP.Plugin;component/Resources/{fileName}";
                var uri = new Uri(uriStr, UriKind.Absolute);
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // 2. Fallback to direct file on disk next to assembly
                try
                {
                    string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                    string filePath = Path.Combine(asmDir, "Resources", fileName);
                    if (!File.Exists(filePath))
                    {
                        filePath = Path.Combine(asmDir, fileName);
                    }
                    if (File.Exists(filePath))
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                catch
                {
                    // Ignore fallback failure
                }
                return null;
            }
        }

        private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
        {
            UpdateDocState(e.Document);
        }

        private void OnDocumentCreated(object? sender, DocumentCreatedEventArgs e)
        {
            UpdateDocState(e.Document);
        }

        private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
        {
            _activeDocTitle = null;
            _activeDocPath = null;
            _isDocOpen = false;
            _dockablePane?.UpdateUiState(_uiState, "No document open");
            UpdateSessionRegistry();
        }

        private void OnIdling(object? sender, IdlingEventArgs e)
        {
            if (sender is UIApplication uiapp)
            {
                _dockablePane?.Initialize(uiapp);

                var doc = uiapp.ActiveUIDocument?.Document;
                if (doc != null)
                {
                    // Check if edit / modifying mode is active
                    bool inSketch = doc.IsModifiable;
                    _uiState = inSketch ? RevitUiStates.InSketchMode : RevitUiStates.Ready;

                    if (_activeDocTitle != doc.Title)
                    {
                        UpdateDocState(doc);
                    }
                    else
                    {
                        _dockablePane?.UpdateUiState(_uiState, doc.Title);
                    }
                }
                else
                {
                    _uiState = RevitUiStates.Ready;
                    _dockablePane?.UpdateUiState(_uiState, "No document open");
                }
            }

            // Periodically refresh heartbeat in session registry
            if ((DateTime.UtcNow - _lastHeartbeatUtc).TotalSeconds > 10)
            {
                _lastHeartbeatUtc = DateTime.UtcNow;
                UpdateSessionRegistry();
            }
        }

        private void UpdateDocState(Document doc)
        {
            _activeDocTitle = doc.Title;
            _activeDocPath = doc.IsModelInCloud ? doc.GetCloudModelPath()?.GetModelGUID().ToString() : doc.PathName;
            _isDocOpen = true;
            _dockablePane?.UpdateUiState(_uiState, _activeDocTitle);
            UpdateSessionRegistry();
        }

        private void UpdateSessionRegistry()
        {
            try
            {
                var session = GetCurrentSessionInfo();
                SessionRegistry.RegisterSession(session);
            }
            catch
            {
                // Ignore transient write errors
            }
        }

        public RevitSessionInfo GetCurrentSessionInfo()
        {
            return new RevitSessionInfo
            {
                ProcessId = _processId,
                RevitVersion = _revitVersion,
                PipeName = _pipeName,
                MainWindowHandle = Process.GetCurrentProcess().MainWindowHandle.ToInt64(),
                MainWindowTitle = Process.GetCurrentProcess().MainWindowTitle,
                ActiveDocumentPath = _activeDocPath,
                ActiveDocumentTitle = _activeDocTitle,
                IsDocumentOpen = _isDocOpen,
                UiState = _uiState,
                StartTimeUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                LastHeartbeatUtc = DateTime.UtcNow
            };
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ShowAgentPaneCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var pane = commandData.Application.GetDockablePane(AgentActivityDockablePane.DockablePaneId);
                if (pane != null)
                {
                    if (pane.IsShown())
                    {
                        pane.Hide();
                    }
                    else
                    {
                        pane.Show();
                    }
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
