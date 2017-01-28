
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

// Disable warning "...execution of the current method continues before the call is completed..."
#pragma warning disable 4014

// Disable warning to "consider using the 'await' operator to await non-blocking API calls"
#pragma warning disable 1998

namespace BluetoothGATT
{
    /// <summary>
    /// Sample app that communicates with Bluetooth device using the GATT profile
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // ---------------------------------------------------
        //           GATT Notification Handlers
        // ---------------------------------------------------

        // IR temperature change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#IR_Temperature_Sensor
        async void tempChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
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
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                AmbTempOut.Text = string.Format("Chip:\t{0:0.0####}", AmbTemp);
                ObjTempOut.Text = string.Format("IR:  \t{0:0.0####}", tObj);
            });
        }

        // Accelerometer change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Accelerometer_2
        async void accelChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            double x = (SByte)bArray[0] / 64.0;
            double y = (SByte)bArray[1] / 64.0;
            double z = (SByte)bArray[2] / 64.0 * -1;

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RecTranslateTransform.X = x * 90;
                RecTranslateTransform.Y = y * -90;

                AccelXOut.Text = "X: " + x.ToString();
                AccelYOut.Text = "Y: " + y.ToString();
                AccelZOut.Text = "Z: " + z.ToString();
            });
        }

        // Humidity change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Humidity_Sensor_2
        async void humidChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            double humidity = (double)((((UInt16)bArray[1] << 8) + (UInt16)bArray[0]) & ~0x0003);
            humidity = (-6.0 + 125.0 / 65536 * humidity); // RH= -6 + 125 * SRH/2^16
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                HumidOut.Text = humidity.ToString();
            });
        }

        // Magnetometer change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Magnetometer
        async void magnoChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            Int16 data = (Int16)(((UInt16)bArray[1] << 8) + (UInt16)bArray[0]);
            double x = (double)data * (2000.0 / 65536);
            data = (Int16)(((UInt16)bArray[3] << 8) + (UInt16)bArray[2]);
            double y = (double)data * (2000.0 / 65536);
            data = (Int16)(((UInt16)bArray[5] << 8) + (UInt16)bArray[4]);
            double z = (double)data * (2000.0 / 65536);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                MagnoXOut.Text = "X: " + x.ToString();
                MagnoYOut.Text = "Y: " + y.ToString();
                MagnoZOut.Text = "Z: " + z.ToString();
            });
        }

        async void movementChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);
            if (bArray.Length == 18)
            {
                Int16 dataGyroX = (Int16)(((UInt16)bArray[16] << 8) + (UInt16)bArray[17]);
                Int16 dataGyroY = (Int16)(((UInt16)bArray[14] << 8) + (UInt16)bArray[15]);
                Int16 dataGyroZ = (Int16)(((UInt16)bArray[12] << 8) + (UInt16)bArray[13]);
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    GyroXOut.Text = "X: " + ((int)sensorMpu9250GyroConvert(dataGyroX)).ToString();
                    GyroYOut.Text = "Y: " + ((int)sensorMpu9250GyroConvert(dataGyroY)).ToString();
                    GyroZOut.Text = "Z: " + ((int)sensorMpu9250GyroConvert(dataGyroZ)).ToString();
                });

                Int16 dataAccX = (Int16)(((UInt16)bArray[10] << 8) + (UInt16)bArray[11]);
                Int16 dataAccY = (Int16)(((UInt16)bArray[8] << 8) + (UInt16)bArray[9]);
                Int16 dataAccZ = (Int16)(((UInt16)bArray[6] << 8) + (UInt16)bArray[7]);

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //RecTranslateTransform.X = x * 90;
                    //RecTranslateTransform.Y = y * -90;

                    AccelXOut.Text = "X: " + (dataAccX).ToString();
                    AccelYOut.Text = "Y: " + (dataAccY).ToString();
                    AccelZOut.Text = "Z: " + (dataAccZ).ToString();
                });



                Int16 dataMagX = (Int16)(((UInt16)bArray[4] << 8) + (UInt16)bArray[5]);
                Int16 dataMagY = (Int16)(((UInt16)bArray[2] << 8) + (UInt16)bArray[3]);
                Int16 dataMagZ = (Int16)(((UInt16)bArray[0] << 8) + (UInt16)bArray[1]);
                //double x = (double)data * (2000.0 / 65536);
                //data = (Int16)(((UInt16)bArray[3] << 8) + (UInt16)bArray[2]);
                //double y = (double)data * (2000.0 / 65536);
                //data = (Int16)(((UInt16)bArray[5] << 8) + (UInt16)bArray[4]);
                //double z = (double)data * (2000.0 / 65536);

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    MagnoXOut.Text = "X: " + ((int)sensorMpu9250MagConvert(dataMagX)).ToString();
                    MagnoYOut.Text = "Y: " + ((int)sensorMpu9250MagConvert(dataMagY)).ToString();
                    MagnoZOut.Text = "Z: " + ((int)sensorMpu9250MagConvert(dataMagZ)).ToString();
                });
            }
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
        int accRange { get; set; } = ACC_RANGE_2G;

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


        async void opticalChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            if (bArray.Length == 2)
            {


                double lumo = sensorOpt3001Convert(bArray);
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    LuxOut.Text = lumo.ToString();
                });
            }


        }

        double sensorOpt3001Convert(byte[] bArray)
        {
            Int32 rawData = bArray[1] * 256 + bArray[0];
            Int32 e, m;

            m = rawData & 0x0FFF;
            e = (rawData & 0xF000) >> 12;

            return m * (0.01 * Math.Pow(2.0, e));
        }

        async void pressureCC2650Changed(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {

            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            if (bArray.Length == 6)
            {
                Int32 t = bArray[2] * 256;
                Int32 tempT = bArray[1] + t;
                t = tempT * 256 + bArray[0];
                double tempr = (double)t / 100;


                Int32 p = bArray[5] * 256;
                Int32 tempP = bArray[4] + p;
                p = tempP * 256 + bArray[3];
                double pres = (double)p / 100;


                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    BaroOutTemp.Text = tempr.ToString();
                    BaroOut.Text = pres.ToString();
                });
            }

        }


        // Barometric Pressure change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Barometric_Pressure_Sensor_2
        async void pressureChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            if (baroCalibrationData != null)
            {
                UInt16 c3 = (UInt16)(((UInt16)baroCalibrationData[5] << 8) + (UInt16)baroCalibrationData[4]);
                UInt16 c4 = (UInt16)(((UInt16)baroCalibrationData[7] << 8) + (UInt16)baroCalibrationData[6]);
                Int16 c5 = (Int16)(((UInt16)baroCalibrationData[9] << 8) + (UInt16)baroCalibrationData[8]);
                Int16 c6 = (Int16)(((UInt16)baroCalibrationData[11] << 8) + (UInt16)baroCalibrationData[10]);
                Int16 c7 = (Int16)(((UInt16)baroCalibrationData[13] << 8) + (UInt16)baroCalibrationData[12]);
                Int16 c8 = (Int16)(((UInt16)baroCalibrationData[15] << 8) + (UInt16)baroCalibrationData[14]);

                byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
                DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

                Int64 s, o, p, val;
                UInt16 Pr = (UInt16)(((UInt16)bArray[3] << 8) + (UInt16)bArray[2]);
                Int16 Tr = (Int16)(((UInt16)bArray[1] << 8) + (UInt16)bArray[0]);

                // Sensitivity
                s = (Int64)c3;
                val = (Int64)c4 * Tr;
                s += (val >> 17);
                val = (Int64)c5 * Tr * Tr;
                s += (val >> 34);

                // Offset
                o = (Int64)c6 << 14;
                val = (Int64)c7 * Tr;
                o += (val >> 3);
                val = (Int64)c8 * Tr * Tr;
                o += (val >> 19);

                // Pressure (Pa)
                p = ((Int64)(s * Pr) + o) >> 14;
                double pres = (double)p / 100;

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    BaroOut.Text = pres.ToString();
                });
            }
        }

        // Gyroscope change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Gyroscope_2
        async void gyroChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            Int16 data = (Int16)(((UInt16)bArray[1] << 8) + (UInt16)bArray[0]);
            double x = (double)data * (500.0 / 65536);
            data = (Int16)(((UInt16)bArray[3] << 8) + (UInt16)bArray[2]);
            double y = (double)data * (500.0 / 65536);
            data = (Int16)(((UInt16)bArray[5] << 8) + (UInt16)bArray[4]);
            double z = (double)data * (500.0 / 65536);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                GyroXOut.Text = "X: " + x.ToString();
                GyroYOut.Text = "Y: " + y.ToString();
                GyroZOut.Text = "Z: " + z.ToString();
            });
        }

        // Key press change handler
        // Algorithm taken from http://processors.wiki.ti.com/index.php/SensorTag_User_Guide#Simple_Key_Service
        async void keyChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs)
        {
            byte[] bArray = new byte[eventArgs.CharacteristicValue.Length];
            DataReader.FromBuffer(eventArgs.CharacteristicValue).ReadBytes(bArray);

            byte data = bArray[0];

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if ((data & 0x01) == 0x01)
                    KeyROut.Background = new SolidColorBrush(Colors.Green);
                else
                    KeyROut.Background = new SolidColorBrush(Colors.Red);

                if ((data & 0x02) == 0x02)
                    KeyLOut.Background = new SolidColorBrush(Colors.Green);
                else
                    KeyLOut.Background = new SolidColorBrush(Colors.Red);

                if ((data & 0x04) == 0x04)
                    ReedOut.Background = new SolidColorBrush(Colors.Green);
                else
                    ReedOut.Background = new SolidColorBrush(Colors.Red);


            });
        }
    }
}
