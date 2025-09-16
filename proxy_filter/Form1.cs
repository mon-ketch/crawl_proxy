using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace proxy_filter
{
    public partial class Form1 : Form
    {
        private readonly string outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live_ips.txt");
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(15); // số kết nối đồng thời
        private CancellationTokenSource cts;
        private bool isRunning = false;

        private List<string> allIPs = new List<string>();
        private int currentIPIndex = 0;
        private int currentPort = 0;

        public Form1()
        {
            InitializeComponent();
            textBox1.Multiline = true;
            textBox1.ScrollBars = ScrollBars.Vertical;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                // Start hoặc Resume
                button1.Text = "Stop";
                isRunning = true;
                cts = new CancellationTokenSource();
                var token = cts.Token;

                if (allIPs.Count == 0) // lần đầu tạo danh sách IP
                {
                    string startIP = textBox2.Text;
                    string endIP = textBox3.Text;
                    allIPs = GenerateIPRange(startIP, endIP).ToList();
                    currentIPIndex = 0;
                    currentPort = int.Parse(textBox4.Text);
                    textBox1.Clear();
                    listBox1.Items.Clear();
                    if (File.Exists(outputFile)) File.Delete(outputFile);
                }

                try
                {
                    await RunScan(token);
                    if (!token.IsCancellationRequested)
                        MessageBox.Show($"Quét xong! Proxy live đã lưu vào {outputFile}", "Hoàn tất");
                }
                catch (OperationCanceledException)
                {
                    AddListBoxText("Quét đã bị dừng.");
                }
                finally
                {
                    isRunning = false;
                    button1.Text = "Resume";
                }
            }
            else
            {
                // Stop
                cts.Cancel();
                button1.Text = "Resume";
                isRunning = false;
            }
        }

        private async Task RunScan(CancellationToken token)
        {
            for (int i = currentIPIndex; i < allIPs.Count; i++)
            {
                string ip = allIPs[i];
                currentIPIndex = i; // lưu index hiện tại

                for (int port = currentPort; port <= int.Parse(textBox5.Text); port++)
                {
                    currentPort = port; // lưu port hiện tại

                    token.ThrowIfCancellationRequested();

                    await semaphore.WaitAsync(token);
                    try
                    {
                        bool isPortOpen = await CheckPort(ip, port);
                        AddListBoxText($"Check {ip}:{port}" + (isPortOpen ? " | Live" : " | Die"));

                        if (isPortOpen)
                        {
                            bool isProxyAlive = await TestProxy(ip, port);
                            AddListBoxText($"Test {ip}:{port}" + (isProxyAlive ? " | Live" : " | Die"));

                            if (isProxyAlive)
                            {
                                string result = $"Check {ip}:{port}";
                                AddTextBoxText(result);
                                try { await File.AppendAllTextAsync(outputFile, result + Environment.NewLine, token); }
                                catch { }
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
                currentPort = int.Parse(textBox4.Text); // reset port cho IP mới
            }
        }

        private void AddListBoxText(string text)
        {
            listBox1.BeginInvoke((Action)(() =>
            {
                listBox1.Items.Add(text);
                listBox1.TopIndex = listBox1.Items.Count - 1;
            }));
        }

        private void AddTextBoxText(string text)
        {
            textBox1.BeginInvoke((Action)(() =>
            {
                textBox1.AppendText(text + Environment.NewLine);
                textBox1.SelectionStart = textBox1.Text.Length;
                textBox1.ScrollToCaret();
            }));
        }

        // Sinh dãy IP
        static IEnumerable<string> GenerateIPRange(string startIP, string endIP)
        {
            var start = Array.ConvertAll(startIP.Split('.'), byte.Parse);
            var end = Array.ConvertAll(endIP.Split('.'), byte.Parse);

            for (int a = start[0]; a <= end[0]; a++)
                for (int b = (a == start[0] ? start[1] : 0); b <= (a == end[0] ? end[1] : 255); b++)
                    for (int c = (a == start[0] && b == start[1] ? start[2] : 0); c <= (a == end[0] && b == end[1] ? end[2] : 255); c++)
                        for (int d = (a == start[0] && b == start[1] && c == start[2] ? start[3] : 0); d <= (a == end[0] && b == end[1] && c == end[2] ? end[3] : 255); d++)
                            yield return $"{a}.{b}.{c}.{d}";
        }

        // Kiểm tra port TCP
        static async Task<bool> CheckPort(string ip, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(ip, port);
                    var timeoutTask = Task.Delay(300); // timeout nhanh
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    return completedTask == connectTask && client.Connected;
                }
            }
            catch { return false; }
        }

        // Test proxy HTTP/HTTPS
        static async Task<bool> TestProxy(string ip, int port)
        {
            try
            {
                var proxy = new WebProxy($"{ip}:{port}");
                var handler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(3);

                var response = await client.GetAsync("https://www.google.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            // Dừng quét nếu đang chạy
            if (isRunning && cts != null)
            {
                cts.Cancel();
                isRunning = false;
            }

            // Reset các biến trạng thái
            allIPs.Clear();
            currentIPIndex = 0;
            currentPort = int.Parse(textBox4.Text);

            // Xóa dữ liệu hiển thị
            textBox1.Clear();
            listBox1.Items.Clear();

            // Xóa file kết quả
            if (File.Exists(outputFile))
                File.Delete(outputFile);

            // Reset nút Start
            button1.Text = "Start";

            AddListBoxText("Reset Done !");
        }
    }
}
