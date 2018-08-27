using System;
using Microsoft.SPOT;
using static Microsoft.SPOT.Hardware.Cpu;
using SecretLabs.NETMF.Hardware.Netduino;
using SecretLabs.NETMF.Hardware;
using VirtualBreadboard.Remoting.NETMF.Firmata;
using Microsoft.SPOT.Hardware;
using N = SecretLabs.NETMF.Hardware.Netduino;

namespace Netduino.Firmata
{
    public class Netduino3Board : BoardDef
    {
        public class RemotePinImpl : RemotePin
        {
            Cpu.Pin _pin;
            OutputPort _digitalOutput;
            InterruptPort _digitalInput;
            AnalogInput _analogInput;
            Microsoft.SPOT.Hardware.PWM _pwm;
            int _pinMode = FirmataHost.NC;
            AnalogChannel _analogChannel;
            PWMChannel _pwmChannel;

            public RemotePinImpl(Cpu.Pin pin, AnalogChannel analogChannel, PWMChannel pwmChannel)
            {
                _pin = pin;
                _analogChannel = analogChannel;
                _pwmChannel = pwmChannel;

                PreviousValue = -1;
            }

            public override int PinMode
            {
                get
                {
                    return _pinMode;
                }
                set
                {
                    if (_pinMode != value)
                    {
                        switch (_pinMode)
                        {
                            case FirmataHost.INPUT:
                                _digitalInput.OnInterrupt -= _digitalInput_OnInterrupt;
                                _digitalInput.DisableInterrupt();
                                _digitalInput.Dispose();
                                _digitalInput = null;
                                break;
                            case FirmataHost.OUTPUT:
                                _digitalOutput.Dispose();
                                _digitalOutput = null;
                                break;
                            case FirmataHost.ANALOG:
                                _digitalInput.Dispose();
                                _digitalInput = null;
                                break;
                            case FirmataHost.PWM:
                                _pwm.Stop();
                                _pwm.Dispose();
                                _pwm = null;
                                break;
                            case FirmataHost.SERVO:
                                //TODO: Foundation servo
                                break;
                        }

                        switch (value)
                        {
                            case FirmataHost.INPUT:

                                _digitalInput = new InterruptPort(_pin, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeBoth);
                        
                                _digitalInput.OnInterrupt += _digitalInput_OnInterrupt;
                                _digitalInput.EnableInterrupt();

                                break;
                            case FirmataHost.OUTPUT:
                                _digitalOutput = new OutputPort(_pin, false);
                                break;
                            case FirmataHost.ANALOG:
                                _analogInput = new AnalogInput(_analogChannel);
                                break;
                            case FirmataHost.PWM:
                                _pwm = new Microsoft.SPOT.Hardware.PWM(_pwmChannel,490,0,false);
                                _pwm.Start();
                                break;
                            case FirmataHost.SERVO:
                                //TODO: Foundation servo
                                break;
                            case FirmataHost.NC:
                                //do nothing
                                break;
                        }
                        _pinMode = value;
                    }

                }
            }

            int PreviousValue{get;set;}

            private void _digitalInput_OnInterrupt(uint port, uint data, DateTime time)
            {
                int checkValue = Value ;
                if(checkValue != PreviousValue)
                {
                    NotifyChanged();
                }
                PreviousValue = checkValue;
            }

            public override int Value
            {
                get
                {
                    switch (_pinMode)
                    {
                        case FirmataHost.INPUT:
                            if (_digitalInput != null)
                            {
                                if( _digitalInput.Read())
                                {
                                    return FirmataHost.HIGH;
                                }else
                                {
                                    return FirmataHost.LOW;
                                }
                            }
                            break;
                        case FirmataHost.ANALOG:
                            if (_analogInput != null)
                            {
                                return (int) ( _analogInput.Read() * 1024);
                            }
                            break;
                        default:
                            return 0;
                    }
                    return 0;
                }
                set
                {
                    switch (_pinMode)
                    {
                        case FirmataHost.OUTPUT:
                            if (_digitalOutput != null)
                            {
                                _digitalOutput.Write(value != 0);

                            }

                            break;

                        case FirmataHost.PWM:
                            if(_pwm != null)
                            {
                                _pwm.DutyCycle = (double)value / 255.0;
                            }
                            break;
                        case FirmataHost.SERVO:
                            break;

                    }

                }
            }
        }
        public override RemotePin CreatePin(int pin)
        {
            return new RemotePinImpl(MapPin(pin), MapAnalogChannel(pin), MapPWMChannel(pin));
        }

        private PWMChannel MapPWMChannel(int pin)
        {
            switch (pin)
            {
                case 3: return PWMChannels.PWM_PIN_D3;
                case 5: return PWMChannels.PWM_PIN_D5;
                case 6: return PWMChannels.PWM_PIN_D6;
                case 9: return PWMChannels.PWM_PIN_D9;
                case 10: return PWMChannels.PWM_PIN_D10;
                case 11: return PWMChannels.PWM_PIN_D11;
 
            }
            return PWMChannels.PWM_NONE;
        }

        private AnalogChannel MapAnalogChannel(int pin)
        {
            switch (pin)
            {
              
                case 14: return AnalogChannels.ANALOG_PIN_A0;
                case 15: return AnalogChannels.ANALOG_PIN_A1;
                case 16: return AnalogChannels.ANALOG_PIN_A2;
                case 17: return AnalogChannels.ANALOG_PIN_A3;
                case 18: return AnalogChannels.ANALOG_PIN_A4;
                case 19: return AnalogChannels.ANALOG_PIN_A5;

            }
            return Microsoft.SPOT.Hardware.Cpu.AnalogChannel.ANALOG_NONE; 
        }
        private Cpu.Pin MapPin(int pin)
        {
            switch (pin)
            {
                case 0: return Pins.GPIO_PIN_D0;
                case 1: return Pins.GPIO_PIN_D1;
                case 2: return Pins.GPIO_PIN_D2;
                case 3: return Pins.GPIO_PIN_D3;
                case 4: return Pins.GPIO_PIN_D4;
                case 5: return Pins.GPIO_PIN_D5;
                case 6: return Pins.GPIO_PIN_D6;
                case 7: return Pins.GPIO_PIN_D7;
                case 8: return Pins.GPIO_PIN_D8;
                case 9: return Pins.GPIO_PIN_D9;
                case 10: return Pins.GPIO_PIN_D10;
                case 11: return Pins.GPIO_PIN_D11;
                case 12: return Pins.GPIO_PIN_D12;
                case 13: return Pins.GPIO_PIN_D13;
                case 14: return Pins.GPIO_PIN_A0;
                case 15: return Pins.GPIO_PIN_A1;
                case 16: return Pins.GPIO_PIN_A2;
                case 17: return Pins.GPIO_PIN_A3;
                case 18: return Pins.GPIO_PIN_A4;
                case 19: return Pins.GPIO_PIN_A5;

            }
            return Cpu.Pin.GPIO_NONE;
        }
        //#define TOTAL_ANALOG_PINS       6
        public override int TOTAL_ANALOG_PINS
        {
            get
            {
                return 6;
            }
         
        }

        //#define TOTAL_PINS              20 // 14 digital + 6 analog
        public override int TOTAL_PINS   
        {
            get{
                return 20;
            }   
        }

        public override bool IS_PIN_ANALOG(int p)
        {
            return ((p) >= 14 & (p) < 14 + TOTAL_ANALOG_PINS );
        }

        //#define IS_PIN_DIGITAL(p)       ((p) >= 2 && (p) <= 19)
        public override bool IS_PIN_DIGITAL(int p)
        {
            return ((p) >= 2 & (p) <= 19);
        }

        public override bool IS_PIN_I2C(int p)
        {
            return false;
            // IS_PIN_I2C(p)           ((p) == 18 || (p) == 19)
        }

        public override bool IS_PIN_PWM(int p)
        {

            switch (p)
            {
                case 3:
                case 5:
                case 6:
                case 9:
                case 10:
                case 11:
                    return true;
                default:
                    return false;
            }

        }

        //#define IS_PIN_SERVO(p)         (IS_PIN_DIGITAL(p) && (p) - 2 < MAX_SERVOS)
        public override bool IS_PIN_SERVO(int p)
        {
            return false;
        }

        //#define PIN_TO_ANALOG(p)        ((p) - 14)
        public override int PIN_TO_ANALOG(int p)
        {
            return p - 14;
        }
        public override int ANALOG_TO_PIN(int p)
        {
            return p + 14;
        }

        public override int PIN_TO_DIGITAL(int p)
        {
            return p;
        }

        //#define PIN_TO_PWM(p)           PIN_TO_DIGITAL(p)
        public override int PIN_TO_PWM(int p)
        {
            return p;
        }

        //#define PIN_TO_SERVO(p)         ((p) - 2)
        public override int PIN_TO_SERVO(int p)
        {
            return p - 2;
        }

        //#define VERSION_BLINK_PIN       13
        public override int VERSION_BLINK_PIN 
        {
            get
            {
                return 13;
            }
            
        }
    }
}
