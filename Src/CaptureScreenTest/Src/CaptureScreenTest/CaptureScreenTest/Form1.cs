using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
using System.Web;
using System.Xml.Linq;
namespace CaptureScreenTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private static SharpDX.DXGI.Factory1 factory = new SharpDX.DXGI.Factory1();
        private static Adapter adapter = null;
        private static Device device = null;
        private static SharpDX.Direct2D1.Factory1 fact = new SharpDX.Direct2D1.Factory1();
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private static Output1 output1;
        private static Texture2DDescription textureDesc;
        private static Texture2D screenTexture;
        private static System.Drawing.Bitmap bitmap;
        private void Form1_Load(object sender, EventArgs e)
        {
            InitCaptureScreen();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            CaptureScreen();
        }
        public static void InitCaptureScreen()
        {
            Screen targetScreen = Screen.PrimaryScreen;
            adapter = factory.Adapters.Where(x => x.Outputs.Any(o => o.Description.DeviceName == targetScreen.DeviceName)).FirstOrDefault();
            device = new Device(adapter);
            Output output = null;
            for (int i = 0; i < adapter.GetOutputCount(); i++)
            {
                output = adapter.GetOutput(i);
                if (output.Description.DeviceName == targetScreen.DeviceName)
                {
                    break;
                }
                else
                {
                    output.Dispose();
                }
            }
            output1 = output.QueryInterface<Output1>();
            if (output1.Description.Rotation == DisplayModeRotation.Rotate90)
            {
                width = targetScreen.Bounds.Height;
                height = targetScreen.Bounds.Width;
                int offsetX = targetScreen.Bounds.X;
            }
            else if (output1.Description.Rotation == DisplayModeRotation.Rotate270)
            {
                width = targetScreen.Bounds.Height;
                height = targetScreen.Bounds.Width;
                int offsetY = targetScreen.Bounds.Y;
            }
            textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            screenTexture = new Texture2D(device, textureDesc);
        }
        public void CaptureScreen()
        {
            using (var duplicatedOutput = output1.DuplicateOutput(device))
            {
                bool captureDone = false;
                SharpDX.DXGI.Resource screenResource = null;
                OutputDuplicateFrameInformation duplicateFrameInformation;
                for (int i = 0; !captureDone; i++)
                {
                    try
                    {
                        duplicatedOutput.TryAcquireNextFrame(1000, out duplicateFrameInformation, out screenResource);
                        if (i == 0)
                        {
                            screenResource.Dispose();
                            continue;
                        }
                        using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                        {
                            device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);
                        }
                        var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);
                        var boundsRect = new System.Drawing.Rectangle(0, 0, width, height);
                        using (bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                        {
                            var bitmapData = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                            var sourcePtr = mapSource.DataPointer;
                            var destinationPtr = bitmapData.Scan0;
                            for (int y = 0; y < height; y++)
                            {
                                Utilities.CopyMemory(destinationPtr, sourcePtr, width * 4);
                                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                                destinationPtr = IntPtr.Add(destinationPtr, bitmapData.Stride);
                            }
                            bitmap.UnlockBits(bitmapData);
                            bitmap.Save("capture.jpg", ImageFormat.Jpeg);
                        }
                        captureDone = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                    finally
                    {
                        if (screenResource != null)
                        {
                            screenResource.Dispose();
                        }
                        duplicatedOutput.ReleaseFrame();
                    }
                }
            }
        }
    }
}
