using STM32MP1Programmer.DeviceEnumeration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace STM32MP1Programmer
{
    /// <summary>
    /// Interaction logic for DeviceConnectionRequestWindow.xaml
    /// </summary>
    public partial class DeviceConnectionRequestWindow : Window
    {
        private string _DeviceIDRegex;
        DispatcherTimer _Timer;

        public static string GetDeviceIDRegex() => File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "DeviceID.txt"));

        public DeviceConnectionRequestWindow()
        {
            InitializeComponent();
             _DeviceIDRegex = GetDeviceIDRegex();

            _Timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, CheckForDevices, Dispatcher.CurrentDispatcher);
            _Timer.Start();
        }

        private void CheckForDevices(object sender, EventArgs e)
        {
            try
            {
                var regex = new Regex(_DeviceIDRegex);

                using (var set = new DeviceInformationSet())
                {
                    var devices = set.GetAllDevices().Where(d => regex.IsMatch(d.HardwareID)).ToArray();
                    if (devices.Length == 1)
                        Close();
                }
            }
            catch
            {

            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _Timer.Stop();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://visualgdb.com/tools/STM32MP1Programmer/connecting");
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
