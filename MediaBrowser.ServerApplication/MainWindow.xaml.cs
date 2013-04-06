﻿using MediaBrowser.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.ServerApplication.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace MediaBrowser.ServerApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The _app host
        /// </summary>
        private readonly IApplicationHost _appHost;

        /// <summary>
        /// The _log manager
        /// </summary>
        private readonly ILogManager _logManager;

        /// <summary>
        /// The _configuration manager
        /// </summary>
        private readonly IServerConfigurationManager _configurationManager;

        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IDisplayPreferencesManager _displayPreferencesManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        /// <param name="jsonSerializer">The json serializer.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="appHost">The app host.</param>
        /// <exception cref="System.ArgumentNullException">logger</exception>
        public MainWindow(ILogManager logManager, IApplicationHost appHost, IServerConfigurationManager configurationManager, IUserManager userManager, ILibraryManager libraryManager, IJsonSerializer jsonSerializer, IDisplayPreferencesManager displayPreferencesManager)
        {
            if (logManager == null)
            {
                throw new ArgumentNullException("logManager");
            }
            if (appHost == null)
            {
                throw new ArgumentNullException("appHost");
            }
            if (configurationManager == null)
            {
                throw new ArgumentNullException("configurationManager");
            }

            _logger = logManager.GetLogger("MainWindow");
            _appHost = appHost;
            _logManager = logManager;
            _configurationManager = configurationManager;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _jsonSerializer = jsonSerializer;
            _displayPreferencesManager = displayPreferencesManager;

            InitializeComponent();

            Loaded += MainWindowLoaded;
        }

        /// <summary>
        /// Mains the window loaded.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            DataContext = this;

            UpdateButtons();

            LoadLogWindow(null, EventArgs.Empty);
            _logManager.LoggerLoaded += LoadLogWindow;
            _configurationManager.ConfigurationUpdated += Instance_ConfigurationUpdated;
        }

        /// <summary>
        /// Handles the ConfigurationUpdated event of the Instance control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        void Instance_ConfigurationUpdated(object sender, EventArgs e)
        {
            UpdateButtons();

            Dispatcher.InvokeAsync(() =>
            {
                var logWindow = App.Instance.Windows.OfType<LogWindow>().FirstOrDefault();

                if ((logWindow == null && _configurationManager.Configuration.ShowLogWindow) || (logWindow != null && !_configurationManager.Configuration.ShowLogWindow))
                {
                    _logManager.ReloadLogger(_configurationManager.Configuration.EnableDebugLevelLogging ? LogSeverity.Debug : LogSeverity.Info);
                }
            });
        }

        private void UpdateButtons()
        {
            Dispatcher.InvokeAsync(() =>
            {
                var developerToolsVisibility = _configurationManager.Configuration.EnableDeveloperTools
                                                   ? Visibility.Visible
                                                   : Visibility.Collapsed;

                separatorDeveloperTools.Visibility = developerToolsVisibility;
                cmdReloadServer.Visibility = developerToolsVisibility;
                cmOpenExplorer.Visibility = developerToolsVisibility;
            });
        }

        /// <summary>
        /// Loads the log window.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="EventArgs" /> instance containing the event data.</param>
        void LoadLogWindow(object sender, EventArgs args)
        {
            CloseLogWindow();

            Dispatcher.InvokeAsync(() =>
            {
                // Add our log window if specified
                if (_configurationManager.Configuration.ShowLogWindow)
                {
                    Trace.Listeners.Add(new WindowTraceListener(new LogWindow(_logManager)));
                }
                else
                {
                    Trace.Listeners.Remove("MBLogWindow");
                }
                // Set menu option indicator
                cmShowLogWindow.IsChecked = _configurationManager.Configuration.ShowLogWindow;

            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// Closes the log window.
        /// </summary>
        void CloseLogWindow()
        {
            Dispatcher.InvokeAsync(() =>
            {
                foreach (var win in Application.Current.Windows.OfType<LogWindow>())
                {
                    win.Close();
                }
            });
        }

        /// <summary>
        /// Handles the Click event of the cmdApiDocs control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        void cmdApiDocs_Click(object sender, EventArgs e)
        {
            App.OpenUrl("http://localhost:" + _configurationManager.Configuration.HttpServerPortNumber + "/" +
                      Kernel.Instance.WebApplicationName + "/metadata");
        }

        void cmdSwaggerApiDocs_Click(object sender, EventArgs e)
        {
            App.OpenUrl("http://localhost:" + _configurationManager.Configuration.HttpServerPortNumber + "/" +
                      Kernel.Instance.WebApplicationName + "/swagger-ui/index.html");
        }

        void cmdGithubWiki_Click(object sender, EventArgs e)
        {
            App.OpenUrl("https://github.com/MediaBrowser/MediaBrowser/wiki");
        }
        
        /// <summary>
        /// Occurs when [property changed].
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called when [property changed].
        /// </summary>
        /// <param name="info">The info.</param>
        public void OnPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                try
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(info));
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error in event handler", ex);
                }
            }
        }

        #region Context Menu events
        /// <summary>
        /// Handles the click event of the cmOpenExplorer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void cmOpenExplorer_click(object sender, RoutedEventArgs e)
        {
            new LibraryExplorer(_jsonSerializer, _logger, _appHost, _userManager, _libraryManager, _displayPreferencesManager).Show();
        }

        /// <summary>
        /// Handles the click event of the cmOpenDashboard control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void cmOpenDashboard_click(object sender, RoutedEventArgs e)
        {
            var user = _userManager.Users.FirstOrDefault(u => u.Configuration.IsAdministrator);
            OpenDashboard(user);
        }

        /// <summary>
        /// Opens the dashboard.
        /// </summary>
        private void OpenDashboard(User loggedInUser)
        {
            App.OpenDashboardPage("dashboard.html", loggedInUser, _configurationManager);
        }
        
        /// <summary>
        /// Handles the click event of the cmVisitCT control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void cmVisitCT_click(object sender, RoutedEventArgs e)
        {
            App.OpenUrl("http://community.mediabrowser.tv/");
        }

        /// <summary>
        /// Handles the click event of the cmdBrowseLibrary control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void cmdBrowseLibrary_click(object sender, RoutedEventArgs e)
        {
            var user = _userManager.Users.FirstOrDefault(u => u.Configuration.IsAdministrator);
            App.OpenDashboardPage("index.html", user, _configurationManager);
        }

        /// <summary>
        /// Handles the click event of the cmExit control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void cmExit_click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Handles the click event of the cmdReloadServer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void cmdReloadServer_click(object sender, RoutedEventArgs e)
        {
            App.Instance.Restart();
        }

        /// <summary>
        /// Handles the click event of the CmShowLogWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void CmShowLogWindow_click(object sender, RoutedEventArgs e)
        {
            _configurationManager.Configuration.ShowLogWindow = !_configurationManager.Configuration.ShowLogWindow;
            _configurationManager.SaveConfiguration();
            LoadLogWindow(sender, e);
        }

        #endregion
    }
}
