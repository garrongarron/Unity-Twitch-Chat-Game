using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TwitchIRC : MonoBehaviour
{
    System.IO.StreamWriter output;
    public string oauth;
    public string nickName;
    public string channelName;
    private string server = "irc.twitch.tv";
    private int port = 6667;

    //event(buffer).
    public class MsgEvent : UnityEngine.Events.UnityEvent<string> { }
    public MsgEvent messageRecievedEvent = new MsgEvent();

    private string buffer = string.Empty;
    private bool stopThreads = false;
    private Queue<string> commandQueue = new Queue<string>();
    private List<string> recievedMsgs = new List<string>();
    private System.Threading.Thread inProc, outProc;
    
    private void StartIRC()
    {
        System.Net.Sockets.TcpClient sock = new System.Net.Sockets.TcpClient();
        sock.Connect(server, port);
        if (!sock.Connected)
        {
            Debug.Log("Failed to connect!");
            return;
        }
        var networkStream = sock.GetStream();
        var input = new System.IO.StreamReader(networkStream);
        output = new System.IO.StreamWriter(networkStream);

        //Send PASS & NICK.
        output.WriteLine("PASS " + oauth);
        output.WriteLine("NICK " + "SamuBot" );
        output.WriteLine("USER " + "SamuBot" + " 8 * :" + "SamuBot");
        output.Flush();

        //output proc
        outProc = new System.Threading.Thread(() => IRCOutputProcedure(output));
        outProc.Start();
        //input proc
        inProc = new System.Threading.Thread(() => IRCInputProcedure(input, networkStream));
        inProc.Start();
    }

    private void IRCInputProcedure(System.IO.TextReader input, System.Net.Sockets.NetworkStream networkStream)
    {
        while (!stopThreads)
        {
            if (!networkStream.DataAvailable)
                continue;

            buffer = input.ReadLine();
            
            //was message?
            if (buffer.Contains("PRIVMSG #"))
            {
                lock (recievedMsgs)
                {
                    recievedMsgs.Add(buffer);
                }
            }

            //TODO: change the way you check for command
            if (buffer.Contains(":!"))
            {
                SendMsg(" answer to the command ");
            }

            //Send pong reply to any ping messages
            if (buffer.StartsWith("PING "))
            {
                SendCommand(buffer.Replace("PING", "PONG"));
            }

            // After server sends 001 command, we can join a channel
            if (buffer.Split(' ')[1] == "001")
            {
                SendCommand("JOIN #" + channelName.ToLower());
            }
        }
    }


    private void IRCOutputProcedure(System.IO.TextWriter output)
    {
        System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        stopWatch.Start();
        while (!stopThreads)
        {
            lock (commandQueue)
            {
                if (commandQueue.Count > 0) //do we have any commands to send?
                {
                    //have enough time passed since we last sent a message/command?
                    if (stopWatch.ElapsedMilliseconds > 1750)
                    {
                        //send msg.
                        output.WriteLine(commandQueue.Peek());
                        output.Flush();
                        //remove msg from queue.
                        commandQueue.Dequeue();
                        //restart stopwatch.
                        stopWatch.Reset();
                        stopWatch.Start();
                    }
                }
            }
        }
    }

    public void SendCommand(string cmd)
    {
        lock (commandQueue)
        {
            commandQueue.Enqueue(cmd);
        }
    }
    public void SendMsg(string msg)
    {
        lock (commandQueue)
        {
            commandQueue.Enqueue("PRIVMSG #" + channelName.ToLower() + " :" + msg);
        }
    }

    //MonoBehaviour Events.
    void Start()
    {
        messageRecievedEvent.AddListener(Show);
    }

    public void Show(string line){
        string[] split = line.Split(' ');
        int exclamationPointPosition = split[0].IndexOf("!");
        string username = split[0].Substring(1, exclamationPointPosition - 1);
        //Skip the first character, the first colon, then find the next colon
        int secondColonPosition = line.IndexOf(':', 1);//the 1 here is what skips the first character
        string message = line.Substring(secondColonPosition + 1);//Everything past the second colon
        Debug.Log($"{username} said '{message}'");
    }

    void OnEnable()
    {
        stopThreads = false;
        StartIRC();
    }
    void OnDisable()
    {
        stopThreads = true;
        //while (inProc.IsAlive || outProc.IsAlive) ;
        //print("inProc:" + inProc.IsAlive.ToString());
        //print("outProc:" + outProc.IsAlive.ToString());
    }
    void OnDestroy()
    {
        stopThreads = true;
        //while (inProc.IsAlive || outProc.IsAlive) ;
        //print("inProc:" + inProc.IsAlive.ToString());
        //print("outProc:" + outProc.IsAlive.ToString());
    }
    void Update()
    {
        lock (recievedMsgs)
        {
            if (recievedMsgs.Count > 0)
            {
                for (int i = 0; i < recievedMsgs.Count; i++)
                {
                    messageRecievedEvent.Invoke(recievedMsgs[i]);
                    // Debug.Log(recievedMsgs[i]);  
                }
                recievedMsgs.Clear();
            }
        }
    }
}
