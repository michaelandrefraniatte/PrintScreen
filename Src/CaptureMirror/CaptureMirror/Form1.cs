using System.Drawing.Imaging;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Rectangle = System.Drawing.Rectangle;
using Bitmap = System.Drawing.Bitmap;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Xml.Linq;
namespace CaptureMirror
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
        private static WindowRenderTarget target;
        private static SharpDX.Direct2D1.Factory1 fact = new SharpDX.Direct2D1.Factory1();
        private static RenderTargetProperties renderProp;
        private static HwndRenderTargetProperties winProp;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private static byte[] imageBytesOrigin;
        private static bool closed;
        private void Form1_Shown(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            this.ClientSize = new System.Drawing.Size(width, height);
            this.Location = new System.Drawing.Point(0, 0);
            InitDisplayCapture(this.Handle);
            Task.Run(() => taskScreen());
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
        private static void taskScreen()
        {
            while (!closed)
            {
                Bitmap img = CaptureScreen();
                DisplayCapture(img);
                System.Threading.Thread.Sleep(10000);
            }
        }
        public static Bitmap CaptureScreen()
        {
            Bitmap img = null;
            Bitmap bitmap = new Bitmap(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
            Graphics graphics = Graphics.FromImage(bitmap as System.Drawing.Image);
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graphics.SmoothingMode = SmoothingMode.HighSpeed;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
            imageBytesOrigin = CropBitmapToJPEGBytes(bitmap);
            MessageBox.Show((imageBytesOrigin.Length / 1024).ToString());
            byte[] clientDataScreen = new byte[1024 * 2000];
            byte[] clientDataScreenReconstruct = new byte[1024 * 2000];
            byte[] clientDataScreenConstruct;
            clientDataScreen = imageBytesOrigin;
            try
            {
                for (int nelement = 0; nelement < 2; nelement++)
                {
                    int lngth = clientDataScreen.Length / 2;
                    clientDataScreenConstruct = new byte[lngth + 1];
                    Array.ConstrainedCopy(clientDataScreen, lngth * nelement, clientDataScreenConstruct, 1, lngth);
                    clientDataScreenConstruct[0] = (byte)nelement;
                    int position = (int)clientDataScreenConstruct[0];
                    lngth = clientDataScreenConstruct.Length - 1;
                    Array.ConstrainedCopy(clientDataScreenConstruct, 1, clientDataScreenReconstruct, lngth * position, lngth);
                }
                img = CropJPEGBytesToBitmap(clientDataScreenReconstruct);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return img;
        }
        private static System.Drawing.Bitmap CompressImage(Bitmap bitmap)
        {
            ImageCodecInfo jpegEncoder = ImageCodecInfo.GetImageDecoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            EncoderParameters encoderParameters = new EncoderParameters(1);
            int jpegQuality = 30;
            encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQuality);
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, jpegEncoder, encoderParameters);
                return (Bitmap)System.Drawing.Image.FromStream(ms);
            }
        }
        private static byte[] CropBitmapToJPEGBytes(Bitmap orig)
        {
            orig = CompressImage(orig);
            Rectangle rect = new Rectangle(0, 0, orig.Width, orig.Height);
            System.Drawing.Imaging.BitmapData bmpData = orig.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, orig.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * orig.Height;
            MessageBox.Show((bytes / orig.Height / 4).ToString());
            byte[] rgbValues = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);
            orig.UnlockBits(bmpData);
            MemoryStream ms = new MemoryStream();
            orig.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
        private static Bitmap CropJPEGBytesToBitmap(byte[] imageBytes)
        {
            MemoryStream s = new MemoryStream(imageBytes);
            System.Drawing.Image returnImage = System.Drawing.Image.FromStream(s);
            return (Bitmap)returnImage;
        }
        private static void InitDisplayCapture(IntPtr handle)
        {
            renderProp = new RenderTargetProperties()
            {
                DpiX = 0,
                DpiY = 0,
                MinLevel = SharpDX.Direct2D1.FeatureLevel.Level_DEFAULT,
                PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                Type = RenderTargetType.Hardware,
                Usage = RenderTargetUsage.None
            };
            winProp = new HwndRenderTargetProperties()
            {
                Hwnd = handle,
                PixelSize = new Size2(width, height),
                PresentOptions = PresentOptions.Immediately
            };
            target = new WindowRenderTarget(fact, renderProp, winProp);
        }
        private static void DisplayCapture(Bitmap image1)
        {
            using (var bmp = image1)
            {
                System.Drawing.Imaging.BitmapData bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                SharpDX.DataStream stream = new SharpDX.DataStream(bmpData.Scan0, bmpData.Stride * bmpData.Height, true, false);
                SharpDX.Direct2D1.PixelFormat pFormat = new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied);
                SharpDX.Direct2D1.BitmapProperties bmpProps = new SharpDX.Direct2D1.BitmapProperties(pFormat);
                SharpDX.Direct2D1.Bitmap result = new SharpDX.Direct2D1.Bitmap(target, new SharpDX.Size2(width, height), stream, bmpData.Stride, bmpProps);
                bmp.UnlockBits(bmpData);
                stream.Dispose();
                bmp.Dispose();
                target.BeginDraw();
                target.DrawBitmap(result, 1.0f, BitmapInterpolationMode.NearestNeighbor);
                target.EndDraw();
            }
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closed = true;
        }
    }
}