using STM32WBUpdater.DeviceEnumeration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace STM32WBUpdater
{
    /// <summary>
    /// Interaction logic for UploadWindow.xaml
    /// </summary>
    public partial class UploadWindow : Window
    {
        private readonly STM32WBUpdaterConfiguration _Configuration;
        private string _DataDirectory;

        public class ControllerImpl : INotifyPropertyChanged
        {
            public STM32WBUpdaterConfiguration Configuration { get; }

            public STM32WBUpdaterConfiguration.ProgrammableBinary SelectedBinary
            {
                get
                {
                    return selectedBinary;
                }

                set
                {
                    selectedBinary = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBinary)));
                }
            }

            bool _ShowDetails;
            public bool ShowDetails
            {
                get
                {
                    return _ShowDetails;
                }

                set
                {
                    _ShowDetails = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDetails)));
                }
            }

            string _StatusText;
            public string StatusText
            {
                get
                {
                    return _StatusText;
                }

                set
                {
                    _StatusText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
                }
            }

            public enum ControllerStatus
            {
                None,
                Succeeded,
                Running,
                Failed,
            }

            ControllerStatus _Status;

            public bool IsReady => _Status != ControllerStatus.Running;

            public ControllerStatus Status
            {
                get
                {
                    return _Status;
                }

                set
                {
                    _Status = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReady)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public string[] DeviceTypes => Configuration.DeviceTypes.Split('/');

            int _SelectedDeviceIndex = -1;
            public int SelectedDeviceIndex
            {
                get => _SelectedDeviceIndex;
                set
                {
                    _SelectedDeviceIndex = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDeviceIndex)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompatibleStacks)));
                }
            }

            public STM32WBUpdaterConfiguration.ProgrammableBinary[] CompatibleStacks => Configuration.Stacks.Where(st => SelectedDeviceIndex < 0 || st.IsCompatibleWithDevice(SelectedDeviceIndex)).ToArray();


            public string VersionText => $"Wireless Stack Updater {Configuration.Version}. Copyright (c) 2019-2020, Sysprogs OU.";

            public ControllerImpl(STM32WBUpdaterConfiguration cfg)
            {
                Configuration = cfg;
            }

            private STM32WBUpdaterConfiguration.ProgrammableBinary selectedBinary;
        }

        public readonly ControllerImpl Controller;

        public UploadWindow()
        {
            InitializeComponent();
            Loaded += UploadWindow_Loaded;

            var ser = new XmlSerializer(typeof(STM32WBUpdaterConfiguration));

            using (var fs = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("STM32WBUpdater.STM32WBUpdater.xml"))
                _Configuration = (STM32WBUpdaterConfiguration)ser.Deserialize(fs);

            DataContext = Controller = new ControllerImpl(_Configuration);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_DataDirectory != null)
            {
                try
                {
                    Directory.Delete(_DataDirectory, true);
                }
                catch { }
            }
        }

        private void UploadWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var wnd = new DeviceConnectionRequestWindow(_Configuration) { Owner = this };
            wnd.ShowDialog();
        }

        Dispatcher _Dispatcher = Dispatcher.CurrentDispatcher;

        async Task<string[]> RunProgrammerTool(string header, string args)
        {
            Controller.StatusText = header;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(header + "\r\n") { Foreground = Brushes.DarkBlue });
            txtLog.Document.Blocks.Add(paragraph);

            var process = new Process();
            process.StartInfo.FileName = System.IO.Path.Combine(_DataDirectory, "STM32CubeProgrammer", "STM32_Programmer_CLI.exe");
            process.StartInfo.Arguments = args;
            paragraph.Inlines.Add(new Run(process.StartInfo.FileName + " " + process.StartInfo.Arguments + "\r\n") { Foreground = Brushes.DarkBlue });

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            List<string> lines = new List<string>();
            DataReceivedEventHandler lineHandler = (s, e) =>
                {
                    if (e.Data == null)
                        return;
                    lock (lines)
                        lines.Add(e.Data);

                    _Dispatcher.BeginInvoke(new Action(() =>
                    {
                        paragraph.Inlines.Add(new Run(e.Data + "\r\n"));
                        txtLog.ScrollToEnd();
                    }));
                };

            process.OutputDataReceived += lineHandler;
            process.ErrorDataReceived += lineHandler;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            await Task.Run(() => process.WaitForExit());
            paragraph.Inlines.Add(new Run($"{process.StartInfo.FileName} exited with code {process.ExitCode}\r\n"));

            if (process.ExitCode != 0)
                throw new Exception($"{process.StartInfo.FileName} exited with code {process.ExitCode}");

            return lines.ToArray();
        }
        
        bool OutputContains(string[] output, string marker) => output.FirstOrDefault(l => l.IndexOf(marker, StringComparison.InvariantCultureIgnoreCase) != -1) != null;

        async Task ProgramBinary(string title, string iface, STM32WBUpdaterConfiguration.ProgrammableBinary binary, bool isFUS, string successMarker)
        {
            string fullBinaryPath = System.IO.Path.Combine(_DataDirectory, "stacks", binary.FileName);
            if (!File.Exists(fullBinaryPath))
                throw new Exception("Missing " + fullBinaryPath);

            var output = await RunProgrammerTool(title, $"-c port={iface} -fwupgrade \"{System.IO.Path.GetFullPath(fullBinaryPath)}\" 0x{binary.GetParsedBaseAddress(Controller.SelectedDeviceIndex):x8} firstinstall=" + (isFUS ? '0' : '1'));
            if (!OutputContains(output, successMarker))
                throw new Exception("Failed to program the binary");
        }

        private async void Program_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Controller.SelectedDeviceIndex < 0)
                    throw new Exception("Please select the device type. Binary programming addresses are different for different devices.");

                Controller.Status = ControllerImpl.ControllerStatus.Running;

                if (_DataDirectory == null)
                {
                    Controller.StatusText = "Extracting STM32Programmer tool...";
                    var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "STM32WBUpdater");
                    if (Directory.Exists(dir))
                    {
                        var paragraph = new Paragraph();
                        paragraph.Inlines.Add(new Run($"Deleting {dir}...\r\n") { Foreground = Brushes.DarkBlue });
                        txtLog.Document.Blocks.Add(paragraph);

                        Directory.Delete(dir, true);
                    }

                    Directory.CreateDirectory(dir);
                    {
                        var paragraph = new Paragraph();
                        paragraph.Inlines.Add(new Run($"Unpacking to {dir}...\r\n") { Foreground = Brushes.DarkBlue });
                        txtLog.Document.Blocks.Add(paragraph);
                    }

                    await Task.Run(() =>
                    {
                        using (var fs = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("STM32WBUpdater.data.zip"))
                        {
                            new ZipArchive(fs, ZipArchiveMode.Read).ExtractToDirectory(dir);
                        }
                    });

                    _DataDirectory = dir;
                }

#if DEBUG
                foreach (var checkedStack in Controller.Configuration.Stacks)
                {
                    string fullBinaryPath = System.IO.Path.Combine(_DataDirectory, "stacks", checkedStack.FileName);
                    if (!File.Exists(fullBinaryPath))
                        throw new Exception("DEBUG CHECK: One of the binaries is missing: " + fullBinaryPath);
                }
#endif

                FixDriversIfNeeded();

                var binary = Controller.SelectedBinary ?? throw new Exception("No binary selected");

                string iface = "usb1";
                txtLog.Document.Blocks.Clear();

                string[] output = await RunProgrammerTool("Deleting previous wireless stack...", $"-c port={iface} -fwdelete");
                if (!OutputContains(output, "Firmware delete finished"))
                    throw new Exception("Failed to delete the previous firmware version");

                const uint fusVersionAddress = 0x20030030;

                uint oldFUSVersion = 0;

                for (; ; )
                {
                    uint detectedFUSVersion = 0;

                    for (int iter = 0; ; iter++)
                    {
                        try
                        {
                            output = await RunProgrammerTool("Checking FUS binary version...", $"-c port={iface} -r32 0x{fusVersionAddress:x8} 1");
                            Regex rgValue = new Regex($"0x{fusVersionAddress:x8} *: *([0-9]{{8}})($| |:)");
                            var value = output.Select(l => rgValue.Match(l)).FirstOrDefault(m => m.Success)?.Groups[1].Value ?? throw new Exception("Failed to read existing FUS binary address");
                            detectedFUSVersion = uint.Parse(value, NumberStyles.AllowHexSpecifier, null);
                            break;
                        }
                        catch when (iter < 5)
                        {
                            await Task.Run(() => Thread.Sleep(2000));
                            continue;
                        }
                    }

                    if (detectedFUSVersion == oldFUSVersion)
                        throw new Exception($"FUS version (0x{oldFUSVersion}) has not changed after updating the FUS binary");

                    var triggeredBootloader = _Configuration.Bootloaders.FirstOrDefault(b => b.ShouldProgram(detectedFUSVersion)) ?? throw new Exception($"Don't know which FUS binary to use for current version 0x{detectedFUSVersion:x8}");
                    if (triggeredBootloader.FileName == null)
                        break;  //This means the current bootloader is up-to-date.

                    oldFUSVersion = detectedFUSVersion;
                    await ProgramBinary($"Updating FUS binary to {triggeredBootloader.Version}...", iface, triggeredBootloader, true, "Starting wireless satck finished");
                }

                await ProgramBinary("Programming wireless stack...", iface, binary, false, "Firmware Upgrade Success");

                Controller.StatusText = "Wireless stack updated successfully.";
                Controller.Status = ControllerImpl.ControllerStatus.Succeeded;
                MessageBox.Show(Controller.StatusText, "STM32WBUpdater", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Controller.StatusText = "Stack update failed. Use the button on the right to view the details.";
                Controller.Status = ControllerImpl.ControllerStatus.Failed;
                MessageBox.Show($"{ex.Message}\r\nPlease try replugging the device and programming it again.", "STM32WBUpdater", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FixDriversIfNeeded()
        {
            var regex = new Regex(_Configuration.SupportedDeviceIDRegex);

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("Checking whether the driver needs updating...\r\n") { Foreground = Brushes.DarkBlue });
            txtLog.Document.Blocks.Add(paragraph);

            for (int pass = 0; pass < 2; pass++)
            {
                using (var set = new DeviceInformationSet())
                {
                    var devices = set.GetAllDevices().Where(d => regex.IsMatch(d.HardwareID)).ToArray();
                    if (devices.Length != 1)
                        return;

                    var device = devices[0];
                    paragraph.Inlines.Add(new Run($"Found {device.DeviceID}...\r\n") { Foreground = Brushes.DarkBlue });

                    if (device.UserFriendlyName == "STM32 Bootloader")
                        return;

                    Controller.StatusText = "Installing STM32 bootloader drivers...";

                    paragraph.Inlines.Add(new Run($"Bootloader driver not installed for {device.UserFriendlyName}\r\n") { Foreground = Brushes.DarkBlue });

                    var driver = set.GetCompatibleDrivers(device).FirstOrDefault(d => d.Description == "STM32 Bootloader");
                    if (driver.Description == null)
                    {
                        if (pass == 0)
                        {
                            string driverDir = System.IO.Path.Combine(_DataDirectory, "Driver");
                            paragraph.Inlines.Add(new Run($"Copying drivers from {driverDir}...\r\n") { Foreground = Brushes.DarkBlue });

                            if (!DeviceInformationSet.SetupCopyOEMInf(System.IO.Path.Combine(driverDir, "STM32Bootloader.inf"),
                                                                      driverDir,
                                                                        DeviceInformationSet.OemSourceMediaType.SPOST_PATH,
                                                                        DeviceInformationSet.OemCopyStyle.Default,
                                                                        null,
                                                                        0,
                                                                        IntPtr.Zero,
                                                                        null))
                            {
                                throw new LastWin32ErrorException("Failed to install the STM32 Bootloader driver");
                            }

                            continue;
                        }
                        else
                            throw new Exception("STM32 Bootloader Driver Not Found");
                    }

                    paragraph.Inlines.Add(new Run($"Installing the STM32 bootloader driver...\r\n") { Foreground = Brushes.DarkBlue });
                    set.InstallSpecificDriverForDevice(device, driver, IntPtr.Zero);
                }
            }
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Program_Click(sender, e);
        }

        private void ShowLicense_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.st.com/SLA0044");
        }
    }
}
