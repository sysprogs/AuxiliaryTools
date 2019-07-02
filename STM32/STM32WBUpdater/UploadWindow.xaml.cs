using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

            public event PropertyChangedEventHandler PropertyChanged;

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

            _DataDirectory = "data";

            var ser = new XmlSerializer(typeof(STM32WBUpdaterConfiguration));
            using (var fs = File.OpenRead(System.IO.Path.Combine(_DataDirectory, "STM32WBUpdater.xml")))
                _Configuration = (STM32WBUpdaterConfiguration)ser.Deserialize(fs);

            DataContext = Controller = new ControllerImpl(_Configuration);
        }

        private void UploadWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var wnd = new DeviceConnectionRequestWindow(_Configuration) { Owner = this };
            wnd.ShowDialog();
        }
    }
}
