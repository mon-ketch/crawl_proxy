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
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(30); // 30 kết nối TCP đồng thời

        // =====================================================
        // HƯỚNG DẪN TÍNH SỐ IP TRONG DÃY
        //
        // Ví dụ dãy IP từ 14.160.0.0 → 14.162.5.255
        //
        // Công thức:
        // Số IP = (A2 - A1) * 256^3 
        //         + (B2 - B1) * 256^2 
        //         + (C2 - C1) * 256^1 
        //         + (D2 - D1) * 256^0
        // Sau đó cộng 1 vì số đầu dãy cũng tính
        //
        // Ví dụ:
        // Start IP: 14.160.0.0
        // End IP:   14.162.5.255
        //
        // A: 14 → 14 → (14-14)*256^3 = 0
        // B: 162-160 → 2*256^2 = 131072
        // C: 5-0 → 5*256 = 1280
        // D: 255-0 → 255*1 = 255
        //
        // Tổng số IP = 0 + 131072 + 1280 + 255 + 1 = 132608 IP
        //
        // =====================================================

        public Form1()
        {
            InitializeComponent();
            textBox1.Multiline = true;
            textBox1.ScrollBars = ScrollBars.Vertical;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            textBox1.Clear();
            listBox1.Items.Clear();

            if (File.Exists(outputFile))
                File.Delete(outputFile);

            string startIP = textBox1.Text; // "14.160.0.0";
            string endIP = textBox2.Text;  //"14.255.255.255"; // demo
            var allIPs = GenerateIPRange(startIP, endIP).ToList();

            var tasks = new List<Task>();

            foreach (var ip in allIPs)
            {
                // Mỗi IP = 1 Task
                tasks.Add(Task.Run(async () =>
                {
                    for (int port = 1; port <= 65535; port++) // demo port, có thể thay 65535
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            

                            // Lưu kết quả vào biến riêng
                            bool isPortOpen = await CheckPort(ip, port);

                            // Update ListBox ngay
                            listBox1.Invoke((Action)(() =>
                            {
                                listBox1.Items.Add($"Check {ip}:{port}" + (isPortOpen ? " | Live " : " | Die"));
                                listBox1.TopIndex = listBox1.Items.Count - 1;
                            }));

                            if (isPortOpen)
                            {

                                // Nếu port là HTTP/HTTPS → test proxy
                                
                                bool isProxyAlive = await TestProxy(ip, port);
                                // Update ListBox ngay
                                listBox1.Invoke((Action)(() =>
                                {
                                    listBox1.Items.Add($"Test {ip}:{port}" + ( isProxyAlive ? " | Live " : " | Die" ) );
                                    listBox1.TopIndex = listBox1.Items.Count - 1;
                                }));

                                string result = $"Check {ip}:{port}";

                                if (isProxyAlive)
                                {
                                    textBox1.Invoke((Action)(() =>
                                    {
                                        textBox1.AppendText(result + Environment.NewLine);
                                        textBox1.SelectionStart = textBox1.Text.Length;
                                        textBox1.ScrollToCaret();
                                    }));

                                    try { await File.AppendAllTextAsync(outputFile, result + Environment.NewLine); }
                                    catch { }
                                }
                                
                            }
                        }
                        finally { semaphore.Release(); }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            MessageBox.Show($"Quét xong! Proxy live đã lưu vào {outputFile}", "Hoàn tất");
            button1.Enabled = true;
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
    }
}
