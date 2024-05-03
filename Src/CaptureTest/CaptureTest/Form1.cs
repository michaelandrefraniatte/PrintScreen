using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.DXGI;
using SharpDX;
using SharpDX.Direct3D11;
using System.Drawing.Imaging;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using DesktopDuplication;
using System.IO;

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
        private System.Drawing.Image bmp, img;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private Device mDevice;
        private Texture2DDescription mTextureDesc;
        private OutputDescription mOutputDesc;
        private OutputDuplication mDeskDupl;
        private Texture2D desktopImageTexture = null;
        private OutputDuplicateFrameInformation frameInfo = new OutputDuplicateFrameInformation();
        private System.Drawing.Bitmap finalImage1, finalImage2;
        private bool isFinalImage1 = false;
        private System.Drawing.Bitmap FinalImage
        {
            get
            {
                return isFinalImage1 ? finalImage1 : finalImage2;
            }
            set
            {
                if (isFinalImage1)
                {
                    finalImage2 = value;
                    if (finalImage1 != null) finalImage1.Dispose();
                }
                else
                {
                    finalImage1 = value;
                    if (finalImage2 != null) finalImage2.Dispose();
                }
                isFinalImage1 = !isFinalImage1;
            }
        }
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
            InitCaptureScreen();
            Task.Run(() => CopyScreen());
        }
        private void CopyScreen()
        {
            while (true)
            {
                bmp = null;
                CaptureScreen();
                bmp = img;
                if (bmp != null)
                    this.BackgroundImage = bmp;
                Thread.Sleep(1000);
            }
        }
        public void InitCaptureScreen()
        {
            Adapter1 adapter = null;
            try
            {
                adapter = new SharpDX.DXGI.Factory1().GetAdapter1(0);
            }
            catch { }
            this.mDevice = new Device(adapter);
            Output output = null;
            try
            {
                output = adapter.GetOutput(0);
            }
            catch { }
            var output1 = output.QueryInterface<Output1>();
            this.mOutputDesc = output.Description;
            this.mTextureDesc = new Texture2DDescription()
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            try
            {
                this.mDeskDupl = output1.DuplicateOutput(mDevice);
            }
            catch
            {
            }
        }
        public void CaptureScreen()
        {
            RetrieveFrame();
            try
            {
                ProcessFrame();
            }
            catch
            {
                ReleaseFrame();
            }
            try
            {
                ReleaseFrame();
            }
            catch
            { 
            }
        }
        private void RetrieveFrame()
        {
            if (desktopImageTexture == null)
                desktopImageTexture = new Texture2D(mDevice, mTextureDesc);
            SharpDX.DXGI.Resource desktopResource = null;
            frameInfo = new OutputDuplicateFrameInformation();
            try
            {
                mDeskDupl.AcquireNextFrame(500, out frameInfo, out desktopResource);
            }
            catch {}
            using (var tempTexture = desktopResource.QueryInterface<Texture2D>())
                mDevice.ImmediateContext.CopyResource(tempTexture, desktopImageTexture);
            desktopResource.Dispose();
        }
        private void ProcessFrame()
        {
            MemoryStream file = new MemoryStream();

            // Get the desktop capture texture
            var mapSource = mDevice.ImmediateContext.MapSubresource(desktopImageTexture, 0, MapMode.Read, MapFlags.None);

            FinalImage = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            var boundsRect = new System.Drawing.Rectangle(0, 0, width, height);
            // Copy pixels from screen capture Texture to GDI bitmap
            var mapDest = FinalImage.LockBits(boundsRect, ImageLockMode.WriteOnly, FinalImage.PixelFormat);
            var sourcePtr = mapSource.DataPointer;
            var destPtr = mapDest.Scan0;
            for (int y = 0; y < height; y++)
            {
                // Copy a single line 
                Utilities.CopyMemory(destPtr, sourcePtr, width * 4);

                // Advance pointers
                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                destPtr = IntPtr.Add(destPtr, mapDest.Stride);
            }

            // Release source and dest locks
            FinalImage.UnlockBits(mapDest);
            mDevice.ImmediateContext.UnmapSubresource(desktopImageTexture, 0);
            FinalImage.Save(file, System.Drawing.Imaging.ImageFormat.Jpeg);
            MemoryStreamToBmp(file);
        }
        private void ReleaseFrame()
        {
            try
            {
                mDeskDupl.ReleaseFrame();
            }
            catch  { }
        }
        private void MemoryStreamToBmp(MemoryStream MS)
        {
            MS.Seek(0, SeekOrigin.Begin);
            img = System.Drawing.Bitmap.FromStream(MS);
        }
    }
}