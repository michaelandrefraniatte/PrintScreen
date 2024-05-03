using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Windows.Forms;
using System;
using System.IO;
using DXGI = SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;
using D2D = SharpDX.Direct2D1;
using WIC = SharpDX.WIC;
using Interop = SharpDX.Mathematics.Interop;
using System.Drawing;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D11;
using System.Drawing.Imaging;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using SharpDX.DXGI;
using SharpDX.Direct3D9;
using SharpDX.Direct2D1;
using SharpDX.Win32;
using SharpDX.WIC;
using System.Data;

namespace CaptureScreen
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch;
        private Microsoft.Xna.Framework.Graphics.Texture2D texture1 = null, texture1temp = null;
        private System.Drawing.Image bmp, img;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private Device mDevice;
        private Texture2DDescription mTextureDesc;
        private OutputDescription mOutputDesc;
        private OutputDuplication mDeskDupl;
        private D3D11.Texture2D desktopImageTexture = null;
        private OutputDuplicateFrameInformation frameInfo = new OutputDuplicateFrameInformation();
        private byte[] raw;
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
        public Game1()
        {
            InitCaptureScreen();
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 500;
            _graphics.PreferredBackBufferHeight = 400;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new Microsoft.Xna.Framework.Graphics.SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Exit();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                CaptureScreen();
                byte[] dataraw = raw;
                texture1 = byteArrayToTexture(dataraw);
                texture1temp = texture1;
                GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);
                _spriteBatch.Begin();
                _spriteBatch.Draw(texture1temp, new Microsoft.Xna.Framework.Vector2(0, 0), new Microsoft.Xna.Framework.Rectangle(0, 0, width, height), Microsoft.Xna.Framework.Color.White);
                _spriteBatch.End();
                base.Draw(gameTime);
            }
            catch { }
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
                desktopImageTexture = new D3D11.Texture2D(mDevice, mTextureDesc);
            SharpDX.DXGI.Resource desktopResource = null;
            frameInfo = new OutputDuplicateFrameInformation();
            try
            {
                mDeskDupl.AcquireNextFrame(500, out frameInfo, out desktopResource);
            }
            catch { }
            using (var tempTexture = desktopResource.QueryInterface<D3D11.Texture2D>())
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
            raw = file.ToArray();
        }
        private void ReleaseFrame()
        {
            try
            {
                mDeskDupl.ReleaseFrame();
            }
            catch { }
        }
        private Microsoft.Xna.Framework.Graphics.Texture2D byteArrayToTexture(byte[] imageBytes)
        {
            using (var stream = new MemoryStream(imageBytes))
            {
                stream.Seek(0, SeekOrigin.Begin);
                var tx = Microsoft.Xna.Framework.Graphics.Texture2D.FromStream(GraphicsDevice, stream);
                return tx;
            }
        }
    }
}