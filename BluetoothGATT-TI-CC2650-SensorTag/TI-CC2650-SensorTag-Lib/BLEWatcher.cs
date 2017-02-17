using System;
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

namespace TICC2650SensorTag
{
    public class TICC2650SensorTag_BLEWatcher
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


        //Watcher for Bluetooth LE Services
        public void StartBLEWatcher()
        {
            int discoveredServices = 0;
            // Hook up handlers for the watcher events before starting the watcher
            OnBLEAdded = async (watcher, deviceInfo) =>
            {
                await Task.Run(async () =>
                //Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    Debug.WriteLine("OnBLEAdded: " + deviceInfo.Id);
                    GattDeviceService service = null;
                    try
                    {
                        service = await GattDeviceService.FromIdAsync(deviceInfo.Id);
                    }
                    catch (Exception ex)
                    {
                        string msg = ex.Message;
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
                        if (discoveredServices > 0) // == NUM_SENSORS)
                        {
                            UpdateButtons_WhenSensorsAreReady_CallBack?.Invoke();

                            if (discoveredServices == CC2650SensorTag.NUM_SENSORS_TO_TEST)
                            {
                                blewatcher.Stop();
                                Debug.WriteLine("blewatcher Stopped.");
                            }
                            discoveredServices = 0;
                            // UserOut.Text = "Sensors on!";
                        }
                    }
                });
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


            aqs += " OR ";
            aqs += "(" + GattDeviceService.GetDeviceSelectorFromUuid(new Guid(CC2650SensorTag.DEVICE_BATTERY_SERVICE)) + ")";
            aqs += " OR ";
            aqs += "(" + GattDeviceService.GetDeviceSelectorFromUuid(new Guid(CC2650SensorTag.UUID_PROPERTIES_SERVICE)) + ")";


            blewatcher = DeviceInformation.CreateWatcher(aqs);
            blewatcher.Added += OnBLEAdded;
            blewatcher.Updated += OnBLEUpdated;
            blewatcher.Removed += OnBLERemoved;
            blewatcher.Start();
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
        }

        private void StopBLEWatcher()
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
