namespace TICC2650SensorTag
{
    public interface ITICC2650SensorTag_BLEWatcher
    {
        void StartBLEWatcher(Windows.UI.Xaml.Controls.Page mainPage2, DeviceInfoDel SetDevInfo);

        void StopBLEWatcher();
    }
}