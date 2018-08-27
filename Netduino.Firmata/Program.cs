using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using System.IO.Ports;
using System.Threading;
using System.Text;
using VirtualBreadboard.Remoting.NETMF.Firmata;
using Netduino.Firmata;


namespace NetduinoBlink
{
  

    public class Program
    {

        //COM 1
        //D1 == > TX (output)
        //D0 < == RX (input)

        //
        //COM 4
        //SCL ==> TX
        //SDA ==> RX
        public static void Main()
        {
            //Connecting to firmata test program
            SerialPort comms = new SerialPort(SerialPorts.COM1, 57600, Parity.None, 8, StopBits.One);
            comms.Open();
            FirmataHost remoteIO = new FirmataHost(new SerialConnection(comms), new Netduino3Board(), FirmataHost.OUTPUT);

            //Connecting to VirtualBreadboard TLC
            // SerialPort comms = new SerialPort(SerialPorts.COM1, 115200, Parity.None, 8, StopBits.One);
            // comms.Open();
            // FirmataHost remoteIO = new FirmataHost(new SerialConnection(comms), new Netduino3Board(), FirmataHost.NC);

            remoteIO.Start();
 
            bool running = true;
            while (running)
            {
                //Do other stuff..
                Thread.Sleep(250); // sleep for 250ms 
            }
            remoteIO.Start();
        }

        
    }
}
