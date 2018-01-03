﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGR = System.Drawing.Graphics;

using NFX.Graphics;
using NFX.PAL.Graphics;

namespace NFX.PAL.NetFramework.Graphics
{
  /// <summary>
  /// Implements Canvas using .NET framework GDI+ Graphics object
  /// </summary>
  public sealed class NetCanvas : DisposableObject, IPALCanvas
  {


    internal NetCanvas(NGR graphics)
    {
      m_Graphics = graphics;
    }

    protected override void Destructor()
    {
      base.Destructor();
      DisposeAndNull(ref m_Graphics);
    }

    private NGR m_Graphics;


    public InterpolationMode Interpolation
    {
      get => xlat(m_Graphics.InterpolationMode);
      set => m_Graphics.InterpolationMode = xlat(value);
    }

    private static InterpolationMode xlat(System.Drawing.Drawing2D.InterpolationMode mode)
    {
      switch(mode)
      {
        case System.Drawing.Drawing2D.InterpolationMode.Low:                 return InterpolationMode.Low;
        case System.Drawing.Drawing2D.InterpolationMode.High:                return InterpolationMode.High;
        case System.Drawing.Drawing2D.InterpolationMode.Bilinear:            return InterpolationMode.Bilinear;
        case System.Drawing.Drawing2D.InterpolationMode.Bicubic:             return InterpolationMode.Bicubic;
        case System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor:     return InterpolationMode.NearestNeighbor;
        case System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear: return InterpolationMode.HQBilinear;
        case System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic:  return InterpolationMode.HQBicubic;
        //Invalid = -1,
        //Default,
        default: return InterpolationMode.Default;
      }
    }

    private static System.Drawing.Drawing2D.InterpolationMode xlat(InterpolationMode mode)
    {
      switch(mode)
      {
        case  InterpolationMode.Low:              return System.Drawing.Drawing2D.InterpolationMode.Low                 ;
        case  InterpolationMode.High:             return System.Drawing.Drawing2D.InterpolationMode.High                ;
        case  InterpolationMode.Bilinear:         return System.Drawing.Drawing2D.InterpolationMode.Bilinear            ;
        case  InterpolationMode.Bicubic:          return System.Drawing.Drawing2D.InterpolationMode.Bicubic             ;
        case  InterpolationMode.NearestNeighbor:  return System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor     ;
        case  InterpolationMode.HQBilinear:       return System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear ;
        case  InterpolationMode.HQBicubic:        return System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic  ;
        //Invalid = -1,
        //Default,
        default: return System.Drawing.Drawing2D.InterpolationMode.Default;
      }
    }
  }
}
