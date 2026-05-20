using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AttacheCase
{
  [ToolboxItem(true)]
  public class RoundedBorderLabel : Label
  {
    private Color _borderColor = Color.Gray;
    private int _borderThickness = 1;
    private int _cornerRadius = 8;

    public RoundedBorderLabel()
    {
      // ちらつき防止・自前描画の設定
      SetStyle(ControlStyles.UserPaint
           | ControlStyles.AllPaintingInWmPaint
           | ControlStyles.OptimizedDoubleBuffer
           | ControlStyles.ResizeRedraw
           | ControlStyles.SupportsTransparentBackColor, true);

      // 既定では枠線がテキストに重ならないよう内側に余白を確保
      Padding = new Padding(6);
      BackColor = Color.Transparent;
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "Gray")]
    [Description("枠線の色")]
    public Color BorderColor
    {
      get => _borderColor;
      set { _borderColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(1)]
    [Description("枠線の太さ(px)")]
    public int BorderThickness
    {
      get => _borderThickness;
      set { _borderThickness = Math.Max(0, value); Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(8)]
    [Description("角丸の半径(px)。0 で直角")]
    public int CornerRadius
    {
      get => _cornerRadius;
      set { _cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
      // 背景・テキストは基底クラスに描画させる
      base.OnPaint(e);

      if (_borderThickness <= 0)
        return;

      var g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.PixelOffsetMode = PixelOffsetMode.Half;

      // 線幅の半分だけ内側に寄せ、枠線が端で切れないようにする
      var inset = _borderThickness / 2f;
      var rect = new RectangleF(
        inset,
        inset,
        Width - _borderThickness,
        Height - _borderThickness);

      using var path = CreateRoundedPath(rect, _cornerRadius);
      using var pen = new Pen(_borderColor, _borderThickness);
      pen.Alignment = PenAlignment.Center;
      g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, int radius)
    {
      var path = new GraphicsPath();

      if (radius <= 0)
      {
        path.AddRectangle(rect);
        path.CloseFigure();
        return path;
      }

      // 半径が矩形サイズを超えないよう制限
      var d = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));

      path.StartFigure();
      path.AddArc(rect.X, rect.Y, d, d, 180, 90);        // 左上
      path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);    // 右上
      path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); // 右下
      path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);    // 左下
      path.CloseFigure();
      return path;
    }
  }
}


