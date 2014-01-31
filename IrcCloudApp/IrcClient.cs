using System.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Threading;
using WebSocket4Net;

namespace IrcCloudApp
{
    public class IrcClient
    {
        public Dictionary<int, Connection> Connections = new Dictionary<int, Connection>();
        private bool _backlogProcessing;
        private readonly List<KeyValuePair<string, string>> _cookies = new List<KeyValuePair<string, string>>();
        private int _idleInterval;
        private Boolean _isConnected;
        private Queue<String> _messageBacklog = new Queue<string>();
        private String _sessionId = "";
        private WebSocket _socket;

        // TODO: Catch expections
        public ObservableCollection<Connection> ObsConnections = new ObservableCollection<Connection>();

        private string _streamid;
        private readonly Dispatcher _dispatcher;

        public delegate void ConnectionAdded(object sender, EventArgs e);

        public event ConnectionAdded connectionAdded;

        // header
        private int _time;

        public IrcClient(Dispatcher disp)
        {
            _dispatcher = disp;
        }

        public void FetchConnections()
        {
            if (!_isConnected)
                throw new Exception("Not connected");
            _socket = new WebSocket("wss://www.irccloud.com/", "", _cookies, null, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)", "https://www.irccloud.com");
            _socket.MessageReceived += socket_MessageReceived;
            _socket.Open();
        }

        ///
        public Boolean Login(String email, String password)
        {
            email = HttpUtility.UrlEncode(email);
            password = HttpUtility.UrlEncode(password);

            var request = (HttpWebRequest)WebRequest.Create(@"https://www.irccloud.com/chat/login");
            // set inital properties
            request.KeepAlive = false;
            request.Method = "POST";
            request.Referer = @"https://www.irccloud.com";
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
            request.ContentType = "application/x-www-form-urlencoded;";
            // ready the data to send.
            string data = "email=" + email + "&password=" + password;
            var byteA = Encoding.UTF8.GetBytes(data);
            request.ContentLength = byteA.Length;
            Stream dStream = request.GetRequestStream();
            dStream.Write(byteA, 0, byteA.Length);
            dStream.Close();
            // sent now lets fetch the data..
            var resp = (HttpWebResponse)request.GetResponse();

            // More streams!
            dStream = resp.GetResponseStream();
            Debug.Assert(dStream != null, "dStream != null");
            var reader = new StreamReader(dStream);
            string respData = reader.ReadToEnd();

            // close this stuff down.
            reader.Close();
            dStream.Close();
            resp.Close();

            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(respData);
            if (dict["success"] == "false")
                return false;
            _sessionId = dict["session"];
            _cookies.Add(new KeyValuePair<string, string>("session", _sessionId));
            _isConnected = true;
            return true;
        }

        public void SendMessage(int cid, string channel, string message)
        {
            var dict = new Dictionary<string, dynamic>
            {
                {"_reqid", 1},
                {"_method", "say"},
                {"cid", cid},
                {"to", channel},
                {"msg", message}
            };
            string data = JsonConvert.SerializeObject(dict);
            _socket.Send(data);
        }

        public HttpWebResponse SendRequest(String url, String type = "POST", String data = "", List<String> headers = null)
        {
            var socket = (HttpWebRequest)WebRequest.Create(url);

            // set inital properties
            socket.Method = type;
            socket.Referer = @"https://www.irccloud.com";
            socket.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
            socket.ContentType = "application/x-www-form-urlencoded;";
            socket.CookieContainer = new CookieContainer(1);
            if (headers != null)
            {
                foreach (var s in headers)
                    socket.Headers.Add(s);
            }
            socket.CookieContainer.Add(new Cookie("session", _sessionId, "/", "www.irccloud.com"));
            socket.Host = "www.irccloud.com";
            var resp = (HttpWebResponse)socket.GetResponse();
            return resp;
        }

        private void BufferMsg(Dictionary<string, dynamic> dict, bool isMe = false)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            var m = new Message { BufferId = (int)dict["bid"], ChannelName = dict["chan"], Eid = (long)dict["eid"], Text = dict["msg"], From = dict["from"] };
            buf.AddMessage(m, isMe);
        }

        private void ChannelInit(Dictionary<string, dynamic> dict)
        {
            int bid = (int)dict["bid"];
            int cid = (int)dict["cid"];
            Connection currCon = Connections[cid];

            if (!(currCon.Buffers[bid] is ChannelBuffer))
            {
                Console.WriteLine("Buffer not channel");
                return;
            }
            ChannelBuffer buffer = (ChannelBuffer)currCon.Buffers[bid];
            buffer.Name = dict["chan"];
            foreach (var member in dict["members"])
            {
                Member m = new Member { Nickname = member["nick"], Realname = member["realname"], Ircserver = member["ircserver"], Mode = member["mode"], Away = member["away"], Mask = new Usermask { Identprefix = member["ident_prefix"], User = member["user"], Userhost = member["userhost"], Mask = member["usermask"] } };
                buffer.AddMember(m);
            }
            buffer.Mode = dict["mode"];
            Topic topic = new Topic { Text = dict["topic"]["text"], Author = dict["topic"]["nick"], Time = dict["topic"]["time"], Mask = new Usermask { Identprefix = dict["topic"]["ident_prefix"], User = dict["topic"]["user"], Userhost = dict["topic"]["userhost"], Mask = dict["topic"]["usermask"] } };
            buffer.Topic = topic;
            DateTime time = new DateTime(1970, 01, 01, 0, 0, 0, 0);
            buffer.Timestamp = time.AddSeconds(Math.Max(0, dict["timestamp"]));
            buffer.ChannelType = dict["channel_type"];
            buffer.ChannelUrl = dict["url"];
        }

        private void JoinedChannel(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            long eid = dict["eid"];
            Member m = new Member { Nickname = dict["nick"], Mask = new Usermask { Identprefix = dict["ident_prefix"], User = dict["from_name"], Userhost = dict["from_host"], Mask = dict["hostmask"] } };
            buf.AddMessage(new Message { ChannelName = buf.Name, Eid = eid, From = m.Nickname, Text = m.Nickname + " has joined the channel..", Format = false });
            buf.AddMember(m);
        }

        private void MakeBuffer(Dictionary<string, dynamic> dict)
        {
            int cid = (int)dict["cid"];
            Connection con = Connections[cid];
            ChatBuffer buffer;
            if (dict["buffer_type"] == "channel")
            {
                buffer = new ChannelBuffer(_dispatcher,cid) { ConnectionId = (int)dict["cid"], Bufferid = (int)dict["bid"], BufferType = dict["buffer_type"], Name = dict["name"], Deferred = dict["deferred"], TimeOut = dict["timeout"], Archived = dict["archived"], MinEid = dict["min_eid"], Created = (int)dict["created"], LastSeenEid = dict["last_seen_eid"] };
            }
            else
            {
                buffer = new ChatBuffer(_dispatcher,cid) { ConnectionId = (int)dict["cid"], Bufferid = (int)dict["bid"], BufferType = dict["buffer_type"], Name = dict["name"], Deferred = dict["deferred"], TimeOut = dict["timeout"], Archived = dict["archived"], MinEid = dict["min_eid"], Created = (int)dict["created"], LastSeenEid = dict["last_seen_eid"] };
            }
            con.AddChannel((int)dict["bid"], buffer);


            Console.WriteLine("Added new buffer " + buffer.Name + " to #" + con.ConnectionId);
        }

        private void MakeServer(Dictionary<string, dynamic> dict)
        {
            Connection newCon = new Connection(_dispatcher)
            {
                ConnectionId = (int) dict["cid"],
                HostName = dict["hostname"],
                Port = (int) dict["port"],
                UseSsl = dict["ssl"],
                Name = dict["name"],
                Nick = dict["nick"],
                Realname = dict["realname"]
            };
            if (dict.ContainsKey("password"))
                newCon.Password = dict["password"];
            newCon.NickservPassword = dict["nickserv_pass"];
            newCon.JoinCommands = dict["join_commands"];
            var j = (Newtonsoft.Json.Linq.JArray)dict["ignores"];
            newCon.Ignores = null;
            newCon.Away = (String)dict["away"];
            newCon.Status = dict["status"];
            newCon.FailInfo = dict["fail_info"];
            newCon.IrcServer = dict["ircserver"];
            newCon.IdentPrefix = dict["ident_prefix"];
            newCon.User = dict["user"];
            newCon.UserHost = dict["userhost"];
            newCon.UserMask = dict["usermask"];
            Connections.Add(newCon.ConnectionId, newCon);
            if (connectionAdded != null)
                connectionAdded(newCon, new EventArgs());
            _dispatcher.Invoke(() => ObsConnections.Add(newCon));
            Console.WriteLine("Added new connection #" + newCon.ConnectionId);
        }

        private void NickChange(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            String newNick = dict["newnick"];
            String oldNick = dict["oldnick"];
            long eid = dict["eid"];
            buf.ChangeMemberNick(oldNick, newNick, eid);
        }

        private void Quit(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            String message = dict["msg"];
            Member m = new Member { Nickname = dict["nick"], Mask = new Usermask { Identprefix = dict["ident_prefix"], User = dict["from_name"], Userhost = dict["from_host"], Mask = dict["hostmask"] } };
            buf.RemoveMember(m, message, dict["eid"]);
        }

        private void socket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {// e.Message.Contains("stat_user") ||
            if (!e.Message.Contains("type"))
                return;
            Dictionary<String, dynamic> dict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(e.Message);
            if (!dict.ContainsKey("type"))
                return;
            String type = dict["type"];
            switch (type)
            {
                case "header":
                    _time = (int)dict["time"];
                    _idleInterval = (int)dict["idle_interval"];
                    _streamid = (string)dict["streamid"];
                    break;

                case "oob_include":
                    var request = SendRequest("https://www.irccloud.com" + dict["url"], "GET", "", new List<string> { "Accept-Encoding: gzip" });
                    _backlogProcessing = true;
                    var dStream = request.GetResponseStream();
                    byte[] bytes;
                    using (var memstream = new MemoryStream())
                    {
                        dStream.CopyTo(memstream);
                        bytes = memstream.ToArray();
                    }

                    var data = Ionic.Zlib.GZipStream.UncompressString(bytes);
                    var output = data.Split(new[] { ",\n", "\n", "\n]", "[\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    foreach (var msg in output)
                    {
                        socket_MessageReceived(sender, new MessageReceivedEventArgs(msg));
                    }
                    break;

                case "makeserver":
                    MakeServer(dict);
                    break;

                case "makebuffer":
                    MakeBuffer(dict);
                    break;

                case "channel_init":
                    ChannelInit(dict);
                    break;

                case "buffer_msg":
                    BufferMsg(dict);
                    break;

                case "buffer_me_msg":
                    BufferMsg(dict, true);
                    break;

                case "nickchange":
                    NickChange(dict);
                    break;

                case "you_nickchange":
                    NickChange(dict);
                    break;

                case "joined_channel":
                    JoinedChannel(dict);
                    break;

                case "you_joined_channel":
                    JoinedChannel(dict);
                    break;

                case "parted_channel":
                    PartedChannel(dict);
                    break;

                case "you_parted_channel":
                    PartedChannel(dict);
                    break;

                case "kicked_channel":
                    UserKicked(dict);
                    break;

                case "backlog_starts":
                    _backlogProcessing = true;
                    break;

                case "backlog_complete":
                    break;

                case "user_channel_mode":
                    UserChannelMode(dict);
                    break;
                case "channel_mode":
                    ChannelMode(dict);
                    break;
                case "channel_mode_is":
                    ChannelMode(dict);
                    break;
                case "channel_timestamp":
                    ChannelTimestamp(dict);
                    break;
                case "quit":
                    Quit(dict);
                    break;
                case "quit_server":
                    QuitServer(dict);
                    break;
                default:
                    using (var file = new StreamWriter(@"log.txt", true, Encoding.Default))
                    {
                        file.WriteLine(type);
                    }
                    break;
                    
            }
        }

        private void QuitServer(Dictionary<string, dynamic> dict)
        {
            // todo ? something.
        }

        public void GetBacklog(ChatBuffer buf)
        {
            var bid = Connections[buf.ParentConnection].Buffers.FirstOrDefault(x => x.Value == buf).Key;
            var request = SendRequest("https://www.irccloud.com" + "/chat/backlog?cid="+buf.ParentConnection+"&bid="+bid+"&num="+50, "GET", "", new List<string> { "Accept-Encoding: gzip" });
            _backlogProcessing = true;
            var dStream = request.GetResponseStream();
            byte[] bytes;
            using (var memstream = new MemoryStream())
            {
                Debug.Assert(dStream != null, "dStream != null");
                dStream.CopyTo(memstream);
                bytes = memstream.ToArray();
            }

            var data = Ionic.Zlib.GZipStream.UncompressString(bytes);
            var output = data.Split(new[] { ",\n", "\n", "\n]",", \n", "[\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var msg in output)
            {
                socket_MessageReceived(this, new MessageReceivedEventArgs(msg));
            }
        }
        private void ChannelTimestamp(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
           
            long eid = dict["eid"];
            DateTime time = new DateTime(1970, 01, 01, 0, 0, 0, 0);
            buf.Timestamp = time.AddSeconds(Math.Max(0, dict["timestamp"])); 
        }

        private void ChannelMode(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            long eid = dict["eid"];
            string @from = dict.ContainsKey("from") ? dict["from"] : dict["server"];
            string newmode = dict["newmode"];
            string diff = dict["diff"];
            buf.SetMode(eid, from, newmode,diff);
        }

        private void UserKicked(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            long eid = dict["eid"];
            String kicker = dict["kicker"];
            String msg = dict["msg"];
            String kicked = dict["nick"];
            Usermask mask = new Usermask { Identprefix = dict["kicker_ident_prefix"], User = dict["kicker_name"], Userhost = dict["kicker_host"], Mask = dict["kicker_hostmask"] };
            buf.KickMember(kicker, msg, kicked, mask, eid);
        }

        private void PartedChannel(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            long eid = dict["eid"];
            string nick = dict["nick"];
            string msg = dict["msg"];
            buf.MemberLeft(eid, nick, msg);
        }

        private void UserChannelMode(Dictionary<string, dynamic> dict)
        {
            Connection con = Connections[(int)dict["cid"]];
            ChatBuffer buf = con.Buffers[(int)dict["bid"]];
            string @from = dict.ContainsKey("from") ? dict["from"] : dict["server"];
            String nick = dict["nick"];
            String mode = dict["diff"];
            Usermask fromMask = new Usermask();
            if (dict.ContainsKey("from"))
                fromMask = new Usermask { Identprefix = dict["ident_prefix"], User = dict["from_name"], Userhost = dict["from_host"], Mask = dict["hostmask"] };
            Usermask targetMask = new Usermask { Identprefix = dict["target_ident_prefix"], User = dict["target_name"], Userhost = dict["target_host"], Mask = dict["target_hostmask"] };

            buf.SetUserMode(from, fromMask, nick, mode, targetMask);
        }
    }
}