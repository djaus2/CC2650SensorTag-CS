//
// Note that this sample only supports the CC2650 Sensor Tag: http://processors.wiki.ti.com/index.php/CC2650_SensorTag
//


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// Required APIs to use Bluetooth GATT
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

// Required APIs to use built in GUIDs
using Windows.Devices.Enumeration;

// Required APIs for buffer manipulation & async operations
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using TICC2650SensorTag;
using Windows.UI.Xaml.Input;

// Disable warning "...execution of the current method continues before the call is completed..."
#pragma warning disable 4014

// Disable warning to "consider using the 'await' operator to await non-blocking API calls"
#pragma warning disable 1998

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace BluetoothGATT
{
    public delegate void DeviceInfoDel(DeviceInformation devInfo);

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage2 : Page
    {
        
        private void HideMenu()
        {
            MySplitView.IsPaneOpen = false;
        }
        private void ShowMenu()
        {
            MySplitView.IsPaneOpen = true;
        }
        private void HamburgerButton_Click(object sender, RoutedEventArgs e)

        {

            MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;

        }


        private DeviceWatcher deviceWatcher = null;

        private DeviceInformationDisplay DeviceInfoConnected = null;

        //Handlers for device detection
        private TypedEventHandler<DeviceWatcher, DeviceInformation> handlerAdded = null;
        private TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> handlerUpdated = null;
        private TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> handlerRemoved = null;
        private TypedEventHandler<DeviceWatcher, Object> handlerEnumCompleted = null;
        private TypedEventHandler<DeviceWatcher, Object> handlerStopped = null;

        private TICC2650SensorTag_BLEWatcher CC2650SensorTag_BLEWatcher = null;

        TaskCompletionSource<string> providePinTaskSrc;
        TaskCompletionSource<bool> confirmPinTaskSrc;

        private enum MessageType { YesNoMessage, OKMessage };
        public ObservableCollection<DeviceInformationDisplay> ResultCollection
        {
            get;
            private set;
        }

        public MainPage2()
        {
            this.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            this.InitializeComponent();
            CC2650SensorTag.SetUp_SensorsLists();

            UserOut.Text = "Searching for Bluetooth LE Devices...";
            resultsListView.IsEnabled = false;
            PairButton.IsEnabled = false;

            ResultCollection = new ObservableCollection<DeviceInformationDisplay>();

            DataContext = this;
            //Start Watcher for pairable/paired devices

            SysInfo.GetInfo();
            txtCompany.Text += "    OS Version: " + SysInfo.SystemVersion;

            CC2650SensorTag_BLEWatcher = new TICC2650SensorTag_BLEWatcher(this.UpdateButtons_WhenSensorsAreReady_CallBack, this.CallMeBackTemp, this.initSensor);
            var res = Task<bool>.Run(() => StartWatcher());
        }

        ~MainPage2()
        {
            StopWatcher();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ResultCollection.Clear();
        }


        public void UpdateButtons_WhenSensorsAreReady_CallBack()
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                SensorList.IsEnabled = true;
                DisableNotifButton.IsEnabled = true;
                EnableNotifButton.IsEnabled = true;
                ReadButton.IsEnabled = true;
                InitButton.IsEnabled = false;

                EnableIOIOButton.IsEnabled = true;
                DisableIOIOButton.IsEnabled = true;
                AllOffIOButton.IsEnabled = true;
                BuzzButton.IsEnabled = true;
                LED1Button.IsEnabled = true;
                LED2Button.IsEnabled = true;

                UserOut.Text = "Sensors on!     ";
            });
        }


        public static int numDevices { get; set; } = 2;
        private int numDevicesFound = 0;
        //Watcher for Bluetooth LE Devices based on the Protocol ID
        private async void StartWatcher()
        {



            string aqsFilter;
            numDevicesFound = 0;

            var bTSupported = await  BT.GetBluetoothIsSupportedAsync();

            if (!bTSupported)
            {
                Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    UserOut.Text = "No Bluetooth radios available";
                    System.Diagnostics.Debug.WriteLine("No Bluetooth radios available!");
                });
                return;
            }


            var bTIsEnabled = await  BT.GetBluetoothIsEnabledAsync();
            if (!bTIsEnabled)
            {
                bool bAccess = true;
                Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    var access = await BT.RequestAccessAsync();
                    bAccess = access;
                });

                

                if (bAccess)
                {

                    bTIsEnabled = await BT.SetBluetoothStateOnAsync();
                    if (!bTIsEnabled)
                    {
                        Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            UserOut.Text = "Bluetooth isn't enabled! Turn on Failed";
                            System.Diagnostics.Debug.WriteLine("Bluetooth isn't enabled! Turn on Failed.");
                        });
                        return;
                    }
                }


            }

            //If sensors are paired we can skip straight to getting GATT services
            //Assumes only one sensor paired
            //Seeingthat we are already paired cut straight to the chase
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UserOut.Text = "Setting up SensorTag";
            });
            CC2650SensorTag_BLEWatcher.StartBLEWatcher(this, SetDeviceInfo);
            return;


            // Request the IsPaired property so we can display the paired status in the UI
            string[] requestedProperties = { "System.Devices.Aep.IsPaired" };

            //for bluetooth LE Devices
            aqsFilter = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";

            deviceWatcher = DeviceInformation.CreateWatcher(
                aqsFilter,
                requestedProperties,
                DeviceInformationKind.AssociationEndpoint
                );

            // Hook up handlers for the watcher events before starting the watcher

            handlerAdded = async (watcher, deviceInfo) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (numDevicesFound < numDevices)
                    {
                        //Depending upon firware there are two names.
                        if (CC2650SensorTag.DeviceAltSensorNames.Contains(deviceInfo.Name))
                        {
                            Debug.WriteLine("Watcher Add: " + deviceInfo.Id);
                            ResultCollection.Add(new DeviceInformationDisplay(deviceInfo));


                            UpdatePairingButtons();
                            //resultsListView.IsEnabled = true;
                            UserOut.Text = "Found at least one SensorTag. " + " Select for pairing. Still searching for others though.";
                            var st = watcher.Status;
                            numDevicesFound++;
                            if (numDevicesFound == numDevices)
                            {
                                if (watcher != null)
                                    if (st != DeviceWatcherStatus.Stopped)
                                        watcher.Stop();
                            }
                        }
                    }
                });
            };
            deviceWatcher.Added += handlerAdded;

            handlerUpdated = async (watcher, deviceInfoUpdate) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    //Debug.WriteLine("Watcher Update: " + deviceInfoUpdate.Id);
                    // Find the corresponding updated DeviceInformation in the collection and pass the update object
                    // to the Update method of the existing DeviceInformation. This automatically updates the object
                    // for us.
                    foreach (DeviceInformationDisplay deviceInfoDisp in ResultCollection)
                    {
                        if (deviceInfoDisp.Id == deviceInfoUpdate.Id)
                        {
                            deviceInfoDisp.Update(deviceInfoUpdate);
                            break;
                        }
                    }
                });
            };
            deviceWatcher.Updated += handlerUpdated;



            handlerRemoved = async (watcher, deviceInfoUpdate) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Debug.WriteLine("Watcher Remove: " + deviceInfoUpdate.Id);
                    // Find the corresponding DeviceInformation in the collection and remove it
                    foreach (DeviceInformationDisplay deviceInfoDisp in ResultCollection)
                    {
                        if (deviceInfoDisp.Id == deviceInfoUpdate.Id)
                        {
                            ResultCollection.Remove(deviceInfoDisp);
                            UpdatePairingButtons();
                            if (ResultCollection.Count == 0)
                            {
                                UserOut.Text = "Searching for Bluetooth LE Devices...";
                            }
                            break;
                        }
                    }
                });
            };
            deviceWatcher.Removed += handlerRemoved;

            handlerStopped = async (watcher, obj) =>
            {
                Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Debug.WriteLine($"Watcher Stopped: Found {ResultCollection.Count} Bluetooth LE Devices");

                    if (ResultCollection.Count > 0)
                    {
                        UserOut.Text = "Select a device for pairing";
                    }
                    else
                    {
                        UserOut.Text = "No Bluetooth LE Devices found.";
                    }
                    UpdatePairingButtons();
                });
            };

            handlerEnumCompleted = async (watcher, obj) =>
            {
                Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Debug.WriteLine($"Found {ResultCollection.Count} Bluetooth LE Devices");

                    if (ResultCollection.Count > 0)
                    {
                        UserOut.Text = "Select a " + CC2650SensorTag.DeviceAltSensorNames + " for pairing. Search done.";
                    }
                    else
                    {
                        UserOut.Text = "Search done: No Bluetooth LE Devices found.";
                    }
                    UpdatePairingButtons();
                });
            };

            deviceWatcher.EnumerationCompleted += handlerEnumCompleted;

            deviceWatcher.Start();
        }


        private void StopWatcher()
        {
            if (null != deviceWatcher)
            {
                // First unhook all event handlers except the stopped handler. This ensures our
                // event handlers don't get called after stop, as stop won't block for any "in flight" 
                // event handler calls.  We leave the stopped handler as it's guaranteed to only be called
                // once and we'll use it to know when the query is completely stopped. 
                deviceWatcher.Added -= handlerAdded;
                deviceWatcher.Updated -= handlerUpdated;
                deviceWatcher.Removed -= handlerRemoved;
                deviceWatcher.EnumerationCompleted -= handlerEnumCompleted;
                // In my case I don't want to exercise the Stopped handler when exiting the app.
                deviceWatcher.Stopped -= handlerStopped;

                if (DeviceWatcherStatus.Started == deviceWatcher.Status ||
                    DeviceWatcherStatus.EnumerationCompleted == deviceWatcher.Status)
                {

                    deviceWatcher.Stop();
                }

            }
        }



        /// <summary>
        /// A little care here with threading to avoid issues with the data state (Values/Raw).
        /// </summary>
        private CC2650SensorTag.GattDataModes gattDataMode { get; set; } = CC2650SensorTag.GattDataModes.Values;



        //See https://msdn.microsoft.com/en-us/library/system.threading.readerwriterlockslim(v=vs.110).aspx
        //for ReaderWriterLockSlim
        private ReaderWriterLockSlim gattDataModeLock = new ReaderWriterLockSlim();
        private MainPage2 owner;

        private void chkDataModeValues_Checked(object sender, RoutedEventArgs e)
        {
            
            if (sender is RadioButton)
            {
                RadioButton ChkValues = (RadioButton)sender;
                if (ChkValues != null)
                {
                    bool state = (ChkValues.IsChecked == true);
                    gattDataModeLock.EnterWriteLock();
                    {
                        if (state)
                            gattDataMode = CC2650SensorTag.GattDataModes.Values;
                        else
                            gattDataMode = CC2650SensorTag.GattDataModes.Raw;
                    }
                    gattDataModeLock.ExitWriteLock();
                }
            }
        }


        //private int DoneSensorCout { get; set; } = 0;
        private async Task initSensor(CC2650SensorTag.SensorIndexes sensorIndx)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sensorIndx >= 0 && sensorIndx != CC2650SensorTag.SensorIndexes.IO_SENSOR && sensorIndx != CC2650SensorTag.SensorIndexes.REGISTERS)
                {
                    //temp.CallMeBack = CallMeBackTemp;
                    switch (sensorIndx)
                    {
                        case (CC2650SensorTag.SensorIndexes.IR_SENSOR):
                            IRTitle.Foreground = new SolidColorBrush(Colors.Green);
                            break;
                        case (CC2650SensorTag.SensorIndexes.MOVEMENT):
                            AccelTitle.Foreground = new SolidColorBrush(Colors.Green);
                            GyroTitle.Foreground = new SolidColorBrush(Colors.Green);
                            MagnoTitle.Foreground = new SolidColorBrush(Colors.Green);
                            //temp.setSensorPeriod(1000);
                            break;
                        case (CC2650SensorTag.SensorIndexes.HUMIDITY):
                            HumidTitle.Foreground = new SolidColorBrush(Colors.Green);
                            break;
                        case (CC2650SensorTag.SensorIndexes.OPTICAL):
                            LuxTitle.Foreground = new SolidColorBrush(Colors.Green);
                            break; ;
                        case (CC2650SensorTag.SensorIndexes.BAROMETRIC_PRESSURE):
                            BaroTitle.Foreground = new SolidColorBrush(Colors.Green);
                            BaroTitleTemp.Foreground = new SolidColorBrush(Colors.Green);
                            break;
                        case (CC2650SensorTag.SensorIndexes.KEYS):
                            KeyTitle.Foreground = new SolidColorBrush(Colors.Green);
                            break;
                        default:
                            break;
                    }
                }
                //DoneSensorCout++;
                //UserOut.Text = UserOut.Text.Substring(0, UserOut.Text.Length - 5) + string.Format(" {0,4}", DoneSensorCout);
            });

            Debug.WriteLine("End init sensor(new): " + sensorIndx.ToString());
        }



        public async void CallMeBackTemp(CC2650SensorTag.SensorData data)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                bool state = true;
                //gattDataModeLock.EnterReadLock();
                //{
                    if (gattDataMode == CC2650SensorTag.GattDataModes.Values)
                        state = true;
                    else
                        state = false;
               // }
                //gattDataModeLock.ExitReadLock();
                if (state)
                {
                    switch (data.Sensor_Index)
                    {
                        case (CC2650SensorTag.SensorIndexes.IR_SENSOR):
                            AmbTempOut.Text = string.Format("Chip:\t{0:0.0####}", data.Values[0]);
                            ObjTempOut.Text = string.Format("IR:  \t{0:0.0####}", data.Values[1]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.MOVEMENT):
                            GyroXOut.Text = string.Format("X:  \t{0:0.0####}", data.Values[0]);
                            GyroYOut.Text = string.Format("Y:  \t{0:0.0####}", data.Values[1]);
                            GyroZOut.Text = string.Format("Z:  \t{0:0.0####}", data.Values[2]);


                            AccelXOut.Text = string.Format("X:  \t{0:0.0####}", data.Values[3]);
                            AccelYOut.Text = string.Format("Y:  \t{0:0.0####}", data.Values[4]);
                            AccelZOut.Text = string.Format("Z:  \t{0:0.0####}", data.Values[5]);

                            RecTranslateTransform.X = data.Values[3] * 40;

                            RecTranslateTransform.Y = data.Values[4] * -40;

                            SolidColorBrush purpleBrush = new SolidColorBrush();
                            purpleBrush.Color = Colors.Purple;
                            SolidColorBrush redBrush = new SolidColorBrush();
                            redBrush.Color = Colors.Red;
                            SolidColorBrush greenBrush = new SolidColorBrush();
                            greenBrush.Color = Colors.Green;
                            SolidColorBrush yellowBrush = new SolidColorBrush();
                            yellowBrush.Color = Colors.Yellow;
                            SolidColorBrush whiteBrush = new SolidColorBrush();
                            whiteBrush.Color = Colors.White;

                            if (Math.Abs(data.Values[5]) < 0.2)
                                AccPointer.Fill = whiteBrush;
                            else if (data.Values[5] < -1.5)
                                AccPointer.Fill = redBrush;
                            else if (data.Values[5] < 0)
                                AccPointer.Fill = purpleBrush;
                            else if (data.Values[5] > 1.5)
                                AccPointer.Fill = greenBrush;
                            else //if (data.Values[5] > 0)
                                AccPointer.Fill = yellowBrush;


                            MagnoXOut.Text = string.Format("X:  \t{0:0.0####}", data.Values[6]);
                            MagnoYOut.Text = string.Format("Y:  \t{0:0.0####}", data.Values[7]);
                            MagnoZOut.Text = string.Format("Z:  \t{0:0.0####}", data.Values[8]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.HUMIDITY):
                            HumidOut.Text = string.Format("H:\t{0:0.0####}", data.Values[0]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.OPTICAL):
                            LuxOut.Text = string.Format("L:\t{0:0.0####}", data.Values[0]);
                            break; ;
                        case (CC2650SensorTag.SensorIndexes.BAROMETRIC_PRESSURE):
                            BaroOutTemp.Text = string.Format("T:\t{0:0.0####}", data.Values[1]);
                            BaroOut.Text = string.Format("P:\t{0:0.0####}", data.Values[0]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.KEYS):
                            if (data.Values[0] > 0)
                                KeyROut.Background = new SolidColorBrush(Colors.Green);
                            else
                                KeyROut.Background = new SolidColorBrush(Colors.Red);

                            if (data.Values[1] > 0)
                                KeyLOut.Background = new SolidColorBrush(Colors.Green);
                            else
                                KeyLOut.Background = new SolidColorBrush(Colors.Red);

                            if (data.Values[2] > 0)
                                ReedOut.Background = new SolidColorBrush(Colors.Green);
                            else
                                ReedOut.Background = new SolidColorBrush(Colors.Red);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    switch (data.Sensor_Index)
                    {
                        case (CC2650SensorTag.SensorIndexes.IR_SENSOR):
                            AmbTempOut.Text = string.Format("Chip:\t{0:000} {1:000} {2:000} {3:000}", data.Raw[3], data.Raw[2], data.Raw[1], data.Raw[0]);
                            ObjTempOut.Text = string.Format("");
                            break;
                        case (CC2650SensorTag.SensorIndexes.MOVEMENT):
                            GyroXOut.Text = string.Format("X:  \t{0:000} {1:000}", data.Raw[17], data.Raw[16]);
                            GyroYOut.Text = string.Format("Y:  \t{0:000} {1:000}", data.Raw[15], data.Raw[14]);
                            GyroZOut.Text = string.Format("Z:  \t{0:000} {1:000}", data.Raw[13], data.Raw[11]);


                            AccelXOut.Text = string.Format("X:  \t{0:000} {1:000}", data.Raw[11], data.Raw[10]);
                            AccelYOut.Text = string.Format("Y:  \t{0:000} {1:000}", data.Raw[9], data.Raw[8]);
                            AccelZOut.Text = string.Format("Z:  \t{0:000} {1:000}", data.Raw[7], data.Raw[6]);

                            MagnoXOut.Text = string.Format("X:  \t{0:000} {1:000}", data.Raw[5], data.Raw[4]);
                            MagnoYOut.Text = string.Format("Y:  \t{0:000} {1:000}", data.Raw[3], data.Raw[2]);
                            MagnoZOut.Text = string.Format("Z:  \t{0:000} {1:000}", data.Raw[1], data.Raw[0]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.HUMIDITY):
                            HumidOut.Text = string.Format("H:\t{0:000} {1:000} {2:000} {3:000}", data.Raw[3], data.Raw[2], data.Raw[1], data.Raw[0]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.OPTICAL):
                            LuxOut.Text = string.Format("L:  \t{0:000} {1:000}", data.Raw[1], data.Raw[0]);
                            break; ;
                        case (CC2650SensorTag.SensorIndexes.BAROMETRIC_PRESSURE):
                            BaroOut.Text = string.Format("T:\t{0:000} {1:000} {2:000}", data.Raw[5], data.Raw[4], data.Raw[3]);
                            BaroOutTemp.Text = string.Format("P:\t{0:000} {1:000} {2:000}", data.Raw[2], data.Raw[1], data.Raw[0]);
                            break;
                        case (CC2650SensorTag.SensorIndexes.KEYS):
                            if (data.Values[0] > 0)
                                KeyROut.Background = new SolidColorBrush(Colors.Green);
                            else
                                KeyROut.Background = new SolidColorBrush(Colors.Red);

                            if (data.Values[1] > 0)
                                KeyLOut.Background = new SolidColorBrush(Colors.Green);
                            else
                                KeyLOut.Background = new SolidColorBrush(Colors.Red);

                            if (data.Values[2] > 0)
                                ReedOut.Background = new SolidColorBrush(Colors.Green);
                            else
                                ReedOut.Background = new SolidColorBrush(Colors.Red);
                            break;
                        default:
                            break;
                    }
                }
            });
        }

        // ---------------------------------------------------
        //     Hardware Configuration Helper Functions
        // ---------------------------------------------------





        private async void PairButton_Click(object sender, RoutedEventArgs e)
        {
            return;
            //HideMenu();
            DeviceInformationDisplay deviceInfoDisp = resultsListView.SelectedItem as DeviceInformationDisplay;

            if (deviceInfoDisp != null)
            {
                PairButton.IsEnabled = false;
                bool paired = true;
                if (deviceInfoDisp.IsPaired != true)
                {
                    paired = false;
                    DevicePairingKinds ceremoniesSelected = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.DisplayPin | DevicePairingKinds.ProvidePin | DevicePairingKinds.ConfirmPinMatch;
                    DevicePairingProtectionLevel protectionLevel = DevicePairingProtectionLevel.None;//.Default;

                    // Specify custom pairing with all ceremony types and protection level EncryptionAndAuthentication
                    DeviceInformationCustomPairing customPairing = deviceInfoDisp.DeviceInformation.Pairing.Custom;

#if (WINDOWS_10_IOT_CORE)
                    UserOut.Text = "Not paired. With IoT-Core you need to pair outside of app. Use Device Portal";
                  
                    DevicePairingKinds _ceremoniesSelected = DevicePairingKinds.ConfirmOnly; 
                    ShowPairingPanel("Confirm Pairing",_ceremoniesSelected);


                    PairingRequestedHandler2(null, DevicePairingKinds.ConfirmOnly);
                    HidePairingPanel();
                    DevicePairingResult result = await customPairing.PairAsync(ceremoniesSelected, protectionLevel);
                    DevicePairingResultStatus resultStatus = result.Status;


#else
                    customPairing.PairingRequested += PairingRequestedHandler;
                    DevicePairingResult result = await customPairing.PairAsync(ceremoniesSelected, protectionLevel);

                    customPairing.PairingRequested -= PairingRequestedHandler;
                     DevicePairingResultStatus resultStatus =result.Status;
#endif
                    if (resultStatus == DevicePairingResultStatus.Paired)
                    {
                        paired = true;
                    }
                    else
                    {
#if (WINDOWS_10_IOT_CORE)
                        UserOut.Text = "Not paired. With IoT-Core you need to pair outside of app. Use Device Portal";
#else
                        UserOut.Text = "Pairing Failed " + resultStatus.ToString();
#endif
                        System.Diagnostics.Debug.WriteLine(UserOut.Text);
                    }
                }

                if (paired)
                {
                    // device is paired, set up the sensor Tag            
                    UserOut.Text = "Setting up SensorTag";

                    DeviceInfoConnected = deviceInfoDisp;

                    //Start watcher for Bluetooth LE Services
                    CC2650SensorTag_BLEWatcher.StartBLEWatcher(this, SetDeviceInfo);
                }
                UpdatePairingButtons();
            }
        }
        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            return;
            UpdatePairingButtons();
            if (resultsListView.SelectedIndex != -1)
                PairButton_Click(null, null);
        }

        private async void PairingRequestedHandler(
         DeviceInformationCustomPairing sender,
         DevicePairingRequestedEventArgs args)
        {
            await PairingRequestedHandler2(args, args.PairingKind);
        }
        private async Task PairingRequestedHandler2(
             DevicePairingRequestedEventArgs args,
             DevicePairingKinds kind)
        {

            switch (kind)
            {
                case DevicePairingKinds.ConfirmOnly:
                    // Windows itself will pop the confirmation dialog as part of "consent" if this is running on Desktop or Mobile
                    // If this is an App for 'Windows IoT Core' where there is no Windows Consent UX, you may want to provide your own confirmation.
                    args.Accept();
                    break;

                case DevicePairingKinds.DisplayPin:
                    // We just show the PIN on this side. The ceremony is actually completed when the user enters the PIN
                    // on the target device. We automatically except here since we can't really "cancel" the operation
                    // from this side.
                    args.Accept();

                    // No need for a deferral since we don't need any decision from the user
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ShowPairingPanel(
                            "Please enter this PIN on the device you are pairing with: " + args.Pin,
                            args.PairingKind);

                    });
                    break;

                case DevicePairingKinds.ProvidePin:
                    // A PIN may be shown on the target device and the user needs to enter the matching PIN on 
                    // this Windows device. Get a deferral so we can perform the async request to the user.
                    var collectPinDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        string pin = await GetPinFromUserAsync();
                        if (!string.IsNullOrEmpty(pin))
                        {
                            args.Accept(pin);
                        }

                        collectPinDeferral.Complete();
                    });
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // We show the PIN here and the user responds with whether the PIN matches what they see
                    // on the target device. Response comes back and we set it on the PinComparePairingRequestedData
                    // then complete the deferral.
                    var displayMessageDeferral = args.GetDeferral();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        bool accept = await GetUserConfirmationAsync(args.Pin);
                        if (accept)
                        {
                            args.Accept();
                        }

                        displayMessageDeferral.Complete();
                    });
                    break;
            }
        }

        private void ShowPairingPanel(string text, DevicePairingKinds pairingKind)
        {
            pairingPanel.Visibility = Visibility.Collapsed;
            pinEntryTextBox.Visibility = Visibility.Collapsed;
            okButton.Visibility = Visibility.Collapsed;
            yesButton.Visibility = Visibility.Collapsed;
            noButton.Visibility = Visibility.Collapsed;
            pairingTextBlock.Text = text;

            switch (pairingKind)
            {
                case DevicePairingKinds.ConfirmOnly:
                case DevicePairingKinds.DisplayPin:
                    // Don't need any buttons
                    break;
                case DevicePairingKinds.ProvidePin:
                    pinEntryTextBox.Text = "";
                    pinEntryTextBox.Visibility = Visibility.Visible;
                    okButton.Visibility = Visibility.Visible;
                    break;
                case DevicePairingKinds.ConfirmPinMatch:
                    yesButton.Visibility = Visibility.Visible;
                    noButton.Visibility = Visibility.Visible;
                    break;
            }

            pairingPanel.Visibility = Visibility.Visible;
        }

        private void HidePairingPanel()
        {
            pairingPanel.Visibility = Visibility.Collapsed;
            pairingTextBlock.Text = "";
        }

        private async Task<string> GetPinFromUserAsync()
        {
            HidePairingPanel();
            CompleteProvidePinTask(); // Abandon any previous pin request.

            ShowPairingPanel(
                "Please enter the PIN shown on the device you're pairing with",
                DevicePairingKinds.ProvidePin);

            providePinTaskSrc = new TaskCompletionSource<string>();

            return await providePinTaskSrc.Task;
        }

        // If pin is not provided, then any pending pairing request is abandoned.
        private void CompleteProvidePinTask(string pin = null)
        {
            if (providePinTaskSrc != null)
            {
                providePinTaskSrc.SetResult(pin);
                providePinTaskSrc = null;
            }
        }

        private async Task<bool> GetUserConfirmationAsync(string pin)
        {
            HidePairingPanel();
            CompleteConfirmPinTask(false); // Abandon any previous request.

            ShowPairingPanel(
                "Does the following PIN match the one shown on the device you are pairing?: " + pin,
                DevicePairingKinds.ConfirmPinMatch);

            confirmPinTaskSrc = new TaskCompletionSource<bool>();

            return await confirmPinTaskSrc.Task;
        }

        // If pin is not provided, then any pending pairing request is abandoned.
        private void CompleteConfirmPinTask(bool accept)
        {
            if (confirmPinTaskSrc != null)
            {
                confirmPinTaskSrc.SetResult(accept);
                confirmPinTaskSrc = null;
            }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            // OK button is only used for the ProvidePin scenario
            CompleteProvidePinTask(pinEntryTextBox.Text);
            HidePairingPanel();
        }

        private void yesButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteConfirmPinTask(true);
            HidePairingPanel();
        }

        private void noButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteConfirmPinTask(false);
            HidePairingPanel();
        }

        private async void UnpairButton_Click(object sender, RoutedEventArgs e)
        {
            return;
            //HideMenu();
            DeviceInformationDisplay deviceInfoDisp = resultsListView.SelectedItem as DeviceInformationDisplay;
            Debug.WriteLine("Unpair");

            UnpairButton.IsEnabled = false;
            SensorList.IsEnabled = false;
            InitButton.IsEnabled = false;
            EnableNotifButton.IsEnabled = false;
            DisableNotifButton.IsEnabled = false;

            EnableIOIOButton.IsEnabled = false;
            DisableIOIOButton.IsEnabled = false;
            AllOffIOButton.IsEnabled = false;
            BuzzButton.IsEnabled = false;
            LED1Button.IsEnabled = false;
            LED2Button.IsEnabled = false;
            DeviceInfoConnected = null;

            Debug.WriteLine("Disable Sensors");
            for (int i = 0; i < CC2650SensorTag.NUM_SENSORS; i++)
            {
                if (CC2650SensorTag.ServiceList[i] != null)
                {
                    //await disableSensor(i);
                    try
                    {
                        //await disableSensor(i);
                        await CC2650SensorTag.SensorsCharacteristicsList[i].DisableNotify();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("UnpairButton_Click(): DisableNotify error. Service:  " +  i.ToString() + "\r\n" + ex.Message);
                    }
                }
            }

            Debug.WriteLine("UnpairAsync");
            try
            {
                DeviceUnpairingResult dupr = await deviceInfoDisp.DeviceInformation.Pairing.UnpairAsync();
                string unpairResult = $"Unpairing result = {dupr.Status}";
                Debug.WriteLine(unpairResult);
                UserOut.Text = unpairResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unpair exception = " + ex.Message);
            }

            for (int i = 0; i < CC2650SensorTag.ServiceList.Length; i++)
            {
                CC2650SensorTag.ServiceList[i] = null;
            }

            UpdatePairingButtons();
            StartWatcher();
        }

        private void UpdatePairingButtons()
        {
            var deviceInfoDisp = (DeviceInformationDisplay)resultsListView.SelectedItem;
            bool bSelectableDevices = (resultsListView.Items.Count > 0);

            // If something on the list of bluetooth devices is selected
            if ((null != deviceInfoDisp) && ((deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                || deviceWatcher.Status == DeviceWatcherStatus.Stopped))
            {
                bool bIsConnected = (DeviceInfoConnected != null);

                // If we're paired and this app has connected to the device then allow the user to unpair, 
                // from the selected device, but do not allow the use to pair with or select some other device.
                if ((deviceInfoDisp.DeviceInformation.Pairing.IsPaired) && (bIsConnected))
                {
                    resultsListView.IsEnabled = false;
                    UnpairButton.IsEnabled = true;
                    PairButton.IsEnabled = false;
                }
                // Otherwise, we're either unpaired OR we are paired to something but this app hasn't connected to it
                // so allow the user to select one of the BLE devices from the list
                else
                {
                    resultsListView.IsEnabled = bSelectableDevices;
                    UnpairButton.IsEnabled = false;
                    PairButton.IsEnabled = bSelectableDevices;
                }
            }
            // otherwise there are no devices selected by the user, so allow the user to select something
            // so long as there are items on the list to select
            else
            {
                resultsListView.IsEnabled = bSelectableDevices;
                PairButton.IsEnabled = false;
            }
           
        }


        // ---------------------------------------------------
        //             Pairing Process Handlers and Functions -- End
        // ---------------------------------------------------

        //This isn't working so this button is disabled
        private async void InitButton_Click(object sender, RoutedEventArgs e)
        {
            if (SensorList.SelectedIndex >= 0)
            {

                await initSensor((CC2650SensorTag.SensorIndexes)SensorList.SelectedIndex);
            }
        }

        private async void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            //HideMenu();
            if (SensorList.SelectedIndex >= 0)
            {
                await CC2650SensorTag.SensorsCharacteristicsList[SensorList.SelectedIndex].EnableNotify();
                //enableSensor(GATTClassCharacteristics.SensorIndexes[SensorList.SelectedIndex]);
                CC2650SensorTag.ActiveCharacteristicNotifications[SensorList.SelectedIndex] = CC2650SensorTag.SensorsCharacteristicsList[SensorList.SelectedIndex].Notification;
                if (CC2650SensorTag.DisableSensorWithDisableNotifications)
                    await CC2650SensorTag.SensorsCharacteristicsList[SensorList.SelectedIndex].TurnOnSensor();
            }
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            //HideMenu();
            if (SensorList.SelectedIndex >= 0)
            {
                if (CC2650SensorTag.DisableSensorWithDisableNotifications)
                    await CC2650SensorTag.SensorsCharacteristicsList[SensorList.SelectedIndex].TurnOffSensor();
                await CC2650SensorTag.SensorsCharacteristicsList[SensorList.SelectedIndex].DisableNotify();
                //disableSensor(GATTClassCharacteristics.SensorIndexes[SensorList.SelectedIndex]);
                CC2650SensorTag.ActiveCharacteristicNotifications[SensorList.SelectedIndex] = null;

            }
        }

        private async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            //HideMenu();
            CC2650SensorTag.SensorData sensorData = await CC2650SensorTag.SensorsCharacteristicsList
                [SensorList.SelectedIndex].ReadSensor(true, true, CC2650SensorTag.DisableSensorWithDisableNotifications);
        }

        private async void BuzzButton_ClickOn(object sender, RoutedEventArgs e)
        {
            //HideMenu();
            string name = "";
            if (sender is Button)
            {
                Button butt = (Button)sender;

                if (butt != null)
                {
                    name = butt.Name.Substring(0, 4);
                }
            }
            else if (sender is TextBlock)
            {
                TextBlock tb = (TextBlock)sender;

                if (tb != null)
                {
                    name = tb.Text.Replace(" " ,"");
                }
            }

            switch (name)
            {
                case "LED1":
                    await CC2650SensorTag.GlobalActionIO(CC2650SensorTag.IOActions.On, 1);
                    break;
                case "LED2":
                    await CC2650SensorTag.GlobalActionIO(CC2650SensorTag.IOActions.On, 2);
                    break;
                case "Buzz":
                    await CC2650SensorTag.GlobalActionIO(CC2650SensorTag.IOActions.On, 4);
                    break;
            }
            
        }

        private async void BuzzButton_ClickOff(object sender, RoutedEventArgs e)
        {
            //HideMenu();
            string name = "";
            if (sender is Button)
            {
                Button butt = (Button)sender;

                if (butt != null)
                {
                    name = butt.Name.Replace("IOButton", "");
                }
            }
            else if (sender is TextBlock)
            {
                TextBlock tb = (TextBlock)sender;

                if (tb != null)
                {
                    name = tb.Text.Replace(" ", "");
                }
            }

            switch (name)
     
            {
                case "AllOff":
                    await CC2650SensorTag.GlobalActionIO(CC2650SensorTag.IOActions.AllOff, 0);
                    break;
                case "EnableIO":
                    await CC2650SensorTag.GlobalActionIO(CC2650SensorTag.IOActions.Enable, 0);
                    break;
                case "DisableIO":
                    await CC2650SensorTag.GlobalActionIO(CC2650SensorTag.IOActions.Disable, 0);
                    break;
            }
            
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            //HideMenu();
            Application.Current.Exit();
        }

        private void InitButton1_Click(object sender, TappedRoutedEventArgs e)
        {
            //HideMenu();
            this.Frame.Navigate(typeof( DeviceProperties), this);

        }

        

        public void SetDeviceInfo(DeviceInformation value)
        {
            Debug.WriteLine("BLEWatcher Add: " + value.Id);
            ResultCollection.Add(new DeviceInformationDisplay(value));
        }


    }
}
