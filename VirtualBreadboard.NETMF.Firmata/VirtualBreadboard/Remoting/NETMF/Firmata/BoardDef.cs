using System;
 
namespace VirtualBreadboard.Remoting.NETMF.Firmata
{
    public abstract class BoardDef
    {
        public const int DUEMILANOVE = 0;
        public const int DIECIMILA = 1;
        public const int NANO = 2;
        public const int MEGA = 3;
        public const int LILYPAD = 4;
        public const int MINI = 5;
        public const int WIRING = 6;
        public const int OTHER = 7;

        public abstract RemotePin CreatePin(int pin);
        public abstract int TOTAL_ANALOG_PINS { get; }
        public abstract int TOTAL_PINS { get; }
        public abstract int VERSION_BLINK_PIN { get; }
        public abstract bool IS_PIN_DIGITAL(int p);
        public abstract bool IS_PIN_ANALOG(int p);
        public abstract bool IS_PIN_PWM(int p);
        public abstract bool IS_PIN_SERVO(int p);
        public abstract bool IS_PIN_I2C(int p);
        public abstract int PIN_TO_DIGITAL(int p);
        public abstract int PIN_TO_ANALOG(int p);
        public abstract int PIN_TO_PWM(int p);
        public abstract int PIN_TO_SERVO(int p);
        public abstract int ANALOG_TO_PIN(int p);
    

    }
}
