using Gma.System.MouseKeyHook;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWserver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Guid myUUID = new Guid("fa87c0d0-afac-11de-8a39-0800200c9a66");   //unique id, which the client identifies
        BluetoothListener btListener;
        BluetoothRadio myradio = BluetoothRadio.PrimaryRadio;
        Stream reader;
       // Stream sender;
        BluetoothClient btClient;
        
        bool disconFlag = false;
        private IKeyboardMouseEvents myGlobalHook;   //used to register mouse events beyond this application (use only if external editbox text required)

        
        private const int WM_SETTEXT = 0x000c;   //constants for the SendMessage API
        private const int WM_GETTEXT = 0x000d;

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, StringBuilder lParam); //in this app, used to get text from external edit box

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out System.Drawing.Point lpPoint);   //fetches mouse curosr position

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(System.Drawing.Point p);    //fetches handle at position got from GetCursorPos

        [DllImport("user32.dll")]
        static extern IntPtr ChildWindowFromPoint(IntPtr hWndParent, System.Drawing.Point Point);   //fetches child handles of parent handle

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize); //in this app, used for mouse functions.

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT                                    //structure to send in SendInput API
        {
            public SendInputEventType type;
            public DeviceUnion mkhi;
        }
        [StructLayout(LayoutKind.Explicit)]
        struct DeviceUnion
        {
            [FieldOffset(0)]
            public MouseInputData mi;
        }
 
        [StructLayout(LayoutKind.Sequential)]
        struct MouseInputData
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public MouseEventFlags dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [Flags]
        enum MouseEventFlags : uint
        {
            MOUSEEVENTF_MOVE = 0x0001,
            MOUSEEVENTF_LEFTDOWN = 0x0002,
            MOUSEEVENTF_LEFTUP = 0x0004,
            MOUSEEVENTF_RIGHTDOWN = 0x0008,
            MOUSEEVENTF_RIGHTUP = 0x0010,
            MOUSEEVENTF_MIDDLEDOWN = 0x0020,
            MOUSEEVENTF_MIDDLEUP = 0x0040,
            MOUSEEVENTF_XDOWN = 0x0080,
            MOUSEEVENTF_XUP = 0x0100,
            MOUSEEVENTF_WHEEL = 0x0800,
            MOUSEEVENTF_VIRTUALDESK = 0x4000,
            MOUSEEVENTF_ABSOLUTE = 0x8000
        }
        enum SendInputEventType : int
        {
            InputMouse,
            InputKeyboard,
            InputHardware
        }

        struct POINT
        {
            long x;
            long y;
        }

        public MainWindow()
        {
            InitializeComponent();
            status("Hello");
            while (myradio == null)                                     //checks if bluetooth on/off and only allows progress when bluetooth is switched on
            {
                myradio = BluetoothRadio.PrimaryRadio;

                if (myradio != null)
                {
                    start.IsEnabled = true;
                    disconnect.IsEnabled = false;
                    break;
                }
                else
                {
                    start.IsEnabled = false;
                    disconnect.IsEnabled = false;
                    System.Windows.MessageBox.Show("Turn on Bluetooth and Click OK", "Swalekh Wireless");

                }

            }
          
        }

        private void startListen_Click(object sender, RoutedEventArgs e)    //Click listener for start button
        {
            status("clicked start");

            startService();

            initializeConnection();
        }

        public void subscibeHook()      //initialises variable for global mouse click events
        {
            myGlobalHook = Hook.GlobalEvents();
            myGlobalHook.MouseDoubleClick += MyGlobalHook_MouseDoubleClick;
        }

        private void MyGlobalHook_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)        //registers global mouse click event
        {
            status("double clicked outside");
            //readTextBoxData();
            readsomedata();
            //throw new NotImplementedException();
        }

       
        private void unsubscribeHook()          //disposes the global hook
        {
            myGlobalHook.MouseDoubleClick -= MyGlobalHook_MouseDoubleClick;
            myGlobalHook.Dispose();
        }
        public void startService()          //called when start button is clicked and displays system bluetooth info
        {
            
            //subscibeHook();
            //status("hook active");
            start.IsEnabled = false;
            disconnect.IsEnabled = true;
            status("Name: " + myradio.Name);
            status("Address: " + myradio.LocalAddress);
        }

        private void initializeConnection()         //starts thread for bluetooth connection
        {
            disconFlag = false;
            status("server started");
            Thread btServerThread = new Thread(new ThreadStart(serverConThread));
            btServerThread.Start();
        }

        private void serverConThread()          //listenes for clients and connects to them and gets a data stream
        {
            //bool z = btListener.Pending();
            //status("" + z);
            btListener = new BluetoothListener(myUUID);
            btListener.Start();
            //bool y = btListener.Pending();
            
              //  status(""+ y);
            
            try
            {
                btClient = btListener.AcceptBluetoothClient();
            }
            catch (Exception e)
            {
                status("no client was connected");
            }
            
            if (btClient!= null)             
            {
                //bool x = btListener.Pending();
                //status("" + x);
                btListener.Stop();
                btListener = null;
                status("stopped listening to connections");
                reader = btClient.GetStream();
                //sender = btClient.GetStream();
                //readTextBoxData();
                //senddata(reader);
                readdata(reader);
                //senddata(reader);
            }
            else
            {
                discon();
            }

            //   chooseWindow();
        }


        private void readsomedata()             //used to read data from editboxes outside of this application, procs when the edit box is double clicked
        {
            StringBuilder builder = new StringBuilder(500);
            System.Drawing.Point p;
            //POINT p;
            GetCursorPos(out p);
            IntPtr handle = WindowFromPoint(p);
            status("phandle:" + handle);
            IntPtr childHandle = ChildWindowFromPoint(handle, p);
            status("chandle:" + childHandle);
            SendMessage(childHandle, WM_GETTEXT, builder.Capacity, builder);
            if (builder.Length == 0)
            {

                SendMessage(handle, WM_GETTEXT, builder.Capacity, builder);
                status("handle in if " + handle);
                status("text got: " + builder.ToString());
            }
            else
            {

                status("handle in else " + childHandle);
                status("text got: " + builder.ToString());
            }
            //status("handle " + childHandle);
            //status("text got: " + builder.ToString());

        }


        private void senddata(Stream sender)
        {
            string send = "hello world";
            Byte[] s = new Byte[15];
            s = Encoding.UTF8.GetBytes(send);
            sender.Write(s, 0, s.Length);
        }

        private void readdata(Stream reader)                //reads data sent from client and behaves according to type of input
        {

            string result = "";
            int count;
            MemoryStream ms = new MemoryStream();
            //Stream ms;
            Byte[] b = new Byte[50];
            Byte[] read = new Byte[1024];
            while (disconFlag == false)
            {
                try
                {
                    count = reader.Read(read, 0, read.Length);
                    ms.Write(read, 0, count);
                    b = ms.ToArray();
                    result = Encoding.UTF8.GetString(b);
                    b = null;
                    status("rec: " + result);
                    //Random ran = new Random();
                    if (result.StartsWith("!?"))                    
                    {

                        string newstr = result.Substring(2);
                        string[] coordinates = newstr.Split(',');
                        int x = int.Parse(coordinates[0]);
                        int y = int.Parse(coordinates[1]);
                        MouseMovement(x, y);
                    }
                    else if (result == "880")
                    {
                        mouzLeftClick();
                        //mouzRightClick();
                    }
                    else if (result == "900")
                    {
                        //mouzLeftClick();
                        mouzRightClick();
                    }
                    else if (result == "882")
                    {
                        mouzDoubleClick();
                    }
                    else                                //keyboard data processing
                    {
                        sendDataExternal(result);
                    }

                   
                    result = "";
                    ms = new MemoryStream(ms.Capacity);

                }
                catch (IOException e)
                {
                    reader.Close();
                    //status("");
                }

                

            }
            //reader.Close();

        }

        private void sendDataExternal(string result)        
        {
            string res;
            //SetForegroundWindow(chandler);
            switch (result)
            {
                case "+":
                    res = "{+}";
                    break;
                case "^":
                    res = "{^}";
                    break;
                case "%":
                    res = "{%}";
                    break;
                case "~":
                    res = "{~}"; 
                    break;
                case "{":
                    res = "{{}";
                    break;
                case "}":
                    res = "{}}";
                    break;
                case "(":
                    res = "{(}";
                    break;
                case ")":
                    res = "{)}";
                    break;
                case "[":
                    res = "{[}";
                    break;
                case "]":
                    res = "{]}";
                    break;
                default:
                    res = result;
                    break;
            }
            SendKeys.SendWait(res);             //function to display keystrokes at present caret position
        }

        private void mouzLeftClick()            //mouse left click function
        {
            INPUT mouseInputdn = new INPUT();
            // mouseInput.type = SendInputEventType.InputMouse;
            //mouseInput.mkhi.mi.dx = x;
            //mouseInput.mkhi.mi.dy = y;
             mouseInputdn.mkhi.mi.mouseData = 0;
            //mouseInput.mouseData = 0;
            mouseInputdn.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTDOWN;
            //mouseInput.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTDOWN;
            SendInput(1, ref mouseInputdn, Marshal.SizeOf(new INPUT()));

            INPUT mouseInputup = new INPUT();
            mouseInputup.mkhi.mi.mouseData = 0;
            mouseInputup.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTUP;
            //mouseInput.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTUP;
            SendInput(1, ref mouseInputup, Marshal.SizeOf(new INPUT()));
        }

        private void mouzDoubleClick()          //mouse double click function
        {
            INPUT mouseInputdn = new INPUT();
            mouseInputdn.mkhi.mi.mouseData = 0;
            mouseInputdn.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTDOWN;
            SendInput(1, ref mouseInputdn, Marshal.SizeOf(new INPUT()));

            INPUT mouseInputup = new INPUT();
            mouseInputup.mkhi.mi.mouseData = 0;
            mouseInputup.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTUP;
            SendInput(1, ref mouseInputup, Marshal.SizeOf(new INPUT()));

            SendInput(1, ref mouseInputdn, Marshal.SizeOf(new INPUT()));
            SendInput(1, ref mouseInputup, Marshal.SizeOf(new INPUT()));
        }

        private void mouzRightClick()       //mouse right click function
        {
            INPUT mouseInput = new INPUT();
            // mouseInput.type = SendInputEventType.InputMouse;
            //mouseInput.mkhi.mi.dx = x;
            //mouseInput.mkhi.mi.dy = y;
            mouseInput.mkhi.mi.mouseData = 0;

            mouseInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_RIGHTDOWN;
            SendInput(1, ref mouseInput, Marshal.SizeOf(new INPUT()));

            mouseInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_RIGHTUP;
            SendInput(1, ref mouseInput, Marshal.SizeOf(new INPUT()));
        }

        private void MouseMovement(int x, int y)           // mouse movement function
        {
            INPUT mouseInput = new INPUT();
            // mouseInput.type = SendInputEventType.InputMouse;
            mouseInput.mkhi.mi.dx = x;
            mouseInput.mkhi.mi.dy = y;
            mouseInput.mkhi.mi.mouseData = 0;

            
            System.Drawing.Point p;
            //if (GetCursorPos(out p)) ;
            // SetCursorPos(x, y);


            // mouseInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTDOWN;
            mouseInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_MOVE;
            //mouseInput.dwFlags = MouseEventFlags.MOUSEEVENTF_MOVE;
            SendInput(1, ref mouseInput, Marshal.SizeOf(new INPUT()));

            // mouseInput.mkhi.mi.dwFlags = MouseEventFlags.MOUSEEVENTF_LEFTUP;
            // SendInput(1, ref mouseInput, Marshal.SizeOf(new INPUT()));

           
            // SetCursorPos(p.X, p.Y);
        }

        private delegate void updateUICallback(string message);
        private void updateUI(string message)               
        {
            infoBox.AppendText(message + System.Environment.NewLine);


        }
        private void status(string message)             //used for displaying text on the UI
        {
            infoBox.Dispatcher.Invoke(new updateUICallback(this.updateUI), new object[] { message });
        }

        private void disconnect_Click(object sender, RoutedEventArgs e)     //disconnect button click listener
        {
            status("Disconnected");
            disconFlag = true;
            discon();
        }

        private void send_Click(object sender, RoutedEventArgs e)
        {
            senddata(reader);
        }

        private void discon()           ///disposes all resources used
        {
            
            //unsubscribeHook();
            if (btListener != null)
            {
                btListener.Stop();
                btListener = null;
            }

            if (reader != null)
            {
                reader.Close();
                reader = null;
            }

            if (btClient != null)
            {
                btClient.Dispose();
                btClient = null;
            }

            this.Dispatcher.Invoke((Action)(() =>
            {
                start.IsEnabled = true;
                disconnect.IsEnabled = false;
            }));

        }
    }
}
