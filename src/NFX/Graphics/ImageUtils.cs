﻿#warning Implement using PAL

/*<FILE_LICENSE>
* NFX (.NET Framework Extension) Unistack Library
* Copyright 2003-2018 Agnicore Inc. portions ITAdapter Corp. Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
</FILE_LICENSE>*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NFX.Graphics
{
  /// <summary>
  /// Proivdes helpers for centering and fitting images
  /// </summary>
  public static class ImageUtils
  {
    [ThreadStatic] private static Dictionary<Color, int> ts_FirstHist  = new Dictionary<Color, int>();
    [ThreadStatic] private static Dictionary<Color, int> ts_SecondHist = new Dictionary<Color, int>();
    [ThreadStatic] private static Dictionary<Color, int> ts_ThirdHist  = new Dictionary<Color, int>();
    [ThreadStatic] private static Dictionary<Color, int> ts_BckHist    = new Dictionary<Color, int>();

    /// <summary>
    /// Extracts three main colors and background color from source image.
    /// Does the same as <see cref="ExtractMainColors"/> method but performs the second extraction attempt 
    /// if the first attempt returns almost the same colors
    /// </summary>
    /// <param name="srcBmp">Source image</param>
    /// <param name="imgDistEps">Color similarity factor. If less that specified value, then the second extraction attempt will be performed</param>
    /// <param name="resizeWidth">Preprocessing image width</param>
    /// <param name="resizeHeight">Preprocessing image height</param>
    /// (source image color quality reduced down to 256/<paramref name="dwnFactor1"/> color per each of RGB channel (2*2*2=8 base colors used as default))</param>
    /// <param name="dwnFactor2">Secondary downgrade factor for inner-area main color selection</param>
    /// <param name="interiorPct">Value within (0,1) range that indicates portion of image interior,
    /// i.e. 0.9 means that 10% part of the image will be used for boundary detection</param>
    /// <param name="imgDistEps">Color similarity factor. If less that specified value, then the second extraction attempt will be performed</param>
    /// <returns>Three main colors and background volor</returns>
    public static Color[] ExtractMainColors2Iter(Image srcBmp,
                                                 int resizeWidth = 64, int resizeHeight = 64,
                                                 int dwnFactor1 = 128, int dwnFactor2 = 24,
                                                 float interiorPct = 0.9F,
                                                 float imgDistEps = 0.2F)
    {
      var topColors = ExtractMainColors(srcBmp, resizeWidth, resizeHeight, dwnFactor1, dwnFactor2, interiorPct);
      var d12 = colorsAbsDist(topColors[0], topColors[1]);
      var d23 = colorsAbsDist(topColors[1], topColors[2]);
      if (d12 < imgDistEps && d23 < imgDistEps)
        topColors = ExtractMainColors(srcBmp, resizeWidth, resizeHeight, dwnFactor1 / 2, dwnFactor2, interiorPct);

      return topColors;
    }

    /// <summary>
    /// Extracts three main colors and background color from source image by the following algorithm:
    /// 1. source image color quality reduced down to 256/<paramref name="dwnFactor1"/> color per each of RGB channel
    ///    (2*2*2=8 base colors used as default)
    /// 2. four areas (three main and one background) with the biggest color frequencies are taken
    /// 3. color frequency analisys is performed in each area which gives area main color
    ///    (image color quality reduced down to 256/<paramref name="dwnFactor2"/> color per each of RGB channel)
    ///
    /// Background area search is limited to [1-<paramref name="interiorPct"/>, <paramref name="interiorPct"/>] portion of image interior
    /// </summary>
    /// <param name="srcImg">Source image</param>
    /// <param name="resizeWidth">Preprocessing image width</param>
    /// <param name="resizeHeight">Preprocessing image height</param>
    /// <param name="dwnFactor1">Main downgrade factor
    /// (source image color quality reduced down to 256/<paramref name="dwnFactor1"/> color per each of RGB channel (2*2*2=8 base colors used as default))</param>
    /// <param name="dwnFactor2">Secondary downgrade factor for inner-area main color selection</param>
    /// <param name="interiorPct">Value within (0,1) range that indicates portion of image interior,
    /// i.e. 0.9 means that 10% part of the image will be used for boundary detection</param>
    /// <returns>Three main colors and background volor</returns>
    public static unsafe Color[] ExtractMainColors(Image srcImg,
                                                   int resizeWidth = 64, int resizeHeight = 64,
                                                   int dwnFactor1 = 128, int dwnFactor2 = 24,
                                                   float interiorPct = 0.9F)
    {
      var height = srcImg.Height;
      var width  = srcImg.Width;
      var interiorWidth  = interiorPct * resizeWidth;
      var interiorHeight = interiorPct * resizeHeight;
      var mainHist = new Dictionary<Color, int>();
      var backHist = new Dictionary<Color, int>();

      // STEP 1: resize image
      using (var rszImg = srcImg.ResizeTo(resizeWidth, resizeHeight))
      using (var dwnImg = Image.Of(width, height, rszImg.XResolution, rszImg.YResolution, rszImg.PixelFormat))
      {
        // STEP 2: extract downgraded (very few colors) - color histogramm (main and background)
        // IMPORTANT: these colors will be used below not as itself but only AS A MASK 
        for (int x=0; x<resizeWidth; x++)
        for (int y=0; y<resizeHeight; y++)
        {
          var p = rszImg.GetPixel(x, y);

          var a = p.A - p.A%dwnFactor1;
          var r = p.R - p.R%dwnFactor1;
          var g = p.G - p.G%dwnFactor1;
          var b = p.B - p.B%dwnFactor1;
          var color = Color.FromArgb(a, r, g, b);

          // histogramm for a main color
          if (!mainHist.ContainsKey(color)) mainHist[color] = 1;
          else mainHist[color] += 1;

          // histogramm for a background color
          if (Math.Abs(2 * x - resizeWidth) >= interiorWidth || 
              Math.Abs(2 * y - resizeHeight) >= interiorHeight)
          {
            if (!backHist.ContainsKey(color)) backHist[color] = 1;
            else backHist[color] += 1;
          }

          dwnImg.SetPixel(x, y, p);
        }

        // take background area color and the first three colors (i.e. main image areas) except background
        var backArea = backHist.FirstMax(h => h.Value).Key;
        var areas = mainHist.Where(h => h.Key != backArea).OrderByDescending(h => h.Value).Take(3).ToList();
        var firstArea  = (areas.Count > 0) ? areas[0].Key : backArea;
        var secondArea = (areas.Count > 1) ? areas[1].Key : firstArea;
        var thirdArea  = (areas.Count > 2) ? areas[2].Key : secondArea;

        // get histogramm for background area each of three main areas
        ts_FirstHist.Clear();
        ts_SecondHist.Clear();
        ts_ThirdHist.Clear();
        ts_BckHist.Clear();

        // STEP 3: fill color (1,2,3+background) histogramms
        for (int x=0; x<resizeWidth; x++)
        for (int y=0; y<resizeHeight; y++)
        {
          // fetch histogramm by a mask
          var maskP = dwnImg.GetPixel(x, y);
          Dictionary<Color, int> h;
          if (maskP == firstArea)       h = ts_FirstHist;
          else if (maskP == secondArea) h = ts_SecondHist;
          else if (maskP == thirdArea)  h = ts_ThirdHist;
          else if (maskP == backArea)   h = ts_BckHist;
          else continue;
          
          var p = rszImg.GetPixel(x, y);
          var a = p.A - p.A%dwnFactor2;
          var r = p.R - p.R%dwnFactor2;
          var g = p.G - p.G%dwnFactor2;
          var b = p.B - p.B%dwnFactor2;
          var color = Color.FromArgb(a, r, g, b);

          if (!h.ContainsKey(color)) h[color] = 1;
          else h[color] += 1;
        }

        // STEP 4: extract color for each histogram
        var firstHist  = ts_FirstHist;
        var secondHist = (ts_SecondHist.Count > 0) ? ts_SecondHist : firstHist;
        var thirdHist  = (ts_ThirdHist.Count > 0) ? ts_ThirdHist : secondHist;
        var bckHist    = (ts_BckHist.Count > 0) ? ts_BckHist : thirdHist;
        var topColors = new[]
        {
          colorFromHist(firstHist),
          colorFromHist(secondHist),
          colorFromHist(thirdHist),
          colorFromHist(bckHist)
        };

        return topColors;
      }

      #region deprecated, remove after testing
      //var height = srcBmp.Height;
      //var width = srcBmp.Width;
      //var mainHist = new Dictionary<Color, int>();
      //var backHist = new Dictionary<Color, int>();

      //using (var dwnBmp = new Image(width, height))
      //{
      //  BitmapData srcData = null;
      //  BitmapData dwnData = null;

      //  try
      //  {
      //    // extract downgraded (very few colors) color histogramm
      //    srcData = srcBmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, srcBmp.PixelFormat);
      //    int srcBytesPerPixel = Bitmap.GetPixelFormatSize(srcBmp.PixelFormat) / 8;
      //    int srcHeightInPixels = srcData.Height;
      //    int srcWidthInBytes = srcData.Width * srcBytesPerPixel;
      //    byte* srcFirstPixel = (byte*)srcData.Scan0;

      //    dwnData = dwnBmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, dwnBmp.PixelFormat);
      //    int dwnBytesPerPixel = Bitmap.GetPixelFormatSize(dwnBmp.PixelFormat) / 8;
      //    int dwnHeightInPixels = dwnData.Height;
      //    int dwnWidthInBytes = dwnData.Width * dwnBytesPerPixel;
      //    byte* dwnFirstPixel = (byte*)dwnData.Scan0;

      //    for (int y = 0; y < height; y++)
      //    {
      //      byte* srcLine = srcFirstPixel + (y * srcData.Stride);
      //      byte* dwnLine = dwnFirstPixel + (y * dwnData.Stride);
      //      for (int x = 0; x < srcWidthInBytes; x += srcBytesPerPixel)
      //      {
      //        var b = dwnFactor1 * (srcLine[x] / dwnFactor1);
      //        var g = dwnFactor1 * (srcLine[x + 1] / dwnFactor1);
      //        var r = dwnFactor1 * (srcLine[x + 2] / dwnFactor1);
      //        var color = Color.FromArgb(r, g, b);

      //        // histogramm for main color
      //        if (!mainHist.ContainsKey(color)) mainHist[color] = 1;
      //        else mainHist[color] += 1;

      //        // histogramm for background color
      //        if (Math.Abs(2 * x - srcWidthInBytes) >= interiorPct * srcWidthInBytes || Math.Abs(2 * y - height) >= interiorPct * height)
      //        {
      //          if (!backHist.ContainsKey(color)) backHist[color] = 1;
      //          else backHist[color] += 1;
      //        }

      //        dwnLine[x] = (byte)b;
      //        dwnLine[x + 1] = (byte)g;
      //        dwnLine[x + 2] = (byte)r;
      //      }
      //    }

      //    // take background area color and the first three colors (i.e. main image areas) except background
      //    var backArea = backHist.OrderByDescending(h => h.Value).First().Key;
      //    var areas = mainHist.Where(h => h.Key != backArea).OrderByDescending(h => h.Value).Take(3).ToList();
      //    var firstArea = (areas.Count > 0) ? areas[0].Key : backArea;
      //    var secondArea = (areas.Count > 1) ? areas[1].Key : firstArea;
      //    var thirdArea = (areas.Count > 2) ? areas[2].Key : secondArea;

      //    // get histogramm for background area each of three main areas
      //    if (ts_FirstHist == null) ts_FirstHist = new Dictionary<Color, int>(); else ts_FirstHist.Clear();
      //    if (ts_SecondHist == null) ts_SecondHist = new Dictionary<Color, int>(); else ts_SecondHist.Clear();
      //    if (ts_ThirdHist == null) ts_ThirdHist = new Dictionary<Color, int>(); else ts_ThirdHist.Clear();
      //    if (ts_BckHist == null) ts_BckHist = new Dictionary<Color, int>(); else ts_BckHist.Clear();

      //    for (int y = 0; y < height; y++)
      //    {
      //      byte* srcLine = srcFirstPixel + (y * srcData.Stride);
      //      byte* dwnLine = dwnFirstPixel + (y * dwnData.Stride);
      //      for (int x = 0; x < srcWidthInBytes; x += srcBytesPerPixel)
      //      {
      //        var b = dwnLine[x];
      //        var g = dwnLine[x + 1];
      //        var r = dwnLine[x + 2];
      //        var color = Color.FromArgb(r, g, b);
      //        Dictionary<Color, int> h;
      //        if (color == firstArea) h = ts_FirstHist;
      //        else if (color == secondArea) h = ts_SecondHist;
      //        else if (color == thirdArea) h = ts_ThirdHist;
      //        else if (color == backArea) h = ts_BckHist;
      //        else continue;

      //        b = (byte)(dwnFactor2 * (srcLine[x] / dwnFactor2));
      //        g = (byte)(dwnFactor2 * (srcLine[x + 1] / dwnFactor2));
      //        r = (byte)(dwnFactor2 * (srcLine[x + 2] / dwnFactor2));
      //        color = Color.FromArgb(r, g, b);
      //        if (!h.ContainsKey(color)) h[color] = 1;
      //        else h[color] += 1;
      //      }
      //    }
      //  }
      //  finally
      //  {
      //    if (srcData != null) srcBmp.UnlockBits(srcData);
      //    if (dwnBmp != null) dwnBmp.UnlockBits(dwnData);
      //  }

      //  // extract color for each histogram
      //  var firstHist = ts_FirstHist;
      //  var secondHist = (ts_SecondHist.Count > 0) ? ts_SecondHist : firstHist;
      //  var thirdHist = (ts_ThirdHist.Count > 0) ? ts_ThirdHist : secondHist;
      //  var bckHist = (ts_BckHist.Count > 0) ? ts_BckHist : thirdHist;
      //  var topColors = new[]
      //  {
      //    colorFromHist(firstHist),
      //    colorFromHist(secondHist),
      //    colorFromHist(thirdHist),
      //    colorFromHist(bckHist)
      //  };

      //  return topColors;
      //}
      #endregion
    }

    /// <summary>
    /// Scales source image so it fits in the desired image size preserving aspect ratio.
    /// This function is usable for profile picture size/aspect normalization
    /// </summary>
    public static Image NormalizeCenteredImage(this Image srcImage, int targetWidth = 128, int targetHeight = 128, int xDpi = 96, int yDpi = 96)
    {
      if (srcImage == null || targetWidth < 1 || targetHeight < 1 || xDpi < 1 || yDpi < 1)
        throw new NFXException(StringConsts.ARGUMENT_ERROR + "NormalizeCenteredImage(...)");

      var result = Image.Of(targetWidth, targetHeight, xDpi, yDpi);

      using (var canvas = result.CreateCanvas())
      {
        var scx = srcImage.Width / 2;
        var scy = srcImage.Height / 2;
        var sar = srcImage.Width / (double)srcImage.Height;
        int sx, sy, sw, sh;

        if (targetHeight > targetWidth)
        {
          var ky = srcImage.Height / (double)targetHeight;
          sw = (int)(ky * targetWidth);
          sh = srcImage.Height;
        }
        else
        {
          var kx = srcImage.Width / (double)targetWidth;
          sw = srcImage.Width;
          sh = (int)(kx * targetHeight);
        }

        if (sw > srcImage.Width)
        {
          var k = (sw - srcImage.Width) / (double)srcImage.Width;
          sw = srcImage.Width;
          sh = (int)(sh * (1.0 - k * sar));
        }
        if (sh > srcImage.Height)
        {
          var k = (sh - srcImage.Height) / (double)srcImage.Height;
          sh = srcImage.Height;
          sw = (int)(sw * (1.0 - k / sar));
        }

        sx = scx - sw / 2;
        sy = scy - sh / 2;

        canvas.Interpolation = InterpolationMode.HQBicubic;
        canvas.DrawImage(srcImage,
                     new Rectangle(0, 0, targetWidth, targetHeight),
                     new Rectangle(sx, sy, sw, sh));
      }

      return result;
    }

    /// <summary>
    /// Scales source image so it uniformly (without cropping) fits in the desired image size preserving aspect ratio.
    /// This function is usable for profile picture size/aspect normalization
    /// </summary>
    public static Image FitCenteredImage(this Image srcImage,
                                         int targetWidth = 128, int targetHeight = 128, int xDpi = 96, int yDpi = 96,
                                         Color? bColor = null)
    {
      if (srcImage == null || targetWidth < 1 || targetHeight < 1 || xDpi < 1 || yDpi < 1)
        throw new NFXException(StringConsts.ARGUMENT_ERROR + "FitCenteredImage(...)");

      var result = Image.Of(targetWidth, targetHeight, xDpi, yDpi);
      result.MakeTransparent();

      using (var canvas = result.CreateCanvas())
      {
        var xAspect = targetWidth / (float)srcImage.Width;
        var yAspect = targetHeight / (float)srcImage.Height;
        var ar = Math.Min(xAspect, yAspect);

        var newWidth = (int)(srcImage.Width * ar);
        var newHeight = (int)(srcImage.Height * ar);
        var newX = (targetWidth - newWidth) / 2;
        var newY = (targetHeight - newHeight) / 2;

        canvas.Interpolation = InterpolationMode.HQBicubic;
        canvas.Clear(bColor ?? Color.White);
        canvas.DrawImage(srcImage, newX, newY, newWidth, newHeight);
      }

      return result;
    }

    #warning WHo uses this method?
    //public static Bitmap ModifyBitsPerBixel(Image img, PixelFormat format)
    //{
    //  var bmp = new Bitmap(img.Width, img.Height, format);
    //  using (var gr = Graphics.FromImage(bmp))
    //    gr.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));

    //  return bmp;
    //}

    #region .pvt

    /// <summary>
    /// Extracts "main" color from color histogramm.
    /// Takes into account three most-frequent colors with their frequencies (more frequent gives greater contribution)
    /// </summary>
    private static Color colorFromHist(Dictionary<Color, int> hist)
    {
      var topColors   = hist.OrderByDescending(h => h.Value).Take(3).ToList();
      var firstColor  = topColors[0];
      var secondColor = topColors.Count > 1 ? topColors[1] : firstColor;
      var thirdColor  = topColors.Count > 2 ? topColors[2] : secondColor;
      var cnt = firstColor.Value + secondColor.Value + thirdColor.Value;

      var r = (firstColor.Key.R * firstColor.Value + secondColor.Key.R * secondColor.Value + thirdColor.Key.R * thirdColor.Value) / cnt;
      var g = (firstColor.Key.G * firstColor.Value + secondColor.Key.G * secondColor.Value + thirdColor.Key.G * thirdColor.Value) / cnt;
      var b = (firstColor.Key.B * firstColor.Value + secondColor.Key.B * secondColor.Value + thirdColor.Key.B * thirdColor.Value) / cnt;

      return Color.FromArgb(r, g, b);
    }

    /// <summary>
    /// Calculates abs color metric
    /// </summary>
    private static float colorsAbsDist(Color c1, Color c2)
    {
      var d = Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
      return d / 256.0F;
    }

    #endregion
  }
}