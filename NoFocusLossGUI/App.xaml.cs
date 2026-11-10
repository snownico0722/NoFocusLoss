using System;
using System.Globalization;
using System.Resources;
using System.Windows;

namespace NoFocusLossGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal static bool IsUiSmoke { get; private set; }
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // .NET does not enable legacy ANSI code pages until the provider is registered.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var index = Array.IndexOf(e.Args, "--ui-smoke");
            IsUiSmoke = index >= 0;
            var directory = IsUiSmoke && index + 1 < e.Args.Length ? e.Args[index + 1] : "ui-smoke";
            try
            {
                var window = new MainWindow();
                MainWindow = window;
                window.Show();
                if (IsUiSmoke)
                {
                    await WindowTheme.CaptureAsync(window, window.Theme, directory);
                    Shutdown(0);
                }
            }
            catch (Exception exception)
            {
                if (IsUiSmoke)
                {
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "error.txt"), exception.ToString());
                }
                else MessageBox.Show(exception.Message, "NoFocusLoss", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }
    }

    public static class UiStrings
    {
        private static ResourceManager ResourceManager
        {
            get { return Properties.Resources.ResourceManager; }
        }

        private static CultureInfo SelectedCulture
        {
            get
            {
                var culture = CultureInfo.CurrentUICulture;
                var name = culture.Name;

                if (!name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    return culture;

                if (name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase))
                {
                    return CultureInfo.GetCultureInfo("zh-TW");
                }

                return CultureInfo.GetCultureInfo("zh-CN");
            }
        }

        public static string Get(string key)
        {
            var value = ResourceManager.GetString(key, SelectedCulture);
            if (!string.IsNullOrEmpty(value))
                return value;

            value = ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        public static string AppSubtitle => Get("AppSubtitle");
        public static string ThemeTooltip => Get("ThemeTooltip");
        public static string SearchHint => Get("SearchHint");
        public static string AvailableTitle => Get("AvailableTitle");
        public static string AvailableHint => Get("AvailableHint");
        public static string ManagedTitle => Get("ManagedTitle");
        public static string ManagedHint => Get("ManagedHint");
        public static string EmptyAvailable => Get("EmptyAvailable");
        public static string EmptyManaged => Get("EmptyManaged");
        public static string SafetyNote => Get("SafetyNote");

        public static string WindowTitle { get { return Get("WindowTitle"); } }
        public static string Injectable { get { return Get("Injectable"); } }
        public static string Inject { get { return Get("Inject"); } }
        public static string InjectTooltip { get { return Get("InjectTooltip"); } }
        public static string Refresh { get { return Get("Refresh"); } }
        public static string RefreshTooltip { get { return Get("RefreshTooltip"); } }
        public static string BlockCursor { get { return Get("BlockCursor"); } }
        public static string BlockCursorTooltip { get { return Get("BlockCursorTooltip"); } }
        public static string Injected { get { return Get("Injected"); } }
        public static string Unload { get { return Get("Unload"); } }
        public static string UnloadTooltip { get { return Get("UnloadTooltip"); } }
        public static string AlreadyLoaded { get { return Get("AlreadyLoaded"); } }
        public static string InjectionTimedOut { get { return Get("InjectionTimedOut"); } }
        public static string InjectionFailed { get { return Get("InjectionFailed"); } }
        public static string InitializationTimedOut { get { return Get("InitializationTimedOut"); } }
        public static string InitializationCallFailed { get { return Get("InitializationCallFailed"); } }
        public static string InitializationUnsafeToUnload { get { return Get("InitializationUnsafeToUnload"); } }
        public static string InitializationFailed { get { return Get("InitializationFailed"); } }
        public static string CursorOptionTimedOut { get { return Get("CursorOptionTimedOut"); } }
        public static string CursorOptionFailed { get { return Get("CursorOptionFailed"); } }
        public static string InjectionFailedWithMessage { get { return Get("InjectionFailedWithMessage"); } }
        public static string UnloadTimedOut { get { return Get("UnloadTimedOut"); } }
        public static string UnloadUnsafe { get { return Get("UnloadUnsafe"); } }
        public static string UnloadFailedWithMessage { get { return Get("UnloadFailedWithMessage"); } }
    }
}
