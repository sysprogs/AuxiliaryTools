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

            public string VersionText => $"STM32WBUpdater {Configuration.Version}. Copyright (c) 2019, Sysprogs OU.";

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

            var output = await RunProgrammerTool(title, $"-c port={iface} -fwupgrade \"{System.IO.Path.GetFullPath(fullBinaryPath)}\" 0x{binary.ParsedBaseAddress:x8} firstinstall=" + (isFUS ? '0' : '1'));
            if (!OutputContains(output, successMarker))
                throw new Exception("Failed to program the binary");
        }

        private async void Program_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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

                var binary = Controller.SelectedBinary ?? throw new Exception("No binary selected");

                string iface = "usb1";
                txtLog.Document.Blocks.Clear();

                string[] output = await RunProgrammerTool("Deleting previous wireless stack...", $"-c port={iface} -fwdelete");
                if (!OutputContains(output, "Firmware delete finished"))
                    throw new Exception("Failed to delete the previous firmware version");

                uint fusVersionAddress = 0x20030030;
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
                        continue;
                    }
                }

                if (detectedFUSVersion > _Configuration.ParsedExpectedBootloaderVersion)
                    throw new Exception($"The bootloader in the device (0x{detectedFUSVersion:x8}) is newer than the bootloader shipped with this tool ({_Configuration.ExpectedBootloaderVersion}). Please update the tool.");
                else if (detectedFUSVersion < _Configuration.ParsedExpectedBootloaderVersion)
                {
                    await ProgramBinary("Updating FUS binary...", iface, _Configuration.Bootloader, true, "Starting wireless satck finished");
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
