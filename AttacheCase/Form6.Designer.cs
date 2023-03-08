
namespace AttacheCase
{
  partial class Form6
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form6));
      this.panelMain = new System.Windows.Forms.Panel();
      this.label3 = new System.Windows.Forms.Label();
      this.buttonCancel = new System.Windows.Forms.Button();
      this.buttonGenerate = new System.Windows.Forms.Button();
      this.label1 = new System.Windows.Forms.Label();
      this.pictureBox1 = new System.Windows.Forms.PictureBox();
      this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
      this.panelMain.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
      this.SuspendLayout();
      // 
      // panelMain
      // 
      this.panelMain.Controls.Add(this.buttonCancel);
      this.panelMain.Controls.Add(this.buttonGenerate);
      this.panelMain.Controls.Add(this.label1);
      this.panelMain.Controls.Add(this.pictureBox1);
      this.panelMain.Controls.Add(this.label3);
      this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
      this.panelMain.Location = new System.Drawing.Point(0, 0);
      this.panelMain.Name = "panelMain";
      this.panelMain.Size = new System.Drawing.Size(495, 331);
      this.panelMain.TabIndex = 3;
      // 
      // label3
      // 
      this.label3.BackColor = System.Drawing.Color.Transparent;
      this.label3.Font = new System.Drawing.Font("MS UI Gothic", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
      this.label3.ForeColor = System.Drawing.Color.ForestGreen;
      this.label3.Location = new System.Drawing.Point(34, 214);
      this.label3.Name = "label3";
      this.label3.Size = new System.Drawing.Size(449, 68);
      this.label3.TabIndex = 14;
      this.label3.Text = "ロックファイルは、「暗号化」専用キーで、キーファイルは「復号」専用です。通信する相手へ暗号化することしかできないロックファイルを渡すことで、鍵交換（パスワード交換" +
    "）することなく重要なファイルのやりとりすることができます。";
      this.label3.Click += new System.EventHandler(this.label3_Click);
      // 
      // buttonCancel
      // 
      this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.buttonCancel.Location = new System.Drawing.Point(383, 285);
      this.buttonCancel.Name = "buttonCancel";
      this.buttonCancel.Size = new System.Drawing.Size(92, 24);
      this.buttonCancel.TabIndex = 11;
      this.buttonCancel.Text = "キャンセル(&C)";
      this.buttonCancel.UseVisualStyleBackColor = true;
      this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
      // 
      // buttonGenerate
      // 
      this.buttonGenerate.Location = new System.Drawing.Point(285, 285);
      this.buttonGenerate.Name = "buttonGenerate";
      this.buttonGenerate.Size = new System.Drawing.Size(92, 24);
      this.buttonGenerate.TabIndex = 10;
      this.buttonGenerate.Text = "作成する(&G)";
      this.buttonGenerate.UseVisualStyleBackColor = true;
      this.buttonGenerate.Click += new System.EventHandler(this.buttonGenerate_Click);
      // 
      // label1
      // 
      this.label1.BackColor = System.Drawing.Color.Transparent;
      this.label1.Font = new System.Drawing.Font("MS UI Gothic", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
      this.label1.ImageAlign = System.Drawing.ContentAlignment.TopLeft;
      this.label1.Location = new System.Drawing.Point(34, 127);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(320, 86);
      this.label1.TabIndex = 9;
      this.label1.Text = "ペアになった、\r\n\r\n・錠前（ロックファイル＝公開鍵）と、\r\n・鍵（キーファイル＝秘密鍵）\r\n\r\nの２つを作成します。";
      // 
      // pictureBox1
      // 
      this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
      this.pictureBox1.Location = new System.Drawing.Point(144, 24);
      this.pictureBox1.Name = "pictureBox1";
      this.pictureBox1.Size = new System.Drawing.Size(200, 100);
      this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
      this.pictureBox1.TabIndex = 8;
      this.pictureBox1.TabStop = false;
      // 
      // Form6
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this.buttonCancel;
      this.ClientSize = new System.Drawing.Size(495, 331);
      this.Controls.Add(this.panelMain);
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "Form6";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "公開鍵・秘密鍵の作成";
      this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form6_FormClosed);
      this.Load += new System.EventHandler(this.Form6_Load);
      this.panelMain.ResumeLayout(false);
      this.panelMain.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
      this.ResumeLayout(false);

    }

    #endregion
    private System.Windows.Forms.Panel panelMain;
    private System.Windows.Forms.Label label3;
    private System.Windows.Forms.Button buttonCancel;
    private System.Windows.Forms.Button buttonGenerate;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.PictureBox pictureBox1;
    private System.Windows.Forms.SaveFileDialog saveFileDialog1;
  }
}