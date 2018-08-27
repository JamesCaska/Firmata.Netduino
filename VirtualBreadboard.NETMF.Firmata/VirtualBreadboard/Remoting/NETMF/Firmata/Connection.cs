using System;
using System.Collections;

namespace VirtualBreadboard.Remoting.NETMF.Firmata
{
    public abstract class Connection
    {
        ArrayList _listeners = new ArrayList();

        public abstract void Write(byte[] buffer, int start, int len);
        public abstract void PrintDebug(string debugMessage);
         
        protected void NotifyDataArrived(byte[] buffer, int start, int len)
        {
            foreach(IConnectionListener a in _listeners)
            {
                a.DataReceived(buffer, start, len);
            }
        }
        public void AddListener(IConnectionListener listener)
        {
            if(!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }
        public void RemoveListener(IConnectionListener listener)
        {
            if (_listeners.Contains(listener))
            {
                _listeners.Remove(listener);
            }
        }


    }
}
