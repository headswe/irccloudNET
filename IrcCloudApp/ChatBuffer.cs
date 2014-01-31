using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace IrcCloudApp
{
    public struct Topic
    {
        public string Author { get; set; }

        public Usermask Mask { get; set; }

        public string Text { get; set; }

        public int Time { get; set; }
    }

    public class ChatBuffer
    {
        public delegate void MemberAdded(object sender, MemberEventArgs e);

        public delegate void MemberKicked(object sender, MemberKickedEventArgs e);

        public delegate void MemberModeChanged(object sender, MemberModeChangedEventArgs e);

        public delegate void MemberNickChanged(object sender, MemberNickChangedEventArgs e);

        public delegate void MemberRemoved(object sender, MemberQuitEventArgs e);

        /// <summary>
        ///     Sends sender as a Message CLASS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void MessageAdded(object sender, MessageEventArgs e);

        public delegate void MessageMeAdded(object sender, MessageEventArgs e);

        private readonly Dispatcher _dispatcher;

        private readonly List<Member> _members = new List<Member>();

        private readonly List<Message> _messages = new List<Message>();
        public bool Archived = false;

        public bool Deferred = false;

        public ObservableCollection<Member> Members = new ObservableCollection<Member>();

        public ObservableCollection<Message> Messages = new ObservableCollection<Message>();
        public int ParentConnection = -1;
        public bool TimeOut = false;

        public ChatBuffer(Dispatcher disp, int cid)
        {
            _dispatcher = disp;
            ParentConnection = cid;
        }

        public int Bufferid { get; set; }

        public string BufferType { get; set; }

        public String ChannelType { get; set; }

        public String ChannelUrl { get; set; }

        public int ConnectionId { get; set; }
        public int Created { get; set; }

        public long LastSeenEid { get; set; }

        public long MinEid { get; set; }

        public String Mode { get; set; }

        public string Name { get; set; }
        public DateTime Timestamp { get; set; }

        public Topic Topic { get; set; }
        public event MemberAdded memberAdded;

        public event MemberKicked memberKicked;

        public event MemberModeChanged memberModeChanged;

        public event MemberNickChanged memberNickChanged;


        public event MemberRemoved memberRemoved;

        public event MessageAdded messageAdded;

        public event MessageAdded messageMeAdded;

        public void SortMessages()
        {
            _dispatcher.Invoke(() =>
            {
                List<Message> msg = (Messages.OrderBy(o => o.Eid).ToList());
                Messages.Clear();
                foreach (Message m in msg)
                {
                    Messages.Add(m);
                }
            });
        }

        public override string ToString()
        {
            return Name;
        }

        internal void AddMember(Member m)
        {
            if (_members.Find(x => x.Nickname == m.Nickname) != null)
                return;
            _members.Add(m);
            if (memberAdded != null)
                memberAdded(this, new MemberEventArgs {Member = m});

            _dispatcher.Invoke(() => Members.Add(m));
            SortMembers();
            //  Console.WriteLine(m.nickname+" joined " + name);
        }

        internal void AddMessage(Message m, bool isMe = false)
        {
            _messages.Add(m);
            _dispatcher.Invoke(() => Messages.Add(m));
            if (messageAdded != null && !isMe)
                messageAdded(this, new MessageEventArgs {Message = m});
            else if (messageMeAdded != null && isMe)
                messageMeAdded(this, new MessageEventArgs {Message = m});
            // Console.WriteLine("Message added to " + name);
        }

        internal void ChangeMemberNick(string oldNick, string newNick, long eid)
        {
            if (_members.Find(x => x.Nickname == newNick) != null)
                return;
            Member member = _members.Find(x => x.Nickname == oldNick);
            if (member == null)
                return;
            member.Nickname = newNick;
            SortMembers();
            if (memberNickChanged != null)
                memberNickChanged(this,
                    new MemberNickChangedEventArgs {Member = member, OldNick = oldNick, NewNick = newNick, Eid = eid});
            AddMessage(new Message
            {
                ChannelName = Name,
                Eid = eid,
                From = oldNick,
                Text = oldNick + " --> " + newNick,
                Format = false
            });
        }

        internal void KickMember(string kicker, string msg, string kicked, Usermask mask, long eid)
        {
            Member kickerMember = GetMember(kicker);
            Member kickedMember = GetMember(kicked);
            if (kickedMember == null)
                kickerMember = new Member {Nickname = kicker};
            if (kickedMember == null)
                kickedMember = new Member {Nickname = kicker};
            RemoveMember(kickedMember, "", -1, false);
            if (memberKicked != null)
                memberKicked(this,
                    new MemberKickedEventArgs {Eid = eid, Kicked = kickedMember, Kicker = kickerMember, Message = msg});
            AddMessage(new Message
            {
                ChannelName = Name,
                Eid = eid,
                From = kicker,
                Text = "has kicked " + kicked + " (" + msg + ")"
            });
        }

        internal void RemoveMember(Member m, string message, long eid = -1, bool _event = true)
        {
            Member member = _members.Find(x => x.Nickname == m.Nickname);
            if (member == null)
                return;
            _members.Remove(member);
            _dispatcher.Invoke(new Action(() => Members.Remove(m)));
            if (memberRemoved != null && _event)
                memberRemoved(this, new MemberQuitEventArgs {Member = m, Message = message, Eid = eid});
            SortMembers();
        }

        internal void SetMode(long eid, string from, string newmode, string diff)
        {
            Mode = newmode;
            // todo add eventhandlers.
        }

        internal void SetUserMode(string from, Usermask fromMask, string nick, string mode, Usermask targetMask)
        {
            Member member = _members.Find(x => x.Nickname == nick);
            if (member == null)
                return;
            member.Mode = mode;
            if (memberModeChanged != null)
                memberModeChanged(this,
                    new MemberModeChangedEventArgs
                    {
                        Member = member,
                        FromNick = nick,
                        FromMask = fromMask,
                        NewMode = mode
                    });
        }

        private Member GetMember(string nickname)
        {
            return _members.Find(x => x.Nickname == nickname);
        }

        private void SortMembers()
        {
            _dispatcher.Invoke(() =>
            {
                Members.Clear();
                foreach (Member m in _members)
                {
                    Members.Add(m);
                }
            });
        }

        internal void MemberLeft(long eid, string nick, string msg)
        {
            Member member = _members.Find(x => x.Nickname == nick);
            if (member == null)
                return;
            RemoveMember(member, nick, eid);
            AddMessage(new Message
            {
                ChannelName = Name,
                Eid = eid,
                From = nick,
                Text = nick + " has left the channel..",
                Format = false
            });
        }
    }
}