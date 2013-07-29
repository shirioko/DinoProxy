using ARSoft.Tools.Net.Dns;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Forms;

namespace DinoProxy
{
    public partial class Form1 : Form
    {
        private string targetIP;
        static string rewriteHost;
        static IPAddress rewriteIP;

        public static string resolveHost(string hostname)
        {
            IPHostEntry ips = Dns.GetHostEntry(hostname);
            if (ips != null && ips.AddressList != null && ips.AddressList.Length > 0)
            {
                return ips.AddressList.First().ToString();
            }
            return null;
        }

        protected void startDnsServer()
        {
            try
            {
                DnsServer server = new DnsServer(System.Net.IPAddress.Any, 10, 10, onDnsQuery);
                this.AddListItem("Started DNS proxy...");
                server.ExceptionThrown += dnsServer_ExceptionThrown;
                server.Start();
            }
            catch (Exception e)
            {
                this.AddListItem(String.Format("ERROR STARTING DNS SERVER: {0}", e.Message));
            }
        }

        private void dnsServer_ExceptionThrown(object sender, ARSoft.Tools.Net.Dns.ExceptionEventArgs e)
        {
            startDnsServer();
        }

        public Form1()
        {
            InitializeComponent();
        }

        delegate void AddListItemCallback(String text);
        delegate void AddLogItemCallback(byte[] data, bool toClient);

        private void AddListItem(String data)
        {
            if (this.textBox1.InvokeRequired)
            {
                AddListItemCallback a = new AddListItemCallback(AddListItem);
                this.Invoke(a, new object[] { data });
            }
            else
            {
                this.textBox1.AppendText(String.Format("{0}\r\n", data));
                this.textBox1.DeselectAll();
                //log to file
                try
                {
                    File.AppendAllLines("DinoProxy.log", new String[] { data });
                }
                catch (Exception)
                {

                }
            }
        }

        static System.Net.IPAddress GetIP()
        {
            IPHostEntry host;
            System.Net.IPAddress localIP = null;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (System.Net.IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    localIP = ip;
                }
            }
            return localIP;
        }

        public string[] GetAllIPs()
        {
            List<string> res = new List<string>();
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (System.Net.IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    res.Add(ip.ToString());
                }
            }
            return res.ToArray();
        }

        static DnsMessageBase onDnsQuery(DnsMessageBase message, System.Net.IPAddress clientAddress, ProtocolType protocol)
        {
            message.IsQuery = false;

            DnsMessage query = message as DnsMessage;
            DnsMessage answer = null;

            if ((query != null) && (query.Questions.Count == 1))
            {
                //HOOK:
                //rewrite DNS
                if (query.Questions[0].RecordType == RecordType.A
                    &&
                    query.Questions[0].Name == Form1.rewriteHost
                    )
                    
                {
                    query.ReturnCode = ReturnCode.NoError;
                    query.AnswerRecords.Add(new ARecord(query.Questions[0].Name, 30, Form1.rewriteIP));
                    return query;
                }
                // send query to upstream server
                try
                {
                    DnsQuestion question = query.Questions[0];
                    answer = DnsClient.Default.Resolve(question.Name, question.RecordType, question.RecordClass);

                    // if got an answer, copy it to the message sent to the client
                    if (answer != null)
                    {
                        foreach (DnsRecordBase record in (answer.AnswerRecords))
                        {
                            query.AnswerRecords.Add(record);
                        }
                        foreach (DnsRecordBase record in (answer.AdditionalRecords))
                        {
                            query.AnswerRecords.Add(record);
                        }

                        query.ReturnCode = ReturnCode.NoError;
                        return query;
                    }
                }
                catch (Exception )
                { }
            }
            // Not a valid query or upstream server did not answer correct
            message.ReturnCode = ReturnCode.ServerFailure;
            return message;
        }

        private void startVenom()
        {
            //get vars


            //do stuff
            this.targetIP = GetIP().ToString();
            if (String.IsNullOrEmpty(this.targetIP))
            {
                this.AddListItem("DNS ERROR: Could not find your local IP address");
                return;
            }

            //start DNS server
            Thread srv = new Thread(new ThreadStart(startDnsServer));
            srv.IsBackground = true;
            srv.Start();

            string[] ips = this.GetAllIPs();
            if (ips.Length > 1)
            {
                this.AddListItem(String.Format("WARNING: Multiple IP addresses found: {0}", String.Join(" ,", ips)));
            }

            this.AddListItem(String.Format("Set your DNS address on your phone to {0} (Settings->WiFi->Static IP->DNS)", ips.First()));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.textBox1.Clear();
            bool valid = true;
            //validate input
            string host = this.textBox2.Text;
            if (string.IsNullOrEmpty(host))
            {
                valid = false;
                this.AddListItem("Please enter a host address to rewrite");
            }
            string ip = this.textBox3.Text;
            if (string.IsNullOrEmpty(ip))
            {
                valid = false;
                this.AddListItem("Please add an IP address to rewrite the host address to");
            }
            IPAddress ipparse;
            if (!IPAddress.TryParse(ip, out ipparse))
            {
                valid = false;
                this.AddListItem("Please enter a vaid IP address");
            }

            if (valid)
            {
                //set vars
                Form1.rewriteHost = host;
                Form1.rewriteIP = IPAddress.Parse(ip);

                //disable controls
                this.textBox2.Enabled = false;
                this.textBox3.Enabled = false;
                this.button1.Enabled = false;

                //start stuff
                Thread t = new Thread(new ThreadStart(startVenom));
                t.IsBackground = true;
                t.Start();
            }
        }
    }
}
