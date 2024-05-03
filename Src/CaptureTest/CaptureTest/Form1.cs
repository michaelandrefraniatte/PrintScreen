using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DXGI = SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;
using D2D = SharpDX.Direct2D1;
using WIC = SharpDX.WIC;
using Interop = SharpDX.Mathematics.Interop;

namespace CaptureTest
{
    public partial class Form1 : Form
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        public static uint CurrentResolution = 0;
        private Image bitmap, img;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private static long length = 0;
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            GetLength();
            Task.Run(() => CopyScreen());
        }
        private void GetLength() 
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp)) 
            { 
                g.Clear(Color.Black); 
            }
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            length = ms.Length;
        }
        private void CopyScreen()
        {
            while (true)
            {
                bitmap = null;
                CaptureScreen();
                bitmap = img;
                if (bitmap != null)
                    this.BackgroundImage = bitmap;
                Thread.Sleep(100);
            }
        }
        public void CaptureScreen()
        {
            MemoryStream file = new MemoryStream();
            D3D11.Device _device;
            DXGI.OutputDuplication _outputDuplication;
            try
            {
                var adapterIndex = 0;
                var outputIndex = 0;
                using (var dxgiFactory = new DXGI.Factory1())
                using (var dxgiAdapter = dxgiFactory.GetAdapter1(adapterIndex))
                using (var output = dxgiAdapter.GetOutput(outputIndex))
                using (var dxgiOutput = output.QueryInterface<DXGI.Output1>())
                {
                    _device = new D3D11.Device(dxgiAdapter, D3D11.DeviceCreationFlags.BgraSupport);
                    _outputDuplication = dxgiOutput.DuplicateOutput(_device);
                }
                using (var dxgiDevice = _device.QueryInterface<DXGI.Device>())
                using (var d2dFactory = new D2D.Factory1())
                using (var d2dDevice = new D2D.Device(d2dFactory, dxgiDevice))
                {
                    _outputDuplication.AcquireNextFrame(1000000000, out var _, out var frame);
                    using (frame)
                    {
                        using (var frameDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.None))
                        using (var frameSurface = frame.QueryInterface<DXGI.Surface>())
                        using (var frameBitmap = new D2D.Bitmap1(frameDc, frameSurface))
                        {
                            var desc = new D3D11.Texture2DDescription
                            {
                                CpuAccessFlags = D3D11.CpuAccessFlags.None,
                                BindFlags = D3D11.BindFlags.RenderTarget,
                                Format = DXGI.Format.B8G8R8A8_UNorm,
                                Width = (int)(frameSurface.Description.Width),
                                Height = (int)(frameSurface.Description.Height),
                                OptionFlags = D3D11.ResourceOptionFlags.None,
                                MipLevels = 1,
                                ArraySize = 1,
                                SampleDescription = { Count = 1, Quality = 0 },
                                Usage = D3D11.ResourceUsage.Default
                            };
                            using (var texture = new D3D11.Texture2D(_device, desc))
                            using (var textureDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.None))
                            using (var textureSurface = texture.QueryInterface<DXGI.Surface>())
                            using (var textureBitmap = new D2D.Bitmap1(textureDc, textureSurface))
                            {
                                textureDc.Target = textureBitmap;
                                textureDc.BeginDraw();
                                textureDc.DrawBitmap(
                                    frameBitmap,
                                    new Interop.RawRectangleF(0, 0, desc.Width, desc.Height),
                                    1,
                                    D2D.InterpolationMode.HighQualityCubic,
                                    null,
                                    null);
                                textureDc.EndDraw();
                                using (var wic = new WIC.ImagingFactory2())
                                using (var jpegEncoder = new WIC.BitmapEncoder(wic, WIC.ContainerFormatGuids.Jpeg))
                                {
                                    jpegEncoder.Initialize(file);
                                    using (var jpegFrame = new WIC.BitmapFrameEncode(jpegEncoder))
                                    {
                                        jpegFrame.Initialize();
                                        using (var imageEncoder = new WIC.ImageEncoder(wic, d2dDevice))
                                        {
                                            imageEncoder.WriteFrame(textureBitmap, jpegFrame, new WIC.ImageParameters(
                                                new D2D.PixelFormat(desc.Format, D2D.AlphaMode.Premultiplied),
                                                textureDc.DotsPerInch.Width,
                                                textureDc.DotsPerInch.Height,
                                                0,
                                                0,
                                                desc.Width,
                                                desc.Height));
                                        }
                                        jpegFrame.Commit();
                                        jpegEncoder.Commit();
                                    }
                                }
                            }
                        }
                    }
                }
                _outputDuplication.ReleaseFrame();
                _device.Dispose();
                _outputDuplication.Dispose();
                MemoryStreamToBmp(file);
            }
            catch  
            {
                img = null;
            }
        }
        private void MemoryStreamToBmp(MemoryStream MS)
        {
            if (MS.Length > length)
            {
                MS.Seek(0, SeekOrigin.Begin);
                img = Bitmap.FromStream(MS);
            }
            else
                img = null;
        }
    }
}