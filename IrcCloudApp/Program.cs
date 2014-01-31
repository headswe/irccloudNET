using System.Threading;

namespace IrcCloudApp
{
    class Program
    {
        
        
       // [STAThread]
        static void Main()
        {
            var thread = new Thread(TestRun);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        static void TestRun()
        {
            UserControl1 control = new UserControl1();
            control.ShowDialog();
        }
    }
}
