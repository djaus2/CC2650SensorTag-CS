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
using System.Threading.Tasks;

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

        private void buttonDone_Click(object sender, RoutedEventArgs e)

        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void buttonRefresh_Click(object sender, RoutedEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await DoGetProp();
            });
        }


        private  async Task  DoGetProp()
        { 
            System.Collections.Generic.Dictionary<CC2650SensorTag.SensorTagProperties, byte[]> props = await CC2650SensorTag.GetProperties();

            int count = 0;


            foreach (CC2650SensorTag.SensorTagProperties val in props.Keys)
            {
                byte[] bytes = props[val];
                string Valstr = "";
                if (bytes != null)
                {

                    count++;
                    TextBlock PropBlock = (TextBlock)this.FindName("Name" + count.ToString());
                    TextBlock ValueBlock = (TextBlock)this.FindName("Value" + count.ToString());
                    if (!CC2650SensorTag.showbytes.Contains(val))
                    {
                        Valstr = System.Text.Encoding.UTF8.GetString(bytes);
                        if (Valstr != null)
                            if (Valstr != "")
                            {
                                PropBlock.Text = val.ToString();
                                ValueBlock.Text = Valstr;
                            }

                    }
                    else
                    {
                        if (bytes != null)
                        {
                            PropBlock.Text = val.ToString() + "[" + bytes.Length.ToString() + "]";

                            for (int i = 0; i < bytes.Length; i++)
                            {
                                Valstr += " " + bytes[i].ToString("X2");
                            }
                            ValueBlock.Text = Valstr;
                        }
                        //NB:
                        //    Re: PNP_ID App got: pnp_id[7] { 01 0D 00 00 00 10 01 }
                        //    From:
                        //    https://e2e.ti.com/support/wireless_connectivity/bluetooth_low_energy/f/538/p/434053/1556237
                        //
                        //    In devinfoservice.c, you can find vendor ID and product ID information below where TI's vendor ID is 0x000D. 
                        //    static uint8 devInfoPnpId[DEVINFO_PNP_ID_LEN] ={ 
                        //    1, // Vendor ID source (1=Bluetooth SIG) 
                        //    LO_UINT16(0x000D), HI_UINT16(0x000D), // Vendor ID (Texas Instruments) 
                        //    LO_UINT16(0x0000), HI_UINT16(0x0000), // Product ID (vendor-specific) 
                        //    LO_UINT16(0x0110), HI_UINT16(0x0110) // Product version (JJ.M.N)};  
                        // 
                    }
                }
            }
       
        }//End of fn 

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(()  => this.buttonRefresh_Click(null,null)).Wait();
        }


    } //End of class
}//End of ns

