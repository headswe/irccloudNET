using System;

namespace IrcCloudApp
{
    public class Member
    {
        public String Nickname { get; set; }
        public String Realname { get; set; }
        public String Ircserver { get; set; }
        public String Mode { get; set; }
        public int Hops { get; set; }
        public bool Away { get; set; }
        public Usermask Mask { get; set; }

        public override string ToString()
        {
            return Nickname;
        }
    }

    public struct Usermask
    {
        public string Mask { get; set; }

        public String User { get; set; }
        public String Identprefix { get; set; }
        public String Userhost { get; set; }
    }
}