﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ClientStudentVer
{
    public partial class Form1 : Form
    {
        static char[] invalidChars =
            { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            '`', '~', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '+', '=',
            '{', '[', '}', ']', '\\', '|', ':', ';', '"', '\'', '<', '>', '?', ',', '.', '/',
            'á', 'à', 'ả', 'ã', 'ạ','Á', 'À', 'Ả', 'Ã', 'Ạ','í', 'ì', 'ỉ', 'ĩ', 'ị','Í', 'Ì', 'Ỉ', 'Ĩ', 'Ị',
            'ó', 'ò', 'ỏ', 'õ', 'ọ', 'Ó', 'Ò', 'Ỏ', 'Õ', 'Ọ', 'ú', 'ù', 'ủ', 'ũ', 'ụ','Ú', 'Ù', 'Ủ', 'Ũ', 'Ụ',
            'é', 'è', 'ẻ', 'ẽ', 'ẹ','É', 'È', 'Ẻ', 'Ẽ', 'Ẹ','ă','ắ', 'ặ', 'ẳ', 'ẵ', 'ằ','Ă','Ắ', 'Ặ', 'Ẳ', 'Ẵ', 'Ằ',
            'â', 'ấ', 'ậ', 'Â', 'Ấ', 'Ậ','ế', 'ề', 'ê', 'ể', 'ễ', 'ệ', 'Ế', 'Ề', 'Ê', 'Ể', 'Ễ', 'Ệ',
            'ơ', 'ớ', 'ờ', 'ở', 'ỡ','ợ','Ơ', 'Ớ', 'Ờ', 'Ở', 'Ỡ', 'Ợ','ô', 'ố', 'ồ', 'ổ', 'ỗ', 'ộ', 'Ô', 'Ố', 'Ồ', 'Ổ', 'Ỗ', 'Ộ',
            'ư', 'ứ', 'ừ', 'ử', 'ữ', 'ự','Ư', 'Ứ', 'Ừ', 'Ử', 'Ữ', 'Ự', 'đ', 'Đ'};

        TcpClient tcpClient;
        List<KeyValuePair<string, int>> serverIP_PortList = new List<KeyValuePair<string, int>>();
        NetworkStream stream;

        // Cert-related folders and components
        RSA PublicKey;
        static string CertSavedPath = @"..\\resources\\QuanNN.crt";
        static string encrFolder = @"..\\resources\";
        static string EncryptedSymmetricKeyPath = @"..\\resources\\Key.enc";
        string cert_thumbprint = "95266410248877b4db407a0449e6e18516cca8e8";  // QuanNN-cert
        X509Certificate2 cert;
        static byte[] ClientSessionKey, ClientIV;
        static string credentials;
        public Form1()
        {
            InitializeComponent();
            Buttons_NotClicked();
            EstablishTCPConnection();
        }
        private string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        private static void CreateSymmetricKey(RSA rsaPublicKey)
        {
            using (Aes aes = Aes.Create())
            {
                // Create instance of Aes for
                // symetric encryption of the data.
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                //aes.Key = Encoding.UTF8.
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    // Create symmetric key (or session key)
                    RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(rsaPublicKey);
                    ClientSessionKey = new byte[aes.Key.Length];
                    ClientSessionKey = aes.Key;
                    ClientIV = new byte[aes.IV.Length];
                    ClientIV = aes.IV;
                    byte[] keyEncrypted = keyFormatter.CreateKeyExchange(aes.Key, aes.GetType());
                    // Create byte arrays to contain
                    // the length values of the key and IV.
                    byte[] LenK = new byte[4];
                    byte[] LenIV = new byte[4];

                    int lKey = keyEncrypted.Length;
                    LenK = BitConverter.GetBytes(lKey);
                    int lIV = aes.IV.Length;
                    LenIV = BitConverter.GetBytes(lIV);

                    // Write the following to the FileStream
                    // for the encrypted file (outFs):
                    // - length of the key
                    // - length of the IV
                    // - ecrypted key
                    // - the IV
                    // - the encrypted cipher content
                    using (FileStream outFs = new FileStream(EncryptedSymmetricKeyPath, FileMode.Create))
                    {
                        outFs.Write(LenK, 0, 4);
                        outFs.Write(LenIV, 0, 4);
                        outFs.Write(keyEncrypted, 0, lKey);
                        outFs.Write(aes.IV, 0, lIV);
                        
                        outFs.Close();
                    }
                }
            }
        }
        private void SendEncryptedFile(string encryptedFile)
        {
            // Send the key to server
            FileStream fs = new FileStream(encryptedFile, FileMode.Open);
            fs.CopyTo(stream);
            fs.Close();
            Print_log("Send " + encryptedFile + " to server.");
        }
        private static void EncryptFile(string inFile, RSA rsaPublicKey)
        {
            using (Aes aes = Aes.Create())
            {
                // Create instance of Aes for
                // symetric encryption of the data.
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Key = ClientSessionKey;
                aes.IV = ClientIV;
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    // Create symmetric key (or session key)
                    RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(rsaPublicKey);
                    
                    byte[] keyEncrypted =  keyFormatter.CreateKeyExchange(aes.Key, aes.GetType());

                    // Create byte arrays to contain
                    // the length values of the key and IV.
                    byte[] LenK = new byte[4];
                    byte[] LenIV = new byte[4];

                    int lKey = keyEncrypted.Length;
                    LenK = BitConverter.GetBytes(lKey);
                    int lIV = aes.IV.Length;
                    LenIV = BitConverter.GetBytes(lIV);

                    // Write the following to the FileStream
                    // for the encrypted file (outFs):
                    // - length of the key
                    // - length of the IV
                    // - ecrypted key
                    // - the IV
                    // - the encrypted cipher content

                    int startFileName = inFile.LastIndexOf("\\") + 1;
                    // Change the file's extension to ".enc"
                    string outFile = encrFolder + inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) + ".enc";
                    Directory.CreateDirectory(encrFolder);

                    using (FileStream outFs = new FileStream(outFile, FileMode.Create))
                    {

                        outFs.Write(LenK, 0, 4);
                        outFs.Write(LenIV, 0, 4);
                        outFs.Write(keyEncrypted, 0, lKey);
                        outFs.Write(aes.IV, 0, lIV);

                        // Now write the cipher text using
                        // a CryptoStream for encrypting.
                        using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                        {

                            // By encrypting a chunk at
                            // a time, you can save memory
                            // and accommodate large files.
                            int count = 0;
                            // blockSizeBytes can be any arbitrary size.
                            int blockSizeBytes = aes.BlockSize / 8;
                            byte[] data = new byte[blockSizeBytes];
                            int bytesRead = 0;

                            using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                            {
                                do
                                {
                                    count = inFs.Read(data, 0, blockSizeBytes);
                                    outStreamEncrypted.Write(data, 0, count);
                                    bytesRead += count;
                                }
                                while (count > 0);
                                inFs.Close();
                            }
                            outStreamEncrypted.FlushFinalBlock();
                            outStreamEncrypted.Close();
                        }
                        outFs.Close();
                    }
                }
            }
        }

        // Decrypt a file using a private key.
        private static void DecryptFile(string inFile)
        {

            // Create instance of Aes for
            // symetric decryption of the data.
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;

                // Create byte arrays to get the length of
                // the encrypted key and IV.
                // These values were stored as 4 bytes each
                // at the beginning of the encrypted package.
                byte[] LenK = new byte[4];
                byte[] LenIV = new byte[4];
                string decrFolder = @"..\resources\";
                // Construct the file name for the decrypted file.
                string outFile = decrFolder + inFile.Substring(0, inFile.LastIndexOf(".")) + ".txt";

                // Use FileStream objects to read the encrypted
                // file (inFs) and save the decrypted file (outFs).
                using (FileStream inFs = new FileStream(encrFolder + inFile, FileMode.Open))
                {

                    inFs.Seek(0, SeekOrigin.Begin);
                    inFs.Seek(0, SeekOrigin.Begin);
                    inFs.Read(LenK, 0, 3);
                    inFs.Seek(4, SeekOrigin.Begin);
                    inFs.Read(LenIV, 0, 3);

                    // Convert the lengths to integer values.
                    int lenK = BitConverter.ToInt32(LenK, 0);
                    int lenIV = BitConverter.ToInt32(LenIV, 0);

                    // Determine the start position of
                    // the cipher text (startC)
                    // and its length(lenC).
                    int startC = lenK + lenIV + 8;
                    int lenC = (int)inFs.Length - startC;

                    // Create the byte arrays for
                    // the encrypted Aes key,
                    // the IV, and the cipher text.
                    byte[] KeyEncrypted = new byte[lenK];
                    byte[] IV = new byte[lenIV];

                    // Extract the key and IV
                    // starting from index 8
                    // after the length values.
                    inFs.Seek(8, SeekOrigin.Begin);
                    inFs.Read(KeyEncrypted, 0, lenK);
                    inFs.Seek(8 + lenK, SeekOrigin.Begin);
                    inFs.Read(IV, 0, lenIV);
                    Directory.CreateDirectory(decrFolder);
                    // Use RSA
                    // to decrypt the Aes key.
                    //byte[] KeyDecrypted = ClientSessionKey; //rsaPrivateKey.Decrypt(KeyEncrypted, RSAEncryptionPadding.Pkcs1);
                    
                    // Decrypt the key.
                    using (ICryptoTransform transform = aes.CreateDecryptor(ClientSessionKey, ClientIV))
                    {

                        // Decrypt the cipher text from
                        // from the FileSteam of the encrypted
                        // file (inFs) into the FileStream
                        // for the decrypted file (outFs).
                        using (FileStream outFs = new FileStream(outFile, FileMode.Create))
                        {

                            int count = 0;

                            int blockSizeBytes = aes.BlockSize / 8;
                            byte[] data = new byte[blockSizeBytes];

                            // By decrypting a chunk a time,
                            // you can save memory and
                            // accommodate large files.

                            // Start at the beginning
                            // of the cipher text.
                            inFs.Seek(startC, SeekOrigin.Begin);
                            using (CryptoStream outStreamDecrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                            {
                                do
                                {
                                    count = inFs.Read(data, 0, blockSizeBytes);
                                    outStreamDecrypted.Write(data, 0, count);
                                }
                                while (count > 0);

                                outStreamDecrypted.FlushFinalBlock();
                                outStreamDecrypted.Close();
                            }
                            outFs.Close();
                        }
                        inFs.Close();
                    }
                }
            }
        }
        private void SignInButton_Clicked()
        {

            string dumb = "";
            DisableSignInButton(dumb);
            DisableSignUpButton(dumb);
            VisibleComboBox(dumb);
            VisibleSendButton(dumb);
        }
        private void Buttons_NotClicked()
        {
            SignOutButton.Enabled = false;
            SignOutButton.Visible = false;
            UpdateButt.Enabled = false;
            UpdateButt.Visible = false;
            FinishSignUpButt.Enabled = false;
            FinishSignUpButt.Visible = false;
        }
        private string GenerateInfo()
        {
            return
                            "Content-type: application/json\r\n"
                           + "Connection: keep-alive \r\n"
                           + "Upgrade-Insecure-Requests: 1\r\n"
                           + "User-Agent: C# client\r\n"
                           + "Accept: text/html;v=b3;q=0.7\r\n"
                           + "Accept-Encoding: gzip, deflate\r\n"
                           + "Accept-Language: en-US,en;q=0.9\r\n"
                           + "\r\n";
        }
        private void DeleteButton_Click()
        {
            string reqHeader = "DELETE /test" + " HTTP/1.1\r\n" // request line
                                                                      // request headers
                           + "Host: https://" + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                           + "Content-length: 0" + "\r\n"
                           + "Authorization: Basic " + credentials + "\r\n"
                           + GenerateInfo();
            PrintRequest(reqHeader);
            File.WriteAllText(@"..\resources\DELETE.txt", reqHeader);
            EncryptFile(@"..\resources\DELETE.txt", PublicKey);
            SendEncryptedFile(@"..\resources\DELETE.enc");
        }
        private void GetButton_Click()
        {
            string reqHeader = "GET /index.html" + " HTTP/1.1\r\n" // request line
                                                                   // request headers
                           + "Host: https://" + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                           + "Content-length: 0" + "\r\n"
                           + "Authorization: Basic " + credentials + "\r\n"
                           + GenerateInfo();
            PrintRequest(reqHeader);
            File.WriteAllText(@"..\resources\GET.txt", reqHeader);
            EncryptFile(@"..\resources\GET.txt", PublicKey);
            SendEncryptedFile(@"..\resources\GET.enc");
        }
        private void HeadButton_Click()
        {
            string reqHeader = "HEAD /index.html" + " HTTP/1.1\r\n" // request line
                                                                    // request headers
                           + "Host: https://" + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                           + "Content-length: 0" + "\r\n"
                           + "Authorization: Basic " + credentials + "\r\n"
                           + GenerateInfo();
            PrintRequest(reqHeader);
            File.WriteAllText(@"..\resources\HEAD.txt", reqHeader);
            EncryptFile(@"..\resources\HEAD.txt", PublicKey);
            SendEncryptedFile(@"..\resources\HEAD.enc");
        }
        private void DefaultButton_Click(string method)
        {
            string reqHeader = method + " /index.html" + " HTTP/1.1\r\n" // request line
                                                                    // request headers
                           + "Host: https://" + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                           + "Content-length: 0" + "\r\n"
                           + "Authorization: Basic " + credentials + "\r\n"
                           + GenerateInfo();
            PrintRequest(reqHeader);
            File.WriteAllText(@"..\resources\default.txt", reqHeader);
            EncryptFile(@"..\resources\default.txt", PublicKey);
            SendEncryptedFile(@"..\resources\default.enc");
        }
        private void Print_log(string log)
        {
            if (LogTextBox.InvokeRequired)
            {
                LogTextBox.Invoke(new Action<string>(Print_log), log);
                return;
            }
            LogTextBox.AppendText(log + Environment.NewLine + Environment.NewLine);
        }
        private void DisableSignUpButton(string s)
        {
            if (SignUpButton.InvokeRequired)
            {
                SignUpButton.Invoke(new Action<string>(DisableSignUpButton), s);
                return;
            }
            SignUpButton.Enabled = false;
        }
        private void VisibleComboBox(string s)
        {
            if (MethodComboBox.InvokeRequired)
            {
                MethodComboBox.Invoke(new Action<string>(VisibleComboBox), s);
                return;
            }
            MethodComboBox.Visible = true;
        }
        private void VisibleSendButton(string s)
        {
            if (SendButton.InvokeRequired)
            {
                SendButton.Invoke(new Action<string>(VisibleSendButton), s);
                return;
            }
            SendButton.Visible = true;
        }
        private void DisableSignInButton(string s)
        {
            if (SignInButton.InvokeRequired)
            {
                SignInButton.Invoke(new Action<string>(DisableSignInButton), s);
                return;
            }
            SignInButton.Enabled = false;
        }
        private int HandleResponseHeader(string headers)
        {
            print_status(headers);
            string resp_line = headers.Substring(0, headers.IndexOf("\r\n"));
            string[] resp_line_items = resp_line.Split(' ');
            PrintResponse(headers);
            if (resp_line_items[1] != "200")  // failed request
            {
                return 1;
            }
            return 0;
        }
        private void print_status(string message)
        {
            if (statusLabel.InvokeRequired)
            {
                statusLabel.Invoke(new Action<string>(print_status), message);
                return;
            }
            string resp_line = message.Substring(0, message.IndexOf("\r\n"));
            string[] resp_line_items = resp_line.Split(' ');
            statusLabel.Text = "";
            for (int i = 1; i < resp_line_items.Length; i++)
            {
                statusLabel.Text += resp_line_items[i] + " ";
            }
        }
        private void PrintResponse(string msg)
        {
            if (RespHeadTextBox.InvokeRequired)
            {
                RespHeadTextBox.Invoke(new Action<string>(PrintResponse), msg);
                return;
            }
            RespHeadTextBox.Text = msg;
        }
        private void PrintRequest(string msg)
        {
            if (RequestTextBox.InvokeRequired)
            {
                RequestTextBox.Invoke(new Action<string>(PrintRequest), msg);
                return;
            }
            RequestTextBox.Text = msg;
        }
        private void HandleServerCert()
        {
            // Load server cert (1 file .pfx for priv key and 1 file .crt for public key)
            cert = new X509Certificate2(CertSavedPath);

            // "Validate" the cert
            if (cert.Thumbprint.ToLower().ToString() == cert_thumbprint)
                Print_log("Right cert.");

            PublicKey = (RSA)cert.PublicKey.Key;    // Get public key
            CreateSymmetricKey (PublicKey);
        }
        private void ReceiveCert()
        {
            byte[] certbuffer = new byte[1998];
            stream.Read(certbuffer, 0, certbuffer.Length);
            Print_log("Receive the cert.");

            // Save the server cert to local folder
            File.WriteAllBytes(CertSavedPath, certbuffer);
            stream.Flush();
        }
        private void ReceiveSave_File(string outFile)
        {
            // Receive Server File
            byte[] FileBuffer = new byte[2000];
            int bufferLen = stream.Read(FileBuffer, 0, FileBuffer.Length);
            Print_log("The file of server is received.");

            // Save the client file to local folder
            FileStream fs = new FileStream(@"..\resources\" + outFile, FileMode.Create);
            fs.Write(FileBuffer, 0, bufferLen);
            fs.Close();
            Print_log("Save the encrypted file to a folder.");
        }
        private void LoadFile(string inFile)
        {
            StreamReader sr = new StreamReader(inFile);
            string response = sr.ReadToEnd();

            string[] resp_items = response.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.None);
            int Errorflag = HandleResponseHeader(resp_items[0]);
            if (Errorflag == 0)
            {
                SignInButton_Clicked();
                Print_body(resp_items[1]);
            }
            sr.Close();
        }
        private void Print_body(string body)
        {
            if (RespHeadTextBox.InvokeRequired)
            {
                RespHeadTextBox.Invoke(new Action<string>(Print_body), body);
                return;
            }
            webBrowser.DocumentText = body;
        }
        private void StartClient()
        {
            stream = tcpClient.GetStream();
            ReceiveCert();
            HandleServerCert();

            SendEncryptedFile(EncryptedSymmetricKeyPath);    // Send encrypted key
            try
            {
                while (true)
                {
                    HandleResponse();
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Print_log("ERR_CONNECTION_TIMED_OUT");
            }
        }
        private void HandleResponse()
        {
            ReceiveSave_File("response.enc");
            DecryptFile("response.enc");
            Print_log("Decrypt file successfullly.");
            LoadFile(@"..\resources\" + "response.txt");
        }
        private void EstablishTCPConnection()
        {
            try
            {
                string ServerHostname = "127.0.0.1";
                int ServerPort = 8089;

                // Create tcp client
                tcpClient = new TcpClient();

                // connect to the server socket
                tcpClient.Connect(ServerHostname, ServerPort);
                stream = tcpClient.GetStream();
                Print_log("Connected to " + ServerHostname + ": " + ServerPort);

                Task.Run(() => StartClient());
                // Replacement
                /*Thread ctThread = new Thread(StartClient);
                ctThread.Start();*/
            }
            catch (SocketException)
            {
                Print_log("Unable to connect to server's socket.");
                SignInButton.Enabled = false;
                SignUpButton.Enabled = false;
            }
            catch (Exception ex)
            {
                Print_log(ex.ToString());
            }
        }
        
        private void SendButton_Click(object sender, EventArgs e)
        {
            Print_body("");
            switch (MethodComboBox.Text)
            {
                case "GET":
                    {
                        GetButton_Click();
                    }
                    break;
                case "DELETE":
                    {
                        DeleteButton_Click();
                    }
                    break;
                case "HEAD":
                    {
                        HeadButton_Click();
                    }
                    break;
                default:
                    {
                        DefaultButton_Click(MethodComboBox.Text);
                    }
                    break;
            }
        }
        private bool InvalidCharCheck()
        {
            foreach (char ch in usernameTextBox.Text)
            {
                if (invalidChars.Contains(ch))
                {
                    MessageBox.Show("Invalid characters existed in username!");
                    usernameTextBox.Clear();
                    return true;
                }
            }
            return false;
        }
        private void SignInButton_Click(object sender, EventArgs e)
        {
            if (InvalidCharCheck() == false)
            {
                credentials = Base64Encode(usernameTextBox.Text + '|' + passTextBox.Text);
                string reqBody = "{\r\n"
                                        + usernameTextBox.Text + ": " + passTextBox.Text + "\r\n"
                                        + "}\r\n";
                string reqHeader = "POST /login" + " HTTP/1.1\r\n" // request line
                                                                   // request headers
                               + "Host: https://" + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                               + "Content-length: " + reqBody.Length + "\r\n"
                               + "Authorization: Basic " + credentials + "\r\n"
                               + GenerateInfo();
                PrintRequest(reqHeader + reqBody);
                File.WriteAllText(@"..\resources\POST.txt", reqHeader + reqBody);
                EncryptFile(@"..\resources\POST.txt", PublicKey);
                SendEncryptedFile(@"..\resources\POST.enc");
            }
        }
        private void SignUpButton_Click(object sender, EventArgs e)
        {
            if (InvalidCharCheck() == false)
            {
                credentials = Base64Encode(usernameTextBox.Text + '|' + passTextBox.Text);
                string reqBody = "{\r\n  "
                                        + usernameTextBox.Text + ": " + passTextBox.Text + "\r\n"
                                        + "}\r\n";
                string reqHeader = "POST /register" + " HTTP/1.1\r\n" // request line
                                                                      // request headers
                               + "Host: https://" + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                               + "Content-length: " + reqBody.Length + "\r\n"
                               + "Authorization: Basic " + credentials + "\r\n"
                               + GenerateInfo();
                PrintRequest(reqHeader + reqBody);
                File.WriteAllText(@"..\resources\POST.txt", reqHeader + reqBody);
                EncryptFile(@"..\resources\POST.txt", PublicKey);
                SendEncryptedFile(@"..\resources\POST.enc");
            }
        }

        //     BEGINNING OF CONTROL METHOD  //////////////////
        private void usernameTextBox_Click(object sender, EventArgs e)
        {
            if (usernameTextBox.Text == "Tên tài khoản")
            {
                usernameTextBox.Text = "";
            }
        }
        private void usernameTextBox_Leave(object sender, EventArgs e)
        {
            if (usernameTextBox.Text == "")
            {
                usernameTextBox.Text = "Tên tài khoản";
            }
        }
        private void passTextBox_Leave(object sender, EventArgs e)
        {
            if (passTextBox.Text == "")
            {
                passTextBox.Text = "Mật khẩu";
                passTextBox.UseSystemPasswordChar = false;
            }
            else
            {
                passTextBox.UseSystemPasswordChar = true;
            }
        }
        private void passTextBox_Click(object sender, EventArgs e)
        {
            if (passTextBox.Text == "Mật khẩu")
            {
                passTextBox.Text = "";
            }
        }
        private void passTextBox_TextChanged(object sender, EventArgs e)
        {
            passTextBox.UseSystemPasswordChar = true;
        }
        private void SignOutButton_Click(object sender, EventArgs e)
        {
        }
        private void FinishSignUpButt_Click(object sender, EventArgs e)
        {
        }
        private void UpdateButt_Click(object sender, EventArgs e)
        {
        }
    }
}
