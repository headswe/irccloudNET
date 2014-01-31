using System;

namespace IrcCloudApp
{
    public class MessageEventArgs
    {
        public Message Message  {get;set;}
        public long Eid { get; set; }
    }
    public class MemberEventArgs
    {
        public Member Member { get; set; }
        public long Eid { get; set; }
    }
    public class MemberQuitEventArgs
    {
        public Member Member { get; set; }
        public String Message { get; set; }
        public long Eid { get; set; }
    }
    public class MemberNickChangedEventArgs
    {
        public Member Member { get; set; }
        public String OldNick { get; set; }
        public String NewNick { get; set; }
        public long Eid { get; set; }
    }
    public class MemberKickedEventArgs
    {
        public Member Kicked { get; set; }
        public Member Kicker {get;set;}
        public String Message { get; set; }
        public long Eid { get; set; }
    }
    public class MemberModeChangedEventArgs
    {
        public Member Member { get; set; }
        public String NewMode { get; set; }
        public String FromNick { get; set; }
        public Usermask FromMask { get; set; }
        public long Eid { get; set; }
    }
}
