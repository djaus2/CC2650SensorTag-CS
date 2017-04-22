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
using System;
using System.Collections.Generic;

// Disable warning "...execution of the current method continues before the call is completed..."
#pragma warning disable 4014

// Disable warning to "consider using the 'await' operator to await non-blocking API calls"
#pragma warning disable 1998

namespace TICC2650SensorTag
{

    /// <summary>
    /// Sample app that communicates with Bluetooth device using the GATT profile
    /// </summary>
    public sealed partial class CC2650SensorTag
    {

        private async Task<bool> WriteSensor(byte[] bytes, ServiceCharacteristicsEnum character)
        {
            bool ret = false;
            Debug.WriteLine("Begin WriteSensor: " + SensorIndex.ToString());
            try
            {
                if (GattService != null)
                {
                    GattCharacteristic characteristic = null;
                    GattCharacteristicProperties flag = GattCharacteristicProperties.Write;
                    switch (character)
                    {
                        case ServiceCharacteristicsEnum.Data:
                            characteristic = this.Data;
                            break;
                        case ServiceCharacteristicsEnum.Notification:
                            flag = GattCharacteristicProperties.Notify;
                            characteristic = this.Notification;
                            break;
                        case ServiceCharacteristicsEnum.Configuration:
                            characteristic = this.Configuration;
                            break;
                        case ServiceCharacteristicsEnum.Period:
                            characteristic = this.Period;
                            break;
                        case ServiceCharacteristicsEnum.Address:
                            characteristic = this.Address;
                            break;
                        case ServiceCharacteristicsEnum.Device_Id:
                            characteristic = this.Device_Id;
                            break;
                    }
                    if (characteristic != null)
                    {
                        if (characteristic.CharacteristicProperties.HasFlag(flag))
                        {
                            var writer = new Windows.Storage.Streams.DataWriter();
                            writer.WriteBytes(bytes);

                            var status = await characteristic.WriteValueAsync(writer.DetachBuffer());
                            if (status == GattCommunicationStatus.Success)
                                ret = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: WriteSensor(): " + SensorIndex.ToString() + " " + ex.Message);
            }
            Debug.WriteLine("End WriteSensor " + SensorIndex.ToString());
            return ret;
        }

        /// <summary>
        /// Manually read sensor values from their Data Characteristic
        /// </summary>
        /// <param name="disableNotify">Notify needs to be off. Can optionally set this.</param>
        /// <param name="updateDisplay">Whether to callback to UI with data.</param>
        /// <param name="turnSensorOffOn">Whether to turn sensor on before data read and off afterwards. PS: Have found reads don't work reliably with this. Only need notifications turned off.</param>
        /// <returns>Buffer for data. Is created in the call.</returns>
        public async Task<SensorData> ReadSensor(bool disableNotify, bool updateDisplay, bool turnSensorOffOn)
        {
            byte[] bytes = null;
            SensorData sensorData = null;
            Debug.WriteLine("Begin ReadSensor: " + SensorIndex.ToString());
            try
            {
                if (SensorIndex >= 0 && SensorIndex != SensorIndexes.IO_SENSOR && SensorIndex != SensorIndexes.REGISTERS)
                {
                    if (GattService != null)
                    {
                        if (disableNotify)
                            await DisableNotify();
                        //Enable Sensor
                        if (turnSensorOffOn)
                            await TurnOnSensor();
                        try
                        {
                            bytes = await ReadSensorBase(ServiceCharacteristicsEnum.Notification);//..Data);
                                                                                                  //Disable Sensor
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error-1: ReadSensor(): " + SensorIndex.ToString() + " " + ex.Message);
                        }
                        if (turnSensorOffOn)
                            await TurnOffSensor();
                    }
                }

                if ((bytes != null) && (updateDisplay))
                {
                    switch (SensorIndex)
                    {
                        case SensorIndexes.KEYS:
                            sensorData = await keyChangedProc(bytes, updateDisplay);
                            break;
                        case SensorIndexes.IR_SENSOR:
                            sensorData = await tempChangedProc(bytes, updateDisplay);
                            break;
                        case SensorIndexes.HUMIDITY:
                            sensorData = await humidChangedProc(bytes, updateDisplay);
                            break;
                        case SensorIndexes.OPTICAL:
                            sensorData = await opticalChangedProc(bytes, updateDisplay);
                            break;
                        case SensorIndexes.MOVEMENT:
                            sensorData = await movementChangedProc(bytes, updateDisplay);
                            break;
                        case SensorIndexes.BAROMETRIC_PRESSURE:
                            sensorData = await pressureCC2650ChangedProc(bytes, updateDisplay);
                            break;
                        case SensorIndexes.IO_SENSOR:
                            break;
                        case SensorIndexes.REGISTERS:
                            break;
                        default:
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: ReadSensor(): " + SensorIndex.ToString() + " " + ex.Message);
            }
            Debug.WriteLine("End ReadSensor: " + SensorIndex.ToString());
            return sensorData;
        }

        /// <summary>
        /// Read a readable sensor characteristic, typically the Data characteristic
        /// </summary>
        /// <param name="character">The characteristic to read from.</param>
        /// <returns>Buffer for data. Is created in the call</returns>
        public async Task<byte[]> ReadSensorBase(ServiceCharacteristicsEnum character)
        {
            byte[] bytes = null;
            Debug.WriteLine("Begin ReadSensorBase: " + SensorIndex.ToString());
            bool ret = false;
            try
            {
                if (GattService != null)
                {
                    bytes = new byte[DataLength[(int)SensorIndex]];
                    GattCharacteristic characteristic = null;
                    GattCharacteristicProperties flag = GattCharacteristicProperties.Read;
                    switch (character)
                    {
                        case ServiceCharacteristicsEnum.Data:
                            characteristic = this.Data;
                            break;
                        case ServiceCharacteristicsEnum.Notification:
                            characteristic = this.Notification;
                            break;
                        case ServiceCharacteristicsEnum.Configuration:
                            characteristic = this.Configuration;
                            break;
                        case ServiceCharacteristicsEnum.Period:
                            characteristic = this.Period;
                            break;
                        case ServiceCharacteristicsEnum.Address:
                            characteristic = this.Address;
                            break;
                        case ServiceCharacteristicsEnum.Device_Id:
                            characteristic = this.Device_Id;
                            break;
                    }
                    if (characteristic != null)
                    {
                        if (characteristic.CharacteristicProperties.HasFlag(flag))
                        {
                            GattReadResult result = null;
                            try
                            {
                                result = await characteristic.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);

                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error-1: ReadSensorBase(): " + SensorIndex.ToString() + " " + ex.Message);
                                result = await characteristic.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Cached);
                            }
                            if (result != null)
                            {
                                var status = result.Status;
                                if (status == GattCommunicationStatus.Success)
                                {
                                    ret = true;
                                    var dat = result.Value;
                                    var xx = dat.GetType();
                                    var yy = dat.Capacity;
                                    var zz = dat.Length;

                                    bytes = new byte[result.Value.Length];

                                    Windows.Storage.Streams.DataReader.FromBuffer(result.Value).ReadBytes(bytes);
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: ReadSensorBase(): " + SensorIndex.ToString() + " " + ex.Message);
            }
            Debug.WriteLine("End ReadSensorBase: " + SensorIndex.ToString());
            if (!ret)
                bytes = null;
            return bytes;
        }

        public enum IOActions { On, AllOff, Enable, Disable };

        public async static Task GlobalActionIO(IOActions action, int target)
        {
            Debug.WriteLine("Begin GlobalActionIO: ");
            try
            {
                if (SensorsCharacteristicsList != null)
                    if (SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR] != null)
                        if (SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR].Configuration != null)
                            if (SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR].Data != null)
                                await SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR].ActionIO(action, target);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: GlobalActionIO() - " + ex.Message);
            }
            Debug.WriteLine("End GlobalActionIO: ");
        }

        private bool IO_IsOn { get; set; } = false;
        private async Task ActionIO(IOActions action, int target)
        {
            Debug.WriteLine("Begin ActionIO: ");
            //Set in Remote mode
            byte[] bytes = new byte[] { 0x00 };//off
            bool res = true;
            try
            {
                if (action == IOActions.Enable)
                {
                    if (!IO_IsOn)
                    {
                        //Turn IO ON
                        bytes[0] = 0;
                        res = await this.WriteSensor(bytes, ServiceCharacteristicsEnum.Data);
                        if (res)
                        {
                            bytes[0] = 1;//on
                            res = await this.WriteSensor(bytes, ServiceCharacteristicsEnum.Configuration);
                            if (res)
                            {
                                IO_IsOn = true;
                                Debug.WriteLine("Sensor IO enabled.");
                            }
                        }
                        if (!res)
                            Debug.WriteLine("Sensor IO enable failed.");
                    }
                }
                else if ((action == IOActions.On) || (action == IOActions.AllOff))
                {
                    if (IO_IsOn)
                    {
                        if (action == IOActions.AllOff)
                            target = 0;
                        if (!(new List<int> { 0, 1, 2, 4, 3, 5, 6, 7 }.Contains(target)))
                            return;
                        //Turn on target/s (Could toggle)
                        bytes[0] = (byte)target;
                        res = await this.WriteSensor(bytes, ServiceCharacteristicsEnum.Data);
                        if (res)
                        {
                            switch (target)
                            {
                                case 0:
                                    Debug.WriteLine("LEDS/BUZZ OFF");
                                    break;
                                case 1:
                                    Debug.WriteLine("LED1 ON");
                                    break;
                                case 2:
                                    Debug.WriteLine("LED21 ON");
                                    break;
                                case 4:
                                    Debug.WriteLine("BUZZ ON");
                                    break;
                                case 3:
                                    Debug.WriteLine("LEDs 1&2 ON");
                                    break;
                                case 5:
                                    Debug.WriteLine("BUZZ & LED1 ON");
                                    break;
                                case 6:
                                    Debug.WriteLine("BUZZ & LED2 ON");
                                    break;
                                case 7:
                                    Debug.WriteLine("LEDs 1&2 + BUZZ ON");
                                    break;
                            }
                        }
                        else
                            Debug.WriteLine("IO failed for target {0}", target);
                    }
                }
                else
                {
                    //Disable
                    if (IO_IsOn)
                    {
                        bytes[0] = 0;
                        res = await this.WriteSensor(bytes, ServiceCharacteristicsEnum.Data);
                        if (res)
                        {
                            bytes[0] = 0;//off
                            res = await this.WriteSensor(bytes, ServiceCharacteristicsEnum.Configuration);

                        }
                        if (res)
                        {
                            IO_IsOn = false;
                            Debug.WriteLine("Sensor IO disabled");
                        }
                        else
                            Debug.WriteLine("Sensor IO disable failed");
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: ActionIO() - " + ex.Message);
            }
            Debug.WriteLine("End ActionIO: ");
        }


    }
}
