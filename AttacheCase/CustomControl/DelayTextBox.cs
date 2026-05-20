using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace AttacheCase.CustomControl
{
  [ToolboxItem(true)]
  // [DesignerCategory("code")] ← 削除またはコメントアウト
  public class DelayTextBox : TextBox
  {
    private Timer _delayTimer;
    private bool _timerElapsed;
    private bool _keysPressed;
    private int _delay = 200;

    [DefaultValue(200)]
    [Category("Behavior")]
    [Description("テキスト変更イベントを発火するまでの遅延時間（ミリ秒）")]
    public int Delay
    {
      get => _delay;
      set
      {
        _delay = Math.Max(0, value);
        // 実行時かつタイマーが生成済みの場合のみインターバルを更新
        if (_delayTimer != null)
        {
          _delayTimer.Interval = _delay;
        }
      }
    }

    public DelayTextBox()
    {
      // コンストラクタでは何もしない（デザイナーでの事故解放を防ぐ）
    }

    /// <summary>
    /// タイマーを実行時のみ、必要最小限のタイミングで生成する
    /// </summary>
    private void EnsureTimerInitialized()
    {
      if (IsInDesignMode()) return;
      if (_delayTimer != null) return;

      _delayTimer = new Timer();
      _delayTimer.Interval = _delay;
      _delayTimer.Tick += DelayTimer_Tick;
    }

    private void DelayTimer_Tick(object sender, EventArgs e)
    {
      _delayTimer.Stop();
      _timerElapsed = true;
      OnTextChanged(EventArgs.Empty);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
      if (IsInDesignMode())
      {
        base.OnKeyPress(e);
        return;
      }

      // 実行時に初めてキーが押されたタイミングでタイマーを生成
      EnsureTimerInitialized();

      _keysPressed = true;
      _delayTimer.Stop();
      _delayTimer.Start();

      base.OnKeyPress(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
      if (IsInDesignMode())
      {
        base.OnTextChanged(e);
        return;
      }

      // タイマー満了、またはコードからの変更（キー入力以外）は即時通知
      if (_timerElapsed || !_keysPressed)
      {
        _timerElapsed = false;
        _keysPressed = false;
        base.OnTextChanged(e);
      }
      // キー入力中の場合は、タイマーのTickによってのみOnTextChangedが呼ばれるのを待つ
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        if (_delayTimer != null)
        {
          _delayTimer.Stop();
          _delayTimer.Tick -= DelayTimer_Tick;
          _delayTimer.Dispose();
          _delayTimer = null;
        }
      }
      base.Dispose(disposing);
    }

    /// <summary>
    /// デザインモード判定（デザイナーで安全に動作するシンプルな実装）
    /// </summary>
    private bool IsInDesignMode()
    {
      // 1. 標準の DesignMode プロパティ
      if (DesignMode) return true;

      // 2. LicenseManager による判定（コンストラクタ時にも有効）
      if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;

      // 3. Site経由の判定（親コントロールがある場合に有効）
      if (Site != null && Site.DesignMode) return true;

      return false;
    }
  }
}