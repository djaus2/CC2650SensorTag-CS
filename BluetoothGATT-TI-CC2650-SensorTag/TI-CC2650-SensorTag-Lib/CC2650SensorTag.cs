using Windows.System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel.DataAnnotations;
using System;

namespace TICC2650SensorTag
{
    public sealed partial class CC2650SensorTag : ICC2650SensorTag
    {

        //Instance Properties
        public GattDeviceService GattService { get; set; } = null;
        public GattCharacteristic Data { get; set; } = null;
        public GattCharacteristic Notification { get; set; } = null;
        public GattCharacteristic Configuration { get; set; } = null;
        public GattCharacteristic Period { get; set; } = null;

        public GattCharacteristic Address { get; set; } = null;
        public GattCharacteristic Device_Id { get; set; } = null;

        public SensorIndexes SensorIndex { get; set; }


        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gattService">Gatt Service found for this sensor</param>
        /// <param name="sensorIndex">SensorIndex</param>
        public CC2650SensorTag(GattDeviceService gattService, SensorIndexes sensorIndex, SensorDataDelegate callMeBack)
        {
            Debug.WriteLine("Begin sensor constructor: " + sensorIndex.ToString());
            try
            {
                GattService = gattService;
                HasSetCallBacks = false;
                SensorIndex = sensorIndex;
                Guid guidNull = Guid.Empty;
                Guid guidData = guidNull;
                Guid guidNotification = guidNull;
                Guid guidConfiguraton = guidNull;
                Guid guidPeriod = guidNull;
                Guid guidAddress = guidNull;
                Guid guidDevId = guidNull;

                CallMeBack = callMeBack;

                IO_IsOn = false;

                switch (SensorIndex)
                {
                    case SensorIndexes.KEYS:
                        guidNotification = BUTTONS_NOTIFICATION_GUID;
                        break;
                    case SensorIndexes.IO_SENSOR:
                        guidData = IO_SENSOR_DATA_GUID;
                        guidConfiguraton = IO_SENSOR_CONFIGURATION_GUID;
                        break;
                    case SensorIndexes.REGISTERS:
                        guidData = REGISTERS_DATA_GUID;
                        guidAddress = REGISTERS_ADDRESS_GUID;
                        guidDevId = REGISTERS_DEVICE_ID_GUID;
                        break;
                    default:
                        guidData = new Guid(UUIDBase[(int)SensorIndex] + SENSOR_GUID_SUFFFIX);
                        guidNotification = new Guid(UUIDBase[(int)SensorIndex] + SENSOR_NOTIFICATION_GUID_SUFFFIX);
                        guidConfiguraton = new Guid(UUIDBase[(int)SensorIndex] + SENSOR_ENABLE_GUID_SUFFFIX);
                        guidPeriod = new Guid(UUIDBase[(int)SensorIndex] + SENSOR_PERIOD_GUID_SUFFFIX);
                        break;

                }

                IReadOnlyList<GattCharacteristic> characteristicList_Data = null;
                IReadOnlyList<GattCharacteristic> characteristicList_Notification = null;
                IReadOnlyList<GattCharacteristic> characteristicList_Configuration = null;
                IReadOnlyList<GattCharacteristic> characteristicList_Period = null;

                IReadOnlyList<GattCharacteristic> characteristicList_Address = null;
                IReadOnlyList<GattCharacteristic> characteristicList_Device_Id = null;

                if (guidData != guidNull)
                    characteristicList_Data = gattService.GetCharacteristics(guidData);
                if (guidNotification != guidNull)
                    characteristicList_Notification = gattService.GetCharacteristics(guidNotification);
                if (guidConfiguraton != guidNull)
                    characteristicList_Configuration = gattService.GetCharacteristics(guidConfiguraton);
                if (guidPeriod != guidNull)
                    characteristicList_Period = gattService.GetCharacteristics(guidPeriod);

                if (guidAddress != guidNull)
                    characteristicList_Address = gattService.GetCharacteristics(guidAddress);
                if (guidDevId != guidNull)
                    characteristicList_Device_Id = gattService.GetCharacteristics(guidDevId);

                if (characteristicList_Data != null)
                    if (characteristicList_Data.Count > 0)
                        Data = characteristicList_Data[0];
                if (characteristicList_Notification != null)
                    if (characteristicList_Notification.Count > 0)
                        Notification = characteristicList_Notification[0];
                if (characteristicList_Configuration != null)
                    if (characteristicList_Configuration.Count > 0)
                        Configuration = characteristicList_Configuration[0];
                if (characteristicList_Period != null)
                    if (characteristicList_Period.Count > 0)
                        Data = characteristicList_Period[0];

                if (characteristicList_Address != null)
                    if (characteristicList_Address.Count > 0)
                        Address = characteristicList_Address[0];
                if (characteristicList_Device_Id != null)
                    if (characteristicList_Device_Id.Count > 0)
                        Device_Id = characteristicList_Device_Id[0];

                SensorsCharacteristicsList[(int)sensorIndex] = this;

                if (SensorIndex >= 0 && SensorIndex != SensorIndexes.IO_SENSOR && SensorIndex != SensorIndexes.REGISTERS)
                {
                    ActiveCharacteristicNotifications[(int)SensorIndex] = Notification;
                    Task.Run(() => this.EnableNotify()).Wait(); //Could leave out Wait but potentially could action this instance too soon
                    Task.Run(() => this.TurnOnSensor()).Wait(); //This launches a new thread for this action but stalls the constructor thread.
                }
            } catch (Exception ex)
            {
                Debug.WriteLine("Error: CC2650SensorTag() Constructor: " + SensorIndex.ToString() + " " + ex.Message);
            }

            Debug.WriteLine("End sensor constructor: " + SensorIndex.ToString());
        }

        public async Task TurnOnSensor()
        {
            Debug.WriteLine("Begin turn on sensor: " + SensorIndex.ToString());
            // Turn on sensor
            try
            {
                if (SensorIndex >= 0 && SensorIndex != SensorIndexes.KEYS && SensorIndex != SensorIndexes.IO_SENSOR && SensorIndex != SensorIndexes.REGISTERS)
                {
                    if (Configuration != null)
                        if (Configuration.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
                        {
                            var writer = new Windows.Storage.Streams.DataWriter();
                            if (SensorIndex == SensorIndexes.MOVEMENT)
                            {
                                byte[] bytes = new byte[] { 0x7f, 0x00 };
                                writer.WriteBytes(bytes);
                            }
                            else
                                writer.WriteByte((Byte)0x01);

                            var status = await Configuration.WriteValueAsync(writer.DetachBuffer());
                        }
                }
            } catch (Exception ex)
            {
                Debug.WriteLine("Error: TurnOnSensor() : " + SensorIndex.ToString() +" " + ex.Message);
            }
            Debug.WriteLine("End turn on sensor: " + SensorIndex.ToString());
        }

        public async Task TurnOffSensor()
        {
            try { 
            Debug.WriteLine("Begin turn off sensor: " + SensorIndex.ToString());
            // Turn on sensor
            if (SensorIndex >= 0 && SensorIndex != SensorIndexes.KEYS && SensorIndex != SensorIndexes.IO_SENSOR && SensorIndex != SensorIndexes.REGISTERS)
            {
                if (Configuration != null)
                    if (Configuration.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
                    {
                        var writer = new Windows.Storage.Streams.DataWriter();
                        if (SensorIndex == SensorIndexes.MOVEMENT)
                        {
                            byte[] bytes = new byte[] { 0x00, 0x00 };//Fixed
                            writer.WriteBytes(bytes);
                        }
                        else

                            writer.WriteByte((Byte)0x00);

                        var status = await Configuration.WriteValueAsync(writer.DetachBuffer());
                    }
            }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: TurnOffSensor(): " + SensorIndex.ToString() + " " + ex.Message);
            }
            Debug.WriteLine("End turn off sensor: " + SensorIndex.ToString());
        }

        private bool HasSetCallBacks = false;

        public async Task EnableNotify()
        {

            Debug.WriteLine("Begin EnableNotify sensor: " + SensorIndex.ToString());
            try
            {
                if (Notification != null)
                {
                    if (!HasSetCallBacks)
                    {
                        switch (SensorIndex)
                        {
                            case SensorIndexes.KEYS:
                                Notification.ValueChanged += keyChanged;
                                break;
                            case SensorIndexes.IR_SENSOR:
                                Notification.ValueChanged += tempChanged;
                                break;
                            case SensorIndexes.HUMIDITY:
                                Notification.ValueChanged += humidChanged;
                                break;
                            case SensorIndexes.OPTICAL:
                                Notification.ValueChanged += opticalChanged;
                                break;
                            case SensorIndexes.MOVEMENT:
                                Notification.ValueChanged += movementChanged;
                                break;
                            case SensorIndexes.BAROMETRIC_PRESSURE:
                                Notification.ValueChanged += pressureCC2650Changed;
                                break;
                            case SensorIndexes.IO_SENSOR:
                                break;
                            case SensorIndexes.REGISTERS:
                                break;
                            default:
                                break;
                        }
                        HasSetCallBacks = true;
                    }
                    if (Notification != null)
                        if (Notification.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                        {
                            Debug.WriteLine("Awaiting EnableNotify sensor: " + SensorIndex.ToString());
                            await Notification.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                            Debug.WriteLine("Awaited EnableNotify sensor: " + SensorIndex.ToString());
                        }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: EnableNotify(): " + SensorIndex.ToString() + " " + ex.Message);
            }
             Debug.WriteLine("(End EnableNotify sensor: " + SensorIndex.ToString());
        }

        public async Task DisableNotify()
        {
            Debug.WriteLine("Begin DisableNotify sensor: " + SensorIndex.ToString());
            try
            {
                if (Notification != null)
                    if (Notification.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                        await Notification.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
            } 
            catch (Exception ex)
            {
                Debug.WriteLine("Error: DisableNotify(): " + SensorIndex.ToString() + " " + ex.Message);
            }
            Debug.WriteLine("End DisableNotify sensor: " + SensorIndex.ToString());
        }
   }
}
