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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace BluetoothGATT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DeviceProperties : Page
    {
        public DeviceProperties()
        {
            this.InitializeComponent();
        }

        public MainPage owner { get; internal set; }

        

        internal void ShowDialog()
        {
            
        }

        private void button_Click(object sender, RoutedEventArgs e)

        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    
    }
}
