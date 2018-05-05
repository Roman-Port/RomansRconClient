using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel;

namespace RomansRconClient
{
    public class RconConnection
    {
        public int connectionID; //Used like a token.
        public string serverIP;
        public Int32 serverPort;
        public string serverPassword;

        //Networking
        private TcpClient client;
        private NetworkStream stream;
        private BinaryWriter networkWriter;
        private BinaryReader networkReader;

        public static RconConnection ConnectToRcon(string ip, Int32 port, string password)
        {
            //Main "constructor"
            //Create the object
            RconConnection rc = new RconConnection();
            //Generate an ID
            rc.connectionID = GenerateID();
            //Set some values
            rc.serverIP = ip;
            rc.serverPort = port;
            rc.serverPassword = password;
            //Prepare network
            rc.PrepareNetworking();
            //Connect and authorize
            rc.PrivateSendPacket(RconPacketType.SERVERDATA_AUTH, password); //Send a packet with the auth type. Include the password.
            //Used for testing
            
            return rc;
        }

        //<Friendly functions>
        [Description("Send an RCON command to the server."), Category("Main Action")]
        public RconResponse SendCommand(string cmd, int timeout=900)
        {
            return PrivateSendPacketAndGetResponse(RconPacketType.SERVERDATA_EXECCOMMAND_OR_SERVERDATA_AUTH_RESPONSE, cmd, timeout);
        }


        //</Friendly functions>



        private void PrepareNetworking()
        {
            //Sets up the network clients.
            //Create TCP client
            client = new TcpClient(serverIP, serverPort);
            //Get the connection stream
            stream = client.GetStream();
            //Get another stream
            networkWriter = new BinaryWriter(stream);
            networkReader = new BinaryReader(stream);
        }

        public void DisposeNetworking()
        {
            //Disconnect
            networkWriter.Close();
            networkReader.Close();
            stream.Close();
            client.Close();
        }

        private static int GenerateID()
        {
            Random rand = new Random();
            return 34;
            return rand.Next(1, int.MaxValue);
        }

        private void PrivateSendPacket(RconPacketType type, string body)
        {
            //Create the packet.
            RconPacket packet = new RconPacket(connectionID, type, body);
            
            //Get the byte data from our packet.
            byte[] rawData = packet.CreatePacket();
            //Send it over the network stream
            networkWriter.Write(rawData);
            networkWriter.Flush();
            //The data has been sent.
        }

        private RconResponse PrivateSendPacketAndGetResponse(RconPacketType type, string body, int timeoutMs = 900, bool reconnectOnFail = true)
        {
            //This is a bit gross. I should find a way around this.
            //We want to hang the thread while we search for the response.
            //If it takes too long, respond with a timeout.
            var task = Task.Run(() => PrivateTaskSendPacketAndGetResponse(type,body,timeoutMs));
            if (task.Wait(TimeSpan.FromMilliseconds(timeoutMs)))
            {
                return task.Result;
            }
            else
            {
                //Timeout.
                if(reconnectOnFail)
                {
                    //Try to reconnect.
                    DisposeNetworking();
                    PrepareNetworking();
                    Console.WriteLine("Reconnected");
                    //Resend command
                    return PrivateSendPacketAndGetResponse(type, body, timeoutMs, false);
                }
                return RconResponse.CreateBadResponse(RconResponseStatus.Timeout);
            }
        }

        private void ClearMainStream()
        {
            var buffer = new byte[4096];
            while (stream.DataAvailable)
            {
                stream.Read(buffer, 0, buffer.Length);
            }
        }

        private async Task<RconResponse> PrivateTaskSendPacketAndGetResponse(RconPacketType type, string body, int timeoutMs)
        {
            //Clear
            ClearMainStream();

            //Send
            PrivateSendPacket(type, body);

            DateTime start = DateTime.UtcNow; //Save the starting date so we can timeout.
            while (true)
            {
                //Keep trying to read from the stream.
                try
                {
                    int responseLength = networkReader.ReadInt32(); //Fetch the packet length. Then, read in.
                    int id = networkReader.ReadInt32(); //Read in the ID of the client that sent this. We should make sure this is ours.
                    RconPacketType responseType = (RconPacketType)networkReader.ReadInt32(); //Fetch packet type
                    //Read bytes in.
                    byte[] buffer = networkReader.ReadBytes(responseLength - 10); //Read the body in. Use the length minus the header length of 10.
                    //Read padding
                    networkReader.Read();
                    networkReader.Read();
                    

                    //Create a response.
                    RconResponse response = RconResponse.CreateOkayResponse(responseType, id, buffer);
                    //Return this
                    return response;
                }
                catch (Exception ex)
                {

                }
                //Some other error occured. Try again, but keep checking for a timeout.
                TimeSpan totalTime = DateTime.UtcNow - start;
                if (totalTime.TotalMilliseconds > timeoutMs)
                {
                    //Timeout. Create bad response.
                    return RconResponse.CreateBadResponse(RconResponseStatus.Timeout);
                }
            }
        }

    }

    class RconPacket
    {
        public int ID;
        public RconPacketType type;
        public string body;

        public RconPacket(int _id, RconPacketType _type, string _body)
        {
            //Main constructor
            ID = _id;
            type = _type;
            body = _body;
            //This does NOT send the packet.
        }

        public byte[] CreatePacket()
        {
            //This will convert our data into a packet ready to send to the server.
            //The packet is definied like this:
            //Size (int32) (Size of body + 10)
            //ID (int32) (Must be the same as the first request sent)
            //Type (int32) (Must be 0,2,3)
            //Body (ASCII string with null termination)
            //One byte string (ASCII with null termination)

            //Convert the body.
            byte[] bodyData = Encoding.ASCII.GetBytes(body);
            //Get the size of the packet. Add 10 to the size of the body data.
            int size = bodyData.Length + 10;
            //Get the byte-version of the ID, type, and size
            byte[] sizeData = IntToLEByte(size);
            byte[] idData = IntToLEByte(ID);
            byte[] typeData = IntToLEByte((int)type);
            //We now have all of the data required to create a packet. Create one.
            byte[] data = new byte[sizeData.Length + idData.Length + typeData.Length + bodyData.Length + 2];
            using(MemoryStream stream = new MemoryStream(data))
            {
                //Write this data.
                stream.Write(sizeData, 0, sizeData.Length);
                stream.Write(idData, 0, idData.Length);
                stream.Write(typeData, 0, typeData.Length);
                stream.Write(bodyData, 0, bodyData.Length);
                //Now add the last single byte
                byte[] buf = new byte[2];
                buf[0] = 0;
                buf[1] = 0;
                stream.Write(buf, 0, buf.Length);
            }
            //OK.
            //File.WriteAllBytes("E:\\" + ID.ToString() + body.Length.ToString(), data);
            return data;
        }

        public static byte[] IntToLEByte(int i)
        {
            //Use BitConverter.
            byte[] data = BitConverter.GetBytes(i);
            //Check if it's little endian
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(data);
            //Done.
            return data;
        }
    }

    public enum RconPacketType
    {
        SERVERDATA_RESPONSE_VALUE,
        NOT_USED,
        SERVERDATA_EXECCOMMAND_OR_SERVERDATA_AUTH_RESPONSE,
        SERVERDATA_AUTH
    }
}
