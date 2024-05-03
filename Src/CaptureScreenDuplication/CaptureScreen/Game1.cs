using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Windows.Forms;
using System;
using System.IO;
using DesktopDuplication;

namespace CaptureScreen
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch _spriteBatch;
        private Microsoft.Xna.Framework.Graphics.Texture2D texture1 = null, texture1temp = null;
        private static int width = Screen.PrimaryScreen.Bounds.Width, height = Screen.PrimaryScreen.Bounds.Height;
        private static DesktopDuplicator desktopDuplicator;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 300;
            _graphics.PreferredBackBufferHeight = 200;
            Exiting += Shutdown;
            InitCapture();
        }

        public void Shutdown(object sender, EventArgs e)
        {
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

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
                texture1 = byteArrayToTexture(CaptureScreen());
                texture1temp = texture1;
                GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);
                _spriteBatch.Begin();
                _spriteBatch.Draw(texture1temp, new Microsoft.Xna.Framework.Vector2(0, 0), new Microsoft.Xna.Framework.Rectangle(0, 0, width, height), Microsoft.Xna.Framework.Color.White);
                _spriteBatch.End();
                base.Draw(gameTime);
            }
            catch { }
        }
        public void InitCapture()
        {
            try
            {
                desktopDuplicator = new DesktopDuplicator(0);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString());
            }
        }
        public byte[] CaptureScreen()
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
                return frame.DesktopImage;
            }
            return null;
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