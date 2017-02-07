
## TI CC2650 SensorTag

This repository is a port of the [ms-iot/Samples/BluetoothGatt/CS GitHub](https://github.com/ms-iot/samples/tree/develop/BluetoothGATT/CS) code to support the Texas Instrument CC2650STK, the **CC2650 SensorTag**. The Microsoft code only supports the TI CC2541 Sensor Tag. This code has been refactored and extended to only support the CC2650.

The code establishes a connection to the service for each of the BTE characteristics and displays their values in real time. A major difference from the CC2541 is that the Gyroscope, Magnetometer and Accelerometer are one characteristic, Motion. Also this tag has a reed switch. By default, sensors run in Notification mode where they periodically update their values to the UI through a UI Callback.

This code also supports the IO Characteristic enabling turning on/off of the LEDs 1 & 2 as well as the Buzzer on and off.

The supported characteristics/services are:            
- IR_SENSOR
- HUMIDITY
- BAROMETRIC_PRESSURE
- IO_SENSOR
- KEYS .. extended to include the reed switch (place a magnet near the power button)
- OPTICAL
- MOVEMENT
- REGISTERS

The code has been specifically refactored so that the CC2650 functionality is defined in a separate class. Whilst the Bluetooth connectivity and UI for displaying data and for user input remains in the MainPage class, characteristic metadata, service code and event handlers are all in the CC2650 class. The class uses a callback mechanism to update data in the UI.

Another small change is that the moving dot for accelerator x-Y display has a variation in color depending upon the Z compoment:
- White is 0 +-0.2
- Red is < -1.4
- Blue is > 1.4  etc.

Also there is option to display RAW data.

Further versions could implement:
- Rather than in **Notifications Mode**,  implement **Poll mode to read data directly from sensors** (Done but needs testing.)
- A Headless version of the code
- An integration with the [Azure IoT Gateway SDK](https://github.com/Azure/azure-iot-gateway-sdk/) This SDK does support the CC2650 tag as an example using the RPI3 but the code is only for Linux running on the RPI3

UI Changes:
- Once one CC2640 SensorTag is found the Device Watcher stops and conection can start. Don't have to wait for enumeration to complete :)'
- Once all sensors have been found and configured, the BLE Watcher stops.
- Device Watcher update debug messages have been commented out. These can come thick and fast.

**NOTE**
PS I think that there might be a problem with GATT on the Creator Editions of Windows 10.
This code was going swimmingly on my laptop until I installed my first 150XX (Windows Insider Fast Ring) build then things went haywire.
I have done the latest testing on build 14393, Anniversary Update, with the same code, and all seems well.
