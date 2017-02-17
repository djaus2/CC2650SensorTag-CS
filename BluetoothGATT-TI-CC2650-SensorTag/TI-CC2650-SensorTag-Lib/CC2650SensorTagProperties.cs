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
        //https://www.bluetooth.com/specifications/gatt/characteristics
        public const string BATTERY_UUID = "2A19"; // "AA71"

        public const string DEVICE_BATTERY_SERVICE = "0000180F-0000-1000-8000-00805F9B34FB";
        public const string DEVICE_BATTERY_LEVEL = "00002A19-0000-1000-8000-00805F9B34FB";

        public const string UUID_PROPERTIES_SERVICE = "0000180a-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_SYSID = "00002A23-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_MODEL_NR = "00002A24-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_SERIAL_NR = "00002A25-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_FW_NR = "00002A26-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_HW_NR = "00002A27-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_SW_NR = "00002A28-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_MANUF_NR = "00002A29-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_CERT = "00002A2A-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_PNP_ID = "00002A50-0000-1000-8000-00805f9b34fb";
        public const string UUID_PROPERTY_NAME = "00002A00-0000-1000-8000-00805f9b34fb";
       

        public enum SensorTagProperties {sysid, device_name, model_name, serial_num, firmware_date, hardware_rev, software_rev, manufacturer_id, cert, pnp_id };


    

    public  static GattDeviceService DevicePropertyService = null;
        private static GattDeviceService DeviceBatteryService = null;
        private static GattCharacteristic DeviceBatteryLevelCharacteristic = null;
        public static void SetUpBattery(GattDeviceService service)
        {
            DeviceBatteryService = service;
            var DeviceBatteryLevelCharacteristicList = DeviceBatteryService.GetCharacteristics(new Guid(DEVICE_BATTERY_LEVEL));
            DeviceBatteryLevelCharacteristic = null;
            if (DeviceBatteryLevelCharacteristicList != null)
                if (DeviceBatteryLevelCharacteristicList.Count() > 0)
                    DeviceBatteryLevelCharacteristic = DeviceBatteryLevelCharacteristicList[0];
        }

        public static async Task<byte[]> GetBatteryLevel()
        {
            byte[] bytes = null;
            GattCharacteristicProperties flag = GattCharacteristicProperties.Read;
            if (DeviceBatteryLevelCharacteristic != null)
            {
                if (DeviceBatteryLevelCharacteristic.CharacteristicProperties.HasFlag(flag))
                {
                    try
                    {
                        GattReadResult result = null;
                        try
                        {
                            result = await DeviceBatteryLevelCharacteristic.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
                        }
                        catch (Exception ex)
                        {
                            string msg = ex.Message;
                        }

                        var status = result.Status;
                        if (status == GattCommunicationStatus.Success)
                        {
                            var dat = result.Value;
                            var xx = dat.GetType();
                            var yy = dat.Capacity;
                            var zz = dat.Length;

                            bytes = new byte[result.Value.Length];

                            Windows.Storage.Streams.DataReader.FromBuffer(result.Value).ReadBytes(bytes);

                        }
                    }

                    catch (Exception ex)
                    {
                        string msg = ex.Message;
                    }


                }

            }
            if (bytes!=null)
                if (bytes.Length == CC2650SensorTag.DataLength[CC2650SensorTag.BATT_INDX])
                {
                    Debug.WriteLine("Battery Level: {0}", bytes[0]);
                }
            return bytes;
        }

        public static async Task<byte[]> ReadProperty(SensorTagProperties property, bool showStartEndMsg)
        {
            if (showStartEndMsg)
                Debug.WriteLine("Begin read property: {0} ", property);
            string guidstr = "";
            byte[] bytes = null;
            switch (property)
            {
                case SensorTagProperties.firmware_date:
                    guidstr = UUID_PROPERTY_FW_NR;
                    break;
                case SensorTagProperties.hardware_rev:
                    guidstr = UUID_PROPERTY_HW_NR;
                    break;
                case SensorTagProperties.manufacturer_id:
                    guidstr = UUID_PROPERTY_MANUF_NR;
                    break;
                case SensorTagProperties.model_name:
                    guidstr = UUID_PROPERTY_MODEL_NR;
                    break;
                case SensorTagProperties.pnp_id:
                    guidstr = UUID_PROPERTY_PNP_ID;
                    break;
                case SensorTagProperties.serial_num:
                    guidstr = UUID_PROPERTY_SERIAL_NR;
                    break;
                case SensorTagProperties.software_rev:
                    guidstr = UUID_PROPERTY_SW_NR;
                    break;
                case SensorTagProperties.sysid:
                    guidstr = UUID_PROPERTY_SYSID;
                    break;
                case SensorTagProperties.cert:
                    guidstr = UUID_PROPERTY_CERT;
                    break;
                case SensorTagProperties.device_name:
                    guidstr = UUID_PROPERTY_NAME;
                    break;
            }

            IReadOnlyList<GattCharacteristic> sidCharacteristicList = DevicePropertyService.GetCharacteristics(new Guid(guidstr));
            GattCharacteristicProperties flag = GattCharacteristicProperties.Read;
            if (sidCharacteristicList != null)
                if (sidCharacteristicList.Count != 0)
                {
                    GattCharacteristic characteristic = sidCharacteristicList[0];
                    if (characteristic.CharacteristicProperties.HasFlag(flag))
                    {
                        try
                        {
                            GattReadResult result = null;
                            try
                            {
                                result = await characteristic.ReadValueAsync(Windows.Devices.Bluetooth.BluetoothCacheMode.Uncached);
                            }
                            catch (Exception ex)
                            {
                                string msg = ex.Message;
                            }

                            var status = result.Status;
                            if (status == GattCommunicationStatus.Success)
                            {
                                var dat = result.Value;
                                var xx = dat.GetType();
                                var yy = dat.Capacity;
                                var zz = dat.Length;

                                bytes = new byte[result.Value.Length];

                                Windows.Storage.Streams.DataReader.FromBuffer(result.Value).ReadBytes(bytes);
                                

                            }
                        }

                        catch (Exception ex)
                        {
                            string msg = ex.Message;
                        }


                    }

                    
                }
            if(showStartEndMsg)
                Debug.WriteLine("End read property: {0} ", property);

            return bytes;
        }

        public async static Task GetProperties()
        {
            List<SensorTagProperties> showbytes = new List<SensorTagProperties>() { SensorTagProperties.sysid, SensorTagProperties.cert, SensorTagProperties.pnp_id };
            Array values = Enum.GetValues(typeof(SensorTagProperties));

            foreach (SensorTagProperties val in values)
            {
                byte[] bytes = null;
                bytes = await ReadProperty( val,false);
                if (bytes != null)
                {
                    if (!showbytes.Contains(val))
                    {
                        string res = System.Text.Encoding.UTF8.GetString(bytes);
                        if (res != null)
                            if (res != "")
                            {
                                
                                Debug.WriteLine("{0} [{1}]: {2}", val.ToString(), res.Length, res);
                            }

                    }
                    else
                    {
                        if (bytes != null)
                        {
                            string str = val.ToString() + "[" + bytes.Length.ToString() + "] {";
                            Debug.Write(str);
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                Debug.Write(" " + bytes[i].ToString("X2"));
                            }
                            Debug.WriteLine(" }");
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
        }



    }
}
