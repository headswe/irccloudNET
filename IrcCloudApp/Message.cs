using System;

namespace IrcCloudApp
{
    public class Message
    {
        public int BufferId { get; set; }
        public string ChannelName { get; set; }
        public long Eid { get; set; }
        public string Text { get; set; }
        public string From { get; set; }
        public Usermask UserMask { get; set; }
        public string FromMode { get; set; }
        public bool Self { get; set; }
        public bool Highlight { get; set; }
        public bool Format = true;
        public override string ToString()
        {
            DateTime time = new DateTime(1970, 01, 01, 0, 0, 0, 0);
            time = time.AddTicks((Eid * 1000) / 100);
            // time.ToLocalTime();
            if (Format)
                return time.ToString("hh:mm") + " " + From + " : " + Text;
            return time.ToString("hh:mm") + " " + Text;
        }
    }
}
