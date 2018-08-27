using System;
using Microsoft.SPOT;
using System.IO.Ports;
using VirtualBreadboard.Remoting.NETMF.Firmata;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;

namespace Netduino.Firmata
{
    public class SerialConnection : Connection
    {
        const int BUFFER_SIZE = 1024;
        static byte[] buffer = new byte[BUFFER_SIZE];
       
        public SerialConnection(SerialPort serialPort)
        {
            SerialPort = serialPort;
            SerialPort.DataReceived += SerialPort_DataReceived;
 
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
          
            if ((e.EventType == SerialData.Chars) && (sender == SerialPort))
            {
                //Echo
                int amount = ((SerialPort)sender).Read(buffer, 0, BUFFER_SIZE);
                if (amount > 0)
                {
                    NotifyDataArrived(buffer, 0, amount);
                }
            }
       
        }

        SerialPort SerialPort { get; set; }

        public override void Write(byte[] buffer, int start, int len)
        {
            SerialPort.Write(buffer, start, len);
        }

        public override void PrintDebug(string debugMessage)
        {
            Debug.Print(debugMessage);
        }
    }
}
