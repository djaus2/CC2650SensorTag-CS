using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace TICC2650SensorTag
{
    public interface ICC2650SensorTag
    {
        GattCharacteristic Address { get; set; }
        CC2650SensorTag.SensorDataDelegate CallMeBack { get; set; }
        GattCharacteristic Configuration { get; set; }
        GattCharacteristic Data { get; set; }
        GattCharacteristic Device_Id { get; set; }
        GattDeviceService GattService { get; set; }
        GattCharacteristic Notification { get; set; }
        GattCharacteristic Period { get; set; }
        CC2650SensorTag.SensorIndexes SensorIndex { get; set; }

        Task DisableNotify();
        Task EnableNotify();
        void keyChanged(GattCharacteristic sender, GattValueChangedEventArgs eventArgs);
        Task<CC2650SensorTag.SensorData> keyChangedProc(byte[] bArray, bool doCallback);
        Task<CC2650SensorTag.SensorData> ReadSensor(bool disableNotify, bool updateDisplay, bool turnSensorOffOn);
        Task<byte[]> ReadSensorBase(CC2650SensorTag.ServiceCharacteristicsEnum character);
        void setSensorPeriod(int period);
        Task TurnOffSensor();
        Task TurnOnSensor();
    }
}