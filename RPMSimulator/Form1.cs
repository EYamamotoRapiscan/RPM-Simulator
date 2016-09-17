using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using RPMSimulator.Properties;

namespace RPMSimulator
{
    public partial class Form1 : Form
    {

        public List<Thread> asl;

        //public List<AsynchronousSocketListener> serverlist;
        string fileName;

        public Form1()
        {
            InitializeComponent();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = System.String.Format("RPM Simulator Version {0}", version);

            openFD.Filter = "CSV|*.csv|All Files|*.*";
            fileName = "";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            Progress<string> progress = new Progress<string>(text => richTextBox1.Text += text);
            richTextBox1.Text += String.Format("Starting Threaded TCP Server on {0}...\n",Environment.MachineName);
            ThreadedTcpSrvr server = new ThreadedTcpSrvr();
            await System.Threading.Tasks.Task.Run(() => server.Run(progress,fileName));
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        delegate void AppendTextDelegate(string text);

        public void AppendText(string text)
        {
            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke(new AppendTextDelegate(this.AppendText), new object[] { text });
            }
            else
            {
                richTextBox1.Text = richTextBox1.Text += text;
            }
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            openFD.ShowDialog();
            
            fileName = openFD.FileName;
            richTextBox1.Text += String.Format("\nFilename = {0}\n", fileName);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

    }

        public class ThreadedTcpSrvr
        {
            private TcpListener client;

            public ThreadedTcpSrvr()
            {
            }
            public void Run(IProgress<string> progress, String filename)
            {
                Int32 port = 1600;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                //IPAddress localAddr = Dns.GetHostEntry("127.0.0.1").AddressList[0];
                client = new TcpListener(localAddr,port);
                client.Start();


                progress.Report("Waiting for clients...");
                Console.WriteLine("Waiting for clients...");
                while (true)
                {
                    while (!client.Pending())
                    {
                        Thread.Sleep(1000);
                    }

                    ConnectionThread newconnection = new ConnectionThread(progress, filename);
                    newconnection.threadListener = this.client;
                    Thread newthread = new Thread(new
                              ThreadStart(newconnection.HandleConnection));
                    newthread.IsBackground = true; //to kill all threads on exit of application
                    newthread.Start();
                }
            }

        }

        class ConnectionThread
        {
            public TcpListener threadListener;
            private static int connections = 0;
            private String filename;
            IProgress<string> progress;

            public ConnectionThread(IProgress<string> p, String f)
            {
                progress = p;
                filename = f;
            }

            public void HandleConnection()
            {
                int recv;
                byte[] data = new byte[1024];

                TcpClient client = threadListener.AcceptTcpClient();
                NetworkStream ns = client.GetStream();
                connections++;
                progress.Report(String.Format("New client accepted: {0} active connections\n", connections));
                Console.WriteLine("New client accepted: {0} active connections", connections);

                String _fileData;

                String[] _fileRows;
                try
                {
                    StreamReader sr = new StreamReader(filename);

                    _fileData = sr.ReadToEnd();

                    _fileRows = _fileData.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                }
                catch(FileNotFoundException e )
                {
                    throw (e);
                }
                //int bkgtime = 5000;
                //int scantime = 200;
                int bkgtime = Properties.Settings.Default.bkgtime;
                int scantime = Properties.Settings.Default.scantime;

                int firstRow = Properties.Settings.Default.FirstRow;
                if (firstRow >= _fileRows.Length || firstRow <0) firstRow = 0;// default to zero
                int lastRow = Properties.Settings.Default.LastRow == 0? _fileRows.Length : Properties.Settings.Default.LastRow;
                if (lastRow > _fileRows.Length || lastRow < 0) lastRow = _fileRows.Length;

                if (firstRow >= lastRow)
                    firstRow = 0;

                Random rndm = new Random(DateTime.Now.Millisecond);


                int randomNumber = Properties.Settings.Default.RandomStart ? rndm.Next(0, _fileRows.Length - 1) : firstRow;
                progress.Report(String.Format("Random Number {0}\n", randomNumber));
                Console.WriteLine("Random Number {0}", randomNumber);

                while (client.Connected)
                {
                    int nbcount = 0;
                    int occupancy = 0;
                    int gammacount = 0;
                    for (int row = randomNumber; row < lastRow; row++)
                    //foreach (var foo in _fileRows)
                    {
                        try
                        {
                            String[] cols = _fileRows[row].Split(',');
                            String bar = cols[0];
                            for (int i = 1; i < cols.Length - 1; i++)
                            {
                                bar = String.Format("{0},{1}", bar, cols[i]);
                            }
                            if (cols[0] == "NB" || cols[0] == "NH")
                            {
                                gammacount = 0;
                                nbcount++;
                                System.Threading.Thread.Sleep(bkgtime);
                                var message = String.Format("{0}\r\n", bar);
                                data = Encoding.ASCII.GetBytes(message);
                                ns.Write(data, 0, data.Length);
                                nbcount = 0;
                            }
                            
                            else if (cols[0] == "NS" || cols[0] == "NA")
                            {
                                occupancy = 1;
                                nbcount = 0;
                                var message = String.Format("{0}\r\n", bar);
                                data = Encoding.ASCII.GetBytes(message);
                                ns.Write(data, 0, data.Length);
                                System.Threading.Thread.Sleep(scantime);
                            }
                            else if (cols[0] == "GS" || cols[0] == "GA")
                            {
                                gammacount++;
                                var message = String.Format("{0}\r\n", bar);
                                data = Encoding.ASCII.GetBytes(message);
                                ns.Write(data, 0, data.Length);
                                if(gammacount >5) //first five messages in an occupancy come out immediately
                                    System.Threading.Thread.Sleep(scantime);
                            }
                            else if (cols[0] == "GX")
                            {
                                occupancy = 0;
                                gammacount = 0;
                                var message = String.Format("{0}\r\n", bar);
                                data = Encoding.ASCII.GetBytes(message);
                                ns.Write(data, 0, data.Length);
                                System.Threading.Thread.Sleep(bkgtime);
                            }
                            else
                            {
                                var message = String.Format("{0}\r\n", bar);
                                data = Encoding.ASCII.GetBytes(message);
                                ns.Write(data, 0, data.Length);
                            }
                        }
                        catch (IOException e) { break; }
                    }
                    randomNumber = firstRow; //after first loop start from first row.
                }

                ns.Close();
                client.Close();
                connections--;
                progress.Report(String.Format("Client disconnected: {0} active connections\n",connections));
                Console.WriteLine("Client disconnected: {0} active connections",
                                   connections);
            }

        }

}
