using System.Collections.Generic;
using System.Diagnostics;
using SharpestInjector;
using System.Windows;
using System.Linq;
using System.IO;
using System.Text;
using System;
using static SharpestInjector.PInvoke;

namespace NoFocusLossGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string InitializeExport = "NoFocusLoss_Initialize";
        private const string DisableCursorBlockingExport = "NoFocusLoss_DisableCursorBlocking";
        private const string ShutdownAndUnloadExport = "NoFocusLoss_ShutdownAndUnload";

        private const long InitializeUnsafeToUnload = 2;
        private const uint RemoteCallTimeoutMs = 5000;
        private const uint WaitObject0 = 0;
        private const uint WaitTimeout = 0x102;
        private const int MaxUnloadAttempts = 16;

        private enum RemoteCallStatus
        {
            Completed,
            Failed,
            TimedOut
        }

        public MainWindow()
        {
            InitializeComponent();
            Dll32 = PeFile.Parse("NoFocusLoss.dll");
            Dll64 = PeFile.Parse("NoFocusLoss64.dll");
        }

        public List<ProcessInfo> ProcessBindTest = new List<ProcessInfo>();
        private readonly PeFile Dll32;
        private readonly PeFile Dll64;

        private void Refresh(object sender, RoutedEventArgs e)
        {
            ProcessBindTest.Clear();
            Processes.Items.Clear();
            InjectedProcesses.Items.Clear();
            var injected = new List<ProcessInfo>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var proc = Injector.GetProcessInfo(process);
                    if (proc.Modules.Count == 0)
                        continue;

                    proc.FileName = Path.GetFileName(proc.Modules.First().Value.Path);
                    PeFile dll = proc.Is64Bit ? Dll64 : Dll32;

                    if (FindLoadedModule(proc, dll) != null)
                        injected.Add(proc);
                    else if (proc.WindowHandle != IntPtr.Zero)
                        ProcessBindTest.Add(proc);
                }
                catch
                {
                    // Protected/exiting processes can't always be inspected.
                }
                finally
                {
                    process.Dispose();
                }
            }

            ProcessBindTest = ProcessBindTest
                .OrderBy(x => x.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var proc in ProcessBindTest)
                Processes.Items.Add(proc);
            foreach (var proc in injected.OrderBy(x => x.ToString(), StringComparer.OrdinalIgnoreCase))
                InjectedProcesses.Items.Add(proc);
        }

        private void Inject(object sender, RoutedEventArgs e)
        {
            var selected = Processes.SelectedItem as ProcessInfo;
            if (selected == null)
                return;

            try
            {
                var current = GetCurrentProcessInfo(selected.Id);
                PeFile dll = current.Is64Bit ? Dll64 : Dll32;

                if (FindLoadedModule(current, dll) != null)
                {
                    MarkAsInjected(current);
                    MessageBox.Show(UiStrings.AlreadyLoaded, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var injectStatus = InjectDll(current, dll);
                if (injectStatus == RemoteCallStatus.TimedOut)
                {
                    MarkAsInjected(current);
                    MessageBox.Show(UiStrings.InjectionTimedOut, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (injectStatus != RemoteCallStatus.Completed)
                {
                    MessageBox.Show(UiStrings.InjectionFailed, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                current = GetCurrentProcessInfo(selected.Id);
                var initStatus = CallExport(current, dll, InitializeExport, out var initResult);

                if (initStatus == RemoteCallStatus.TimedOut)
                {
                    MarkAsInjected(current);
                    MessageBox.Show(UiStrings.InitializationTimedOut, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (initStatus != RemoteCallStatus.Completed)
                {
                    MarkAsInjected(current);
                    MessageBox.Show(UiStrings.InitializationCallFailed, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (initResult.ToInt64() == InitializeUnsafeToUnload)
                {
                    MarkAsInjected(current);
                    MessageBox.Show(UiStrings.InitializationUnsafeToUnload, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (initResult == IntPtr.Zero)
                {
                    if (UnloadDllCompletely(current.Id, dll) != RemoteCallStatus.Completed)
                        MarkAsInjected(current);

                    MessageBox.Show(UiStrings.InitializationFailed, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (BlockCursorCheckBox.IsChecked != true)
                {
                    var optionStatus = CallExport(
                        current, dll, DisableCursorBlockingExport, out var optionResult);

                    if (optionStatus == RemoteCallStatus.TimedOut)
                    {
                        MarkAsInjected(current);
                        MessageBox.Show(UiStrings.CursorOptionTimedOut, UiStrings.WindowTitle,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (optionStatus != RemoteCallStatus.Completed || optionResult == IntPtr.Zero)
                    {
                        if (UnloadDllCompletely(current.Id, dll) != RemoteCallStatus.Completed)
                            MarkAsInjected(current);

                        MessageBox.Show(UiStrings.CursorOptionFailed, UiStrings.WindowTitle,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                MarkAsInjected(current);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(UiStrings.InjectionFailedWithMessage, Environment.NewLine, ex.Message),
                    UiStrings.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Unload(object sender, RoutedEventArgs e)
        {
            var selected = InjectedProcesses.SelectedItem as ProcessInfo;
            if (selected == null)
                return;

            try
            {
                var current = GetCurrentProcessInfo(selected.Id);
                PeFile dll = current.Is64Bit ? Dll64 : Dll32;

                if (FindLoadedModule(current, dll) == null)
                {
                    InjectedProcesses.Items.Remove(selected);
                    if (current.WindowHandle != IntPtr.Zero)
                        Processes.Items.Add(current);
                    return;
                }

                var unloadStatus = UnloadDllCompletely(current.Id, dll);
                if (unloadStatus == RemoteCallStatus.TimedOut)
                {
                    MessageBox.Show(UiStrings.UnloadTimedOut, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (unloadStatus != RemoteCallStatus.Completed)
                {
                    MessageBox.Show(UiStrings.UnloadUnsafe, UiStrings.WindowTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                InjectedProcesses.Items.Remove(selected);
                try
                {
                    current = GetCurrentProcessInfo(selected.Id);
                    if (current.WindowHandle != IntPtr.Zero)
                        Processes.Items.Add(current);
                }
                catch
                {
                    // The target exited while it was being unloaded.
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(UiStrings.UnloadFailedWithMessage, Environment.NewLine, ex.Message),
                    UiStrings.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkAsInjected(ProcessInfo process)
        {
            var existing = Processes.Items.Cast<ProcessInfo>()
                .FirstOrDefault(x => x.Id == process.Id);
            if (existing != null)
                Processes.Items.Remove(existing);

            if (!InjectedProcesses.Items.Cast<ProcessInfo>().Any(x => x.Id == process.Id))
                InjectedProcesses.Items.Add(process);
        }

        private static RemoteCallStatus InjectDll(ProcessInfo process, PeFile dll)
        {
            if (dll.Is64Bit != process.Is64Bit || process.Kernel32 == IntPtr.Zero)
                return RemoteCallStatus.Failed;

            IntPtr processHandle = IntPtr.Zero;
            IntPtr threadHandle = IntPtr.Zero;
            IntPtr remotePath = IntPtr.Zero;
            bool canFreeRemotePath = true;

            try
            {
                processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
                    PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    process.Id);
                if (processHandle == IntPtr.Zero)
                    return RemoteCallStatus.Failed;

                byte[] pathBytes = Encoding.Unicode.GetBytes(dll.FileName + '\0');
                remotePath = VirtualAllocEx(
                    processHandle, IntPtr.Zero, (uint)pathBytes.Length,
                    MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (remotePath == IntPtr.Zero)
                    return RemoteCallStatus.Failed;

                if (!WriteProcessMemory(
                        processHandle, remotePath, pathBytes, (uint)pathBytes.Length,
                        out uint bytesWritten) || bytesWritten != pathBytes.Length)
                    return RemoteCallStatus.Failed;

                int loadLibraryRva = process.IsWOW64
                    ? Constants.LoadLibrary32
                    : Constants.LoadLibrary;
                if (loadLibraryRva == 0)
                    return RemoteCallStatus.Failed;

                var loadLibraryAddress = IntPtr.Add(process.Kernel32, loadLibraryRva);
                threadHandle = CreateRemoteThread(
                    processHandle, IntPtr.Zero, 0, loadLibraryAddress, remotePath, 0, out _);
                if (threadHandle == IntPtr.Zero)
                    return RemoteCallStatus.Failed;

                uint wait = WaitForSingleObject(threadHandle, RemoteCallTimeoutMs);
                if (wait == WaitTimeout)
                {
                    // The target thread may still be using the path after we return.
                    // Leak this tiny allocation rather than create a use-after-free.
                    canFreeRemotePath = false;
                    return RemoteCallStatus.TimedOut;
                }
                if (wait != WaitObject0)
                    return RemoteCallStatus.Failed;

                if (!GetExitCodeThread(threadHandle, out long exitCode) || exitCode == 0)
                    return RemoteCallStatus.Failed;

                return RemoteCallStatus.Completed;
            }
            finally
            {
                if (threadHandle != IntPtr.Zero)
                    CloseHandle(threadHandle);
                if (canFreeRemotePath && remotePath != IntPtr.Zero && processHandle != IntPtr.Zero)
                    VirtualFreeEx(processHandle, remotePath, 0, MEM_RELEASE);
                if (processHandle != IntPtr.Zero)
                    CloseHandle(processHandle);
            }
        }

        private RemoteCallStatus UnloadDllCompletely(uint processId, PeFile dll)
        {
            for (int attempt = 0; attempt < MaxUnloadAttempts; attempt++)
            {
                ProcessInfo current;
                try
                {
                    current = GetCurrentProcessInfo(processId);
                }
                catch
                {
                    return RemoteCallStatus.Completed;
                }

                if (FindLoadedModule(current, dll) == null)
                    return RemoteCallStatus.Completed;

                var status = CallExport(
                    current, dll, ShutdownAndUnloadExport, out var result);

                if (status != RemoteCallStatus.Completed)
                    return status;
                if (result == IntPtr.Zero)
                    return RemoteCallStatus.Failed;
            }

            try
            {
                var current = GetCurrentProcessInfo(processId);
                return FindLoadedModule(current, dll) == null
                    ? RemoteCallStatus.Completed
                    : RemoteCallStatus.Failed;
            }
            catch
            {
                return RemoteCallStatus.Completed;
            }
        }

        private static RemoteCallStatus CallExport(ProcessInfo process, PeFile dll,
            string exportName, out IntPtr result)
        {
            result = IntPtr.Zero;
            try
            {
                var module = FindLoadedModule(process, dll);
                if (module == null || !TryGetExportRva(dll, exportName, out var exportRva))
                    return RemoteCallStatus.Failed;

                return RunRemoteFunction(
                    process, IntPtr.Add(module.MemoryAddress, exportRva), IntPtr.Zero, out result);
            }
            catch
            {
                return RemoteCallStatus.Failed;
            }
        }

        private static RemoteCallStatus RunRemoteFunction(ProcessInfo process, IntPtr function,
            IntPtr parameter, out IntPtr result)
        {
            result = IntPtr.Zero;
            IntPtr processHandle = IntPtr.Zero;
            IntPtr threadHandle = IntPtr.Zero;

            try
            {
                processHandle = OpenProcess(
                    PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
                    PROCESS_VM_WRITE | PROCESS_VM_READ,
                    false,
                    process.Id);
                if (processHandle == IntPtr.Zero)
                    return RemoteCallStatus.Failed;

                threadHandle = CreateRemoteThread(
                    processHandle, IntPtr.Zero, 0, function, parameter, 0, out _);
                if (threadHandle == IntPtr.Zero)
                    return RemoteCallStatus.Failed;

                uint wait = WaitForSingleObject(threadHandle, RemoteCallTimeoutMs);
                if (wait == WaitTimeout)
                    return RemoteCallStatus.TimedOut;
                if (wait != WaitObject0)
                    return RemoteCallStatus.Failed;

                if (!GetExitCodeThread(threadHandle, out long exitCode))
                    return RemoteCallStatus.Failed;

                result = new IntPtr(exitCode);
                return RemoteCallStatus.Completed;
            }
            finally
            {
                if (threadHandle != IntPtr.Zero)
                    CloseHandle(threadHandle);
                if (processHandle != IntPtr.Zero)
                    CloseHandle(processHandle);
            }
        }

        private static ModuleInfo FindLoadedModule(ProcessInfo process, PeFile dll)
        {
            string expectedPath = NormalizePath(dll.FileName);
            return process.Modules.Values.FirstOrDefault(module =>
                string.Equals(NormalizePath(module.Path), expectedPath,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path ?? string.Empty;
            }
        }

        private static bool TryGetExportRva(PeFile dll, string exportName, out int exportRva)
        {
            if (dll.Exports.TryGetValue(exportName, out exportRva))
                return true;

            var decorated = dll.Exports.FirstOrDefault(x =>
                x.Key.IndexOf(exportName, StringComparison.Ordinal) >= 0);
            if (string.IsNullOrEmpty(decorated.Key))
                return false;

            exportRva = decorated.Value;
            return true;
        }

        private static ProcessInfo GetCurrentProcessInfo(uint processId)
        {
            using (var process = Process.GetProcessById((int)processId))
            {
                return Injector.GetProcessInfo(process);
            }
        }
    }
}
