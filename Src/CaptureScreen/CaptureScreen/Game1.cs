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

namespace CaptureScreen
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch;
        private Microsoft.Xna.Framework.Graphics.Texture2D texture1 = null, texture1temp = null;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private static long length = 0;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 300;
            _graphics.PreferredBackBufferHeight = 200;
        }

        private void GetLength()
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Black);
            }
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            length = ms.ToArray().Length;
            texture1temp = byteArrayToTexture(ms.ToArray());
        }

        protected override void Initialize()
        {
            GetLength();

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
            byte[] raw = CaptureScreen();
            if (raw.Length > length)
            {
                texture1 = byteArrayToTexture(raw);
                texture1temp = texture1;
            }
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);
            _spriteBatch.Begin();
            _spriteBatch.Draw(texture1temp, new Microsoft.Xna.Framework.Vector2(0, 0), new Microsoft.Xna.Framework.Rectangle(0, 0, width, height), Microsoft.Xna.Framework.Color.White);
            _spriteBatch.End();
            base.Draw(gameTime);
        }
        public byte[] CaptureScreen()
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
                return file.ToArray();
            }
            catch
            {
                return null;
            }
        }
        private Texture2D byteArrayToTexture(byte[] imageBytes)
        {
            using (var stream = new MemoryStream(imageBytes))
            {
                stream.Seek(0, SeekOrigin.Begin);
                var tx = Texture2D.FromStream(GraphicsDevice, stream);
                return tx;
            }
        }
    }
}