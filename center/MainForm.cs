using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Protocol;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace center
{
    public partial class MainForm : Form
    {
        private AppServer hdServer = new AppServer();
        private List<string> client_session = new List<string>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void symbols_reload()
        {
            symbols.Rows.Clear();
            api_symbols("https://www.herodata.cc/api/symbols", KEY.Text);
        }

        private void ServerStart()
        {
            //HDataCenter as TCP server
            if (!hdServer.Setup(58588))
            {
                printlog(log, "setup fail (port: 58588), try different port number.");
            }

            if (!hdServer.Start())
            {
                printlog(log, "server fail");

            }
            else
            {
                printlog(log, "server is ready. (tcp port: 58588)");
            }

            hdServer.NewSessionConnected += new SessionHandler<AppSession>(hdServer_NewSessionConnected);
            hdServer.NewRequestReceived += new RequestHandler<AppSession, StringRequestInfo>(hdServer_NewRequestReceived);
        }

        public void hdServer_NewSessionConnected(AppSession session)
        {
            if (client_session.ToArray().Count() > 0)
            {
                foreach (string v in client_session.ToArray())
                {
                    if (hdServer.GetSessionByID(v) != null && hdServer.GetSessionByID(v).Connected)
                    {
                        
                    }
                    else
                    {
                        client_session.Remove(v);
                    }
                }
            }

            client_session.Add(session.SessionID);
            printlog(log, string.Format("{0} connect", session.RemoteEndPoint));
        }

        public void hdServer_NewRequestReceived(AppSession session, StringRequestInfo requestInfo)
        {
            string symbol = requestInfo.Key.Trim();
            if (symbol.Length > 0)
            {
                symbol = symbol.Replace("\\", "").Replace("/", "").Replace(".", "");
                string filename = string.Format(@"{0}.csv", symbol);
                printlog(log, string.Format("request {0} from {1}",symbol, session.RemoteEndPoint));
                if (File.Exists(filename))
                {
                    session.Send(File.ReadAllText(filename));
                }
                else
                {
                    printlog(log, string.Format("{0} not found", filename));
                }
            }
        }

        private async void api_symbols(string url, string key)
        {            
            string rowdata = "symbols.txt";
            if (File.Exists(rowdata))
            {
                string line;
                StreamReader file = new StreamReader(rowdata);
                while ((line = file.ReadLine()) != null)
                {
                    //string[] row = ;
                    List<string> row = line.Split(',').ToList();
                    string symbol = row[0];
                    if (symbol != "")
                    {
                        string[] data = loadata(symbol);
                        row.Add(data[1]);
                        row.Add(data[0]);
                        symbols.Rows.Add(row.ToArray());
                    }
                }

                file.Close();
            }
            else
            {
                var client = new HttpClient();
                var requestContent = new FormUrlEncodedContent(new[] {  
                new KeyValuePair<string, string>("key", key) });
                HttpResponseMessage response = await client.PostAsync(url, requestContent);
                HttpContent responseContent = response.Content;

                StreamWriter file = new StreamWriter(rowdata, true);
                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    string line = await reader.ReadToEndAsync();
                    file.WriteLine(line);
                }
                file.Close();
            }
            
        }

        private string[] loadata(string symbol)
        {
            string filename = string.Format("{0}.csv", symbol);
            string[] data = { };
            string close = "n/a", date = "n/a";
            if (File.Exists(filename))
            {
                var lines = File.ReadLines(filename).Take(2).ToArray();
                if (lines.Length > 0)
                {
                    string[] last = lines[1].Split(',');
                    date = last[0];
                    close = last[4];
                }
            }
            
            return new string[] { date, close };
        }

        private void symbols_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 8 && e.RowIndex > -1) //download button
            {
                new Thread(() =>
                {
                    string symbol = symbols[0, e.RowIndex].Value.ToString();
                    string url = string.Format("https://www.herodata.cc/api/download", symbol);
                    string last_date = symbols[7, e.RowIndex].Value.ToString();
                    printlog(log, string.Format("{0} download", symbol));
                    download(url, symbol, KEY.Text, last_date);

                }).Start();
            }
            else if (e.ColumnIndex == 9 && e.RowIndex > -1) //delete button
            {
                string symbol = symbols[0, e.RowIndex].Value.ToString();
                string filename = string.Format("{0}.csv", symbol);
                if(File.Exists(filename))
                {
                    File.Delete(filename);
                    printlog(log, string.Format("{0} delete", filename));
                }

                symbols_reload();
            }
            else if (e.ColumnIndex == 10 && e.RowIndex > -1) //open button
            {
                string symbol = symbols[0, e.RowIndex].Value.ToString();
                string filename = string.Format("{0}.csv", symbol);
                Process.Start("notepad.exe", filename);
            }
            else if (e.ColumnIndex == 11 && e.RowIndex > -1) //disable button
            {                
                DataGridViewCheckBoxCell disableOrNot = (DataGridViewCheckBoxCell)symbols[11, e.RowIndex];
                if (symbols[0, e.RowIndex].Value != null)
                {
                    string symbol = symbols[0, e.RowIndex].Value.ToString();
                    string enableOr = (bool)disableOrNot.EditedFormattedValue ? "enable" : "disable";
                    printlog(log, string.Format("{0} {1}", symbol, enableOr));
                }
            }

        }

        private async void download(string url, string symbol, string key, string last_date)
        {
            var client = new HttpClient();

            // Create the HttpContent for the form to be posted.
            var requestContent = new FormUrlEncodedContent(new[] { 
                new KeyValuePair<string, string>("symbol", symbol), 
                new KeyValuePair<string, string>("key", key),
                new KeyValuePair<string, string>("last_date", last_date) });

            // Get the response.
            HttpResponseMessage response = await client.PostAsync(url, requestContent);

            // Get the response content.
            HttpContent responseContent = response.Content;

            // Get the stream of the content.
            string csv = string.Format("{0}.csv", symbol);
            bool isappend = true;
            if (last_date=="n/a")
            {
                if (File.Exists(csv))
                {
                    File.Delete(csv);
                }
                isappend = false;
            }
            
            if (isappend)
            {
                List<string> lst = new List<string>();
                string line = "";
                StreamReader file = new StreamReader(csv);
                while ((line = file.ReadLine()) != null)
                {
                    lst.Add(line);
                }
                file.Close();

                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    string row = await reader.ReadToEndAsync();
                    if (row != "")
                    {
                        row = row.Remove(row.LastIndexOf(Environment.NewLine));
                        lst.Insert(1, row);
                    }
                }

                TextWriter sw = new StreamWriter(csv);
                lst = lst.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                foreach (String row in lst)
                {
                    sw.WriteLine(row);
                }
                sw.Close();
                
            }
            else
            {
                StreamWriter file = new StreamWriter(csv, true);
                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    string line = await reader.ReadToEndAsync();
                    line = line.Remove(line.LastIndexOf(Environment.NewLine));          
                    file.WriteLine(line);                    
                }
                file.Close();
            }

            string[] data = loadata(symbol);
            int row_id = findRowId(symbols, symbol, 0);
            symbols[6, row_id].Value = data[1];
            symbols[7, row_id].Value = data[0];

            printlog(log, string.Format("{0} job finish", symbol));
        }

        private int findRowId(DataGridView dg, string key, int cid)
        {
            dg.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            int rowid = -1;
            foreach (DataGridViewRow row in dg.Rows)
            {
                if (row.Cells[cid].Value != null)
                {
                    if (row.Cells[cid].Value.ToString() == key)
                    {
                        rowid = row.Index;
                        break;
                    }
                }
            }
            return rowid;
        }

        private void clear_message_ButtonClick(object sender, EventArgs e)
        {
            log.Clear();
        }

        private void printlog(TextBox tb, string msg)
        {
            try
            {
                if (tb.InvokeRequired)
                {
                    Invoke((MethodInvoker)(() => tb.AppendText(msg + "\r\n")));
                }
                else
                {
                    tb.AppendText(msg + "\r\n");
                }
            }
            catch { }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            symbols_reload();
            timer1.Start();
            ServerStart();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            clock.Text = DateTime.UtcNow.ToString("HH:mm:ss");

            //every minute 
            if (DateTime.UtcNow.ToString("mm") != DateTime.UtcNow.AddSeconds(-1).ToString("mm"))
            {
                if (updateAt.Text == DateTime.UtcNow.ToString("HH:mm"))
                {
                    printlog(log, "auto download start");
                    auto_download();
                }
            }

        }

        private void LoadSymbols_Click(object sender, EventArgs e)
        {
            symbols_reload();
        }

        private void auto_download()
        {
            for (int i = 0; i < symbols.RowCount; i++)
            {
                if (symbols[0, i].Value != null)
                {                    
                    DataGridViewCheckBoxCell disableOrNot = (DataGridViewCheckBoxCell)symbols[11, i];
                    bool isEnable = (bool)disableOrNot.EditedFormattedValue;
                    if (isEnable)
                    {
                        Thread.Sleep(2000);
                        string symbol = symbols[0, i].Value.ToString();                        
                        string url = string.Format("https://www.herodata.cc/api/download", symbol);
                        string last_date = symbols[7, i].Value.ToString();
                        printlog(log, string.Format("{0} download", symbol));
                        download(url, symbol, KEY.Text, last_date);
                    }
                }
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            printlog(log, "get premium key: https://www.herodata.cc/account ");
        }

    }
}
