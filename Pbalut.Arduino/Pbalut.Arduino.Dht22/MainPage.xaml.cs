using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Maker.Firmata;
using Microsoft.Maker.RemoteWiring;
using Microsoft.Maker.Serial;
using Pbalut.Arduino.Dht22.Enums;

namespace Pbalut.Arduino.Dht22
{
    public sealed partial class MainPage : Page
    {
        private const string UsbSerialVid = "VID_2341";
        private const string UsbSerialPid = "PID_0043";
        private const uint Baud = 115200;
        private const byte GetTemperature = 0x44;
        private const byte GetHumidity = 0x45;
        private const int TimerIntervalInMiliseconds = 2000;
        private const int SensorId = 2;
        private IStream _connection;
        private RemoteDevice _arduino;
        private UwpFirmata _firmata;
        private Timer _timer;

        public MainPage()
        {
            this.InitializeComponent();
            SetupRemoteArduino();
        }

        private void SetVisibility(VisibilityType visibilityType)
        {
            HumidityProgressRing.Visibility = visibilityType == VisibilityType.Initial
                ? Visibility.Visible
                : Visibility.Collapsed;
            HumidityGrid.Visibility = visibilityType == VisibilityType.Initial
                ? Visibility.Collapsed
                : Visibility.Visible;

            TemperatureProgressRing.Visibility = visibilityType == VisibilityType.Initial
                ? Visibility.Visible
                : Visibility.Collapsed;
            TemperatureGrid.Visibility = visibilityType == VisibilityType.Initial
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void SetupRemoteArduino()
        {
            SetVisibility(VisibilityType.Initial);

            _connection = new UsbSerial(UsbSerialVid, UsbSerialPid);
            _firmata = new UwpFirmata();
            _arduino = new RemoteDevice(_firmata);

            _firmata.StringMessageReceived += Firmata_DataMessageReceived;
            _arduino.DeviceReady += Setup;

            _firmata.begin(_connection);
            _connection.begin(Baud, SerialConfig.SERIAL_8N1);
        }

        private void Setup()
        {
            _timer = new Timer(TimerCallback, null, TimerIntervalInMiliseconds, Timeout.Infinite);
        }

        private void TimerCallback(object state)
        {
            var watch = new Stopwatch();
            watch.Start();
            _firmata.sendSysex(GetTemperature, new byte[] { SensorId }.AsBuffer());
            _firmata.flush();
            _firmata.sendSysex(GetHumidity, new byte[] { SensorId }.AsBuffer());
            _firmata.flush();

            _timer.Change(Convert.ToInt32(Math.Max(0, TimerIntervalInMiliseconds - watch.ElapsedMilliseconds)), Timeout.Infinite);
        }

        private async void Firmata_DataMessageReceived(UwpFirmata caller, StringCallbackEventArgs argv)
        {
            var content = argv.getString();
            await Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                    {
                        var type = content.Split(':')[0];
                        var value = content.Split(':')[1].Trim();

                        if (value != "NAN")
                        {
                            SetVisibility(VisibilityType.DeviceRedy);
                            switch (GetReceivedType(type))
                            {
                                case FirmataDataType.Temperature:
                                    TemperatureTextBlock.Text = $"{value} °C";
                                    break;
                                case FirmataDataType.Humidity:
                                    HumidityTextBlock.Text = $"{value} %";
                                    SetHumidityChartPercentage(value);
                                    break;
                            }
                        }
                    });
        }

        private FirmataDataType GetReceivedType(string type)
        {
            var typeId = Convert.ToInt32(type);
            return (FirmataDataType)typeId;
        }

        private void SetHumidityChartPercentage(string valueStr)
        {
            var value = (int)Convert.ToDouble(valueStr);
            HumidityChart.Percentage = value;
        }
    }
}
