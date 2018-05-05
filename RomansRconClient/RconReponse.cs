using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomansRconClient
{
    public class RconResponse
    {
        //Used as a response from the server

        public string body;
        public RconPacketType type;
        public int id;

        public RconResponseStatus status;
        public Exception ex;

        public static RconResponse CreateOkayResponse(RconPacketType _type, int _id, byte[] _body)
        {
            //Used when a response was correctly downloaded 
            RconResponse rr = new RconResponse();
            rr.type = _type;
            rr.id = _id;
            rr.body = Encoding.ASCII.GetString(_body);
            rr.status = RconResponseStatus.Ok;
            //If the message is "Keep Alive", then set the status.
            if(rr.body=="Keep Alive")
            {
                rr.status = RconResponseStatus.KeepAliveError;
            }
            return rr;
        }

        public static RconResponse CreateBadResponse(RconResponseStatus status)
        {
            //Used when an item was created with a problem.
            RconResponse rr = new RconResponse();
            rr.status = status;
            if (status == RconResponseStatus.Ok) //What!? Wrong function
                throw new Exception("Incorrect status! You're creating a bad response, but you've passed an 'Ok' status in. Check the function you're using and try again.");
            //Return
            return rr;
        }

        public static RconResponse CreatFatalResponse(Exception _ex)
        {
            //Used when an item was created with an error.
            RconResponse rr = new RconResponse();
            rr.status = RconResponseStatus.FatalError;
            rr.ex = _ex;
            return rr;
        }
    }

    public enum RconResponseStatus
    {
        Ok,
        Timeout,
        Failed,
        ServerDisconnected,
        KeepAliveError,
        FatalError /*Fatal errors must be with an exception */
    }
}
