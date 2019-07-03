using STM32WBUpdater.DeviceEnumeration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace STM32WBUpdater
{
    /// <summary>
    /// Interaction logic for DeviceConnectionRequestWindow.xaml
    /// </summary>
    public partial class DeviceConnectionRequestWindow : Window
    {
        readonly STM32WBUpdaterConfiguration _Configuration;
        DispatcherTimer _Timer;

        public DeviceConnectionRequestWindow(STM32WBUpdaterConfiguration config)
        {
            InitializeComponent();

            _Configuration = config;
            _Timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, CheckForDevices, Dispatcher.CurrentDispatcher);
            _Timer.Start();
        }

        private void CheckForDevices(object sender, EventArgs e)
        {
            try
            {
                var regex = new Regex(_Configuration.SupportedDeviceIDRegex);

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
            Process.Start("https://visualgdb.com/tools/STM32WBUpdater/connecting");
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
