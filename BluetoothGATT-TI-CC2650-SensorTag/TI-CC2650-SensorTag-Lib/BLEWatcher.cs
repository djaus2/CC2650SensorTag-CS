using Windows.System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using System.Threading.Tasks;
using System.ServiceModel;
using Windows.Devices.Radios;
using System;
using Windows.UI.Xaml.Controls;
using System.Threading;
using Windows.Storage;

namespace TICC2650SensorTag
{
    //Used to pass the device information back to to the UX


    public static class BT
    {
        
        //https://docs.microsoft.com/en-us/uwp/api/windows.devices.radios.radio#Windows_Devices_Radios_Radio_RequestAccessAsync
        public static async Task<bool> GetBluetoothIsEnabledAsync()
        {
            var radios = await Radio.GetRadiosAsync();
            if (radios != null)
            {
                int count = radios.Count();
            }
            var bluetoothRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
            return (bluetoothRadio != null && bluetoothRadio.State == RadioState.On);
        }


        
        public static async Task<bool> GetBluetoothIsSupportedAsync()
        {
            var radios = await Radio.GetRadiosAsync();
            if (radios != null)
            {
                int count = radios.Count();
            }
                return radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth) != null;
        }

        public static async Task<bool> SetBluetoothStateOnAsync()
        {
            var radios = await Radio.GetRadiosAsync();
            if (radios != null)
            {
                int count = radios.Count();
            }
            if (radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth) != null)
            {
                var btRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
                var state = await btRadio.SetStateAsync(RadioState.On);
                return (state == RadioAccessStatus.Allowed);

            }
            else
                return false;
        }

        public static async Task<bool> SetBluetoothStateOffAsync()
        {
            var radios = await Radio.GetRadiosAsync();
            if (radios != null)
            {
                int count = radios.Count();
            }
            if (radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth) != null)
            {
                var btRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
                var state = await btRadio.SetStateAsync(RadioState.Off);
                return (state == RadioAccessStatus.Allowed);
            }
            else
                return false;
        }

        public static async Task<bool> RequestAccessAsync()
        {
            var radios = await Radio.GetRadiosAsync();
            if (radios != null)
            {
                int count = radios.Count();

                if (count != 0)
                {
                    if (radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth) != null)
                    {
                        var access = await Radio.RequestAccessAsync();

                        return (access == RadioAccessStatus.Allowed);
                    }
                }
            }
            return false;
        }
    }
    public class TICC2650SensorTag_BLEWatcher : ITICC2650SensorTag_BLEWatcher
    {

        private DeviceWatcher blewatcher = null;
        private TypedEventHandler<DeviceWatcher, DeviceInformation> OnBLEAdded = null;
        private TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> OnBLEUpdated = null;
        private TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> OnBLERemoved = null;

        public delegate void UpdateButtons_WhenReadyDelegate();

        private UpdateButtons_WhenReadyDelegate UpdateButtons_WhenSensorsAreReady_CallBack { get; set; } = null;

        public delegate Task initSensorDelegate(CC2650SensorTag.SensorIndexes sensorIndx);


        private initSensorDelegate InitSensorCallback { get; set; } = null;

        private CC2650SensorTag.SensorDataDelegate CallMeBack { get; set; } = null;

        public TICC2650SensorTag_BLEWatcher(
            UpdateButtons_WhenReadyDelegate updateButtons_WhenReady_CallBack,
            CC2650SensorTag.SensorDataDelegate callMeBack, 
            initSensorDelegate initSensorCallback)
        {
            UpdateButtons_WhenSensorsAreReady_CallBack = updateButtons_WhenReady_CallBack;
            CallMeBack = callMeBack;
            InitSensorCallback = initSensorCallback;
        }

        ~TICC2650SensorTag_BLEWatcher()
        {
            StopBLEWatcher();
        }

        Timer EventTimer = null;
        long LastEventCount = 0;
        long counter = 0;
        static long updating = 0;

        private async void EventTimerCallback(object state)
        {
            counter++;
            long currentCount = System.Threading.Interlocked.Read(ref CC2650SensorTag.EventCount);
            long diff = currentCount - LastEventCount;
            LastEventCount = currentCount;


            if ((counter > CC2650SensorTag.NumTimerEventsToWaitBeforeTurningOffUpdates) && (!CC2650SensorTag.SetSensorsManualMode))
            {
                //Give sensors a change to switchto manual mode.
                CC2650SensorTag.SetSensorsManualMode = true;
                return;
            }

            if (sampleFile != null)
                await Windows.Storage.FileIO.AppendTextAsync(sampleFile, counter.ToString() + " " + diff.ToString() + "\r\n");

            if (System.Threading.Interlocked.Read(ref updating) == 1)
                return;

            if (CC2650SensorTag.PeriodicUpdatesOnly)
                if (CC2650SensorTag.SetSensorsManualMode)
                    if (((counter) % CC2650SensorTag.Period) == 0) 
                    {
                        System.Threading.Interlocked.Exchange(ref updating, 1);
                        await CC2650SensorTag.GetBatteryLevel();
                        await CC2650SensorTag.ReadAllSensors();
                        System.Threading.Interlocked.Exchange(ref updating, 0);
                    }
        }

        Windows.Storage.StorageFile sampleFile = null;
        public static bool HasOKd = false;
        public static Page NainPage2 { get; set; } = null;

        //Watcher for Bluetooth LE Services
        public void StartBLEWatcher(Page mainPage2, DeviceInfoDel SetDevInfo, SetupProgressDel setUpProgress2)
        {
            NainPage2 = mainPage2;
          
            HasOKd = false;
            long discoveredServices = 0;
            int notifiedServices = 0;
            ManualResetEvent firstServiceStartedResetEvent = new ManualResetEvent(false);
            CC2650SensorTag.EventCount = 0;
            CC2650SensorTag.SetSensorsManualMode = false;

            //Init values for log
            long start = 0;
            counter = 0;
            System.Threading.Interlocked.Exchange(ref updating, 0);
            // Hook up handlers for the watcher events before starting the watcher
            OnBLEAdded = async (watcher, deviceInfo) =>
            {            
                if (System.Threading.Interlocked.Increment(ref notifiedServices) == 1)
                {


                    //await Task.Run(async () =>
                    await NainPage2.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        //Set up event logging
                        StorageFolder storageFolder = KnownFolders.DocumentsLibrary;
                        sampleFile = await storageFolder.CreateFileAsync("sample.log", CreationCollisionOption.ReplaceExisting);

                        if (CC2650SensorTag.DeviceAltSensorNames.Contains(deviceInfo.Name))
                        {
                            setUpProgress2();
                            Debug.WriteLine("OnBLEAdded1 On UI Thread: " + deviceInfo.Id);
                            GattDeviceService service = null;
                            try
                            {
                                HasOKd = true;
                                service = await GattDeviceService.FromIdAsync(deviceInfo.Id);
                                SetDevInfo(deviceInfo);
                            }
                            catch (Exception ex)
                            {
                                HasOKd = false;
                                string msg = ex.Message;
                                Debug.WriteLine("Error: OnBLEAdded2() on UI Thread(): " + deviceInfo.Id + " " + msg);
                                return;
                            }
                            firstServiceStartedResetEvent.Set();
                            if (service != null)
                            {

                                CC2650SensorTag.SensorIndexes sensorIndx = CC2650SensorTag.SensorIndexes.NOTFOUND;
                                string svcGuid = service.Uuid.ToString().ToUpper();
                                Debug.WriteLine("Found Service: " + svcGuid);

                                // Add this service to the list if it conforms to the TI-GUID pattern for most sensors
                                if (svcGuid == CC2650SensorTag.DEVICE_BATTERY_SERVICE)
                                {
                                    CC2650SensorTag.SetUpBattery(service);
                                    byte[] bytes = await CC2650SensorTag.GetBatteryLevel();
                                    return;
                                }
                                else if (svcGuid == CC2650SensorTag.UUID_PROPERTIES_SERVICE.ToUpper())
                                {
                                    CC2650SensorTag.DevicePropertyService = service;
                                    await CC2650SensorTag.GetProperties();
                                    return;
                                }


                                else if (svcGuid == CC2650SensorTag.IO_SENSOR_GUID_STR)
                                {
                                    sensorIndx = CC2650SensorTag.SensorIndexes.IO_SENSOR;
                                }
                                else if (svcGuid == CC2650SensorTag.REGISTERS_GUID_STR)
                                {
                                    sensorIndx = CC2650SensorTag.SensorIndexes.REGISTERS;
                                }
                                // otherwise, if this is the GUID for the KEYS, then handle it special
                                else if (svcGuid == CC2650SensorTag.BUTTONS_GUID_STR)
                                {
                                    sensorIndx = CC2650SensorTag.SensorIndexes.KEYS;
                                }
                                else if (svcGuid.StartsWith(CC2650SensorTag.SENSOR_GUID_PREFIX))
                                {
                                    // The character at this position indicates the index into the ServiceList 
                                    // container that we want to save this service to.  The rest of this program
                                    // assumes that specific sensor types are at specific indexes in this array
                                    int Indx = (svcGuid[6] - '0');
                                    sensorIndx = CC2650SensorTag.GetSensorIndex(Indx);
                                }
                                // If the index is legal and a service hasn't already been cached, then
                                // cache this service in our ServiceList
                                if (((sensorIndx >= 0) && (sensorIndx <= (CC2650SensorTag.SensorIndexes)CC2650SensorTag.SENSOR_MAX)) && (CC2650SensorTag.ServiceList[(int)sensorIndx] == null))
                                {
                                    CC2650SensorTag.ServiceList[(int)sensorIndx] = service;
                                    await initSensor(sensorIndx);
                                    System.Threading.Interlocked.Increment(ref discoveredServices);
                                }
                                else
                                {

                                }

                                // When all sensors have been discovered, notify the user
                                if (discoveredServices > 0) // == NUM_SENSORS)
                                {
                                    UpdateButtons_WhenSensorsAreReady_CallBack?.Invoke();

                                    if (discoveredServices == CC2650SensorTag.NUM_SENSORS_TO_TEST)
                                    {
                                        blewatcher.Stop();
                                        Debug.WriteLine("blewatcher Stopped.");
                                    }
                                    discoveredServices = 0;
                                    setUpProgress2();
                                    // UserOut.Text = "Sensors on!";
                                }
                                else
                                {
                                    Debug.WriteLine("NO Found Service: " + svcGuid);
                                }
                            }
                        }                  
                    });
                }
                else
                {
                    firstServiceStartedResetEvent.WaitOne();
                    await Task.Run(async () =>
                    //await NainPage2.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        if (CC2650SensorTag.DeviceAltSensorNames.Contains(deviceInfo.Name))
                        {
                            CC2650SensorTag.IncProg();
                            Debug.WriteLine("OnBLEAdded2() Not on UI thread: " + deviceInfo.Id);
                            GattDeviceService service = null;
                            try
                            {
                                service = await GattDeviceService.FromIdAsync(deviceInfo.Id);
                            }
                            catch (Exception ex)
                            {
                                string msg = ex.Message;
                                Debug.WriteLine("Error: OnBLEAdded2() Not on UI Thread: " + deviceInfo.Id  + " " + msg);
                                return;
                            }
                            if (service != null)
                            {
                                CC2650SensorTag.SensorIndexes sensorIndx = CC2650SensorTag.SensorIndexes.NOTFOUND;
                                string svcGuid = service.Uuid.ToString().ToUpper();
                                Debug.WriteLine("Found Service: " + svcGuid);

                                // Add this service to the list if it conforms to the TI-GUID pattern for most sensors
                                if (svcGuid == CC2650SensorTag.DEVICE_BATTERY_SERVICE)
                                {
                                    CC2650SensorTag.SetUpBattery(service);
                                    byte[] bytes = await CC2650SensorTag.GetBatteryLevel();
                                    return;
                                }
                                else if (svcGuid == CC2650SensorTag.UUID_PROPERTIES_SERVICE.ToUpper())
                                {
                                    CC2650SensorTag.DevicePropertyService = service;
                                    await CC2650SensorTag.GetProperties();
                                    return;
                                }


                                else if (svcGuid == CC2650SensorTag.IO_SENSOR_GUID_STR)
                                {
                                    sensorIndx = CC2650SensorTag.SensorIndexes.IO_SENSOR;
                                }
                                else if (svcGuid == CC2650SensorTag.REGISTERS_GUID_STR)
                                {
                                    sensorIndx = CC2650SensorTag.SensorIndexes.REGISTERS;
                                }
                                // otherwise, if this is the GUID for the KEYS, then handle it special
                                else if (svcGuid == CC2650SensorTag.BUTTONS_GUID_STR)
                                {
                                    sensorIndx = CC2650SensorTag.SensorIndexes.KEYS;
                                }
                                else if (svcGuid.StartsWith(CC2650SensorTag.SENSOR_GUID_PREFIX))
                                {
                                    // The character at this position indicates the index into the ServiceList 
                                    // container that we want to save this service to.  The rest of this program
                                    // assumes that specific sensor types are at specific indexes in this array
                                    int Indx = (svcGuid[6] - '0');
                                    sensorIndx = CC2650SensorTag.GetSensorIndex(Indx);
                                }
                                // If the index is legal and a service hasn't already been cached, then
                                // cache this service in our ServiceList
                                if (((sensorIndx >= 0) && (sensorIndx <= (CC2650SensorTag.SensorIndexes)CC2650SensorTag.SENSOR_MAX)) && (CC2650SensorTag.ServiceList[(int)sensorIndx] == null))
                                {
                                    CC2650SensorTag.ServiceList[(int)sensorIndx] = service;
                                    await initSensor(sensorIndx);
                                    System.Threading.Interlocked.Increment(ref discoveredServices);
                                }
                                else
                                {

                                }

                                // When all sensors have been discovered, notify the user
                                long curDiscv = System.Threading.Interlocked.Read(ref discoveredServices);
                                if (curDiscv > 0) // == NUM_SENSORS)
                                {
                                    UpdateButtons_WhenSensorsAreReady_CallBack?.Invoke();

                                    if (curDiscv == CC2650SensorTag.NUM_SENSORS_TO_TEST)
                                    {
                                        blewatcher.Stop();
                                        Debug.WriteLine("blewatcher Stopped.");
                                        System.Threading.Interlocked.Exchange(ref CC2650SensorTag.EventCount, start);
                                        EventTimer = new Timer(EventTimerCallback, null, 0, (int)CC2650SensorTag.UpdatePeriod);
                                        discoveredServices = 0;
                                    }
                                    
                                    // UserOut.Text = "Sensors on!";
                                }

                                
                            }
                            CC2650SensorTag.IncProg();
                        }
                        
                    });
                }
            };

            OnBLEUpdated = async (watcher, deviceInfoUpdate) =>
           {
               await Task.Run(() => Debug.WriteLine($"OnBLEUpdated: {deviceInfoUpdate.Id}"));

           };


            OnBLERemoved = async (watcher, deviceInfoUpdate) =>
            {
                await Task.Run(() => Debug.WriteLine("OnBLERemoved"));
            };

            string aqs = "";
            if (CC2650SensorTag.ServiceSensors)
            {
                for (int ii = 0; ii < CC2650SensorTag.NUM_SENSORS_TO_TEST; ii++)
                {
                    int i = CC2650SensorTag.FIRST_SENSOR + ii;
                    CC2650SensorTag.SensorIndexes sensorIndx = (CC2650SensorTag.SensorIndexes)i;
                    Guid BLE_GUID; Debug.WriteLine("NUMSENSORS " + sensorIndx.ToString());
                    if (sensorIndx == CC2650SensorTag.SensorIndexes.IO_SENSOR)
                        BLE_GUID = CC2650SensorTag.IO_SENSOR_GUID;
                    else if (sensorIndx == CC2650SensorTag.SensorIndexes.REGISTERS)
                        BLE_GUID = CC2650SensorTag.REGISTERS_GUID;
                    else if (sensorIndx != CC2650SensorTag.SensorIndexes.KEYS)
                        BLE_GUID = new Guid(CC2650SensorTag.UUIDBase[i] + CC2650SensorTag.SENSOR_GUID_SUFFFIX);
                    else
                        BLE_GUID = CC2650SensorTag.BUTTONS_GUID;

                    aqs += "(" + GattDeviceService.GetDeviceSelectorFromUuid(BLE_GUID) + ")";

                    if (ii < CC2650SensorTag.NUM_SENSORS_TO_TEST - 1)
                    {
                        aqs += " OR ";
                    }
                }
            }



            if (CC2650SensorTag.Use_DEVICE_BATTERY_SERVICE)
            {
                if (CC2650SensorTag.ServiceSensors)
                    aqs += " OR ";
                aqs += "(" + GattDeviceService.GetDeviceSelectorFromUuid(new Guid(CC2650SensorTag.DEVICE_BATTERY_SERVICE)) + ")";
            }
            if (CC2650SensorTag.Use_UUID_PROPERTIES_SERVICE)
            {
                if ( (CC2650SensorTag.ServiceSensors) || (CC2650SensorTag.Use_DEVICE_BATTERY_SERVICE))
                    aqs += " OR ";
                aqs += "(" + GattDeviceService.GetDeviceSelectorFromUuid(new Guid(CC2650SensorTag.UUID_PROPERTIES_SERVICE)) + ")";
            }


            blewatcher = DeviceInformation.CreateWatcher(aqs);
            blewatcher.Added += OnBLEAdded;
            blewatcher.Updated += OnBLEUpdated;
            blewatcher.Removed += OnBLERemoved;
            blewatcher.Start();
            CC2650SensorTag.IncProg();
        }



        private async Task initSensor(CC2650SensorTag.SensorIndexes sensorIndx)
        {
            GattDeviceService gattService = CC2650SensorTag.ServiceList[(int)sensorIndx];
            if (gattService != null)
            {
                CC2650SensorTag temp = new CC2650SensorTag(gattService, sensorIndx, CallMeBack);
                if (temp != null)
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
               Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        // your code to access the UI thread widgets
                        InitSensorCallback?.Invoke(sensorIndx);
                    });

                }
                //Sensor Specific NonUI actions post init
                switch (sensorIndx)
                {
                    case (CC2650SensorTag.SensorIndexes.IR_SENSOR):
                        break;
                    case (CC2650SensorTag.SensorIndexes.MOVEMENT):
                        temp.setSensorPeriod(1000);
                        break;
                    case (CC2650SensorTag.SensorIndexes.HUMIDITY):
                        break;
                    case (CC2650SensorTag.SensorIndexes.OPTICAL):
                        break; ;
                    case (CC2650SensorTag.SensorIndexes.BAROMETRIC_PRESSURE):;
                        break;
                    case (CC2650SensorTag.SensorIndexes.KEYS):
                        break;
                    default:
                        break;
                }
            }
            CC2650SensorTag.IncProg();
        }

        public void StopBLEWatcher()
        {
            if (null != blewatcher)
            {
                blewatcher.Added -= OnBLEAdded;
                blewatcher.Updated -= OnBLEUpdated;
                blewatcher.Removed -= OnBLERemoved;

                if (DeviceWatcherStatus.Started == blewatcher.Status ||
                    DeviceWatcherStatus.EnumerationCompleted == blewatcher.Status)
                {
                    blewatcher.Stop();
                }
            }
        }



        // Enable and subscribe to specified GATT characteristic





    }
}
