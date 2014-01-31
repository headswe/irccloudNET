using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Threading;
namespace IrcCloudApp
{
    public class Connection
    {
        public int ConnectionId = -1;
        public String HostName = "";
        public int Port = 222;

        // SSL SETTING?
        public Dictionary<int, ChatBuffer> Buffers { get; set; }
        public ObservableCollection<ChatBuffer> ObsBuffers { get; set; }
        readonly Dispatcher _dispatcher;
        public Connection(Dispatcher disp)
        {
            _dispatcher = disp;
            Buffers = new Dictionary<int, ChatBuffer>();
            ObsBuffers = new ObservableCollection<ChatBuffer>();
        }
        public void AddChannel(int bid,ChatBuffer buf)
        {
            Buffers[bid] = buf;
            _dispatcher.Invoke((() => ObsBuffers.Add(buf)));
            
            SortChannels();
        }
        public void SortChannels()
        {
            _dispatcher.Invoke((() =>
            {
                var msg = (ObsBuffers.OrderBy(o => o.Archived).ToList());
                ObsBuffers.Clear();
                foreach (var m in msg)
                {
                    ObsBuffers.Add(m);
                }
            }));
        }
        public void RemoveChannel(int bid, ChatBuffer buf)
        {
            Buffers.Remove(bid);
            _dispatcher.Invoke((new Action(() => ObsBuffers.Remove(buf))));
            SortChannels();
        }
        public bool UseSsl { get; set; }

        public String Name { get; set; }

        public String Nick { get; set; }

        public String Realname { get; set; }

        public String Password { get; set; }

        public String NickservPassword { get; set; }

        public String UserMask { get; set; }

        public String UserHost { get; set; }

        public String User { get; set; }

        public String IdentPrefix { get; set; }

        public String IrcServer { get; set; }

        public dynamic FailInfo { get; set; }

        public String Status { get; set; }

        public String Away { get; set; }

        public List<String> Ignores { get; set; }

        public String JoinCommands { get; set; }
    }
}