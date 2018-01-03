﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

using NFX.PAL.Graphics;

namespace NFX.Graphics
{
  public sealed partial class Canvas : DisposableObject
  {
    public abstract class Brush : Asset<IPALCanvasBrush>
    {
      protected Brush(IPALCanvasBrush handle) : base(handle)
      {

      }
    }
  }
}
