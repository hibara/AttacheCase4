using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using AttacheCase.Properties;

namespace AttacheCase
{
  public sealed class CustomDialog : Form
  {
    private readonly CheckBox _messageBoxCheckBox;

    private const int ButtonPadding = 8;
    private const int ButtonHeight = 23;
    private const int ButtonSpacingWidth = 16;
    private const int ButtonMinWidth = 92;

    private readonly int _messageBoxDefaultHeight;
    private const int MessageBoxMaxWidth = 512;
    private const int MessageBoxHeight = 160;

    private readonly Panel _textPanel;
    private readonly string _messageBoxText;

    private readonly Button _toggleDetailsButton;
    private readonly TextBox _detailsTextBox;
    private bool _detailsVisible;

    /// <summary>
    /// CustomDialogクラス内にあるチェックボックスのON/OFF状態を取得する
    /// </summary>
    public bool CheckBoxChecked => _messageBoxCheckBox?.Checked ?? false;

    /// <summary>
    /// カスタムメッセージダイアログボックスを生成する
    /// </summary>
    /// <param name="messageBoxString">ダイアログに表示される本文</param>
    /// <param name="messageBoxCaption">ウィンドウタイトル文字列</param>
    /// <param name="messageBoxIcon">ダイアログに何の種類のアイコンを表示するか</param>
    /// <param name="buttons">ButtonSpecクラス配列（ダイアログに表示するボタン群を指定する）</param>
    /// <param name="checkBoxText">チェックボックスを表示するか（NULL, または空文字で非表示）</param>
    /// <param name="messageBoxDetailText">表示する詳細テキストがあり、詳細ボタンを表示するか</param>
    public CustomDialog(
      string messageBoxString,
      string messageBoxCaption,
      Icon messageBoxIcon,
      ButtonSpec[] buttons,
      string checkBoxText,
      string messageBoxDetailText)
    {
      // this.Owner = owner;
      //
      // // 親ウィンドウの中央に表示
      // this.StartPosition = FormStartPosition.Manual;
      // this.Top = 0;
      // this.Left = 0;

      //-----------------------------------
      // メッセージボックスのアイコン配置
      //-----------------------------------
      var messageBoxPictureIcon = new PictureBox();
      messageBoxPictureIcon.Location = new Point(16, 16);
      messageBoxPictureIcon.Size = new Size(48, 48);
      messageBoxPictureIcon.SizeMode = PictureBoxSizeMode.Normal;
      messageBoxPictureIcon.Image = messageBoxIcon.ToBitmap();

      //-----------------------------------
      // メッセージボックスのメッセージ表示
      //-----------------------------------
      _messageBoxText = messageBoxString;

      _textPanel = new Panel();
      _textPanel.Location = new Point(64, 20);
      _textPanel.Paint += TextPanel_Paint;
      //TextPanel.Width = (int)Math.Min(MessageBoxMaxWidth + 160, 500); 
      Controls.Add(_textPanel);

      // いったん、メッセージダイアログウィンドウサイズの調整
      //（後に総ボタン数と合計幅によってもう一度調整する）
      AdjustSize();

      var messageBoxWidth = Width;

      //-----------------------------------
      // チェックボックスの配置
      //-----------------------------------
      if (string.IsNullOrEmpty(checkBoxText) == false)
      {
        _messageBoxCheckBox = new CheckBox();
        _messageBoxCheckBox.Text = checkBoxText;
        _messageBoxCheckBox.Location = new Point(_textPanel.Left, _textPanel.Top + _textPanel.Height + 16);
        _messageBoxCheckBox.AutoSize = true;
        Controls.Add(_messageBoxCheckBox);
      }

      //-----------------------------------
      // ウィンドウタイトル文字列の設定
      //-----------------------------------
      Text = messageBoxCaption;
      FormBorderStyle = FormBorderStyle.FixedDialog;
      StartPosition = FormStartPosition.CenterParent;
      MinimizeBox = false;
      MaximizeBox = false;

      //-----------------------------------
      // ボタンの追加
      //-----------------------------------
      // 最大幅のボタンを見つける
      var buttonMaxWidth = buttons
        .Select(b => TextRenderer.MeasureText(b.Text, this.Font).Width + ButtonSpacingWidth) // テキストの長さに基づいて幅を計算
        .Max();

      if (buttonMaxWidth < ButtonMinWidth)
      {
        buttonMaxWidth = ButtonMinWidth;
      }

      // ボタン群の全体の幅を計算
      var totalButtonWidth = (buttonMaxWidth + ButtonPadding) * buttons.Length - ButtonPadding;

      //-----------------------------------
      // 総ボタン幅からメッセージボックスの幅を決める

      // 総ボタン幅か、メッセージボックスの最大幅のどちらか小さい方を採用
      //MessageBoxWidth = (int)Math.Min(TotalButtonWidth + ButtonSpacingWidth * buttons.Length, MessageBoxMaxWidth);
      // 総ボタン幅か、メッセージボックスの最小幅のどちらか大きい方を採用
      messageBoxWidth = Math.Max(messageBoxWidth, totalButtonWidth);

      Size = new Size(messageBoxWidth, MessageBoxHeight);

      // 各ボタンを順番に配置する
      var buttonX = ClientSize.Width - totalButtonWidth;
      var buttonY = ClientSize.Height - ButtonPadding - ButtonHeight - 4;
      foreach (var buttonSpec in buttons)
      {
        var button = new Button
        {
          Text = buttonSpec.Text,
          DialogResult = buttonSpec.Result,
          Size = new Size(buttonMaxWidth, ButtonHeight),
          // ボタンはメッセージダイアログに準じて右下寄せに配置する
          Location = new Point(buttonX - ButtonPadding - 12, buttonY)
        };
        buttonX += buttonMaxWidth + ButtonPadding;

        button.Click += (sender, e) =>
        {
          this.DialogResult = button.DialogResult;
          Close();
        };

        Controls.Add(button);

      }

      //-----------------------------------
      // 「詳細」トグルボタンの追加
      if (string.IsNullOrEmpty(messageBoxDetailText) == false)
      {
        _toggleDetailsButton = new Button();
        _toggleDetailsButton.Text = Resources.MessageBoxShowDetails; // "詳細を表示";
        // 文字列表示分だけボタン幅を伸長する
        buttonMaxWidth = TextRenderer.MeasureText(_toggleDetailsButton.Text, this.Font).Width + ButtonSpacingWidth;
        _toggleDetailsButton.Width = buttonMaxWidth;

        _toggleDetailsButton.Location = new Point(_textPanel.Left, buttonY);
        _toggleDetailsButton.Click += ToggleDetailsButton_Click;
        Controls.Add(_toggleDetailsButton);

        // 詳細テキストボックスの追加
        _detailsTextBox = new TextBox();
        _detailsTextBox.Multiline = true;
        _detailsTextBox.ReadOnly = true;
        _detailsTextBox.ScrollBars = ScrollBars.Vertical;
        _detailsTextBox.Text = messageBoxDetailText;
        _detailsTextBox.Location = new Point(_textPanel.Left, _toggleDetailsButton.Bottom + 12);
        _detailsTextBox.Size = new Size(_textPanel.Width, 100); // 高さは適宜調整してください
        _detailsTextBox.Visible = _detailsVisible; // 初期状態では非表示
        Controls.Add(_detailsTextBox);

      }

      //-----------------------------------
      // 見えないキャンセルボタンを生成
      // ユーザーが `Esc`キーを押したときに、キャンセルボタンが押されたことにする
      var buttonCancel = new Button
      {
        Text = "",
        DialogResult = DialogResult.Cancel,
        Size = new Size(1, 1),
        Location = new Point(0, 0)
      };
      Controls.Add(buttonCancel);
      CancelButton = buttonCancel;

      //-----------------------------------
      // 生成した各コンポーネントをフォームに追加
      Controls.Add(messageBoxPictureIcon);

      //-----------------------------------
      // ダイアログボックスのデフォルト高さを保存
      _messageBoxDefaultHeight = Height;

    }

    /// <summary>
    /// メッセージボックス内の本文の量によって表示を調整する
    /// </summary>
    private void AdjustSize()
    {
      using (var g = CreateGraphics())
      {
        var size = g.MeasureString(_messageBoxText, this.Font, MessageBoxMaxWidth);
        var panelWidth = (int)Math.Min(size.Width, MessageBoxMaxWidth);
        _textPanel.Width = panelWidth;
        _textPanel.Height = (int)size.Height;

        this.Width = _textPanel.Left + panelWidth + 32; // 余白を考慮

        if (_detailsTextBox != null && _detailsTextBox.Visible)
        {
          this.Height += _detailsTextBox.Height + 16; // 詳細テキストボックス高さを追加
        }
        else
        {
          this.Height = _messageBoxDefaultHeight;
        }

      }
    }

    /// <summary>
    /// メッセージを描画する`Panel`の`Paint`イベントを独自処理する
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TextPanel_Paint(object sender, PaintEventArgs e)
    {
      var g = e.Graphics;
      g.DrawString(_messageBoxText, this.Font, Brushes.Black,
        new RectangleF(0, 0, _textPanel.Width, _textPanel.Height));
    }

    private void ToggleDetailsButton_Click(object sender, EventArgs e)
    {
      // 詳細セクションの表示・非表示を切り替える
      _detailsVisible = !_detailsVisible;
      _detailsTextBox.Visible = _detailsVisible;
      _toggleDetailsButton.Text =
        _detailsVisible ? Resources.MessageBoxHideDetails : Resources.MessageBoxShowDetails; // "詳細を隠す" : "詳細を表示"

      // 詳細テキストの表示に合わせてダイアログのサイズを調整
      AdjustSize();
    }
    
  }

  /// <summary>
  /// ボタンの仕様を表すクラス
  /// `DialogResult`でどのボタンが押されたかを判定する
  /// </summary>
  public class ButtonSpec
  {
    public string Text { get; set; }
    public DialogResult Result { get; set; }

    public ButtonSpec(string text, DialogResult result)
    {
      Text = text;
      Result = result;
    }
  }



}
