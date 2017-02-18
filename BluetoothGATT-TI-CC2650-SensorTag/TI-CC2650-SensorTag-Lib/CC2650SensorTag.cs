using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel.DataAnnotations;

namespace TICC2650SensorTag
{
    public sealed partial class CC2650SensorTag
    {
        public static List<string> DeviceAltSensorNames { get; internal set; } = new List<string> { "CC2650 SensorTag" , "SensorTag 2.0" };

        private static int SetUpRunTimes = 0;
        //Class specific enums


        public enum ServiceCharacteristicsEnum
        {
            Data = 1, Notification = 2, Configuration = 3, Period = 4, Address = 5, Device_Id = 6
        }

        /// <summary>
        public static CC2650SensorTag IR_SensorCharacteristics = null; // new GATTClassCharacteristics();
        public static CC2650SensorTag HumidityrCharacteristics = null; //new GATTClassCharacteristics();
        public static CC2650SensorTag BarometricPressureCharacteristics = null; //new GATTClassCharacteristics();
        public static CC2650SensorTag KeysCharacteristics = null; //new GATTClassCharacteristics();
        public static CC2650SensorTag OpticalCharacteristics = null; // new GATTClassCharacteristics();
        public static CC2650SensorTag MovementCharacteristics = null; //new GATTClassCharacteristics();
        public static CC2650SensorTag IO_SensorCharacteristics = null; //new GATTClassCharacteristics();
        public static CC2650SensorTag RegistersCharacteristics = null; //new GATTClassCharacteristics();

        public static CC2650SensorTag[] SensorsCharacteristicsList = new CC2650SensorTag[NUM_SENSORS];

        public static GattDeviceService[] ServiceList = new GattDeviceService[NUM_SENSORS];
        public static GattCharacteristic[] ActiveCharacteristicNotifications = new GattCharacteristic[NUM_SENSORS];


        public const string SENSOR_GUID_PREFIX = "F000AA";
        //The following 4 are prefixed by UUIDBase[i], which is SENSOR_GUID_PREFIX plus a digit, depending upon the sensor.
        public const string SENSOR_GUID_SUFFFIX = "0-0451-4000-B000-000000000000";
        public const string SENSOR_NOTIFICATION_GUID_SUFFFIX = "1-0451-4000-B000-000000000000";
        public const string SENSOR_ENABLE_GUID_SUFFFIX = "2-0451-4000-B000-000000000000";
        public const string SENSOR_PERIOD_GUID_SUFFFIX = "3-0451-4000-B000-000000000000";

        public const string valueServiceUuid = "F000AA71-0451-4000-B000-000000000000";

        public const string BUTTONS_GUID_STR = "0000FFE0-0000-1000-8000-00805F9B34FB";
        public static readonly Guid BUTTONS_GUID = new Guid(BUTTONS_GUID_STR);
        public static readonly Guid BUTTONS_NOTIFICATION_GUID = new Guid("0000FFE1-0000-1000-8000-00805F9B34FB");

        //public static readonly Guid BAROMETER_CONFIGURATION_GUID = new Guid("F000AA42-0451-4000-B000-000000000000");
        //public static readonly Guid BAROMETER_CALIBRATION_GUID = new Guid("F000AA43-0451-4000-B000-000000000000");

        public const string IO_SENSOR_GUID_STR = "F000AA64-0451-4000-B000-000000000000";
        public static readonly Guid IO_SENSOR_GUID = new Guid(IO_SENSOR_GUID_STR);
        public static readonly Guid IO_SENSOR_DATA_GUID = new Guid("F000AA65-0451-4000-B000-000000000000");
        public static readonly Guid IO_SENSOR_CONFIGURATION_GUID = new Guid("F000AA66-0451-4000-B000-000000000000");



        public const string REGISTERS_GUID_STR = "F000AC00-0451-4000-B000-000000000000";
        public static readonly Guid REGISTERS_GUID = new Guid(REGISTERS_GUID_STR);
        public static readonly Guid REGISTERS_DATA_GUID = new Guid("F000AC01-0451-4000-B000-000000000000");
        public static readonly Guid REGISTERS_ADDRESS_GUID = new Guid("F000AC02-0451-4000-B000-000000000000");
        public static readonly Guid REGISTERS_DEVICE_ID_GUID = new Guid("F000AC03-0451-4000-B000-000000000000");



        //Constants for the Sensor device
        /// <summary>
        /// The relative "address" of the characteristic in the service.
        /// There is an overlap.
        /// </summary>
        /// <param name="characteristic">A Service Characteristic</param>
        /// <returns>The relative "address"</returns>
        public int ReAddr(ServiceCharacteristicsEnum characteristic)
        {
            int ret = (int)characteristic;
            switch (characteristic)
            {
                case ServiceCharacteristicsEnum.Address:
                    ret = 2;
                    break;
                case ServiceCharacteristicsEnum.Device_Id:
                    ret = 3;
                    break;
            }
            return ret;
        }

        /// //////////////////////////
        public const int SENSOR_MAX = (int)SensorIndexes.REGISTERS;
        public const int NUM_SENSORS = SENSOR_MAX + 1;
        public const int NUM_SENSORS_TO_TEST =  NUM_SENSORS;
        public const int FIRST_SENSOR = 0;

        /// <summary>
        /// List of sensors
        /// </summary>
        public enum SensorIndexes
        {
            IR_SENSOR,
            HUMIDITY,
            BAROMETRIC_PRESSURE,
            IO_SENSOR,
            KEYS,
            OPTICAL,
            MOVEMENT,
            REGISTERS,
            NOTFOUND
        }

        /// <summary>
        /// The number of bytes in for each sensor's Data characteristic
        /// </summary>
        public static readonly List<int> DataLength = new List<int>(){
            4,
            4,
            6,
            1,
            1,
            2,
            18,
            -1, //Can be 1 to 4 for Registers
            1,
        };

        public static int BATT_INDX = 8; //Num Bytes for Battery Level is 1


        /// <summary>
        /// The prefix for sensor Guids. Keys, IO_SENSOR and REGISTERS excluded as these are specifically defined.
        /// </summary>
        public static readonly List<string> UUIDBase = new List<string>(){
            "F000AA0",
            "F000AA2",
            "F000AA4",
            "",
            "",
            "F000AA7",
            "F000AA8",
            ""
        };

        public static SensorIndexes GetSensorIndex(int Index)
        {
            SensorIndexes senIndx = SensorIndexes.NOTFOUND;
            for (int i = 0; i < UUIDBase.Count(); i++)
            {
                if (UUIDBase[i] != "")
                {
                    char ch = UUIDBase[i][6];
                    int indx = ch - '0';
                    if (indx == Index)
                    {
                        senIndx = (SensorIndexes)i;
                        break;
                    }
                }
            }
            return senIndx;
        }


        //Class global properties

        /// <summary>
        /// If Values then values are determined and displayed
        /// Otherwise the raw bytes are displayed for each sensor
        /// </summary>
        public static GattDataModes GattMode { get; set; } = GattDataModes.Values;


        //Instance Properties
        public GattDeviceService GattService { get; set; } = null;
        public GattCharacteristic Data { get; set; } = null;
        public GattCharacteristic Notification { get; set; } = null;
        public GattCharacteristic Configuration { get; set; } = null;
        public GattCharacteristic Period { get; set; } = null;

        public GattCharacteristic Address { get; set; } = null;
        public GattCharacteristic Device_Id { get; set; } = null;

        public SensorIndexes SensorIndex { get; set; }


        public static void SetUp()
        {
            if (System.Threading.Interlocked.Increment(ref SetUpRunTimes) == 1)
            {
                SensorsCharacteristicsList = new CC2650SensorTag[NUM_SENSORS];
                for (int i = 0; i < SensorsCharacteristicsList.Length; i++)
                {
                    SensorsCharacteristicsList[i] = null;
                }
                ServiceList = new GattDeviceService[NUM_SENSORS];

                for (int i = 0; i < ServiceList.Length; i++)
                {
                    ServiceList[i] = null;
                }
                ActiveCharacteristicNotifications = new GattCharacteristic[NUM_SENSORS];
                for (int i = 0; i < ActiveCharacteristicNotifications.Length; i++)
                {
                    ActiveCharacteristicNotifications[i] = null;
                }

            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gattService">Gatt Service found for this sensor</param>
        /// <param name="sensorIndex">SensorIndex</param>
        public CC2650SensorTag(GattDeviceService gattService, SensorIndexes sensorIndex, SensorDataDelegate callMeBack)
        {
            Debug.WriteLine("Begin sensor constructor: " + sensorIndex.ToString());

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

            Debug.WriteLine("End sensor constructor: " + SensorIndex.ToString());
        }

        public async Task TurnOnSensor()
        {
            Debug.WriteLine("Begin turn on sensor: " + SensorIndex.ToString());
            // Turn on sensor
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
            Debug.WriteLine("End turn on sensor: " + SensorIndex.ToString());
        }

        public async Task TurnOffSensor()
        {
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
            Debug.WriteLine("End turn off sensor: " + SensorIndex.ToString());
        }

        private bool HasSetCallBacks = false;

        public async Task EnableNotify()
        {

            Debug.WriteLine("Begin EnableNotify sensor: " + SensorIndex.ToString());
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
                        await Notification.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            }
            Debug.WriteLine("End EnableNotify sensor: " + SensorIndex.ToString());
        }

        public async Task DisableNotify()
        {
            Debug.WriteLine("Begin DisableNotify sensor: " + SensorIndex.ToString());
            if (Notification != null)
                if (Notification.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                    await Notification.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
            Debug.WriteLine("End DisableNotify sensor: " + SensorIndex.ToString());
        }

        private async Task<bool> WriteSensor(byte[] bytes, ServiceCharacteristicsEnum character)
        {
            Debug.WriteLine("Begin write sensor: " + SensorIndex.ToString());
            bool ret = false;
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
            Debug.WriteLine("End write sensor: " + SensorIndex.ToString());
            return ret;
        }

        /// <summary>
        /// Manually read sensor values from their Data Characteristic
        /// </summary>
        /// <param name="disableNotify">Notify should be off. Can optionally set this.</param>
        /// <returns>Buffer for data. Is created in the call.</returns>
        public async Task<SensorData> ReadSensor( bool disableNotify, bool updateDisplay)
        {
            byte[] bytes = null;
            SensorData sensorData = null;
            Debug.WriteLine("Begin read sensor: " + SensorIndex.ToString());
            if (SensorIndex >= 0 && SensorIndex != SensorIndexes.IO_SENSOR && SensorIndex != SensorIndexes.REGISTERS)
            {
                if (GattService != null)
                {
                    if (disableNotify)
                        await DisableNotify();
                    //Enable Sensor
                    await TurnOnSensor();
                    bytes = await ReadSensorBase(ServiceCharacteristicsEnum.Notification);//..Data);
                    //Disable Sensor
                    await TurnOffSensor();
                }
            }

            if (bytes != null)
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
            Debug.WriteLine("End read sensor: " + SensorIndex.ToString());
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
            Debug.WriteLine("Begin read sensor base: " + SensorIndex.ToString());
            bool ret = false;
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

                            GattReadResult result = await characteristic.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);

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
            Debug.WriteLine("End read sensor base: " + SensorIndex.ToString());
            if (!ret)
                bytes = null;
            return bytes;
        }

        public enum IOActions { On, AllOff,  Enable, Disable};

        public async static Task GlobalActionIO(IOActions action, int target)
        {
            if (SensorsCharacteristicsList != null)
                if (SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR] != null)
                    if (SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR].Configuration != null)
                        if (SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR].Data != null)
                            await SensorsCharacteristicsList[(int)SensorIndexes.IO_SENSOR].ActionIO(action, target);
        }

        private bool IO_IsOn { get; set;} = false;
        private async  Task ActionIO(IOActions action, int target)
        {
            //Set in Remote mode
            byte[] bytes = new byte[] {  0x00 };//off
            bool res = true;
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
    }
}
