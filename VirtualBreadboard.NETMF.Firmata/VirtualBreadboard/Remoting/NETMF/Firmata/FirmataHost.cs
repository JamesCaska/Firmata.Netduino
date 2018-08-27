using System;
using System.Threading;
using System.Collections;
 
/// <summary>
/// C# Implementation of the Firmata Host ( www.firmata.org )
/// 
/// James Caska, www.virtualbreadboard.com
/// </summary>
namespace VirtualBreadboard.Remoting.NETMF.Firmata 
{
 
    public class FirmataHost  : IConnectionListener
    {
           
        //Version numbers for the protocol
        public const int FIRMATA_MAJOR_VERSION = 2; //for non-compatible changes
        public const int FIRMATA_MINOR_VERSION = 0; //for backwards compatible changes
        public const int VERSION_BLINK_PIN = 13;    //digital pin to blink version on
        public const byte MAX_DATA_BYTES = 32;      //Maximum number of data bytes in non-Sysex messages

        //Message command bytes (128-255 / 0x80-0xFF)

        public const byte DIGITAL_MESSAGE = 0x90;   // send data for digital pin
        public const byte ANALOG_MESSAGE = 0xE0;    // send data for an analog pin (or PWM)
        public const byte REPORT_ANALOG = 0xC0;     // enable analog input by pin number                                      
        public const byte REPORT_DIGITAL = 0xD0;    // enable digital input by port pair
        public const byte SET_PIN_MODE = 0xF4;      // set a pin to INPUT/OUTPUT/ANALOG/PWM/SERVO - 0/1/2/3/4
        public const byte REPORT_VERSION = 0xF9;    // report protocol version                                      
        public const byte SYSTEM_RESET = 0xFF;      // reset from MIDI
        public const byte START_SYSEX = 0xF0;       // start a MIDI Sysex message                           
        public const byte END_SYSEX = 0xF7;         // end a MIDI Sysex message

        //Extended command set using Sysex (0-127 / 0x00-0x7F)
        //0x00-0x7F reserved for custom commands
        
        public const byte ANALOG_MAPPING_QUERY = 0x69;//Analog pins

       
        public const byte SERVO_CONFIG = 0x70;      // set maximum angle, minPulse, maxPulse, frequency
        public const byte FIRMATA_STRING = 0x71;    // a string message with 14-bits per character
        public const byte REPORT_FIRMWARE = 0x79;   // report name and version of the firmware

        //0x7E MIDI reserved for non-realtime messages
        public const byte SYSEX_NON_REALTIME = 126;
        //0x7F MIDI reserved for realtime messages
        public const byte SYSEX_REALTIME = 127;

    
        public const int RESERVED_COMMAND = 0x0;            //' 2nd SysEx data byte is a chip-specific command (AVR, PIC, TI, etc).
        public const int ANALOG_MAPPING_RESPONSE = 0x6a;    //' reply with mapping info
        public const int CAPABILITY_QUERY = 0x6b;           //' ask for supported modes and resolution of all pins
        public const int CAPABILITY_RESPONSE = 0x6c;        //' reply with supported modes and resolution
        public const int PIN_STATE_QUERY = 0x6d;            //' ask for a pin's current mode and value
        public const int PIN_STATE_RESPONSE = 0x6e;         //' reply with a pin's current mode and value
        public const int EXTENDED_ANALOG = 0x6f;            //' analog write (PWM, Servo, etc) to any pin
        public const int STRING_DATA = 0x71;                //' a string message with 14-bits per char
        public const int SHIFT_DATA = 0x75;                 //' shiftOut config/data message (34 bits)
        public const int I2C_REQUEST = 0x76;                //' I2C request messages from a host to an I/O board
        public const int I2C_REPLY = 0x77;                  //' I2C reply messages from an I/O board to a host
        public const int I2C_CONFIG = 0x78;                 //' Configure special I2C settings such as power pins and delay times
        public const int SAMPLING_INTERVAL = 0x7a;          //' sampling interval


        public const int LOW = 0;
        public const int HIGH = 1;

        //Pin Modes
        public const int NC = -1;
        public const int INPUT = 0;
        public const int OUTPUT = 1;
        public const byte ANALOG = 2;   //Analog pin in analogInput mode
        public const byte PWM = 3;      //Digital pin in PWM output mode
        public const byte SERVO = 4;    //Digital pin in Servo output mode


        //Variable definitions
      
        //input message handling
        private int waitForData = 0;                //this flag says the next serial input will be data
        private int executeMultiByteCommand = 0;    //execute this after getting multi-byte data
        private int multiByteChannel = 0;           //channel data for multiByteCommands
        private int[] incomingBuffer = new int[MAX_DATA_BYTES + 100]; //multi-byte data
  
        //Sysex
        private bool parsingSysex;
        private int sysexBytesRead;
 
        private int[] digitalOutputData = new int[16];
   
        private int[] digitalOutputReportData = new int[16];
        private int[] digitalInputDataPrevious = new int[16];
        private int[] digitalInputData = new int[16];
     
        private int[] previousDigitalPort;
        private byte[] outgoingBuffer = new byte[1000];
           
        private bool[] dirtyPort = new bool[10];
        private bool _outgoingAvailable;

        RemotePin[] _Pins;
        ArrayList _analogPins;
        BoardDef Board { get; set; }
        Connection Connection { get; set; }
        int DefaultPinMode { get; set; }

        bool _running;
       
        public FirmataHost(Connection connection, BoardDef board, int defaultPinMode)
        {
            this.Board = board;
            this.Connection = connection;
            this.DefaultPinMode = defaultPinMode;

            CreatePins();

        }

        private void CreatePins()
        {
            _analogPins = new ArrayList();
            _Pins = new RemotePin[Board.TOTAL_PINS];
            for (int i = 0; i < Board.TOTAL_PINS; i++)
            {
                _Pins[i] = Board.CreatePin(i);
                _Pins[i].Host = this;
                _Pins[i].PinIndex = i;
                if( i < 2)
                {
                    PinMode(i, NC); //Pins 0,1 are serial port. TODO: generalise as part of board def.
                }
                else
                {
                    PinMode(i, DefaultPinMode);
                }
                
            }
        }

        /// <summary>
        /// Initalise all pins to null
        /// Power Down any PWM pins
        /// </summary>
        private void InitialisePins()
        {
           

            previousDigitalPort = new int[] { -1, -1 };
             
            for (int i = 0; i < Board.TOTAL_PINS; i++)
            {
                PinMode(i, DefaultPinMode);
            }
        }
     
        private void SampleAnalogPins()
        {
            foreach(RemotePin p in _analogPins)
            {
                if( p.AnalogReportEnabled && p.SampleChanged )
                {
                    AnalogWriteReport(Board.PIN_TO_ANALOG( p.PinIndex), p.Value);
                }
            }
            
        }

        public void Start()
        {
            Connection.AddListener(this);
            this._running = true;
            var th = new Thread(() => {
                while (this._running)
                {
                    //Analog pins are processed by polling.. but maybe both should be polled?..maybe is optional
                    SampleAnalogPins();

                    //DigitalPins are processed with Interrupts..

                    // Threading needs to be studied for responsiveness vs Interrupt stacking
                    if (_outgoingAvailable)
                    {
                        _outgoingAvailable = false;
                         
                        for(int i = 0; i < 2; i++)
                        {
                            if( dirtyPort[i])
                            {
                                DigitalWritePort(i, digitalOutputReportData[i]);
                                dirtyPort[i] = false;
                            }
                        }
                    }
                }
            });
            th.Start();

        }

        public void Stop()
        {
            _running = false;
            Connection.RemoveListener(this);
        }

         
        private void WriteSerialPort(byte[] buffer, int start, int len) //LOG, string  msg)
        {
            try
            {
                   
                Connection.Write(buffer, start, len);
                //Connection.PrintDebug(msg);
            }
            catch (Exception ex)
            {
               //LOG Connection.PrintDebug( msg + ": " + ex.ToString());
                Connection.PrintDebug(  "WriteSerialPort : " + ex.ToString());
            }
        }
         

        // Returns On/Off data for an individual Digital Pin
        // [Description("Retuns On/Off data for an individual Digital Pin")]
        public int DigitalRead(int pin)
        {
            return  ( digitalInputData[pin >> 3] >> (pin & 7) )  & 1  ;
        }
          

        // Send a Start Sysex message
        //[Description("Sends a Start Sysex message")]
        public void StartSysex()
        {
            byte[] sendBuffer = { 0 };
            sendBuffer[0] = START_SYSEX;

            WriteSerialPort(sendBuffer, 0, 1);//LOG,  "Problem_sending_Start_Sysex");

        }

        // Send an End Sysex message
        //[Description("Sends an End Sysex message")]
        public void EndSysex()
        {
            byte[] sendBuffer = { 0 };
            sendBuffer[0] = END_SYSEX;
            WriteSerialPort(sendBuffer, 0, 1);//LOG, "Problem sending End Sysex");

        }


        // Sends a message to turn Analog Pin reporting on or off
        //[Description("Sends a message to turn Analog Pin reportng on or off for a pin")]
        public void AnalogPinReport(int pin, int mode)
        {
            byte[] analogPinReportMessage = new byte[2];
            analogPinReportMessage[0] = (byte)(REPORT_ANALOG | pin);
            analogPinReportMessage[1] = (byte)mode;

            WriteSerialPort(analogPinReportMessage, 0, 2);//LOG, "Problem sending analog pin enable / disable message");

        }

        // Turns digital port reporting on or off
        //[Description("Sends a message to toggle reporting for a whole digital port")]
        public void DigitalPortReport(int port, int onOff)
        {
            byte[] digitalPortReportMessage = new byte[2];
            digitalPortReportMessage[0] = (byte)(REPORT_DIGITAL | port);
            digitalPortReportMessage[1] = (byte)(onOff);

            WriteSerialPort(digitalPortReportMessage, 0, 2);//LOG, "Problem sending analog pin enable / disable message");

        }

        // Sends on or off to a pin
        //Cache this to the end of for fast signals.. 
        //[Description("Sends an On or Off message to an individual Digital Pin")]
        public void DigitalWrite(int pin, int value)
        {
            int portNumber = 0;
            portNumber = (pin >> 3) & 15;
            int adjustment = 0;
            adjustment = (1 << (pin & 7));
            byte[] digitalWriteBuffer = { 0, 0, 0 };

            if ((value == 0))
            {
                digitalOutputData[portNumber] = digitalOutputData[portNumber] & (~adjustment);
            }
            else
            {
                digitalOutputData[portNumber] = digitalOutputData[portNumber] | adjustment;
            }

            digitalWriteBuffer[0] = (byte)(DIGITAL_MESSAGE | portNumber);
            digitalWriteBuffer[1] = (byte)(digitalOutputData[portNumber] & 127);
            digitalWriteBuffer[2] = (byte)(digitalOutputData[portNumber] >> 7);

            WriteSerialPort(digitalWriteBuffer, 0, 3);//LOG, "Problem with digitalWrite");


        }

        public void DigitalWritePort(int portNumber, int portData)
        {
           
            outgoingBuffer[0] = (byte)(DIGITAL_MESSAGE | portNumber);
            outgoingBuffer[1] = (byte)(portData & 127);
            outgoingBuffer[2] = (byte)(portData >> 7);

            WriteSerialPort(outgoingBuffer, 0, 3);//LOG, "DigitalWritePort");

        }

      

        public void ResetTarget()
        {

            InitialisePins();

            byte[] resetMessage = { 0 };
            resetMessage[0] = SYSTEM_RESET;
            StartSysex();

            WriteSerialPort(resetMessage, 0, 1);//LOG, "ResetTarget");

            EndSysex();
        }


        // Stores portData (bit masked On/Off data) for a whole port in 
        // digitalInputData array
        //[Description("Puts but masked On/Off data for a whole digital port into array")]
        public void SetDigitalOutputs(int portNumber, int portData)
        {
            int previous = digitalInputData[portNumber];

            digitalInputData[portNumber] = portData;

            int pin = portNumber * 8;
            int mask = 1;

            int changed = previous ^ portData;
             
            for (int i = 0; i <= 7; i++)
            {
                if ((changed & mask) != 0)
                {
                    int pinIndex = pin + i;

                    RemotePin p = _Pins[pinIndex];

                    if (p != null   )
                    {
                        p.Value = DigitalRead(pinIndex) ;
                    }

                }
                mask = mask << 1;
            }
               
        }

        // Stores analog value data in array for each analog pin
        //[Description("Stores analog value data in array")]
        public void SetAnalogOutput(int pin, int value)
        {
            RemotePin p = _Pins[pin];

            if (p != null)
            {
                p.Value = value;
            }
       
        }
          
        private int[] PinMap(int pinCount)
        {
            int[] map = new int[pinCount];
            for (int i = 0; i < pinCount; i++)
            {
                map[i] = i;
            }
            return map;
        }
 

          
        public void processChange(byte processId, params byte[] param)
        {
        }

             
        public void unregisterPinAsSampledInputPin(int pin)
        {
        }

        //Inputs are processed by the sweep
              
        private string getBinary(int v)
        {
            string ret = "";
            for (int i = 0; i <= 31; i++)
            {
                if ((v & 1) == 0)
                {
                    ret += "0";
                }
                else
                {
                    ret += "1";

                }
                v = v >> 1;
            }
            return ret;
        }

           

        private byte[] Merge(byte[] a, byte[] b)
        {
            byte[] c = null;

            c = new byte[a.Length + b.Length];

            Array.Copy(a, c, a.Length);
            Array.Copy(b, 0, c, a.Length, b.Length);
            return c;

        }

        //System capabilities..
        private void SendFirmware()
        {
            string fw = "Standard_Firmata - Netduino";
            int len = fw.Length * 2 + 5;
             
            outgoingBuffer[0] = START_SYSEX;
            outgoingBuffer[1] = REPORT_FIRMWARE;
            outgoingBuffer[2] = FIRMATA_MAJOR_VERSION;
            outgoingBuffer[3] = FIRMATA_MINOR_VERSION;


            for (int i = 0; i <= fw.Length - 1; i++)
            {
                outgoingBuffer[2 * i + 4] = (byte)(((int)fw[i]) & 127);
                outgoingBuffer[2 * i + 5] = (byte)((((int)fw[i]) >> 7) & 127);
            }

            outgoingBuffer[len - 1] = END_SYSEX;

            WriteSerialPort(outgoingBuffer, 0, len);//LOG, "REPORT_FIRMWARE");
        }
        private void SendVersion() { 
      
            outgoingBuffer[0] = START_SYSEX;
            outgoingBuffer[1] = REPORT_FIRMWARE;
            outgoingBuffer[2] = FIRMATA_MAJOR_VERSION;
            outgoingBuffer[3] = FIRMATA_MINOR_VERSION;
            outgoingBuffer[4] = END_SYSEX;

            WriteSerialPort(outgoingBuffer, 0, 5);//LOG, "REPORT_FIRMWARE");
        }
        private void ProcessSysexMessage()
        {
         
            int index = 0;

            switch (incomingBuffer[0])
            {
                case SYSTEM_RESET:
                    SystemReset(); break;
                case REPORT_FIRMWARE:
                    SendFirmware();
                    break;
                case REPORT_VERSION:
                        
                case ANALOG_MAPPING_QUERY:

                    outgoingBuffer[index++] = START_SYSEX;
                    outgoingBuffer[index++] = ANALOG_MAPPING_RESPONSE;
                  
                    for (int i = 0; i < Board.TOTAL_PINS ; i++)
                    {
                        if (Board.IS_PIN_ANALOG(i))
                        {
                            outgoingBuffer[index++] = (byte)Board.PIN_TO_ANALOG(i);
                        }
                        else
                        {
                            outgoingBuffer[index++] = 127;
                        }

                    }
                        
                    outgoingBuffer[index++] = END_SYSEX;
 
                    WriteSerialPort(outgoingBuffer, 0, index);//LOG, "ANALOG_MAPPING_QUERY");
                    break;
                case CAPABILITY_QUERY:

                    outgoingBuffer[index++] = START_SYSEX;
                    outgoingBuffer[index++] = CAPABILITY_RESPONSE;
 
                    for (int i = 0; i < Board.TOTAL_PINS ; i++)
                    {
                        if (Board.IS_PIN_DIGITAL(i))
                        {
                            outgoingBuffer[index++] = INPUT;
                            outgoingBuffer[index++] = 1;
                            outgoingBuffer[index++] = OUTPUT;
                            outgoingBuffer[index++] = 1;
                        }
                        if (Board.IS_PIN_ANALOG(i))
                        {
                            outgoingBuffer[index++] = ANALOG;
                            outgoingBuffer[index++] = 10;
                        }
                        if (Board.IS_PIN_PWM(i))
                        {
                            outgoingBuffer[index++] = PWM;
                            outgoingBuffer[index++] = 8;
                        }
                        outgoingBuffer[index++] = 127;
  
                    }


                    outgoingBuffer[index++] = END_SYSEX;
                          
                    WriteSerialPort(outgoingBuffer, 0, index);//LOG, "CAPABILITY_QUERY");
                    //Console.WriteLine("CAPABILITY_QUERY = " + Convert.ToBase64String(tempBuffer, 0, index));
                    break;
                case PIN_STATE_QUERY:

                    int pinId = incomingBuffer[1];
                     
                    outgoingBuffer[index++] = START_SYSEX;
                    outgoingBuffer[index++] = PIN_STATE_RESPONSE;
                    outgoingBuffer[index++] = (byte)pinId;
                   
                    if (pinId >= Board.TOTAL_PINS )
                    {
                        outgoingBuffer[index++] = INPUT;
                        outgoingBuffer[index++] = 0;
                    }
                    else
                    {
                        if (_Pins[pinId] == null)
                        {
                            outgoingBuffer[index++] = INPUT;
                            outgoingBuffer[index++] = 0;
                        }
                        else
                        {
                            int mode =  _Pins[pinId].PinMode;
                            outgoingBuffer[index++] = (byte)mode;
                           
                            int stateValue = _Pins[pinId].Value;
                             
                            if (mode == PWM)
                            {
                                outgoingBuffer[index++] = (byte)(stateValue & 127);
                                outgoingBuffer[index++] = (byte)((stateValue >> 7) & 127);
                            }
                            else
                            {
                                outgoingBuffer[index++] = (byte)stateValue;
                            }
                        }
                    }


                    outgoingBuffer[index++] = END_SYSEX;
         
                    WriteSerialPort(outgoingBuffer, 0, index);//LOG, "CAPABILITY_QUERY");
                    break;
                case PIN_STATE_RESPONSE:
                    int pin = incomingBuffer[1];
                    int pinState1 = incomingBuffer[2];
                    int pinState2 = incomingBuffer[3];

 
                    break;
                default:
                   // Debug.Assert(false);

                    break;
            }
             

        }


    /// <summary>
    /// Resets all the IO and restores to the standard values.. sends the firmware..
    /// </summary>
    private void SystemReset()
    {
        InitialisePins();
        SendFirmware();
    }

    //The monitorThread continuously exhanges request and response packets.
    //The faster the better..
    //Ultimately want to improve the performance of this..

    // Main procedure to process receieved serial data
    // [Description("Processes receieved serial data")]
    public void DataReceived(byte[] buffer, int startPos, int len)
    {
          
            try
            {

                while (len > 0)
                {
                    int inputData = buffer[startPos++];
                    len--;
     
                    int command = 0;

                    if (parsingSysex == true)
                    {
                        if ((inputData == END_SYSEX))
                        {
                            //stop sysex byte
                            parsingSysex = false;
                            //fire off handler function
                            ProcessSysexMessage();
                        }
                        else
                        {
                            //normal data byte - add to buffer
                            if (sysexBytesRead < incomingBuffer.Length)
                            {
                                incomingBuffer[sysexBytesRead++] = inputData;
                            }
                            else
                            {
                                Connection.PrintDebug( "Overflow ");
                            }

                        }
                    }
                    else if ((waitForData > 0 & inputData < 128))
                    {
                        waitForData = waitForData - 1;
                        incomingBuffer[waitForData] = inputData;

                        if (((waitForData == 0) & executeMultiByteCommand != 0))
                        {
                            switch (executeMultiByteCommand)
                            {
                                case ANALOG_MESSAGE:
                                    SetAnalogOutput(multiByteChannel, (incomingBuffer[0] << 7) + incomingBuffer[1]);
                                    break;
                                case DIGITAL_MESSAGE:
                                    SetDigitalOutputs(multiByteChannel, (incomingBuffer[0] << 7) + incomingBuffer[1]);
                                    break;
                                case SET_PIN_MODE:
                                    PinMode(incomingBuffer[1], incomingBuffer[0]);
                                    break;
                                case REPORT_ANALOG:
                                    ReportAnalog(multiByteChannel, incomingBuffer[0]);
                                    break;
                                case REPORT_DIGITAL:
                                    ReportDigital(multiByteChannel, incomingBuffer[0]);
                                    break;
                            }
                            executeMultiByteCommand = 0;
                        }
                    }
                    else
                    {
                        //remove channel info from command byte if less than 0xF0
                        if ((inputData < 240))
                        {
                            command = inputData & 240;
                            multiByteChannel = inputData & 15;
                        }
                        else
                        {
                            command = inputData;
                        }

                        switch (command)
                        {
                            case ANALOG_MESSAGE:
                                waitForData = 2;
                                executeMultiByteCommand = command;
                                break;
                            case DIGITAL_MESSAGE:
                                waitForData = 2;
                                executeMultiByteCommand = command;
                                break;
                            case SET_PIN_MODE:
                                waitForData = 2;
                                executeMultiByteCommand = command;
                                break;
                            case REPORT_ANALOG:
                                waitForData = 1;
                                executeMultiByteCommand = command;
                                break;
                            case REPORT_DIGITAL:
                                waitForData = 1;
                                executeMultiByteCommand = command;
                                break;
                            case START_SYSEX:
                                parsingSysex = true;
                                sysexBytesRead = 0;
                                break;
                            case REPORT_VERSION:
                                waitForData = 2;
                                executeMultiByteCommand = command;
                                break;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
            }
        }

        private void ReportDigital(int channel, int onOff)
        {
            int pin = 8 * channel;
         
            for (int i = 0; i <= 7; i++)
            {
                if (pin < Board.TOTAL_PINS )
                {
                    RemotePin p = _Pins[pin];
                    if (p != null)
                    {
                        p.DigitalReportEnabled = ( onOff != 0 );
                    }
                    pin += 1;
                }
            }
            
        }

        private void ReportAnalog(int channel, int onOff)
        {
            
            int anPin = Board.ANALOG_TO_PIN(channel);
             
            if (anPin < Board.TOTAL_PINS )
            {
                RemotePin  p = _Pins[anPin];

                if (p != null)
                {
                    p.AnalogReportEnabled = onOff != 0;
                }
            }
           
             
        }

       
        public void PinMode(int pin, int mode)
        {
            RemotePin p = _Pins[pin];
            if (p != null)
            {
                if( p.PinMode == ANALOG)
                {
                    _analogPins.Remove(p);
                }
                p.PinMode = mode;
                if (mode == ANALOG)
                {
                    _analogPins.Add(p);
                }
            }
 
        }
         
        public void AnalogWriteReport(int pin, int value)
        {
            
            outgoingBuffer[0] = (byte)(ANALOG_MESSAGE | (pin & 15));
            outgoingBuffer[1] = (byte)(value & 127);
            outgoingBuffer[2] = (byte)((value >> 7) & 127);

            WriteSerialPort(outgoingBuffer, 0, 3);//LOG, "AnalogWriteReport");

        }


        public void DigitalWriteReport(int pin, int value)
        {
            int port = pin / 8;
            int bit = 1 << (pin & 7);

            int portValue = digitalOutputReportData[port];

            if (value == 1)
            {
                portValue = portValue | bit;
            }
            else
            {
                portValue = (portValue | bit) ^ bit;
            }

            digitalOutputReportData[port] = portValue;
            dirtyPort[port] = true;
            _outgoingAvailable = true;

        }

        
    }
  
   

}
