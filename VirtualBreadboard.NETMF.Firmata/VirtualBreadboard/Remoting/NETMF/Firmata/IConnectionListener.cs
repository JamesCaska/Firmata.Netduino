using System;
 
namespace VirtualBreadboard.Remoting.NETMF.Firmata
{
    public interface IConnectionListener
    {
        void DataReceived(byte[] buffer, int startPos, int len);
    }
}
