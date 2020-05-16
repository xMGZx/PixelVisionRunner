﻿//   
// Copyright (c) Jesse Freeman, Pixel Vision 8. All rights reserved.  
//  
// Licensed under the Microsoft Public License (MS-PL) except for a few
// portions of the code. See LICENSE file in the project root for full 
// license information. Third-party libraries used by Pixel Vision 8 are 
// under their own licenses. Please refer to those libraries for details 
// on the license they use.
// 
// Contributors
// --------------------------------------------------------
// This is the official list of Pixel Vision 8 contributors:
//  
// Jesse Freeman - @JesseFreeman
// Christina-Antoinette Neofotistou @CastPixel
// Christer Kaitila - @McFunkypants
// Pedro Medeiros - @saint11
// Shawn Rakowski - @shwany
//

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PixelVision8.Runner.Importers;
using PixelVision8.Runner.Parsers;

namespace PixelVision8.Runner.Data
{
    public class DisplayTarget : IDisplayTarget
    {
        private readonly int _monitorHeight = 640;
        private readonly int _monitorWidth = 640;
        private readonly GraphicsDeviceManager graphicManager;
        public readonly SpriteBatch spriteBatch;
        private int _monitorScale = 1;
        private bool _useCRT;
        public bool cropScreen = true;
        private Effect crtShader;
        public bool fullscreen = false;
        public Vector2 offset;
        public Texture2D renderTexture;
        public Vector2 scale = new Vector2(1, 1);
        private Effect shaderEffect;
        public bool stretchScreen;
        private int totalPixels;
        private Rectangle visibleRect;

        // TODO think we just need to pass in the active game and not the entire runner?
        public DisplayTarget(GraphicsDeviceManager graphicManager, int width, int height)
        {
            this.graphicManager = graphicManager;

            this.graphicManager.HardwareModeSwitch = false;

            spriteBatch = new SpriteBatch(graphicManager.GraphicsDevice);

            _monitorWidth = MathHelper.Clamp(width, 64, 640);
            _monitorHeight = MathHelper.Clamp(height, 64, 480);
        }

        public bool useCRT
        {
            get
            {
                if (crtShader == null || shaderEffect == null) return false;

                return _useCRT;
            }
            set
            {
                if (crtShader == null) return;

                _useCRT = value;

                shaderEffect = _useCRT ? crtShader : null;
            }
        }

        public float brightness
        {
            get => shaderEffect?.Parameters["brightboost"]?.GetValueSingle() ?? 0;
            set => shaderEffect?.Parameters["brightboost"]?.SetValue(MathHelper.Clamp(value, .5f, 1.5f));
        }

        public float sharpness
        {
            get => shaderEffect?.Parameters["hardPix"]?.GetValueSingle() ?? 0;
            set => shaderEffect?.Parameters["hardPix"]?.SetValue(value);
        }

        public bool HasShader()
        {
            return crtShader != null;
        }

        public Stream shaderPath
        {
            set
            {
                //                Effect tmpEffect;

                using (var reader = new BinaryReader(value))
                {
                    crtShader = new Effect(graphicManager.GraphicsDevice,
                        reader.ReadBytes((int) reader.BaseStream.Length));
                }



                // Sharpness
                // crtShader.Parameters["hardPix"]?.SetValue(-10.0f); // -3.0f (4 - 6)
                //
                // // Brightness
                // crtShader.Parameters["brightboost"]?.SetValue(1f); // 1.0f (.5 - 1.5)
                //
                //
                // crtShader.Parameters["hardScan"]?.SetValue(-6.0f); // -8.0f
                // crtShader.Parameters["warpX"]?.SetValue(0.008f); // 0.031f
                // crtShader.Parameters["warpY"]?.SetValue(0.01f); // 0.041f
                // crtShader.Parameters["shape"]?.SetValue(2f); // 2.0f



                // crtShader.Parameters["hardScan"]?.SetValue(-8.0f);
                // crtShader.Parameters["hardPix"]?.SetValue(-3.0f);
                // crtShader.Parameters["warpX"]?.SetValue(0.031f);
                // crtShader.Parameters["warpY"]?.SetValue(0.041f);
                // crtShader.Parameters["maskDark"]?.SetValue(0.5f);
                // crtShader.Parameters["maskLight"]?.SetValue(1.5f);
                // crtShader.Parameters["scaleInLinearGamma"]?.SetValue(1.0f);
                // crtShader.Parameters["shadowMask"]?.SetValue(3.0f);
                // crtShader.Parameters["brightboost"]?.SetValue(1.0f);
                // crtShader.Parameters["hardBloomScan"]?.SetValue(-1.5f);
                // crtShader.Parameters["hardBloomPix"]?.SetValue(-2.0f);
                // crtShader.Parameters["bloomAmount"]?.SetValue(0.15f);
                // crtShader.Parameters["shape"]?.SetValue(2.0f);

                useCRT = true;
            }
        }

        public int monitorScale
        {
            get => _monitorScale;
            set
            {
                var maxWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                var maxHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

                var fits = false;

                while (fits == false)
                {
                    var newWidth = _monitorWidth * value;
                    var newHeight = _monitorHeight * value;

                    if (newWidth < maxWidth && newHeight < maxHeight)
                    {
                        fits = true;
                        _monitorScale = value;
                    }
                    else
                    {
                        value--;
                    }
                }
            }
        }

        Rectangle displayRect = Rectangle.Empty;

        public void ResetResolution(int gameWidth, int gameHeight, int overScanX = 0, int overScanY = 0)
        {
            if (renderTexture == null || renderTexture.Width != gameWidth || renderTexture.Height != gameHeight)
            {
                renderTexture = new Texture2D(graphicManager.GraphicsDevice, gameWidth/4, gameHeight);

                // Set palette total
                crtShader.Parameters["imageWidth"].SetValue((float)gameWidth);

                // shaderEffect?.Parameters["textureSize"].SetValue(new Vector2(gameWidth, gameHeight));
                // // shaderEffect?.Parameters["videoSize"].SetValue(new Vector2(gameWidth, gameHeight));
                // shaderEffect?.Parameters["outputSize"].SetValue(new Vector2(graphicManager.PreferredBackBufferWidth,
                //     graphicManager.PreferredBackBufferHeight));

                // Set the new number of pixels
                // totalPixels = gameWidth * renderTexture.Height;
            }

            // Calculate the game's resolution
            visibleRect.Width = gameWidth - overScanX;
            visibleRect.Height = renderTexture.Height - overScanY;

            var tmpMonitorScale = fullscreen ? 1 : monitorScale;

            // Calculate the monitor's resolution
            var displayWidth = fullscreen
                ? GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width
                : _monitorWidth *
                  tmpMonitorScale;
            var displayHeight = fullscreen
                ? GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height
                : _monitorHeight * tmpMonitorScale;

            // Calculate the game scale
            // TODO need to figure out scale
            scale.X = (float) displayWidth / visibleRect.Width;
            scale.Y = (float) displayHeight / visibleRect.Height;

            if (!stretchScreen)
            {
                // To preserve the aspect ratio,
                // use the smaller scale factor.
                scale.X = Math.Min(scale.X, scale.Y);
                scale.Y = scale.X;
            }

            offset.X = (displayWidth - visibleRect.Width * scale.X) * .5f;
            offset.Y = (displayHeight - visibleRect.Height * scale.Y) * .5f;

            if (cropScreen && !fullscreen)
            {
                displayWidth = Math.Min(displayWidth, (int) (visibleRect.Width * scale.X));
                displayHeight = Math.Min(displayHeight, (int) (visibleRect.Height * scale.Y));
                offset.X = 0;
                offset.Y = 0;
            }

            displayRect.X = (int)offset.X;
            displayRect.Y = (int)offset.Y;
            displayRect.Width =(int)(visibleRect.Width * scale.X);
            displayRect.Height = (int) (visibleRect.Height * scale.Y);

            visibleRect.Width /= 4;

            // Apply changes
            graphicManager.IsFullScreen = fullscreen;

            if (graphicManager.PreferredBackBufferWidth != displayWidth ||
                graphicManager.PreferredBackBufferHeight != displayHeight)
            {
                graphicManager.PreferredBackBufferWidth = displayWidth;
                graphicManager.PreferredBackBufferHeight = displayHeight;
                graphicManager.ApplyChanges();
            }

        }

        private Texture2D _colorPallete;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixel;

        public void RebuildColorPalette(Color[] colors)
        {
            
            // Create color palette texture
            var cachedColors = colors;//pngReader.colorPalette.ToArray();

            _spriteBatch = new SpriteBatch(graphicManager.GraphicsDevice);

            _colorPallete = new Texture2D(graphicManager.GraphicsDevice, colors.Length, 1);

            var fullPalette = new Color[_colorPallete.Width];
            for (int i = 0; i < fullPalette.Length; i++) { fullPalette[i] = i < cachedColors.Length ? cachedColors[i] : cachedColors[0]; }

            _colorPallete.SetData(colors);

            _pixel = new Texture2D(graphicManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new Color[] { Color.White });

        }

        public void Render(int[] pixels)
        {

            renderTexture.SetData(pixels.Select(Convert.ToByte).ToArray());

            _spriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.PointClamp);
            crtShader.CurrentTechnique.Passes[0].Apply();
            graphicManager.GraphicsDevice.Textures[1] = _colorPallete;
            graphicManager.GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;
            graphicManager.GraphicsDevice.Textures[2] = renderTexture;
            graphicManager.GraphicsDevice.SamplerStates[2] = SamplerState.PointClamp;
            _spriteBatch.Draw(renderTexture,  displayRect, visibleRect, Color.White, 0f, Vector2.Zero, SpriteEffects.None,  1f);
            _spriteBatch.End();

        }

        //        public void CaptureScreenshot()
        //        {
        //            var gd = graphicManager.GraphicsDevice;
        //            
        //            Color[] colors = new Color[gd.Viewport.Width * gd.Viewport.Height];
        //
        //            gd.GetBackBufferData<Color>(colors);
        //            
        //            
        //            
        //        }
    }
}