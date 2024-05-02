using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using Bitmap = System.Drawing.Bitmap;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using DesktopDuplication;

namespace PrintScreen
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        [DllImport("user32.dll")]
        public static extern bool GetAsyncKeyState(System.Windows.Forms.Keys vKey);
        private static bool closed = false;
        private static List<string> list = new List<string>(0);
        private static int listinc = 0;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private static System.Drawing.Bitmap bitmap;
        private static DesktopDuplicator desktopDuplicator;
        private static int[] wd = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        private static int[] wu = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        private static void valchanged(int n, bool val)
        {
            if (val)
            {
                if (wd[n] <= 1)
                {
                    wd[n] = wd[n] + 1;
                }
                wu[n] = 0;
            }
            else
            {
                if (wu[n] <= 1)
                {
                    wu[n] = wu[n] + 1;
                }
                wd[n] = 0;
            }
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closed = true;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            LoadFiles();
            InitCaptureScreen();
            Task.Run(() => Start());
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (keyData == Keys.Escape)
            {
                this.Close();
            }
        }
        private void Start()
        {
            while (!closed)
            {
                valchanged(1, GetAsyncKeyState(Keys.PrintScreen));
                if (wd[1] == 1)
                {
                    CaptureScreen();
                }
                System.Threading.Thread.Sleep(40);
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            listinc++;
            if (listinc >= list.Count)
            {
                listinc = 0;
            }
            ChangeContent();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            listinc--;
            if (listinc < 0)
            {
                listinc = list.Count - 1;
            }
            ChangeContent();
        }
        private void ChangeContent()
        {
            try
            {
                if (list.Count != 0)
                {
                    pictureBox1.BackgroundImage = Bitmap.FromFile(list[listinc]);
                    label1.Text = list[listinc];
                }
                else
                {
                    pictureBox1.BackgroundImage = null;
                    label1.Text = "none";
                }
            }
            catch { }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                System.Drawing.Image oldbitmap = pictureBox1.BackgroundImage;
                pictureBox1.BackgroundImage = null;
                oldbitmap.Dispose();
                File.Delete(label1.Text);
                LoadFiles();
            }
            catch { }
        }
        private void LoadFiles()
        {
            list = new List<string>(0);
            string folderpath = System.Reflection.Assembly.GetEntryAssembly().Location.Replace(@"file:\", "").Replace(Process.GetCurrentProcess().ProcessName + ".exe", "").Replace(@"\", "/").Replace(@"//", "");
            string[] files = Directory.GetFiles(folderpath);
            foreach (string file in files)
            {
                if (file.EndsWith(".jpg"))
                {
                    list.Add(file.Replace(folderpath, ""));
                }
            }
            listinc = list.Count - 1;
            ChangeContent();
        }
        public static void InitCaptureScreen()
        {
            try
            {
                desktopDuplicator = new DesktopDuplicator(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        public void CaptureScreen()
        {
            Application.DoEvents();
            DesktopFrame frame = null;
            try
            {
                frame = desktopDuplicator.GetLatestFrame();
            }
            catch
            {
                desktopDuplicator = new DesktopDuplicator(0);
            }
            if (frame != null)
            {
                bitmap = frame.DesktopImage;
                string RecordFileName = "Print_" + DateTime.Now.ToString().Replace("/", "_").Replace(":", "_").Replace(" ", "_") + ".jpg";
                bitmap.Save(RecordFileName, ImageFormat.Jpeg);
                pictureBox1.BackgroundImage = Bitmap.FromFile(RecordFileName);
                list.Add(RecordFileName);
                listinc = list.Count - 1;
                label1.Text = RecordFileName;
            }
        }
    }
}