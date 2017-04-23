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
        public static List<string> DeviceAltSensorNames { get; internal set; } = new List<string> { "CC2650 SensorTag", "SensorTag 2.0" };

        private static int SetUpRunTimes = 0;
        //Class specific enums

        /// <summary>
        /// When manually reading data it is suggested to disable sensor before and after, to save power.
        /// Have found that only disabling Notifications is needed.
        /// Have also found that manual reads are unreliable when you do a Disable/Enable sensor around a manualk data read
        /// </summary>
        public static bool DisableSensorWithDisableNotifications { get; private set; } = false;

        // Take device.Id: \\?\BTHLEDevice#{0000180a-0000-1000-8000-00805f9b34fb}_Dev_VID&01000d_PID&0000_REV&0110_24718908ed82#6&379cf99a&6&0009#{6e3bb679-4372-40c8-9eaa-4509df260cd8}
        // Split into array using # and _ as delimeters. 
        // The array should eb 12 elements long.
        // The [6]th element should be the tag's BT Address.
        public const int DEVICE_ID_AS_ARRAY_LENGTH = 9;
        public const int DEVICE_ID_AS_ARRAY_BTADDRESS_INDEX = 6;

        public enum ServiceCharacteristicsEnum
        {
            Data = 1, Notification = 2, Configuration = 3, Period = 4, Address = 5, Device_Id = 6
        }

        /// <summary>
        internal static CC2650SensorTag IR_SensorCharacteristics = null; // new GATTClassCharacteristics();
        internal static CC2650SensorTag HumidityrCharacteristics = null; //new GATTClassCharacteristics();
        internal static CC2650SensorTag BarometricPressureCharacteristics = null; //new GATTClassCharacteristics();
        internal static CC2650SensorTag KeysCharacteristics = null; //new GATTClassCharacteristics();
        internal static CC2650SensorTag OpticalCharacteristics = null; // new GATTClassCharacteristics();
        internal static CC2650SensorTag MovementCharacteristics = null; //new GATTClassCharacteristics();
        internal static CC2650SensorTag IO_SensorCharacteristics = null; //new GATTClassCharacteristics();
        internal static CC2650SensorTag RegistersCharacteristics = null; //new GATTClassCharacteristics();

        private  static CC2650SensorTag[] _SensorsCharacteristicsList = new CC2650SensorTag[NUM_SENSORS];
        public static CC2650SensorTag[] SensorsCharacteristicsList
        {
            get {
                return _SensorsCharacteristicsList; 
            }
            set { _SensorsCharacteristicsList = value; }
        }


        public static GattDeviceService[] ServiceList = new GattDeviceService[NUM_SENSORS];
        public static GattCharacteristic[] ActiveCharacteristicNotifications = new GattCharacteristic[NUM_SENSORS];


        internal const string SENSOR_GUID_PREFIX = "F000AA";
        //The following 4 are prefixed by UUIDBase[i], which is SENSOR_GUID_PREFIX plus a digit, depending upon the sensor.
        internal const string SENSOR_GUID_SUFFFIX = "0-0451-4000-B000-000000000000";
        internal const string SENSOR_NOTIFICATION_GUID_SUFFFIX = "1-0451-4000-B000-000000000000";
        internal const string SENSOR_ENABLE_GUID_SUFFFIX = "2-0451-4000-B000-000000000000";
        internal const string SENSOR_PERIOD_GUID_SUFFFIX = "3-0451-4000-B000-000000000000";

        internal const string valueServiceUuid = "F000AA71-0451-4000-B000-000000000000";

        internal const string BUTTONS_GUID_STR = "0000FFE0-0000-1000-8000-00805F9B34FB";
        internal static readonly Guid BUTTONS_GUID = new Guid(BUTTONS_GUID_STR);
        internal static readonly Guid BUTTONS_NOTIFICATION_GUID = new Guid("0000FFE1-0000-1000-8000-00805F9B34FB");

        //internal static readonly Guid BAROMETER_CONFIGURATION_GUID = new Guid("F000AA42-0451-4000-B000-000000000000");
        //internal static readonly Guid BAROMETER_CALIBRATION_GUID = new Guid("F000AA43-0451-4000-B000-000000000000");

        internal const string IO_SENSOR_GUID_STR = "F000AA64-0451-4000-B000-000000000000";
        internal static readonly Guid IO_SENSOR_GUID = new Guid(IO_SENSOR_GUID_STR);
        internal static readonly Guid IO_SENSOR_DATA_GUID = new Guid("F000AA65-0451-4000-B000-000000000000");
        internal static readonly Guid IO_SENSOR_CONFIGURATION_GUID = new Guid("F000AA66-0451-4000-B000-000000000000");



        internal const string REGISTERS_GUID_STR = "F000AC00-0451-4000-B000-000000000000";
        internal static readonly Guid REGISTERS_GUID = new Guid(REGISTERS_GUID_STR);
        internal static readonly Guid REGISTERS_DATA_GUID = new Guid("F000AC01-0451-4000-B000-000000000000");
        internal static readonly Guid REGISTERS_ADDRESS_GUID = new Guid("F000AC02-0451-4000-B000-000000000000");
        internal static readonly Guid REGISTERS_DEVICE_ID_GUID = new Guid("F000AC03-0451-4000-B000-000000000000");



        //////Constants for the Sensor device
        /////// <summary>
        /////// The relative "address" of the characteristic in the service.
        /////// There is an overlap.
        /////// </summary>
        /////// <param name="characteristic">A Service Characteristic</param>
        /////// <returns>The relative "address"</returns>
        ////public int ReAddr(ServiceCharacteristicsEnum characteristic)
        ////{
        ////    int ret = (int)characteristic;
        ////    switch (characteristic)
        ////    {
        ////        case ServiceCharacteristicsEnum.Address:
        ////            ret = 2;
        ////            break;
        ////        case ServiceCharacteristicsEnum.Device_Id:
        ////            ret = 3;
        ////            break;
        ////    }
        ////    return ret;
        ////}

        /// //////////////////////////
        ////internal const int SENSOR_MAX = (int)SensorIndexes.REGISTERS;
        ////public const int NUM_SENSORS = SENSOR_MAX + 1;
        ////internal const int NUM_SENSORS_TO_TEST = NUM_SENSORS;
        ////internal const int FIRST_SENSOR = 0;

        public static bool Use_DEVICE_BATTERY_SERVICE { get; set; } = false;
        public static bool Use_UUID_PROPERTIES_SERVICE { get; set; } = false;

        internal const int SENSOR_MAX = (int)SensorIndexes.REGISTERS;
        public static int NUM_SENSORS { get; set; } = SENSOR_MAX;
        public const int NUM_SENSORS_ALL = SENSOR_MAX + 1;
        public static int NUM_SENSORS_TO_TEST { get; set; } = NUM_SENSORS;
        public static int FIRST_SENSOR { get; set; } = (int)SensorIndexes.IR_SENSOR;



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
        /// The number of bytes in for each sensor's Data characteristic that are used
        /// </summary>
        internal static readonly List<int> DataLength = new List<int>(){
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

        internal static readonly List<int> DataLengthUsed = new List<int>(){
            4,
            2,
            6,
            1,
            1,
            2,
            18,
            -1, //Can be 1 to 4 for Registers
            1,
        };

        internal static int BATT_INDX = 8; //Num Bytes for Battery Level is 1


        /// <summary>
        /// The prefix for sensor Guids. Keys, IO_SENSOR and REGISTERS excluded as these are specifically defined.
        /// </summary>
        internal static readonly List<string> UUIDBase = new List<string>(){
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

        public static void SetUp_SensorsLists()
        {
            Debug.WriteLine("Begin SetUp_SensorsLists() ");
            try
            {
                DisableSensorWithDisableNotifications = false;
                if (System.Threading.Interlocked.Increment(ref SetUpRunTimes) == 1)
                {
                    SensorsCharacteristicsList = new CC2650SensorTag[NUM_SENSORS_ALL];
                    for (int i = 0; i < SensorsCharacteristicsList.Length; i++)
                    {
                        SensorsCharacteristicsList[i] = null;
                    }
                    ServiceList = new GattDeviceService[NUM_SENSORS_ALL];

                    for (int i = 0; i < ServiceList.Length; i++)
                    {
                        ServiceList[i] = null;
                    }
                    ActiveCharacteristicNotifications = new GattCharacteristic[NUM_SENSORS_ALL];
                    for (int i = 0; i < ActiveCharacteristicNotifications.Length; i++)
                    {
                        ActiveCharacteristicNotifications[i] = null;
                    }

                }
                IncProg();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: SetUp() - " + ex.Message);
            }
            Debug.WriteLine("End SetUp_SensorsLists() ");
        }

    }
}
