

## TI CC2650 SensorTag V4.6  Built for SDK 14393 and above.

[This repository](https://github.com/djaus2/CC2650SensorTag-CS) is a port of the [ms-iot/Samples/BluetoothGatt/CS GitHub](https://github.com/ms-iot/samples/tree/develop/BluetoothGATT/CS) code to support the Texas Instruments CC2650STK, the **CC2650 SensorTag**. The Microsoft code only supports the TI CC2541 Sensor Tag. This code has been refactored and extended to only support the CC2650.

Note also Windows 8.1 code on Codeplex: https://sensortag.codeplex.com/ (Not mine)

The code establishes a connection to the service for each of the BTE characteristics and displays their values in real time. A major difference from the CC2541 is that the Gyroscope, Magnetometer and Accelerometer are one characteristic, Motion. Also this tag has a reed switch. By default, sensors run in Notification mode where they periodically update their values to the UI through a UI Callback.

This code also supports the IO Characteristic enabling turning on/off of the LEDs 1 & 2 as well as the Buzzer on and off.

**The supported characteristics/services are:**           
- IR_SENSOR
- HUMIDITY
- BAROMETRIC_PRESSURE
- IO_SENSOR
- KEYS .. extended to include the reed switch (place a magnet near the power button)
- OPTICAL
- MOVEMENT
- REGISTERS

**The app has been tested  on:**
- Windows 10 IoT Core, RPI3. 14393 TBA
- Windows 10 IoT Core, RPI3. 15063 TBA
- Windows 10 IoT Core, RPI3, 16170  Works but there are some BT connectivity issues (works some times but not all).
- Some time get not BT Radios messgae. Reboot fixes that.
- Dragonboard test coming. 
- Windows 10 Desktop Builds 14393  **OK**
- Windows 10 Desktop Builds 15063 TBA
- Windows 10 Phone Builds 14393 TBA
- Windows 10 Phone Builds 15063 TBA

**Note** ARM Appx Packages availble in the repository for side loading on RPI and Phone

**NB:**
This version counts the updates events per 15 seconds and writes to a log file **sample.log** in
\\minwinpc\c$\Data\Users\DefaultAccount\Documents


The code has been specifically refactored so that the CC2650 functionality is defined in a separate class. The UI for displaying data and for user input is in the MainPage class.

Code for handling Bluetooth connectivity, characteristic metadata, service code and event handlers are all in the CC2650 class. The class uses a callback mechanism to update data in the UI.

Another small change is that the moving dot for accelerator X-Y display has a variation in color depending upon the Z compoment:
- White is 0 +-0.2
- Red is < -1.4
- Blue is > 1.4  etc.

**This version of the app requires pairing to be done outside of the app. Ther are no Pair/Unpair buttons.**

At startup there is a progress counter is displayed. Whilst there is no target, it reaches 74 on dekstop and about 84 on the RPI.

Battery Voltage is also displayed after startup if it is enabled.

Sensor services start in Update on Change mode (except motion which is temly (1s)).
Can set to manual mode.

Also there are some  options:
* Display Coverted data, or the RAW data bytes.
* Ignore byte arrays of update data that are all zero (except Luminosity and Keys (0 is used for key up))
* Count and display the number of update events.

**UI Changes:**
- Implemented UI as SplitView with most buttons in the SplitView Pane on left.
- The SplitView menu on left has been rearranged with seperators between functional section.
- Som ecommands have been added, some removed
- **Note:** When manually read, a sensor's Notifications are stopped. Can re-enable
- Properties are displayed on a separate page.
- Can select a subset (as a range) of sensors to start.
- Can select no sensors, as well a choose battery and properties enable/disable.


**NOTE**
*Currently working through some issues with connectivity with the Creators Edition.*

**Further versions could implement:**
- Rather than in **Notifications Mode**,  implement **Poll mode to read data directly from sensors**  **(WORKS)**
- A Headless version of the code
- An integration with the [Azure IoT Gateway SDK](https://github.com/Azure/azure-iot-gateway-sdk/) This SDK does support the CC2650 tag as an example using the RPI3 but the code is only for Linux running on the RPI3




