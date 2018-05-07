using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RomansRconClient;
using System.Threading;

namespace RomansRconClientExample
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                ArkChatTest();
            }).Start();
            Console.ReadLine();s
            return;*/

            RconConnection rc = RconConnection.ConnectToRcon("10.0.1.13", 27020, ""); //127.0.0.1
            Console.WriteLine("connected");
            while (true)
            {
                string msg = Console.ReadLine();
                if (msg == "die")
                    break;
                RconResponse rr = rc.SendCommand(msg);
                Console.WriteLine("sent");
                Console.WriteLine("response status: " + rr.status.ToString());
                Console.WriteLine("response: " + rr.body);
            }


            rc.DisposeNetworking();
            Console.WriteLine("disconnected");
            Console.ReadLine();
        }

        static void ArkChatTest()
        {
            //Connect, then spam.
            RconConnection rc = RconConnection.ConnectToRcon("10.0.1.13", 27020, "");
            Console.WriteLine("connected");
            Random rand = new Random();
            int good = 0;
            int bad = 0;
            while(true)
            {
                Thread.Sleep(300);
                string uuid = RandomString(24,rand);
                string msg = "ServerChat This is a test. Please ignore. APP_ENDORANCE_TEST_ID_"+uuid;
                //Send chat
                rc.SendCommand(msg);
                RconResponse rr = rc.SendCommand("GetChat");
                bool ok = rr.status == RconResponseStatus.Ok;
                if(ok)
                {
                    //Check to see if it contains it
                    ok = rr.body.Contains(uuid);
                }
                if (!ok)
                {
                    Console.Write("\r" + rr.body + "\n");
                }
                Console.Write("\rGot message " + uuid + " - Status: " + ok + " - RawStatus: " + rr.status.ToString() + " - OK: " + good.ToString() + " - Bad: " + bad.ToString());
                if (ok)
                    good++;
                else
                    bad++;
                
            }
        }

        static string RandomString(int length, Random rand)
        {
            string o = "";
            while(o.Length<length)
            {
                o += rand.Next(0, 9).ToString();
            }
            return o;
        }
    }
}
