
using Windows.System;
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
using System;

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
        public class SensorData
        {
            public CC2650SensorTag.SensorIndexes Sensor_Index;
            public double[] Values;
            public byte[] Raw;
        }

        public enum GattDataModes
        {
            Values,
            Raw
        }

        public delegate void SensorDataDelegate(SensorData data);

        private bool checkArray(byte[] bArray)
        {
            bool ret = false;
            if (bArray != null)
            {
                if (bArray.Length == DataLength[(int)this.SensorIndex])
                {
                    int count = 0;
                    for (int i = 0; i < bArray.Length; i++)
                    {
                        count += (int)bArray[i];
                    }
                    if (count == 0)
                        Debug.WriteLine("Invalid byte[] recvd: All zeros " + SensorIndex.ToString());
                    else
                    if (this.SensorIndex == SensorIndexes.HUMIDITY)
                    {
                        if (count != 2 * 0xff)
                            ret = true;
                        else
                            Debug.WriteLine("Invalid byte[] recvd: ff ff 00 00 " + SensorIndex.ToString());
                    }
                    else
                        ret = true;
                }
                else
                    Debug.WriteLine("Invalid byte[] recvd: Num bytes " + SensorIndex.ToString());
            }
            else
            {
                Debug.WriteLine("Invalid byte[] recvd: Null " + SensorIndex.ToString());
            }
            return ret;
        }

        public SensorDataDelegate CallMeBack { get; set; } = null;
        // ---------------------------------------------------
        //           GATT Notification Handlers
        // ---------------------------------------------------

        // IR temperature change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#IR_Temperature_Sensor
        private async void tempChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (checkArray(bArray))
                await tempChangedProc(bArray, true);
        }

        private async Task<SensorData> tempChangedProc(byte[] bArray , bool doCallback)
        {
            SensorData values = null;
            if (bArray.Length == DataLength[(int)this.SensorIndex])
            {
                double AmbTemp = (double)(((UInt16)bArray[3] << 8) + (UInt16)bArray[2]);
                AmbTemp /= 128.0;

                Int16 temp = (Int16)(((UInt16)bArray[1] << 8) + (UInt16)bArray[0]);
                double Vobj2 = (double)temp;
                Vobj2 *= 0.00000015625;
                double Tdie = AmbTemp + 273.15;

                const double S0 = 5.593E-14;            // Calibration factor
                const double a1 = 1.75E-3;
                const double a2 = -1.678E-5;
                const double b0 = -2.94E-5;
                const double b1 = -5.7E-7;
                const double b2 = 4.63E-9;
                const double c2 = 13.4;
                const double Tref = 298.15;

                double S = S0 * (1 + a1 * (Tdie - Tref) + a2 * Math.Pow((Tdie - Tref), 2));
                double Vos = b0 + b1 * (Tdie - Tref) + b2 * Math.Pow((Tdie - Tref), 2);
                double fObj = (Vobj2 - Vos) + c2 * Math.Pow((Vobj2 - Vos), 2);
                double tObj = Math.Pow(Math.Pow(Tdie, 4) + (fObj / S), 0.25);

                tObj = (tObj - 273.15);

                values =  new SensorData { Sensor_Index = SensorIndex, Values = new double[] { AmbTemp, tObj }, Raw = bArray };
                if(doCallback)
                    if (CallMeBack!=null)
                        CallMeBack(values);
               
            }
            return values;
        }

        // Humidity change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Humidity_Sensor_2
        private async void humidChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (checkArray(bArray))
                await humidChangedProc(bArray, true);
        }

        private async Task<SensorData> humidChangedProc(byte[] bArray, bool DoCallBack)
        {
            SensorData values = null;
            if (bArray.Length == DataLength[(int)this.SensorIndex])
            {
                double humidity = (double)((((UInt16)bArray[1] << 8) + (UInt16)bArray[0]) & ~0x0003);
                humidity = (-6.0 + 125.0 / 65536 * humidity); // RH= -6 + 125 * SRH/2^16
                values =  new SensorData {Sensor_Index=SensorIndex, Values = new double[] { humidity }, Raw = bArray };
                if (DoCallBack)
                    if (CallMeBack != null)
                        CallMeBack(values);
            }
            return values;
        }


        private async void movementChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (checkArray(bArray))
                await movementChangedProc(bArray, true);
        }

        private async Task<SensorData> movementChangedProc(byte[] bArray, bool doCallback)
        {
            SensorData values = null;
            if (bArray.Length == DataLength[(int)this.SensorIndex])
            {

                Int16 dataGyroX = (Int16)(((UInt16)bArray[1] << 8) + (UInt16)bArray[0]);
                Int16 dataGyroY = (Int16)(((UInt16)bArray[3] << 8) + (UInt16)bArray[2]);
                Int16 dataGyroZ = (Int16)(((UInt16)bArray[5] << 8) + (UInt16)bArray[4]);

                Int16 dataAccX = (Int16)(((UInt16)bArray[7] << 8) + (UInt16)bArray[6]);
                Int16 dataAccY = (Int16)(((UInt16)bArray[9] << 8) + (UInt16)bArray[8]);
                Int16 dataAccZ = (Int16)(((UInt16)bArray[11] << 8) + (UInt16)bArray[10]);


                Int16 dataMagX = (Int16)(256 * ((UInt16)bArray[13]) + (UInt16)bArray[12]);
                Int16 dataMagY = (Int16)(256 * ((UInt16)bArray[15]) + (UInt16)bArray[14]);
                Int16 dataMagZ = (Int16)(256 * ((UInt16)bArray[17]) + (UInt16)bArray[16]);


                values =  new SensorData
                {
                    Sensor_Index = SensorIndex,
                    Values = new double[] {
                        sensorMpu9250GyroConvert(dataGyroX),
                        sensorMpu9250GyroConvert(dataGyroY),
                        sensorMpu9250GyroConvert(dataGyroZ),
                        sensorMpu9250AccConvert(dataAccX),
                        sensorMpu9250AccConvert(dataAccY),
                        sensorMpu9250AccConvert(dataAccZ),
                        sensorMpu9250MagConvert(dataMagX),
                        sensorMpu9250MagConvert(dataMagY),
                        sensorMpu9250MagConvert(dataMagZ)

                    }, 
                    Raw = bArray
                };
                if (doCallback)
                    if (CallMeBack != null)
                        CallMeBack(values);
            }
            return values;

        }

        double sensorMpu9250GyroConvert(Int16 data)
        {
            //-- calculate rotation, unit deg/s, range -250, +250
            return (data * 1.0) / (65536 / 500);
        }


        // Accelerometer ranges
        const int ACC_RANGE_2G = 0;
        const int ACC_RANGE_4G = 1;
        const int ACC_RANGE_8G = 2;
        const int ACC_RANGE_16G = 3;
        int accRange { get; set; } = ACC_RANGE_16G;

        double sensorMpu9250AccConvert(Int16 rawData)
        {
            double v = 0;

            switch (accRange)
            {
                case ACC_RANGE_2G:
                    //-- calculate acceleration, unit G, range -2, +2
                    v = (rawData * 1.0) / (32768 / 2);
                    break;

                case ACC_RANGE_4G:
                    //-- calculate acceleration, unit G, range -4, +4
                    v = (rawData * 1.0) / (32768 / 4);
                    break;

                case ACC_RANGE_8G:
                    //-- calculate acceleration, unit G, range -8, +8
                    v = (rawData * 1.0) / (32768 / 8);
                    break;

                case ACC_RANGE_16G:
                    //-- calculate acceleration, unit G, range -16, +16
                    v = (rawData * 1.0) / (32768 / 16);
                    break;
            }

            return v;
        }

        double sensorMpu9250MagConvert(Int16 data)
        {
            //-- calculate magnetism, unit uT, range +-4900
            return 1.0 * data;
        }


        private async void opticalChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (checkArray(bArray))
                await opticalChangedProc(bArray, true);
        }

        private async Task<SensorData> opticalChangedProc( byte[] bArray, bool doCallback)
        {
            SensorData values = null;
            if (bArray.Length == DataLength[(int)this.SensorIndex])
            {

                double lumo = sensorOpt3001Convert(bArray);
                values =  new SensorData {Sensor_Index = SensorIndex, Values = new double[] { lumo }, Raw = bArray };
                if (doCallback)
                    if (CallMeBack!=null)
                        CallMeBack(values);
            }
            return values;

        }

        double sensorOpt3001Convert(byte[] bArray)
        {
            Int32 rawData = bArray[1] * 256 + bArray[0];
            Int32 e, m;

            m = rawData & 0x0FFF;
            e = (rawData & 0xF000) >> 12;

            return m * (0.01 * Math.Pow(2.0, e));
        }

        private async void pressureCC2650Changed(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {

            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (checkArray(bArray))
                await pressureCC2650ChangedProc(bArray, true);
        }

        private async Task<SensorData> pressureCC2650ChangedProc(byte[] bArray, bool doCallback)
        {
            SensorData values = null;
            if (bArray.Length == DataLength[(int)this.SensorIndex])
            {
                Int32 t = bArray[2] * 256;
                Int32 tempT = bArray[1] + t;
                t = tempT * 256 + bArray[0];
                double tempr = (double)t / 100;


                Int32 p = bArray[5] * 256;
                Int32 tempP = bArray[4] + p;
                p = tempP * 256 + bArray[3];
                double pres = (double)p / 100;

                values =  new SensorData {Sensor_Index=SensorIndex, Values = new double[] { pres, tempr }, Raw = bArray };
                if (doCallback)
                    if (CallMeBack != null)
                        CallMeBack(values);
            }
            return values;
        }




        // Key press change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Simple_Key_Service
        public async void keyChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (checkArray(bArray))
                await keyChangedProc(bArray, true);
        }

        public async Task<SensorData> keyChangedProc(byte[] bArray, bool doCallback)
        {
            SensorData values = null;
            if (bArray.Length == DataLength[(int)this.SensorIndex])
            {
                byte data = bArray[0];

                double left;
                double right;
                double reed;

                if ((data & 0x01) == 0x01)
                    right = 1;
                else
                    right = 0;

                if ((data & 0x02) == 0x02)
                    left = 1;
                else
                    left = 0;

                if ((data & 0x04) == 0x04)
                    reed = 1;
                else
                    reed = 0;

                values =  new SensorData { Sensor_Index = SensorIndex, Values= new double[] { right, left, reed }, Raw = bArray };
                if(doCallback)
                    if (CallMeBack!=null)
                        CallMeBack(values);
            }
            return values;
        }
        public async void setSensorPeriod( int period)
        {

            GattDeviceService gattService = GattService;
            if (SensorIndex != SensorIndexes.KEYS && SensorIndex != SensorIndexes.IO_SENSOR && gattService != null)
            {
                var characteristicList = gattService.GetCharacteristics(new Guid(UUIDBase[(int)SensorIndex] + SENSOR_PERIOD_GUID_SUFFFIX));
                if (characteristicList != null)
                {
                    if (characteristicList.Count > 0)
                    {
                        GattCharacteristic characteristic = characteristicList[0];

                        if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
                        {
                            var writer = new Windows.Storage.Streams.DataWriter();
                            // Accelerometer period = [Input * 10]ms
                            writer.WriteByte((Byte)(period / 10));
                            await characteristic.WriteValueAsync(writer.DetachBuffer());
                        }
                    }
                }
            }
        }
    }
}
