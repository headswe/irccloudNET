using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace IrcCloudApp
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class UserControl1
    {
       // ObservableCollection<Connection> connections = new ObservableCollection<Connection>();
 
        static IrcClient _client;
        private string UserName;
        private string PassWord;
        public UserControl1()
        {
          
            _client = new IrcClient(Dispatcher.CurrentDispatcher);
            
            Console.WriteLine(_client.Login(UserName, PassWord));
            _client.FetchConnections();
            InitializeComponent();
            Input.SpellCheck.IsEnabled = true;
           
         //   connections = new ObservableCollection<Connection>(client.connections.Values);
           // client.connectionAdded += client_connectionAdded;
            Tree.DataContext = _client.ObsConnections;
            
           // Chat.DataContext = client.connections.First().Value.buffers.First().Value.messages;
        }

        private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            //  TreeViewItem item = (TreeViewItem)sender;
            var value = e.NewValue as ChatBuffer;
            if (value == null) return;
            var buf = value;
            Chat.DataContext = buf.Messages;
            Members.DataContext = buf.Members;
            if (!buf.Archived) return;
            _client.GetBacklog(buf);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var buf = (ChatBuffer)Tree.SelectedItem;
                if (buf == null)
                    return;
                _client.SendMessage(buf.ConnectionId, buf.Name, Input.Text);
                Input.Text = "";
                buf.SortMessages();
            }
            
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
           
        }
    }
}
