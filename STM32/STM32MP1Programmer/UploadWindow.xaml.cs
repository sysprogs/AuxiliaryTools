using Microsoft.Win32;
using STM32MP1Programmer.DeviceEnumeration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
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

namespace STM32MP1Programmer
{
    /// <summary>
    /// Interaction logic for UploadWindow.xaml
    /// </summary>
    public partial class UploadWindow : Window
    {
        public class ProgrammableImage
        {
            public ProgrammableImage(string fullPath)
            {
                FullPath = fullPath;
            }

            public string FullPath { get; }

            public override string ToString()
            {
                return System.IO.Path.GetFileName(FullPath);
            }
        }

        public class ControllerImpl : INotifyPropertyChanged
        {
            public readonly string DataDirectory;
            private readonly ProgrammableImage[] _AllBinaries;
            ProgrammableImage selectedBinary;

            public ProgrammableImage SelectedBinary
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

            private ProgrammableImage[] filteredBinaries;
            public ProgrammableImage[] FilteredBinaries
            {
                get
                {
                    return filteredBinaries;
                }

                set
                {
                    filteredBinaries = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilteredBinaries)));
                }
            }

            private string filter;
            public string Filter
            {
                get
                {
                    return filter;
                }

                set
                {
                    filter = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter)));
                    FilteredBinaries = _AllBinaries?.Where(b => string.IsNullOrEmpty(value) || b.ToString().IndexOf(value, StringComparison.InvariantCultureIgnoreCase) != -1).ToArray();
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

            bool _IsIndeterminate;
            double _Progress;

            public double Progress
            {
                get
                {
                    return _Progress;
                }

                set
                {
                    _Progress = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
                }
            }

            public bool IsIndeterminate
            {
                get
                {
                    return _IsIndeterminate;
                }

                set
                {
                    _IsIndeterminate = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIndeterminate)));
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

            public string VersionText => $"STM32MP1 Image Programmer v1.0. Copyright (c) 2019, Sysprogs OU.";

            public bool HasNoBinaries { get; }

            public ControllerImpl()
            {
                DataDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                var imageDir = DataDirectory + "\\images";
                if (Directory.Exists(imageDir))
                    _AllBinaries = Directory.GetFiles(imageDir, "*.tsv", SearchOption.AllDirectories).Select(d => new ProgrammableImage(d)).ToArray();
                else
                {
                    _AllBinaries = new ProgrammableImage[0];
                    HasNoBinaries = true;
                }

                Filter = "";
            }
        }

        public readonly ControllerImpl Controller;

        public UploadWindow()
        {
            InitializeComponent();
            Loaded += UploadWindow_Loaded;

            DataContext = Controller = new ControllerImpl();
        }


        private void UploadWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var wnd = new DeviceConnectionRequestWindow() { Owner = this };
            wnd.ShowDialog();
        }

        Dispatcher _Dispatcher = Dispatcher.CurrentDispatcher;

        void OutputReadingThreadBody(Paragraph paragraph, StreamReader stream)
        {
            try
            {
                byte[] buffer = new byte[65536];
                for (; ;)
                {
                    int done = stream.BaseStream.Read(buffer, 0, buffer.Length);
                    if (done <= 0)
                        break;
                    string text = Encoding.ASCII.GetString(buffer, 0, done);
                    text = text.Replace("\x3f", "");

                    Regex rgPercentage = new Regex("[ \t]([0-9]+)%");
                    Regex rgDescription = new Regex("(File) *:([^\n]+)");

                    var m = rgPercentage.Matches(text).OfType<Match>().LastOrDefault();
                    if (m != null)
                    {
                        Controller.IsIndeterminate = false;
                        Controller.Progress = double.Parse(m.Groups[1].Value) / 100;
                    }
                    else if (text.Contains("\n"))
                        Controller.IsIndeterminate = true;

                    m = rgDescription.Matches(text).OfType<Match>().LastOrDefault();
                    if (m != null)
                    {
                        Controller.StatusText = $"Programming {m.Groups[2].Value.Trim()}...";
                    }

                    _Dispatcher.BeginInvoke(new Action(() =>
                    {
                        paragraph.Inlines.Add(new Run(text) );
                        txtLog.ScrollToEnd();
                    }));
                }
            }
            catch
            {

            }
        }

        async Task<string[]> RunProgrammerTool(string header, string args, string dir)
        {
            Controller.StatusText = header;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(header + "\r\n") { Foreground = Brushes.DarkBlue });
            txtLog.Document.Blocks.Add(paragraph);

            var process = new Process();
            process.StartInfo.FileName = System.IO.Path.Combine(Controller.DataDirectory, "STM32CubeProgrammer", "STM32_Programmer_CLI.exe");
            process.StartInfo.Arguments = args;
            process.StartInfo.WorkingDirectory = dir;
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
                        paragraph.Inlines.Add(new Run(e.Data + "\r\n") { Foreground = Brushes.Red });
                        txtLog.ScrollToEnd();
                    }));
                };

            process.ErrorDataReceived += lineHandler;

            process.Start();
            process.BeginErrorReadLine();
            new Thread(() => OutputReadingThreadBody(paragraph, process.StandardOutput)).Start();

            await Task.Run(() => process.WaitForExit());
            paragraph.Inlines.Add(new Run($"{process.StartInfo.FileName} exited with code {process.ExitCode}\r\n"));

            if (process.ExitCode != 0)
                throw new Exception($"{process.StartInfo.FileName} exited with code {process.ExitCode}");

            return lines.ToArray();
        }

        bool OutputContains(string[] output, string marker) => output.FirstOrDefault(l => l.IndexOf(marker, StringComparison.InvariantCultureIgnoreCase) != -1) != null;

        async Task ProgramBinary(string title, string iface, ProgrammableImage binary)
        {
            string fullBinaryPath = binary.FullPath;
            if (!File.Exists(fullBinaryPath))
                throw new Exception("Missing " + fullBinaryPath);

            fullBinaryPath = System.IO.Path.GetFullPath(fullBinaryPath);
            var output = await RunProgrammerTool(title, $"-c port={iface} -w \"{fullBinaryPath}\"", System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(fullBinaryPath)));
        }

        private async void Program_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Controller.Status = ControllerImpl.ControllerStatus.Running;

                FixDriversIfNeeded();

                var binary = Controller.SelectedBinary ?? throw new Exception("No binary selected");

                string iface = "usb1";
                txtLog.Document.Blocks.Clear();

                await ProgramBinary("Programming selected image...", iface, binary);

                Controller.StatusText = "Image programmed successfully.";
                Controller.Status = ControllerImpl.ControllerStatus.Succeeded;
                MessageBox.Show(Controller.StatusText, "STM32MP1Programmer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Controller.StatusText = "Image programming failed. Use the button on the right to view the details.";
                Controller.Status = ControllerImpl.ControllerStatus.Failed;
                MessageBox.Show($"{ex.Message}\r\nPlease try replugging the device and programming it again.", "STM32MP1Programmer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FixDriversIfNeeded()
        {
            var regex = new Regex(DeviceConnectionRequestWindow.GetDeviceIDRegex());

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
                            string driverDir = System.IO.Path.Combine(Controller.DataDirectory, "Driver");
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

        private void ProgramCustomFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Locate Layout File",
                Filter = "FLASH Layout Files|*.tsv",
            };

            if (dlg.ShowDialog() == true)
            {
                Controller.SelectedBinary = new ProgrammableImage(dlg.FileName);
                Program_Click(sender, e);
            }

        }
    }
}
