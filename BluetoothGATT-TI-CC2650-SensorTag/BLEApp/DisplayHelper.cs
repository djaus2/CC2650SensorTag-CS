using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Foundation;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml.Media.Imaging;
using TICC2650SensorTag;

namespace BluetoothGATT
{
    public class DeviceInformationDisplay : INotifyPropertyChanged
    {
        private DeviceInformation deviceInfo;

        public DeviceInformationDisplay(DeviceInformation deviceInfoIn)
        {
            deviceInfo = deviceInfoIn;
            UpdateGlyphBitmapImage();
        }

        public DeviceInformationKind Kind
        {
            get
            {
                return deviceInfo.Kind;
            }
        }

        public string Id
        {
            get
            {
                
                return deviceInfo.Id;
            }
        }

        public string Address
        {
            get
            {
                //Get address from the Id
                string[] idPart = deviceInfo.Id.Split(new char[] { '#', '_' });
                if (idPart.Length == CC2650SensorTag.DEVICE_ID_AS_ARRAY_LENGTH)
                    return idPart[CC2650SensorTag.DEVICE_ID_AS_ARRAY_BTADDRESS_INDEX];
                else
                    return deviceInfo.Id;

            }
        }

        public string Name
        {
            get
            {
                return deviceInfo.Name;
            }
        }

        public BitmapImage GlyphBitmapImage
        {
            get;
            private set;
        }

        public bool CanPair
        {
            get
            {
                return deviceInfo.Pairing.CanPair;
            }
        }

        public bool IsPaired
        {
            get
            {
                return deviceInfo.Pairing.IsPaired;
            }
        }

        public IReadOnlyDictionary<string, object> Properties
        {
            get
            {
                return deviceInfo.Properties;
            }
        }

        public DeviceInformation DeviceInformation
        {
            get
            {
                return deviceInfo;
            }

            private set
            {
                deviceInfo = value;
            }
        }

        public void Update(DeviceInformationUpdate deviceInfoUpdate)
        {
            deviceInfo.Update(deviceInfoUpdate);

            OnPropertyChanged("Kind");
            OnPropertyChanged("Id");
            OnPropertyChanged("Name");
            OnPropertyChanged("DeviceInformation");
            OnPropertyChanged("CanPair");
            OnPropertyChanged("IsPaired");

            UpdateGlyphBitmapImage();
        }

        private async void UpdateGlyphBitmapImage()
        {
            BitmapImage glyphBitmapImage;
#if NOT_IOT_CORE
            glyphBitmapImage = new BitmapImage();
            DeviceThumbnail deviceThumbnail = await deviceInfo.GetGlyphThumbnailAsync();
            await glyphBitmapImage.SetSourceAsync(deviceThumbnail);
#else
            string path = "ms-appx:///Assets/AAA.png";
            glyphBitmapImage = new BitmapImage(new Uri(path, UriKind.Absolute));
#endif
            GlyphBitmapImage = glyphBitmapImage;

            OnPropertyChanged("GlyphBitmapImage");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}