using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using TICC2650SensorTag;
using System.Collections.ObjectModel;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace BluetoothGATT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Parameters : Page
    {
        public List<CC2650SensorTag.SensorIndexes> ListEnum { get; set; }// { get { return typeof(CC2650SensorTag.SensorIndexes).GetEnumValues().Cast<CC2650SensorTag.SensorIndexes>().ToList(); } }
        public ObservableCollection<CC2650SensorTag.SensorIndexes> sd { get; set; }

        public Parameters()
        {
            this.InitializeComponent();
            ListEnum = Enum.GetValues(typeof(CC2650SensorTag.SensorIndexes))
                .Cast<CC2650SensorTag.SensorIndexes>()
                //.Take(4)
                .ToList();

            foreach (CC2650SensorTag.SensorIndexes sen in ListEnum)
            {
                if (sen != CC2650SensorTag.SensorIndexes.NOTFOUND)
                {
                    lbstart.Items.Add(sen);
                    lbend.Items.Add(sen);
                    lbstart.SelectedIndex = 0;
                    lbend.SelectedIndex = lbend.Items.Count - 1;
                }
            }
            //txtnumTags..Background = Brushes.White;
 

        }

        public class EnumToStringConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, string language)
            {
                string EnumString;
                try
                {
                    EnumString = Enum.GetName((value.GetType()), value);
                    return EnumString;
                }
                catch
                {
                    return string.Empty;
                }
            }

            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                string EnumString;
                try
                {
                    EnumString = Enum.GetName((value.GetType()), value);
                    return EnumString;
                }
                catch
                {
                    return string.Empty;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                throw new NotImplementedException();
            }


            // No need to implement converting back on a one-way binding 
            public object ConvertBack(object value, Type targetType,
              object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private void lbstart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CC2650SensorTag.SensorIndexes start = (CC2650SensorTag.SensorIndexes)lbstart.SelectedIndex;
            CC2650SensorTag.SensorIndexes end = (CC2650SensorTag.SensorIndexes)lbend.SelectedIndex;
            if (end < start)
            {
                lbend.SelectedIndex = (int)start;
            }
            else
            {
                CC2650SensorTag.FIRST_SENSOR = (int)start;
                CC2650SensorTag.NUM_SENSORS_TO_TEST = 1 + (int)end - (int)start;
            }

        }

        private void Go_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //uint numTags = 2;
            //bool res = uint.TryParse(txtnumTags.Text.Trim(), out numTags);
            //if (res)
            //    MainPage2.numDevices = (int)numTags;

            if ((bool)AA.IsChecked)
                CC2650SensorTag.Use_DEVICE_BATTERY_SERVICE = true;
            else
                CC2650SensorTag.Use_DEVICE_BATTERY_SERVICE = false;

            if ((bool)BB.IsChecked)
                CC2650SensorTag.Use_UUID_PROPERTIES_SERVICE = true;
            else
                CC2650SensorTag.Use_UUID_PROPERTIES_SERVICE = false;

            if ((bool)CC.IsChecked)
                CC2650SensorTag.StartNotifications = true;
            else
                CC2650SensorTag.StartNotifications = false;

            if ((bool)DD.IsChecked)
                CC2650SensorTag.ServiceSensors = true;
            else
                CC2650SensorTag.ServiceSensors = false;

            CC2650SensorTag.SensorIndexes start = (CC2650SensorTag.SensorIndexes)lbstart.SelectedIndex;
            CC2650SensorTag.SensorIndexes end = (CC2650SensorTag.SensorIndexes)lbend.SelectedIndex;

            this.Frame.Navigate(typeof(MainPage2), this);
        }


    }
}
