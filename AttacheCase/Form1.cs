//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttachéCase4 )" -- File encryption software.
// Copyright (C) 2016-2021  Mitsuhiro Hibara
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.If not, see<http://www.gnu.org/licenses/>.
//---------------------------------------------------------------------- 
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AttacheCase.Properties;
using Microsoft.VisualBasic.FileIO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Text;
using System.Globalization;

namespace AttacheCase
{
  public partial class Form1 : Form
  {
    // Status Code
    private const int ENCRYPT_SUCCEEDED = 1; // Encrypt is succeeded
    private const int DECRYPT_SUCCEEDED = 2; // Decrypt is succeeded
    private const int DELETE_SUCCEEDED  = 3; // Delete is succeeded
    private const int READY_FOR_ENCRYPT = 4; // Getting ready for encryption
    private const int READY_FOR_DECRYPT = 5; // Getting ready for decryption
    private const int ENCRYPTING        = 6; // Ecrypting
    private const int DECRYPTING        = 7; // Decrypting
    private const int DELETING          = 8; // Deleting

    // Error Code
    private const int USER_CANCELED            = -1;
    private const int ERROR_UNEXPECTED         = -100;
    private const int NOT_ATC_DATA             = -101;
    private const int ATC_BROKEN_DATA          = -102;
    private const int NO_DISK_SPACE            = -103;
    private const int FILE_INDEX_NOT_FOUND     = -104;
    private const int PASSWORD_TOKEN_NOT_FOUND = -105;
    private const int NOT_CORRECT_HASH_VALUE   = -106;
    private const int INVALID_FILE_PATH        = -107;
    private const int OS_DENIES_ACCESS         = -108;
    private const int DATA_NOT_FOUND           = -109;
    private const int DIRECTORY_NOT_FOUND      = -110;
    private const int DRIVE_NOT_FOUND          = -111;
    private const int FILE_NOT_LOADED          = -112;
    private const int FILE_NOT_FOUND           = -113;
    private const int PATH_TOO_LONG            = -114;
    private const int CRYPTOGRAPHIC_EXCEPTION  = -115;
    private const int RSA_KEY_GUID_NOT_MATCH   = -116;
    private const int IO_EXCEPTION             = -117;

    // File Type
    private const int FILE_TYPE_ERROR           = -1;
    private const int FILE_TYPE_NONE            =  0;
    private const int FILE_TYPE_ATC             =  1;
    private const int FILE_TYPE_ATC_EXE         =  2;
    private const int FILE_TYPE_PASSWORD_ZIP    =  3;
    private const int FILE_TYPE_RSA_DATA        =  4;
    private const int FILE_TYPE_RSA_PRIVATE_KEY =  5;
    private const int FILE_TYPE_RSA_PUBLIC_KEY  =  6;

    // Process Type
    private const int PROCESS_TYPE_ERROR          = -1;
    private const int PROCESS_TYPE_NONE           =  0;
    private const int PROCESS_TYPE_ATC            =  1;
    private const int PROCESS_TYPE_ATC_EXE        =  2;
    private const int PROCESS_TYPE_PASSWORD_ZIP   =  3;
    private const int PROCESS_TYPE_DECRYPTION     =  4;
    private const int PROCESS_TYPE_RSA_ENCRYPTION =  5;
    private const int PROCESS_TYPE_RSA_DECRYPTION =  6;

    // Overwrite Option
    //private const int USER_CANCELED = -1;
    private const int OVERWRITE      = 1;
    private const int OVERWRITE_ALL  = 2;
    private const int KEEP_NEWER     = 3;
    private const int KEEP_NEWER_ALL = 4;
    // Skip Option
    private const int SKIP           = 5;
    private const int SKIP_ALL       = 6;

    private int TempOverWriteOption = -1;

    // The position of mouse down in main form.
    // マウスボタンがダウンされた位置
    private Point MouseDownPoint;

    // AppSettings.cs
    // 
    // private string[] AppSettings.Instance.FileList = null;
    // List<string> FileList = new List<string>();
    // AppSettings.Instance.FileList

    private int LimitOfInputPassword = -1;

    public static BackgroundWorker bkg;
    public static BackgroundWorker bkgDelete;

    //private FileEncrypt2 encryption2, FileEncrypt3, encryption2;
    private FileDecrypt2 decryption2;
    private FileDecrypt3 decryption3;
    private FileEncrypt4 encryption4;
    private FileDecrypt4 decryption4;
    private Wipe wipe;

    // RSA encryption key data
    private string XmlPublicKeyString;
    private string XmlPrivateKeyString;
    private Dictionary<string, string> XmlHashStringList;
    bool fWaitingForKeyFile = false;

    private CancellationTokenSource cts;

    private int FileIndex = 0;
    List<string> OutputFileList = new List<string>();

    // Developer Console window
    Form5 frm5 = null;

    /// <summary>
    /// Constructor
    /// </summary>
    public Form1()
    {
      InitializeComponent();

      tabControl1.Visible = false;

      panelStartPage.Parent = panelOuter;
      panelEncrypt.Parent = panelOuter;
      panelEncryptConfirm.Parent = panelOuter;
      panelDecrypt.Parent = panelOuter;
      panelRsa.Parent = panelOuter;
      panelRsaKey.Parent = panelOuter;
      panelProgressState.Parent = panelOuter;

      // メインウィンドウの終了ボタン
      // Exit button of main window.
      buttonExit.Size = new Size(1, 1);

      // Theme color
      if (AppSettings.Instance.CurrentThemeColorName == "dark")
      {
        this.BackColor = Color.Black;
        ChangeTheme(this.Controls, true);
      }
      else
      {
        this.BackColor = SystemColors.Control;
      }

    }

    //======================================================================
    // フォームイベント ( Form events )
    //======================================================================
    #region
    /// <summary>
    /// Form1 Load event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_Load(object sender, EventArgs e)
    {
      tabControl1.Dock = DockStyle.Fill;

      toolStripStatusLabelEncryptionTime.Visible = false;
      toolStripStatusLabelDataVersion.Text = "-";

      // View start window
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;
      panelStartPage.Visible = true;
      this.AllowDrop = true;

      this.Text = Resources.AttacheCase;

      // Ajust invalid window form position
      this.Width = AppSettings.Instance.FormWidth;
      this.Height = AppSettings.Instance.FormHeight;

      // 初期位置（スクリーン中央）
      // Default window position ( in screen center )
      if (AppSettings.Instance.FormLeft < 0 || AppSettings.Instance.FormLeft > SystemInformation.VirtualScreen.Width)
      {
        this.Left = Screen.GetBounds(this).Width / 2 - this.Width / 2;
      }
      else
      {
        this.Left = AppSettings.Instance.FormLeft;
      }

      if (AppSettings.Instance.FormTop < 0 || AppSettings.Instance.FormTop > SystemInformation.VirtualScreen.Height)
      {
        this.Top = Screen.GetBounds(this).Height / 2 - this.Height / 2;
      }
      else
      {
        this.Top = AppSettings.Instance.FormTop;
      }
      
      // Bring AttcheCase window in front of Desktop
      if (AppSettings.Instance.fWindowForeground == true)
      {
        this.TopMost = true;
      }

      StartProcess();

    }

    /// <summary>
    /// Form1 Resize Event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_Resize(object sender, EventArgs e)
    {
      // ドラッグ＆ドロップの説明文をパネル内で中央表示
      // Center the drag & drop description in the panel.
      labelDragAndDrop.Left = (panelStartPage.Width - panel1.Width) / 2 - labelDragAndDrop.Width / 2 + panel1.Width - 16; ;
      labelDragAndDrop.Top = panelStartPage.Height / 2 - labelDragAndDrop.Height / 2;
    }

    /// <summary>
    /// Form closed event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_FormClosed(object sender, FormClosedEventArgs e)
    {
      // Application path
      AppSettings.Instance.ApplicationPath = Application.ExecutablePath;

      // Application version
      System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
      System.Version ver = asm.GetName().Version;
      AppSettings.Instance.AppVersion = int.Parse(ver.ToString().Replace(".", ""));

      // Save main form position and size
      AppSettings.Instance.FormLeft = this.Left;
      AppSettings.Instance.FormTop = this.Top;
      AppSettings.Instance.FormWidth = this.Width;
      AppSettings.Instance.FormHeight = this.Height;

      if (File.Exists(AppSettings.Instance.IniFilePath) == true)
      {
        AppSettings.Instance.WriteOptionToIniFile(AppSettings.Instance.IniFilePath);
      }
      else
      {
        string[] cmds = Environment.GetCommandLineArgs();
        if (cmds.Count() <= 1)
        {
          // Save settings to registry
          AppSettings.Instance.SaveOptionsToRegistry();
        }
        else
        {
          //起動時のコマンドライン引数の場合、設定を保存しない。
          //If there is a startup command line arguments, not save the settings.
        }
      }
    }

    /// <summary>
    /// Form1 Activate event 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>

    private void Form1_Activated(object sender, EventArgs e)
    {
      string FormThemeColor = this.BackColor == SystemColors.Control ? "light" : "dark";
      string CurrentThemeColor = AppSettings.Instance.CurrentThemeColorName;
      if (FormThemeColor != CurrentThemeColor){
        if (CurrentThemeColor == "dark")
        {
          ChangeTheme(this.Controls, true);
        }
        else
        {
          ChangeTheme(this.Controls, false);
        }
        // Make the title bar (caption bar) lose focus once in order to update it.
        // タイトルバー（キャプションバー）の更新を行うため一旦フォーカスを失わせる。
        Form2 frm2 = new Form2();
        frm2.Show();
        frm2.Dispose();
      }
    }

    /// <summary>
    /// Form1 DragEnter event 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_DragEnter(object sender, DragEventArgs e)
    {
      if (panelStartPage.Visible == true || 
        (panelProgressState.Visible == true && progressBar.Value == progressBar.Maximum))
      {
      }
      else if (panelEncrypt.Visible == true && AppSettings.Instance.fAllowPassFile == true)
      {
      }
      else if (panelDecrypt.Visible == true && AppSettings.Instance.fAllowPassFile == true)
      {
      }
      else if (panelRsa.Visible == true || panelRsaKey.Visible == true)
      {

      }
      else {
        e.Effect = DragDropEffects.None;
        return;
      }

      if (e.Data.GetDataPresent(DataFormats.FileDrop) == true)
      {
        e.Effect = DragDropEffects.Copy;
        if (this.BackColor == SystemColors.Control)
        { // light theme
          panelStartPage.BackColor = Color.Honeydew;
        }
        else
        { // dark theme
          panelStartPage.BackColor = Color.FromArgb(255, 50, 50, 50);
        }
      }

    }

    /// <summary>
    /// Form1 DragLeace event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_DragLeave(object sender, EventArgs e)
    {
      if (this.BackColor == SystemColors.Control)
      { // light theme
        panelStartPage.BackColor = Color.White;
      }
      else
      { // dark theme
        panelStartPage.BackColor = Color.Black;
      }
    }

    /// <summary>
    /// Form1 DragDrop event 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_DragDrop(object sender, DragEventArgs e)
    {
      if (panelStartPage.Visible == false && panelRsa.Visible == false && panelRsaKey.Visible == false)
      {
        return;
      }

      if ( fWaitingForKeyFile == false)
      {
        AppSettings.Instance.FileList = new List<string>();
      }
      string[] ArrayFiles = (string[])e.Data.GetData(DataFormats.FileDrop, false);
      foreach (string FilePath in ArrayFiles)
      {
        AppSettings.Instance.FileList.Add(FilePath);
      }

      StartProcess();

    }
    #endregion

    //======================================================================
    // 削除処理 ( Delete files ) 
    //======================================================================
    #region
    private bool DeleteData(List<string> FileList)
    {
      if (AppSettings.Instance.fCompleteDelFile < 1 || AppSettings.Instance.fCompleteDelFile > 3)
      {
        return(true);
      }

      pictureBoxProgress.Image = pictureBoxDeleteOn.Image;
      labelProgressMessageText.Text = "-";
      progressBar.Value = 0;
      labelProgressPercentText.Text = "- %";
      this.Update();

      // How to delete a way?
      //----------------------------------------------------------------------
      // 通常削除
      // Normal delete
      //----------------------------------------------------------------------
      if (AppSettings.Instance.fCompleteDelFile == 1)
      {
        labelCryptionType.Text = Resources.labelProcessNameDelete;  // Deleting...

        cts = new CancellationTokenSource();
        ParallelOptions po = new ParallelOptions();
        po.CancellationToken = cts.Token;

        try
        {
          labelProgress.Text = Resources.labelNormalDelete;

          SynchronizationContext ctx = SynchronizationContext.Current;
          int count = 0;

          Parallel.ForEach(FileList, po, (FilePath, state) =>
          {
            if (File.Exists(FilePath) == true)
            {
              FileSystem.DeleteFile(FilePath);
            }
            else
            {
              // File or direcrory does not exists.
            }

            Interlocked.Increment(ref count);

            ctx.Post(d =>
            {
              progressBar.Value = (int)((float)count / FileList.Count) * 10000;
            }, null);

            po.CancellationToken.ThrowIfCancellationRequested();

          });

          // Delete root directory
          if (Directory.Exists(FileList[0]) == true)
          {
            FileSystem.DeleteDirectory(
              FileList[0],
              UIOption.OnlyErrorDialogs,
              RecycleOption.DeletePermanently,
              UICancelOption.ThrowException
            );
          }

          labelCryptionType.Text = "";
          // 指定のファイル及びフォルダーは削除されました。
          // The specified files or folders has been deleted.
          labelProgressMessageText.Text = Resources.labelNormalDeleteCompleted;
          progressBar.Value = progressBar.Maximum;
          labelProgressPercentText.Text = "100%";
          buttonCancel.Text = Resources.ButtonTextOK;
          Application.DoEvents();

        }
        catch(Exception e)
        {
          // ユーザーキャンセル
          // User cancel

#if (DEBUG)
          System.Windows.Forms.MessageBox.Show(new Form { TopMost = true }, e.Message);
#endif

          labelCryptionType.Text = "";
          // ファイルまたはフォルダーの削除をキャンセルしました。
          // Deleting files or folders has been canceled.
          labelProgressMessageText.Text = Resources.labelNormalDeleteCanceled;
          progressBar.Value = 0;
          labelProgressPercentText.Text = "- %";
          return (false);
        }
        finally
        {
          cts.Dispose();
        }

        return (true);

      }
      //----------------------------------------------------------------------
      // ゴミ箱への移動
      // Send to the trash
      //----------------------------------------------------------------------
      else if (AppSettings.Instance.fCompleteDelFile == 2)
      {
        labelCryptionType.Text = Resources.labelProcessNameMoveToTrash;  // Move to Trash...
        labelProgress.Text = Resources.labelMoveToTrash;

        cts = new CancellationTokenSource();
        ParallelOptions po = new ParallelOptions();
        po.CancellationToken = cts.Token;

        try
        {
          SynchronizationContext ctx = SynchronizationContext.Current;
          int count = 0;

          Parallel.ForEach(FileList, po, (FilePath, state) =>
          {
            if (File.Exists(FilePath) == true)
            {
              FileSystem.DeleteFile(FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else if (Directory.Exists(FilePath))
            {
              FileSystem.DeleteDirectory(
                FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            }

            Interlocked.Increment(ref count);

            ctx.Post(d =>
            {
              progressBar.Value = (int)((float)count / FileList.Count) * 10000;
            }, null);

            po.CancellationToken.ThrowIfCancellationRequested();

          });

          labelCryptionType.Text = "";
          // ファイル、またはフォルダーのゴミ箱への移動が完了しました。
          // Move files or folders to the trash was completed.
          labelProgressMessageText.Text = Resources.labelMoveToTrashCompleted;
          progressBar.Value = progressBar.Maximum;
          buttonCancel.Text = Resources.ButtonTextOK;
          labelProgressPercentText.Text = "100%";
          Application.DoEvents();

        }
        catch(Exception e)
        {
          // ユーザーキャンセル
          // User cancel

#if (DEBUG)
          System.Windows.Forms.MessageBox.Show(new Form { TopMost = true }, e.Message);
#endif

          labelCryptionType.Text = "";
          // ファイルまたはフォルダーの削除をキャンセルしました。
          // Deleting files or folders has been canceled.
          labelProgressMessageText.Text = Resources.labelNormalDeleteCanceled;
          progressBar.Value = 0;
          labelProgressPercentText.Text = "- %";
          return (false);
        }
        finally
        {
          cts.Dispose();
        }

        return (true);

      }
      //----------------------------------------------------------------------
      // 完全削除
      // Complete deleting
      //----------------------------------------------------------------------
      else if (AppSettings.Instance.fCompleteDelFile == 3)
      {
        bkg = new BackgroundWorker();

        labelCryptionType.Text = Resources.labelProcessNameDeleteCompletely;  // Deleting Completely...
        labelProgress.Text = Resources.labellabelCompletelyDelete;
        pictureBoxProgress.Image = pictureBoxDeleteOn.Image;

        wipe = new Wipe();

        bkg.DoWork += (s, d) =>
          wipe.WipeFile(s, d, FileList, AppSettings.Instance.DelRandNum, AppSettings.Instance.DelZeroNum);

        bkg.RunWorkerCompleted += backgroundWorker_Wipe_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();
      }

      return (true);

    }

    #endregion

    //======================================================================
    /// <summary>
    /// 【Decryption】上書きの確認ダイアログ表示とユーザー応答内容の受け渡し
    ///  Show dialog for confirming to overwrite, and passing user command. 
    /// </summary>
    System.Threading.ManualResetEvent _busy = new System.Threading.ManualResetEvent(false);
    private void DialogMessageForOverWrite(int FileType, string FilePath)
    {
      TempOverWriteOption = USER_CANCELED;

      if (decryption2 == null)
      {
        // Not confirm
        if (AppSettings.Instance.fDecryptConfirmOverwrite == false)
        {
          TempOverWriteOption = OVERWRITE_ALL;
          return;
        }

        if (TempOverWriteOption == OVERWRITE_ALL)
        {
          return;
        }
        else if(TempOverWriteOption == SKIP_ALL)
        {
          return;
        }

      }
      else
      {
        if (AppSettings.Instance.fDecryptConfirmOverwrite == false)
        {
          TempOverWriteOption = OVERWRITE_ALL;
          return;
        }

        if (TempOverWriteOption == OVERWRITE_ALL)
        {
          return;
        }
        else if (TempOverWriteOption == SKIP_ALL)
        {
          return;
        }
      }

      if (!bkg.IsBusy)
      {
        bkg.RunWorkerAsync();
        // Unblock the worker 
        _busy.Set();
      }

      // Show dialog for confirming to orverwrite
      Form4 frm4;
      if (FileType == 0)
      {
        frm4 = new Form4("ComfirmToOverwriteDir", Resources.labelComfirmToOverwriteDir + Environment.NewLine + FilePath);
      }
      else
      {
        frm4 = new Form4("ComfirmToOverwriteFile", Resources.labelComfirmToOverwriteFile + Environment.NewLine + FilePath);
      }

      frm4.ShowDialog();

      TempOverWriteOption = frm4.OverWriteOption;
      


      if (decryption2 != null)
      {
        decryption2.TempOverWriteOption = TempOverWriteOption;
      }
      else if (decryption3 != null)
      {
        decryption3.TempOverWriteOption = TempOverWriteOption;
      }
      else
      {
        decryption4.TempOverWriteOption = TempOverWriteOption;
      }

      frm4.Dispose();

      if (TempOverWriteOption == USER_CANCELED || TempOverWriteOption == SKIP_ALL)
      {
        if (bkg != null && bkg.IsBusy == true)
        {
          bkg.CancelAsync();
        }

        if (cts != null)
        {
          cts.Cancel();
        }
      }

      _busy.Reset();

    }

    //======================================================================
    // 【Decryption】不正なパスエラー表示と、ユーザー応答内容の受け渡し
    //  Invalid path error indication and return of user response contents.
    //======================================================================
    private void DialogMessageInvalidChar(string FilePath)
    {
      if (!bkg.IsBusy)
      {
        bkg.RunWorkerAsync();
        // Unblock the worker 
        _busy.Set();
      }

      // Show dialog for confirming to orverwrite
      Form4 frm4;
      frm4 = new Form4("InvalidChar", Resources.labelInvalidChar + Environment.NewLine + FilePath);
      frm4.ShowDialog();

      int TempInvalidCharOption = frm4.InvalidCharOption;

      frm4.Dispose();

      if (TempInvalidCharOption == USER_CANCELED)
      {
        if (bkg != null && bkg.IsBusy == true)
        {
          bkg.CancelAsync();
        }

        if (cts != null)
        {
          cts.Cancel();
        }
      }

      _busy.Reset();

    }


    /// <summary>
    /// 指定したルートディレクトリのファイルリストを並列処理で取得する
    /// Get a list of files specified root directory in parallel processing
    /// </summary>
    /// <remarks>http://stackoverflow.com/questions/2106877/is-there-a-faster-way-than-this-to-find-all-the-files-in-a-directory-and-all-sub</remarks>
    /// <param name="fileSearchPattern"></param>
    /// <param name="rootFolderPath"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetFileList(string fileSearchPattern, string rootFolderPath)
    {
      Queue<string> pending = new Queue<string>();
      pending.Enqueue(rootFolderPath);
      string[] tmp;
      while (pending.Count > 0)
      {
        rootFolderPath = pending.Dequeue();
        tmp = Directory.GetFiles(rootFolderPath, fileSearchPattern);
        for (int i = 0; i < tmp.Length; i++)
        {
          yield return tmp[i];
        }
        tmp = Directory.GetDirectories(rootFolderPath);
        for (int i = 0; i < tmp.Length; i++)
        {
          pending.Enqueue(tmp[i]);
        }
      }
    }

    /// <summary>
    /// パスワードファイルとして、ファイルからSHA-1ハッシュを取得してバイト列にする
    /// Get a string of the SHA-1 hash from a file such as the password file
    /// </summary>
    /// <param name="FilePath"></param>
    /// <returns></returns>
    private byte[] GetPasswordFileHash2(string FilePath)
    {
      byte[] buffer = new byte[255];
      byte[] result = new byte[32];
      //byte[] header = new byte[12];

      using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
      {
        //SHA1CryptoServiceProviderオブジェクト
        using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
        {
          byte[] array_bytes = sha1.ComputeHash(fs);
          for (int i = 0; i < 20; i++)
          {
            result[i] = array_bytes[i];
          }
        }

        fs.Seek(0, SeekOrigin.Begin);
        while (fs.Read(buffer, 0, 255) > 0)
        {
          // 最後の255バイトを取得しようとしたデータから残り12bytesのパスワードを埋める
          // Fill the rest data with trying to get the last 255 bytes.
        }

        for (int i = 0; i < 12; i++)
        {
          result[20 + i] = buffer[i];
        }

      }
      //string text = System.Text.Encoding.ASCII.GetString(result);
      return (result);

    }

    /// 計算してチェックサム（SHA-256）を得る
    /// Get a check sum (SHA-256) to calculate
    private static byte[] GetPasswordFileSha256(string FilePath)
    {
      using (BufferedStream bs = new BufferedStream(File.OpenRead(FilePath), 16 * 1024 * 1024))
      {
        SHA256Managed sha = new SHA256Managed();
        byte[] result = new byte[32];
        byte[] hash = sha.ComputeHash(bs);
        for (int i = 0; i < 32; i++)
        {
          result[i] = hash[i];
        }
        return (result);
      }
    }

    //======================================================================
    // Backgroundworker
    //======================================================================
    #region

    private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
      ArrayList MessageList = (ArrayList)e.UserState;

      if (e.ProgressPercentage > 0)
      {
        progressBar.Style = ProgressBarStyle.Continuous;
        labelProgressPercentText.Text = ((float)e.ProgressPercentage / 100).ToString("F2") + "%";
        progressBar.Value = e.ProgressPercentage;
      }
      else
      {
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.MarqueeAnimationSpeed = 50;
        progressBar.Value = 0;
      }

      /*
      private const int ENCRYPT_SUCCEEDED = 1; // Encrypt is succeeded.
      private const int DECRYPT_SUCCEEDED = 2; // Decrypt is succeeded.
      private const int DELETE_SUCCEEDED  = 3; // Delete is succeeded.
      private const int READY_FOR_ENCRYPT = 4; // Getting ready for encryption or decryption.
      private const int READY_FOR_DECRYPT = 5; // Getting ready for encryption or decryption.
      private const int ENCRYPTING        = 6; // Ecrypting.
      private const int DECRYPTING        = 7; // Decrypting.
      private const int DELETING          = 8; // Deleting.
      */
      switch ((int)MessageList[0])
      {
        case READY_FOR_ENCRYPT:
          labelCryptionType.Text = Resources.labelGettingReadyForEncryption;
          break;

        case READY_FOR_DECRYPT:
          labelCryptionType.Text = Resources.labelGettingReadyForDecryption;
          break;

        case ENCRYPTING:
          labelCryptionType.Text = Resources.labelProcessNameEncrypt;
          break;

        case DECRYPTING:
          labelCryptionType.Text = Resources.labelProcessNameDecrypt;
          break;

        case DELETING:
          labelCryptionType.Text = Resources.labelProcessNameDelete;
          break;
      }

      notifyIcon1.Text = labelProgressPercentText.Text;
      labelProgressMessageText.Text = (string)MessageList[1];

      /*
      if (Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.IsPlatformSupported)
      {
        // Task bar progress
        Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager taskbarInstance;
        // Task bar progress
        taskbarInstance = Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.Instance;
        taskbarInstance.SetProgressState(Microsoft.WindowsAPICodePack.Taskbar.TaskbarProgressBarState.Normal);
        taskbarInstance.SetProgressValue(e.ProgressPercentage, 10000);
        //taskbarInstance.SetProgressState(Microsoft.WindowsAPICodePack.Taskbar.TaskbarProgressBarState.NoProgress);
      }
      */

      this.Update();

    }

    private void backgroundWorker_Encryption_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      try
      {
        buttonCancel.Text = Resources.ButtonTextOK;

        if (e.Cancelled)
        {
          // Canceled
          labelProgressPercentText.Text = "- %";
          progressBar.Value = 0;
          progressBar.Style = ProgressBarStyle.Continuous;
          labelCryptionType.Text = "";
          notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
          AppSettings.Instance.FileList = null;

          // Atc file is deleted
          if (File.Exists(encryption4.AtcFilePath) == true)
          {
            FileSystem.DeleteFile(encryption4.AtcFilePath);
          }

          // 暗号化の処理はキャンセルされました。
          // Encryption was canceled.
          labelProgressMessageText.Text = Resources.labelEncryptionCanceled;
          return;

        }
        else if (e.Error != null)
        {
          // Atc file is deleted
          if (File.Exists(encryption4.AtcFilePath) == true)
          {
            FileSystem.DeleteFile(encryption4.AtcFilePath);
          }
        }
        else
        {
          /*
          // Status code
          private const int ENCRYPT_SUCCEEDED   = 1; // Encrypt is succeeded.
          private const int DECRYPT_SUCCEEDED   = 2; // Decrypt is succeeded.
          private const int DELETE_SUCCEEDED    = 3; // Delete is succeeded.
          private const int HEADER_DATA_READING = 4; // Header data is reading.
          private const int ENCRYPTING          = 5; // Ecrypting.
          private const int DECRYPTING          = 6; // Decrypting.
          private const int DELETING            = 7; // Deleting.

          // Error code
          private const int ERROR_UNEXPECTED         = -100;
          private const int NOT_ATC_DATA             = -101;
          private const int ATC_BROKEN_DATA          = -102;
          private const int NO_DISK_SPACE            = -103;
          private const int FILE_INDEX_NOT_FOUND     = -104;
          private const int PASSWORD_TOKEN_NOT_FOUND = -105;
          private const int NOT_CORRECT_HASH_VALUE   = -106;
          private const int INVALID_FILE_PATH        = -107;
          private const int OS_DENIES_ACCESS　       = -108;
          private const int DATA_NOT_FOUND           = -109;
          private const int DIRECTORY_NOT_FOUND      = -110;
          private const int DRIVE_NOT_FOUND          = -111;
          private const int FILE_NOT_LOADED          = -112;
          private const int FILE_NOT_FOUND           = -113;
          private const int PATH_TOO_LONG            = -114;
          private const int CRYPTOGRAPHIC_EXCEPTION  = -115;
          private const int RSA_KEY_GUID_NOT_MATCH   = -116;
          private const int IO_EXCEPTION             = -117;
          */
          switch (encryption4.ReturnCode)
          {
            //-----------------------------------
            case ENCRYPT_SUCCEEDED:
              labelProgressPercentText.Text = "100%";
              progressBar.Style = ProgressBarStyle.Continuous;
              progressBar.Value = progressBar.Maximum;
              labelCryptionType.Text = "";
              labelProgressMessageText.Text = Resources.labelCaptionCompleted;  // "Completed"
              notifyIcon1.Text = "100% " + Resources.labelCaptionCompleted;

              FileIndex++;

              if ( AppSettings.Instance.fDeveloperConsole == true)
              {
                toolStripStatusLabelEncryptionTime.Visible = true;
                toolStripStatusLabelEncryptionTime.Text = "Encryption Time : " + encryption4.EncryptionTimeString;
              }

              // One more encryption
              if (FileIndex < AppSettings.Instance.FileList.Count)
              {
                EncryptionProcess();   // Encryption again
                return;
              }
              else
              {
                // Wait for the BackgroundWorker thread end
                while (bkg.IsBusy)
                {
                  Application.DoEvents();
                }

                // Delete file or directories
                if (AppSettings.Instance.fDelOrgFile == true || checkBoxReDeleteOriginalFileAfterEncryption.Checked == true)
                {
                  if (AppSettings.Instance.fConfirmToDeleteAfterEncryption == true)
                  {
                    // 問い合わせ
                    // 暗号化ファイルの元となったファイル及びフォルダーを削除しますか？
                    //
                    // Question
                    // Are you sure to delete the files and folders that are the source of the encrypted file?
                    DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageDeleteOriginalFilesAndFolders,
                      Resources.DialogTitleQuestion, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (ret == DialogResult.Yes)
                    {
                      buttonCancel.Text = Resources.ButtonTextCancel;
                      DeleteData(AppSettings.Instance.FileList);
                    }

                  }
                  else
                  {
                    DeleteData(AppSettings.Instance.FileList);
                  }
                }

              }

              if (AppSettings.Instance.fEndToExit == true)
              {
                Application.Exit();
              }

              return;

            //-----------------------------------
            case OS_DENIES_ACCESS:
              // エラー
              // オペレーティングシステムが I/O エラーまたは特定の種類のセキュリティエラーのために
              // アクセスを拒否しました。処理を中止します。
              //
              // Error
              // Operating system denied access due to I/O error or 
              // certain types of security errors. The process is aborted.
              // 
              MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageOsDeniesAccess,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case NO_DISK_SPACE:
              // エラー
              // 以下のドライブに空き容量がありません。処理を中止します。
              // [ドライブパス名]
              //
              // Alert
              // No free space on the following disk. The process is aborted.
              // [The drive path]
              //
              MessageBox.Show(new Form { TopMost = true },
                Resources.DialogMessageNoDiskSpace + Environment.NewLine + encryption4.DriveName,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case DIRECTORY_NOT_FOUND:
              // エラー
              // ファイルまたはディレクトリの一部が見つかりません。処理を中止します。
              // [システムからのエラーメッセージ]
              //
              // Error
              // A part of the file or directory cannot be found. 
              // The process is aborted.
              // [Error message from the system]
              //
              MessageBox.Show(new Form { TopMost = true },
                Resources.DialogMessageDirectoryNotFound + Environment.NewLine + encryption4.ErrorMessage,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case DRIVE_NOT_FOUND:
              // エラー
              // 使用できないドライブまたは共有にアクセスしようとしました。
              // 処理を中止します。
              // [システムからのエラーメッセージ]
              //
              // Error
              // An attempt was made to access an unavailable drive or share.
              // The process is aborted.
              // [Error message from the system]
              //
              MessageBox.Show(new Form { TopMost = true },
                Resources.DialogMessageDriveNotFound + Environment.NewLine + encryption4.ErrorMessage,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case FILE_NOT_LOADED:
              // エラー
              // マネージドアセンブリが見つかりましたが、読み込むことができませんでした。
              // 処理を中止します。
              // [そのファイルパス]
              //
              // Error
              // A managed assembly was found but could not be loaded.
              // The process is aborted.
              // [The file path]
              //
              MessageBox.Show(new Form { TopMost = true },
                Resources.DialogMessageFileNotLoaded + Environment.NewLine + encryption4.ErrorFilePath,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case FILE_NOT_FOUND:
              // エラー
              // ディスク上に存在しないファイルにアクセスしようとして失敗しました。
              // 処理を中止します。
              // [そのファイルパス]
              //
              // Error
              // An attempt to access a file that does not exist on the disk failed.
              // The process is aborted.
              // [The file path]
              //
              MessageBox.Show(new Form { TopMost = true },
                Resources.DialogMessageFileNotFound + Environment.NewLine + encryption4.ErrorFilePath,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case PATH_TOO_LONG:
              // エラー
              // パス名または完全修飾ファイル名がシステム定義の最大長を超えています。
              // 処理を中止します。
              //
              // Error
              // The path name or fully qualified file name exceeds the system-defined maximum length.
              // The process is aborted.
              //
              MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessagePathTooLong,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case CRYPTOGRAPHIC_EXCEPTION:
              // エラー
              // RSA公開鍵または秘密鍵の形式が違うか、XMLファイル形式ではありません。
              // 処理を中止します。
              //
              // Error
              // The RSA public or private key is in a different format or is not in XML file format.
              // The process is aborted.
              //
              MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageCryptographicException,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case IO_EXCEPTION:
              // エラー
              // [I/O エラーが発生したときにスローされる例外を説明するメッセージ]
              //
              // Error
              // [A message describing the exception that is thrown when an I/O error occurs.]
              MessageBox.Show(new Form { TopMost = true }, encryption4.ErrorMessage,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

            //-----------------------------------
            case ERROR_UNEXPECTED:
            default:
              // エラー
              // 予期せぬエラーが発生しました。処理を中止します。
              //
              // Error
              // An unexpected error has occurred. The process is aborted.
              MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageUnexpectedError,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
              break;

          }// end switch();

          labelProgressPercentText.Text = "- %";
          progressBar.Style = ProgressBarStyle.Continuous;
          progressBar.Value = 0;
          labelCryptionType.Text = Resources.labelCaptionError;
          notifyIcon1.Text = "- % " + Resources.labelCaptionError;
          AppSettings.Instance.FileList = null;
          this.Update();
        }

      }
      finally
      {
        decryption4 = null;
      }

    }

    private void backgroundWorker_Decryption_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {

      buttonCancel.Text = Resources.ButtonTextOK;

      if (e.Cancelled)
      {
        if(TempOverWriteOption == SKIP_ALL)
        {
          labelProgressPercentText.Text = "100 %";
          progressBar.Value = progressBar.Maximum;
          progressBar.Style = ProgressBarStyle.Continuous;
          labelCryptionType.Text = "";
          notifyIcon1.Text = "- % " + Resources.labelCaptionAllSkipped;
          AppSettings.Instance.FileList = null;
          // スキップされました。
          // skipped.
          labelProgressMessageText.Text = Resources.labelCaptionAllSkipped;

        }
        else
        {
          // Canceled
          labelProgressPercentText.Text = "- %";
          progressBar.Value = 0;
          progressBar.Style = ProgressBarStyle.Continuous;
          labelCryptionType.Text = "";
          notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
          AppSettings.Instance.FileList = null;
          // 復号処理はキャンセルされました。
          // Decryption was canceled.
          labelProgressMessageText.Text = Resources.labelDecyptionCanceled;
        }
        this.Update();
        return;

      }
      else if (e.Error != null)
      {
        //e.Error.Message;
        labelProgressPercentText.Text = "- %";
        labelProgressMessageText.Text = e.Error.Message;
        progressBar.Value = 0;
        labelProgressMessageText.Text = Resources.labelCaptionError;     // "Error occurred"
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;
        this.Update();
        return;
      }
      else
      {
        /*
        // Status code
        private const int ENCRYPT_SUCCEEDED   = 1; // Encrypt is succeeded.
        private const int DECRYPT_SUCCEEDED   = 2; // Decrypt is succeeded.
        private const int DELETE_SUCCEEDED    = 3; // Delete is succeeded.
        private const int HEADER_DATA_READING = 4; // Header data is reading.
        private const int ENCRYPTING          = 5; // Ecrypting.
        private const int DECRYPTING          = 6; // Decrypting.
        private const int DELETING            = 7; // Deleting.

        // Error code
        private const int USER_CANCELED            = -1;
        private const int ERROR_UNEXPECTED         = -100;
        private const int NOT_ATC_DATA             = -101;
        private const int ATC_BROKEN_DATA          = -102;
        private const int NO_DISK_SPACE            = -103;
        private const int FILE_INDEX_NOT_FOUND     = -104;
        private const int PASSWORD_TOKEN_NOT_FOUND = -105;
        private const int NOT_CORRECT_HASH_VALUE   = -106;
        private const int INVALID_FILE_PATH        = -107;
        private const int OS_DENIES_ACCESS         = -108;
        private const int DATA_NOT_FOUND           = -109;
        private const int DIRECTORY_NOT_FOUND      = -110;
        private const int DRIVE_NOT_FOUND          = -111;
        private const int FILE_NOT_LOADED          = -112;
        private const int FILE_NOT_FOUND           = -113;
        private const int PATH_TOO_LONG            = -114;
        private const int CRYPTOGRAPHIC_EXCEPTION  = -115;
        private const int RSA_KEY_GUID_NOT_MATCH   = -116;
        private const int IO_EXCEPTION             = -117;
        */
        int ReturnCode;
        if (decryption2 != null)
        {
          ReturnCode = decryption2.ReturnCode;
        }
        else if (decryption3 != null)
        {
          ReturnCode = decryption3.ReturnCode;
        }
        else
        {
          ReturnCode = decryption4.ReturnCode;
        }

        string ErrorFilePath = "";
        switch (ReturnCode)
        {
          //-----------------------------------
          case DECRYPT_SUCCEEDED:
            labelProgressPercentText.Text = "100%";
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = progressBar.Maximum;
            labelCryptionType.Text = "";
            labelProgressMessageText.Text = Resources.labelCaptionCompleted;  // "Completed"
            notifyIcon1.Text = "100% " + Resources.labelCaptionCompleted;

            if (decryption2 != null)
            {
              OutputFileList.AddRange(decryption2.OutputFileList);
            }
            else if (decryption3 != null)
            {
              OutputFileList.AddRange(decryption3.OutputFileList);
            }
            else
            {
              OutputFileList.AddRange(decryption4.OutputFileList);
            }

            //-----------------------------------
            // Developer mode
            if (AppSettings.Instance.fDeveloperConsole == true)
            {
              showDeveloperConsoleWindowDecrypt();
            }

            //-----------------------------------
            // DecryptionEndProcess
            if ( FileIndex < AppSettings.Instance.FileList.Count)
            {
              FileIndex++;
              DecryptionProcess();
              return;
            }

            DecryptionEndProcess();


            this.Update();
            return;

          //-----------------------------------
          case OS_DENIES_ACCESS:
            // エラー
            // ファイルへのアクセスが拒否されました。
            // ファイルの読み書きができる場所（デスクトップ等）へ移動して再度実行してください。
            //
            // Error
            // Access to the file has been denied.
            // Move to a place (eg Desktop) where you can read and write files and try again.
            // 
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageAccessDeny,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case NOT_ATC_DATA:
            // エラー
            // 暗号化ファイルではありません。処理を中止します。
            //
            // Error
            // The file is not encrypted file. The process is aborted.
            if (decryption4 != null)
            {
              ErrorFilePath = decryption4.ErrorFilePath;
            }
            else if (decryption3 != null)
            {
              ErrorFilePath = decryption3.ErrorFilePath;
            }
            else if (decryption2 != null)
            {
              ErrorFilePath = decryption2.ErrorFilePath;
            }
            MessageBox.Show(new Form { TopMost = true }, 
              Resources.DialogMessageNotAtcFile + Environment.NewLine + ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case ATC_BROKEN_DATA:
            // エラー
            // 暗号化ファイル(.atc)は壊れています。処理を中止します。
            //
            // Error
            // Encrypted file ( atc ) is broken. The process is aborted.
            if (decryption4 != null)
            {
              ErrorFilePath = decryption4.ErrorFilePath;
            }
            else if (decryption3 != null)
            {
              ErrorFilePath = decryption3.ErrorFilePath;
            }
            else if (decryption2 != null)
            {
              ErrorFilePath = decryption2.ErrorFilePath;
            }
            MessageBox.Show(new Form { TopMost = true }, 
              Resources.DialogMessageAtcFileBroken + Environment.NewLine + ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case NO_DISK_SPACE:
            // 警告
            // ドライブに空き容量がありません。処理を中止します。
            // [ドライブパス名]
            //
            // Alert
            // No free space on the disk. The process is aborted.
            // [The drive path]
            //
            if (decryption4 != null)
            {
              ErrorFilePath = decryption4.DriveName;
            }
            else if (decryption3 != null)
            {
              ErrorFilePath = decryption3.DriveName;
            }
            else if (decryption2 != null)
            {
              ErrorFilePath = decryption2.DriveName;
            }
            MessageBox.Show(new Form { TopMost = true }, 
              Resources.DialogMessageNoDiskSpace + Environment.NewLine + ErrorFilePath,
              Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case FILE_INDEX_NOT_FOUND:
            // エラー
            // 暗号化ファイル内部で、不正なファイルインデックスがありました。
            //
            // Error
            // Internal file index is invalid in encrypted file.
            MessageBox.Show(Resources.DialogMessageFileIndexInvalid,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case NOT_CORRECT_HASH_VALUE:
            // エラー
            // ファイルのハッシュ値が異なります。ファイルが壊れたか、改ざんされた可能性があります。
            // 処理を中止します。
            //
            // Error
            // The file is not the same hash value. Whether the file is corrupted, it may have been made the falsification.
            // The process is aborted.
            if (decryption4 != null)
            {
              ErrorFilePath = decryption4.ErrorFilePath;
            }
            else if (decryption3 != null)
            {
              ErrorFilePath = decryption3.ErrorFilePath;
            }
            else if (decryption2 != null)
            {
              ErrorFilePath = decryption2.ErrorFilePath;
            }
            MessageBox.Show(new Form { TopMost = true }, 
              Resources.DialogMessageNotSameHash + Environment.NewLine + ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case INVALID_FILE_PATH:
            // エラー
            // ファイル、またはフォルダーパスが不正です。処理を中止します。
            //
            // Error
            // The path of files or folders are invalid. The process is aborted.
            if (decryption4 != null)
            {
              ErrorFilePath = decryption4.ErrorFilePath;
            }
            else if (decryption3 != null)
            {
              ErrorFilePath = decryption3.ErrorFilePath;
            }
            else if (decryption2 != null)
            {
              ErrorFilePath = decryption2.ErrorFilePath;
            }
            MessageBox.Show(new Form { TopMost = true }, 
              Resources.DialogMessageInvalidFilePath + Environment.NewLine + ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case DATA_NOT_FOUND:
            // エラー
            // 暗号化するデータが見つかりません。ファイルは壊れています。
            // 復号できませんでした。
            //
            // Error
            // Encrypted data not found. The file is broken.
            // Decryption failed.
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageDataNotFound,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case PASSWORD_TOKEN_NOT_FOUND:
            // エラー
            // パスワードがちがうか、ファイルが破損している可能性があります。
            // 復号できませんでした。
            //
            // Error
            // Password is invalid, or the encrypted file might have been broken.
            // Decryption is aborted.
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageDecryptionError,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            if (LimitOfInputPassword > 1)
            {
              LimitOfInputPassword--;
              panelStartPage.Visible = false;
              panelEncrypt.Visible = false;
              panelEncryptConfirm.Visible = false;
              panelDecrypt.Visible = true;
              panelRsa.Visible = false;
              panelRsaKey.Visible = false;
              panelProgressState.Visible = false;
              textBoxDecryptPassword.Focus();
              textBoxDecryptPassword.SelectAll();
              return;
            }
            else
            {
              // パスワード回数を超過
              // Exceed times limit of inputting password

              if (AppSettings.Instance.fBroken == true)
              {
                // ファイル破壊を行うか
                // Whether breaking the files
                foreach (string FilePath in AppSettings.Instance.FileList)
                {
                  BreakTheFile(FilePath);
                }
              }
              // スタートページへ戻る
              // Back to Start page.
              panelStartPage.Visible = true;
              panelEncrypt.Visible = false;
              panelEncryptConfirm.Visible = false;
              panelDecrypt.Visible = false;
              panelRsa.Visible = false;
              panelRsaKey.Visible = false;
              panelProgressState.Visible = false;
              textBoxDecryptPassword.Focus();
              textBoxDecryptPassword.SelectAll();
              return;
            }

          //-----------------------------------
          case CRYPTOGRAPHIC_EXCEPTION:
            // エラー
            // RSA公開鍵または秘密鍵の形式が違うか、XMLファイル形式ではありません。
            // 処理を中止します。
            //
            // Error
            // The RSA public or private key is in a different format or is not in XML file format.
            // The process is aborted.
            //
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageCryptographicException,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case RSA_KEY_GUID_NOT_MATCH:
            // エラー
            // RSAで暗号化された公開鍵と異なるペアの鍵が指定されています。復号できません。
            // 処理を中止します。
            //
            // Error
            // A different pair of keys from the RSA-encrypted public key has been specified. Cannot decrypt.
            // The process is aborted.
            //
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageRsaKeyGuidNotMatch,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

          //-----------------------------------
          case ERROR_UNEXPECTED:
          default:
            // エラー
            // 予期せぬエラーが発生しました。処理を中止します。
            //
            // Error
            // An unexpected error has occurred. And stops processing.
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageUnexpectedError,
            Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            break;

        }// end switch();

        labelProgressPercentText.Text = "- %";
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = 0;
        labelCryptionType.Text = Resources.labelCaptionError;
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;
        AppSettings.Instance.FileList = null;
        this.Update();
                
      }

    }

    private void backgroundWorker_Wipe_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {

      buttonCancel.Text = Resources.ButtonTextOK;

      if (e.Cancelled)
      {
        // Canceled
        labelProgressPercentText.Text = "- %";
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Continuous;
        labelCryptionType.Text = "";
        notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
        AppSettings.Instance.FileList = null;

        // ファイル、またはフォルダーの完全削除がキャンセルされました。
        // Complete deleting files or folder has been canceled.
        labelProgressMessageText.Text = Resources.labelCompleteDeleteFileCanceled;
        this.Update();
        return;

      }
      else if (e.Error != null)
      {
        //e.Error.Message;
        labelProgressPercentText.Text = "- %";
        labelProgressMessageText.Text = e.Error.Message;
        progressBar.Value = 0;
        labelProgressMessageText.Text = Resources.labelCaptionError;     // "Error occurred"
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;
        this.Update();
        return;

      }
      else
      {
        switch ((int)e.Result)
        {
          case DELETE_SUCCEEDED:
          
            // The operation completed normally. 
            labelCryptionType.Text = "";
            // ファイル、またはフォルダーの完全削除は正常に完了しました。
            // Files or folders complete deleting is completed normally.
            labelProgressMessageText.Text = Resources.labelCompleteDeletingCompleted;
            labelProgressPercentText.Text = "100%";
            progressBar.Value = progressBar.Maximum;  // 100%
            this.Update();
            return;

          //-----------------------------------
          case ERROR_UNEXPECTED:
            // エラー
            // 予期せぬエラーが発生しました。処理を中止します。
            //
            // Alert
            // An unexpected error has occurred. And stops processing.
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageUnexpectedError,
            Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            this.Update();
            break;

          //-----------------------------------
          case NO_DISK_SPACE:
            // エラー
            // 以下のドライブに空き容量がありません。処理を中止します。
            // [ドライブパス名]
            //
            // Alert
            // No free space on the following disk. The process is aborted.
            // [The drive path]
            MessageBox.Show(new Form { TopMost = true }, 
              Resources.DialogMessageNoDiskSpace + Environment.NewLine + decryption2.DriveName,
              Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            this.Update();
            break;

          default:
            break;

        }

        labelProgressPercentText.Text = "- %";
        progressBar.Value = 0;
        labelCryptionType.Text = "";
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;
        AppSettings.Instance.FileList = null;

      }

    }

    #endregion

    //======================================================================
    // メニューアイテム
    //======================================================================
    #region

    private void ToolStripMenuItemFile_DropDownOpened(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        ToolStripMenuItemEncrypt.Enabled = true;
        ToolStripMenuItemDecrypt.Enabled = true;
      }
      else
      {
        ToolStripMenuItemEncrypt.Enabled = false;
        ToolStripMenuItemDecrypt.Enabled = false;
      }
    }

    private void ToolStripMenuItemEncryptSelectFiles_Click(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        AppSettings.Instance.FileList = new List<string>();
        openFileDialog1.Title = Resources.DialogTitleEncryptSelectFiles;
        openFileDialog1.Filter = Resources.OpenDialogFilterAllFiles;
        openFileDialog1.InitialDirectory = AppSettings.Instance.InitDirPath;
        openFileDialog1.Multiselect = true;
        if (openFileDialog1.ShowDialog() == DialogResult.OK)
        {
          foreach (string filename in openFileDialog1.FileNames)
          {
            AppSettings.Instance.FileList.Add(filename);
          }
          // Check memorized password
          if (AppSettings.Instance.fMyEncryptPasswordKeep == true)
          {
            textBoxPassword.Text = textBoxRePassword.Text = AppSettings.Instance.MyEncryptPasswordString;
          }

          // Encrypt by memorized password without confirming
          if (AppSettings.Instance.fMemPasswordExe == true)
          {
            buttonEncryptStart.PerformClick();
          }
          else
          {
            panelStartPage.Visible = false;
            panelEncrypt.Visible = true;        // Encrypt
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = false;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;
          }
        }
      }
    }

    private void ToolStripMenuItemEncryptSelectFolder_Click(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        AppSettings.Instance.FileList = new List<string>();
        folderBrowserDialog1.Description = Resources.DialogTitleEncryptSelectFolder;
        folderBrowserDialog1.SelectedPath = AppSettings.Instance.InitDirPath;
        if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
        {
          AppSettings.Instance.FileList.Add(folderBrowserDialog1.SelectedPath);
        }
        else{
          return;
        }

        // Check memorized password
        if (AppSettings.Instance.fMyEncryptPasswordKeep == true)
        {
          textBoxPassword.Text = textBoxRePassword.Text = AppSettings.Instance.MyEncryptPasswordString;
        }
          
        // Encrypt by memorized password without confirming
        if (AppSettings.Instance.fMemPasswordExe == true)
        {
          buttonEncryptStart.PerformClick();
        }
        else
        {
          panelStartPage.Visible = false;
          panelEncrypt.Visible = true;        // Encrypt
          panelEncryptConfirm.Visible = false;
          panelDecrypt.Visible = false;
          panelRsa.Visible = false;
          panelRsaKey.Visible = false;
          panelProgressState.Visible = false;
        }
      }
    }

    private void ToolStripMenuItemDecrypt_Click(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        AppSettings.Instance.FileList = null;
        openFileDialog1.Title = Resources.DialogTitleEncryptSelectFiles;
        openFileDialog1.Filter = Resources.SaveDialogFilterAtcFiles;
        openFileDialog1.InitialDirectory = AppSettings.Instance.InitDirPath;
        openFileDialog1.Multiselect = true;
        if (openFileDialog1.ShowDialog() == DialogResult.OK)
        {
          foreach (string filname in openFileDialog1.FileNames) 
          {
            if (AppSettings.Instance.FileList == null)
            {
              AppSettings.Instance.FileList = new List<string>();
            }
            AppSettings.Instance.FileList.Add(filname);
          }

          // Check memorized password
          if (AppSettings.Instance.fMyDecryptPasswordKeep == true)
          {
            textBoxDecryptPassword.Text = AppSettings.Instance.MyDecryptPasswordString;
          }
            
          // Encrypt by memorized password without confirming
          if (AppSettings.Instance.fMemPasswordExe)
          {
            buttonDecryptStart.PerformClick();
          }
          else
          {
            panelStartPage.Visible = false;
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = true;    //Decrypt
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;
          }
        }

      }
    }

    private void ToolStripMenuItemExit_Click(object sender, EventArgs e)
    {
      Application.Exit();
    }

    private void ToolStripMenuItemOption_DropDownOpened(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        ToolStripMenuItemSetting.Enabled = true;
      }
      else
      {
        ToolStripMenuItemSetting.Enabled = false;
      }
    }

    private void ToolStripMenuItemSetting_Click(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        //-----------------------------------
        // Check culture
        switch (AppSettings.Instance.Language)
        {
          case "ja":
            CultureInfo.CurrentUICulture = CultureInfo.CreateSpecificCulture("ja-JP");
            Thread.CurrentThread.CurrentCulture = new CultureInfo("ja-JP");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
            break;
          case "en":
            Thread.CurrentThread.CurrentCulture = new CultureInfo("", true);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("", true);
            break;
          case "":
          default:
            if (CultureInfo.CurrentCulture.Name == "ja-JP")
            {
              CultureInfo.CurrentUICulture = CultureInfo.CreateSpecificCulture("ja-JP");
              Thread.CurrentThread.CurrentCulture = new CultureInfo("ja-JP");
              Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
            }
            else
            {
              Thread.CurrentThread.CurrentCulture = new CultureInfo("", true);
              Thread.CurrentThread.CurrentUICulture = new CultureInfo("", true);
            }
            break;
        }

        // Show Option form
        Form3 frm3 = new Form3();
        frm3.ShowDialog();
        frm3.Dispose();

        pictureBoxAtc.Image = pictureBoxAtcOff.Image;
        pictureBoxExe.Image = pictureBoxExeOff.Image;
        pictureBoxRsa.Image = pictureBoxRsaOff.Image;
        pictureBoxDec.Image = pictureBoxDecOff.Image;

        if (AppSettings.Instance.EncryptionSameFileTypeAlways == 1)
        {
          pictureBoxAtc.Image = pictureBoxAtcOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeAlways == 2)
        {
          pictureBoxExe.Image = pictureBoxExeOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeAlways == 3)
        {
          // Obsolete 
          // pictureBoxRsa.Image = pictureBoxRsaOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeAlways == 4)
        {
          // RSA
          pictureBoxRsa.Image = pictureBoxRsaOn.Image;
        }
        else
        {
          //AppSettings.Instance.EncryptionFileType = 0;
        }
        
      }
    }

    private void ToolStripMenuItemHelpContents_Click(object sender, EventArgs e)
    {
      // Open 'Online Help' in web browser.
      System.Diagnostics.Process.Start("https://hibara.org/software/attachecase/help/");
    }

    private void ToolStripMenuItemAbout_Click(object sender, EventArgs e)
    {
      // Show AttacheCase's information
      Form2 frm2 = new Form2();
      frm2.ShowDialog();
      frm2.Dispose();
    }

#endregion

    //======================================================================
    // 各ウィンドウページが表示されたときに発生するイベント
    //======================================================================
    #region

    private void StartProcess()
    {
      int ProcessType = 0;
      TempOverWriteOption = -1;

      labelPassword.Text = Resources.labelPassword;
      labelInputPasswordAgain.Text = Resources.labelInputPasswordAgainToConfirm;

      textBoxPassword.Enabled = true;
      textBoxPassword.BackColor = SystemColors.Window;
      textBoxPassword.Text = "";
      textBoxRePassword.Enabled = true;
      textBoxRePassword.BackColor = SystemColors.Window;
      textBoxRePassword.Text = "";
      AppSettings.Instance.MyEncryptPasswordBinary = null;

      // self-executable file
      if (AppSettings.Instance.fSaveToExeout == true)
      {
        ProcessType = PROCESS_TYPE_ATC_EXE;
        AppSettings.Instance.EncryptionFileType = FILE_TYPE_ATC_EXE;
      }

      // 明示的な暗号処理、または復号処理
      // Explicit encryption or decryption?
      if (AppSettings.Instance.ProcTypeWithoutAsk > 0)
      {
        if (AppSettings.Instance.ProcTypeWithoutAsk == 1) // Encryption
        {
          panelStartPage.Visible = false;
          panelEncrypt.Visible = true;        // Encrypt
          textBoxPassword.Focus();            // Text box is focused
          panelEncryptConfirm.Visible = false;
          panelDecrypt.Visible = false;
          panelRsa.Visible = false;
          panelRsaKey.Visible = false;
          panelProgressState.Visible = false;

        }
        else if(AppSettings.Instance.ProcTypeWithoutAsk == 2) // Decryption
        {
          panelStartPage.Visible = false;
          panelEncrypt.Visible = false;
          panelEncryptConfirm.Visible = false;
          panelDecrypt.Visible = true;        // Decrypt
          textBoxDecryptPassword.Focus();     // Text box is focused
          panelRsa.Visible = false;
          panelRsaKey.Visible = false;
          panelProgressState.Visible = false;
        }

      }
      // 内容にかかわらず暗号化か復号かを問い合わせる
      // Ask to encrypt or decrypt regardless of contents.
      else if (AppSettings.Instance.FileList.Count() > 0 && AppSettings.Instance.fAskEncDecode == true)
      {
        Form4 frm4 = new Form4("AskEncryptOrDecrypt", "");
        frm4.ShowDialog();
        int ProcessNum = frm4.AskEncryptOrDecrypt;  // 1: Encryption, 2: Decryption, -1: Cancel
        frm4.Dispose();

        //-----------------------------------
        // Encryption
        //-----------------------------------
        if (ProcessNum == 1)
        {
          panelStartPage.Visible = false;
          panelEncrypt.Visible = true;        // Encrypt
          textBoxPassword.Focus();            // Text box is focused
          panelEncryptConfirm.Visible = false;
          panelDecrypt.Visible = false;
          panelRsa.Visible = false;
          panelRsaKey.Visible = false;
          panelProgressState.Visible = false;
        }
        //-----------------------------------
        // Decryption
        //-----------------------------------
        else if (ProcessNum == 2)
        {
          panelStartPage.Visible = false;
          panelEncrypt.Visible = false;
          panelEncryptConfirm.Visible = false;
          panelDecrypt.Visible = true;        // Decrypt
          textBoxDecryptPassword.Focus();     // Text box is focused
          panelRsa.Visible = false;
          panelRsaKey.Visible = false;
          panelProgressState.Visible = false;
        }
        //-----------------------------------
        // Cancel
        //-----------------------------------
        else
        {
          return;
        }

      }
      else
      {
        //----------------------------------------------------------------------
        // 問い合わせず自動判別する
        // Auto detect without asking user

        // File type
        // private const int FILE_TYPE_ERROR           = -1;
        // private const int FILE_TYPE_NONE            =  0;
        // private const int FILE_TYPE_ATC             =  1;
        // private const int FILE_TYPE_ATC_EXE         =  2;
        // private const int FILE_TYPE_PASSWORD_ZIP    =  3;
        // private const int FILE_TYPE_RSA_DATA        =  4;
        // private const int FILE_TYPE_RSA_PRIVATE_KEY =  5;
        // private const int FILE_TYPE_RSA_PUBLIC_KEY  =  6;
        //
        // -----------------------------------
        //
        // Process Type
        // private const int PROCESS_TYPE_ERROR          = -1;
        // private const int PROCESS_TYPE_NONE           =  0;
        // private const int PROCESS_TYPE_ATC            =  1;
        // private const int PROCESS_TYPE_ATC_EXE        =  2;
        // private const int PROCESS_TYPE_PASSWORD_ZIP   =  3;
        // private const int PROCESS_TYPE_DECRYPTION     =  4;
        // private const int PROCESS_TYPE_RSA_ENCRYPTION =  5;
        // private const int PROCESS_TYPE_RSA_DECRYPTION =  6;

        // 自動判別
        // Detect file type
        ProcessType = AppSettings.Instance.DetectFileType();

        if (ProcessType == PROCESS_TYPE_DECRYPTION)
        {
          // Decryption
        }
        else
        {
          // Encryption

          ProcessType = AppSettings.Instance.EncryptionSameFileTypeBefore;

          if (AppSettings.Instance.EncryptionSameFileTypeAlways == FILE_TYPE_ATC)
          {
            ProcessType = PROCESS_TYPE_ATC;
          }
          else if (AppSettings.Instance.EncryptionSameFileTypeAlways == FILE_TYPE_ATC_EXE)
          {
            ProcessType = PROCESS_TYPE_ATC_EXE;
          }
          //else if (AppSettings.Instance.EncryptionSameFileTypeAlways == PROCESS_TYPE_PASSWORD_ZIP)
          //{
          //  ProcessType = PROCESS_TYPE_PASSWORD_ZIP;
          //}
          else if (AppSettings.Instance.EncryptionSameFileTypeAlways == PROCESS_TYPE_DECRYPTION)
          {
            ProcessType = PROCESS_TYPE_RSA_ENCRYPTION;
          }
          else
          {
            ProcessType = AppSettings.Instance.DetectFileType();
          }
        }

        if (AppSettings.Instance.FileList.Count() > 0)
        {
          //----------------------------------------------------------------------
          // Encryption
          if (ProcessType == PROCESS_TYPE_NONE )
          {
            // RSA Encryption
            if (panelRsa.Visible == true)
            {
              if (fWaitingForKeyFile == true)
              {
                panelStartPage.Visible = false;
                panelEncrypt.Visible = false;
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = false;
                panelRsaKey.Visible = false;
                panelProgressState.Visible = true;

                // Error
                labelProgressPercentText.Text = "- %";
                progressBar.Value = 0;
                pictureBoxProgress.Image = pictureBoxPublicAndPrivateKey.Image;
                labelProgress.Text = Resources.labelRsa;
                labelCryptionType.Text = Resources.labelCaptionError;
                // 公開鍵または秘密鍵は読み込まれませんでした。
                // The public or private key was not loaded.
                labelProgressMessageText.Text = Resources.labelKeyFileNotLoaded;
                notifyIcon1.Text = "- % " + Resources.labelCaptionError;
                AppSettings.Instance.FileList = null;
                fWaitingForKeyFile = false;
                buttonCancel.Text = Resources.ButtonTextOK;

                return;
              }
              else
              {
                // ただファイルリストを保持しておく
                // Just keep the file list.
                fWaitingForKeyFile = true;
                // ファイルまたはフォルダーが読み込まれました。暗号化するための公開鍵をここへドラッグ＆ドロップしてください。
                // The file or folder has been loaded.Drag and drop the public key to be encrypted here.
                labelRsaMessage.Text = Resources.labelRsaFilesloaded;
                return;
              }

            }
            panelStartPage.Visible = false;
            panelEncrypt.Visible = true;         // Encrypt
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = false;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;

            this.Activate();              // MainForm is Activated
            textBoxPassword.Focus();      // Text box is focused
          }
          else if (ProcessType == PROCESS_TYPE_ATC || ProcessType == PROCESS_TYPE_ATC_EXE) 
          {
            // RSA Encryption
            if (panelRsa.Visible == true)
            {
              if (fWaitingForKeyFile == true)
              {
                panelStartPage.Visible = false;
                panelEncrypt.Visible = false;
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = false;
                panelRsaKey.Visible = false;
                panelProgressState.Visible = true;

                // Error
                labelProgressPercentText.Text = "- %";
                progressBar.Value = 0;
                pictureBoxProgress.Image = pictureBoxPublicAndPrivateKey.Image;
                labelProgress.Text = Resources.labelRsa;
                labelCryptionType.Text = Resources.labelCaptionError;
                // 公開鍵または秘密鍵は読み込まれませんでした。
                // The public or private key was not loaded.
                labelProgressMessageText.Text = Resources.labelKeyFileNotLoaded;
                notifyIcon1.Text = "- % " + Resources.labelCaptionError;
                AppSettings.Instance.FileList = null;
                fWaitingForKeyFile = false;
                buttonCancel.Text = Resources.ButtonTextOK;

                return;
              }
              else
              {
                // ただファイルリストを保持しておく
                // Just keep the file list.
                fWaitingForKeyFile = true;
                // ファイルまたはフォルダーが読み込まれました。暗号化するための公開鍵をここへドラッグ＆ドロップしてください。
                // The file or folder has been loaded.Drag and drop the public key to be encrypted here.
                labelRsaMessage.Text = Resources.labelRsaFilesloaded;
                return;
              }

            }

            if (AppSettings.Instance.FileList.Count > 0)
            {
              // すでに公開鍵が読み込まれている
              if (string.IsNullOrEmpty(XmlPublicKeyString) == false)
              {
                // RSA暗号化を実行する
                FileIndex = 0;
                EncryptionProcess();
                return;
              }
              // すでに秘密鍵が読み込まれている
              else if (string.IsNullOrEmpty(XmlPrivateKeyString) == false)
              {
                // RSA復号を実行する
                FileIndex = 0;
                DecryptionProcess();
                return;
              }
              else
              {
                panelStartPage.Visible = false;
                panelEncrypt.Visible = true;         // Encrypt
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = false;
                panelRsaKey.Visible = false;
                panelProgressState.Visible = false;

                this.Activate();
                textBoxPassword.Focus();
              }
            }
          }
          //----------------------------------------------------------------------
          // Decryption
          else if (ProcessType == PROCESS_TYPE_DECRYPTION)
          {
            if (panelRsa.Visible == true)
            {
              // ただファイルリストを保持しておく
              return;
            }
            panelStartPage.Visible = false;
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = true;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;
            textBoxDecryptPassword.Focus();     // Text box is focused
            this.Activate();                    // MainForm is Activated
          }
          //----------------------------------------------------------------------
          // RSA
          else if (ProcessType == PROCESS_TYPE_RSA_ENCRYPTION || ProcessType == PROCESS_TYPE_RSA_DECRYPTION)
          {
            for (var i = AppSettings.Instance.FileList.Count - 1; i >=0; i--)
            {
              //----------------------------------------------------------------------
              //   4: RSA Encryption data, 
              //   5: RSA key data ( XML file ), 
              if (AppSettings.Instance.CheckFileType(AppSettings.Instance.FileList[i]) == 5)
              {
                // The public key has been loaded.Drag and drop the ​file or folder to be encrypted here.
                // 公開鍵が読み込まれました。暗号化したいファイルまたはフォルダーをここへドラッグ＆ドロップしてください。
                labelRsaMessage.Text = Resources.labelRsaPublicKeyloaded;
                labelRsaDescription.Text = Resources.labelRsaPublicKeyloaded;

                panelEncrypt.Visible = false;
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = false;
                panelRsaKey.Visible = true;
                panelProgressState.Visible = false;
                panelStartPage.Visible = false;

                using (StreamReader sr = new StreamReader(AppSettings.Instance.FileList[i], Encoding.UTF8))
                {
                  XmlPublicKeyString = sr.ReadToEnd();  // Public key data ( XML data )
                }
                getXmlFileHash(AppSettings.Instance.FileList[i]);
                comboBoxHashList.SelectedIndex = 2; // SHA-1
                labelRsa.Text = Resources.labelRsaPublicKey;
                labelRsaKeyName.Text = Resources.labelRsaPublicKey;
                pictureBoxRsaPage.Image = pictureBoxPublicKey.Image;
                pictureBoxRsaType.Image = pictureBoxPublicKey.Image;
                XmlPrivateKeyString = "";
                AppSettings.Instance.FileList.Remove(AppSettings.Instance.FileList[i]);

              }
              else if (AppSettings.Instance.CheckFileType(AppSettings.Instance.FileList[i]) == 6)
              {
                // The private key has been loaded.Drag and drop the encrypted file to be decrypted here.
                // 秘密鍵が読み込まれました。復号する暗号化ファイルをここへドラッグ＆ドロップしてください。
                labelRsaMessage.Text = Resources.labelRsaPrivateKeyloaded;
                labelRsaDescription.Text = Resources.labelRsaPrivateKeyloaded;

                panelEncrypt.Visible = false;
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = false;
                panelRsaKey.Visible = true;
                panelProgressState.Visible = false;
                panelStartPage.Visible = false;

                using (StreamReader sr = new StreamReader(AppSettings.Instance.FileList[i], Encoding.UTF8))
                {
                  XmlPrivateKeyString = sr.ReadToEnd();  // Private key data ( XML data )
                }
                getXmlFileHash(AppSettings.Instance.FileList[i]);
                comboBoxHashList.SelectedIndex = 0; // GUID
                labelRsa.Text = Resources.labelRsaPrivateKey;
                labelRsaKeyName.Text = Resources.labelRsaPrivateKey;
                pictureBoxRsaPage.Image = pictureBoxPrivateKey.Image;
                pictureBoxRsaType.Image = pictureBoxPrivateKey.Image;
                XmlPublicKeyString = "";
                AppSettings.Instance.FileList.Remove(AppSettings.Instance.FileList[i]);

              }
              //----------------------------------------------------------------------
              else if (AppSettings.Instance.CheckFileType(AppSettings.Instance.FileList[i]) == 4)
              {
                // The encrypted file has been loaded. Drag and drop the private key to be decrypted here.
                // 暗号化ファイルが読み込まれました。復号するための秘密鍵をここへドラッグ＆ドロップしてください。
                labelRsaMessage.Text = Resources.labelRsaEncryptedFileloaded;
                panelEncrypt.Visible = false;
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = true;
                panelRsaKey.Visible = false;
                panelProgressState.Visible = false;
                panelStartPage.Visible = false;
              }
              else
              {
                // ファイルまたはフォルダーが読み込まれました。暗号化するための公開鍵をここへドラッグ＆ドロップしてください。
                // The file or folder has been loaded.Drag and drop the public key to be encrypted here.
                labelRsaMessage.Text = Resources.labelRsaFilesloaded;
                panelEncrypt.Visible = false;
                panelEncryptConfirm.Visible = false;
                panelDecrypt.Visible = false;
                panelRsa.Visible = true;
                panelRsaKey.Visible = false;
                panelProgressState.Visible = false;
                panelStartPage.Visible = false;
              }
            }

            if (AppSettings.Instance.FileList.Count > 0)
            {
              // すでに公開鍵が読み込まれている
              if (string.IsNullOrEmpty(XmlPublicKeyString) == false)
              {
                // RSA暗号化を実行する
                FileIndex = 0;
                EncryptionProcess();
                return;
              }
              // すでに秘密鍵が読み込まれている
              else if (string.IsNullOrEmpty(XmlPrivateKeyString) == false)
              {
                // RSA復号を実行する
                FileIndex = 0;
                DecryptionProcess();
                return;
              }
              else
              {
                if (fWaitingForKeyFile == true)
                {
                  panelStartPage.Visible = false;
                  panelEncrypt.Visible = false;
                  panelEncryptConfirm.Visible = false;
                  panelDecrypt.Visible = false;
                  panelRsa.Visible = false;
                  panelRsaKey.Visible = false;
                  panelProgressState.Visible = true;

                  // Error
                  labelProgressPercentText.Text = "- %";
                  progressBar.Value = 0;
                  pictureBoxProgress.Image = pictureBoxPublicAndPrivateKey.Image;
                  labelProgress.Text = Resources.labelRsa;
                  labelCryptionType.Text = Resources.labelCaptionError;
                  // 公開鍵または秘密鍵は読み込まれませんでした。
                  // The public or private key was not loaded.
                  labelProgressMessageText.Text = Resources.labelKeyFileNotLoaded;
                  notifyIcon1.Text = "- % " + Resources.labelCaptionError;
                  AppSettings.Instance.FileList = null;
                  fWaitingForKeyFile = false;
                  buttonCancel.Text = Resources.ButtonTextOK;

                  return;
                }
                else
                {
                  // ただファイルリストを保持しておく
                  // Just keep the file list.
                  fWaitingForKeyFile = true;
                  return;
                }
              }

            }

          }
          //----------------------------------------------------------------------
          // Password ZIP
          //else if (ProcessType == FILE_TYPE_PASSWORD_ZIP)
          //{
          //  panelStartPage.Visible = false;
          //  panelEncrypt.Visible = false;         
          //  panelEncryptConfirm.Visible = false;   
          //  panelDecrypt.Visible = true;            // Decrypt
          //  panelRsa.Visible = false;
          //  panelProgressState.Visible = false;

          //  pictureBoxEncryption.Image = pictureBoxRsaOn.Image;

          //  this.Activate();                     // MainForm is Activated
          //  textBoxPassword.Focus();             // Text box is focused
          //}
          //----------------------------------------------------------------------
          else
          {
            panelStartPage.Visible = true;       // Main Window
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = false;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;
          }

        }

      }

      //----------------------------------------------------------------------
      // コマンドラインオプションからのパスワードが優先される
      // The password of command line option is still more priority.
      if (AppSettings.Instance.EncryptPasswordStringFromCommandLine != null ||
          AppSettings.Instance.DecryptPasswordStringFromCommandLine != null)
      {
        if (panelEncrypt.Visible == true)
        {
          // Password
          textBoxPassword.Text = AppSettings.Instance.EncryptPasswordStringFromCommandLine;
          textBoxRePassword.Text = AppSettings.Instance.EncryptPasswordStringFromCommandLine;

          // コマンドラインオプションからのパスワード：
          // The password of command line option:
          labelPassword.Text = Resources.labelPasswordOfCommandLineOption;
          labelInputPasswordAgain.Text = Resources.labelPasswordOfCommandLineOption;

          panelEncrypt.Visible = false;
          panelEncryptConfirm.Visible = true;

          buttonEncryptStart.Focus();

          // 確認せず即座に実行
          // Run immediately without confirming
          if (AppSettings.Instance.fMemPasswordExe == true)
          {
            buttonEncryptStart.PerformClick();
          }
        }
        else if (panelDecrypt.Visible == true)
        {
          // Password
          textBoxDecryptPassword.Text = AppSettings.Instance.DecryptPasswordStringFromCommandLine;

          // コマンドラインオプションからのパスワード：
          // The password of command line option:
          labelDecryptionPassword.Text = Resources.labelPasswordOfCommandLineOption;

          buttonDecryptStart.Focus();

          // 確認せず即座に実行
          // Run immediately without confirming
          if (AppSettings.Instance.fMemPasswordExe == true)
          {
            buttonDecryptStart.PerformClick();
          }
        }
      }
      //-----------------------------------
      // 記憶パスワード（パスワードファイルより優先される）
      // Memorized password is priority than the saved password file
      else if (AppSettings.Instance.fMyEncryptPasswordKeep == true || 
                AppSettings.Instance.fMyDecryptPasswordKeep == true)
      {

        if (panelEncrypt.Visible == true)
        {
          // Password
          textBoxPassword.Text = AppSettings.Instance.MyEncryptPasswordString;
          textBoxRePassword.Text = AppSettings.Instance.MyEncryptPasswordString;

          // 記憶パスワード：
          // The memorized password:
          labelPassword.Text = Resources.labelPasswordMemorized;
          labelInputPasswordAgain.Text = Resources.labelPasswordMemorized;

          panelEncrypt.Visible = false;
          panelEncryptConfirm.Visible = true;

          buttonEncryptStart.Focus();

          // 確認せず即座に実行
          // Run immediately without confirming
          if (AppSettings.Instance.fMyEncryptPasswordKeep == true && AppSettings.Instance.fMemPasswordExe == true)
          {
            buttonEncryptStart.PerformClick();
          }
        }
        else if (panelDecrypt.Visible == true)
        {
          // Password
          textBoxDecryptPassword.Text = AppSettings.Instance.MyDecryptPasswordString;

          // 記憶パスワード：
          // The memorized password:
          labelDecryptionPassword.Text = Resources.labelPasswordMemorized;

          buttonDecryptStart.Focus();

          // 確認せず即座に実行
          // Run immediately without confirming
          if (AppSettings.Instance.fMyDecryptPasswordKeep && AppSettings.Instance.fMemPasswordExe == true)
          {
            buttonDecryptStart.PerformClick();
          }
        }

      }
      //-----------------------------------
      // パスワードファイル
      // Password file
      else
      {
        //----------------------------------------------------------------------
        // Decryption
        if (AppSettings.Instance.fCheckPassFileDecrypt == true && panelDecrypt.Visible == true)
        {
          if (File.Exists(AppSettings.Instance.PassFilePath) == true)
          {
            AppSettings.Instance.MyDecryptPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.PassFilePathDecrypt);
            textBoxDecryptPassword.Text = AppSettings.BytesToHexString(AppSettings.Instance.MyDecryptPasswordBinary);

            textBoxDecryptPassword.Enabled = false;
            textBoxDecryptPassword.BackColor = SystemColors.ButtonFace;

            // パスワードファイル：
            // The Password file:
            labelPassword.Text = Resources.labelPasswordFile;
            labelInputPasswordAgain.Text = Resources.labelPasswordFile;

            panelStartPage.Visible = false;
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = true;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;

            buttonDecryptStart.Focus();

            if ( AppSettings.Instance.fPasswordFileExe == true)
            {
              buttonDecryptStart.PerformClick();
            }

          }
          else
          {
            if (AppSettings.Instance.fNoErrMsgOnPassFile == false)
            {
              // 注意
              // 動作設定で指定されたパスワードファイルが見つかりません。
              // [FilePath]
              //
              // Alert
              // Password is not found that specified in setting panel.
              // [FilePath]
              DialogResult ret = MessageBox.Show(
                new Form { TopMost = true },
                Resources.DialogMessagePasswordFileNotFound + Environment.NewLine + AppSettings.Instance.PassFilePathDecrypt,
              Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            return;
          }
        }
        //----------------------------------------------------------------------
        // Encryption
        else if (AppSettings.Instance.fCheckPassFile == true && panelEncrypt.Visible == true)
        {
          if (File.Exists(AppSettings.Instance.PassFilePath) == true)
          {
            AppSettings.Instance.MyDecryptPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.PassFilePath);
            textBoxPassword.Text = AppSettings.BytesToHexString(AppSettings.Instance.MyDecryptPasswordBinary);
            textBoxRePassword.Text = textBoxPassword.Text;

            // パスワードファイル：
            // The Password file:
            labelPassword.Text = Resources.labelPasswordFile;
            labelInputPasswordAgain.Text = Resources.labelPasswordFile;

            textBoxPassword.Enabled = false;
            textBoxPassword.BackColor = SystemColors.ButtonFace;
            textBoxRePassword.Enabled = false;
            textBoxRePassword.BackColor = SystemColors.ButtonFace;

            panelStartPage.Visible = false;
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = true;
            panelDecrypt.Visible = false;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;

            buttonEncryptStart.Focus();

            if (AppSettings.Instance.fPasswordFileExe == true)
            {
              buttonEncryptStart.PerformClick();
            }

          }
          else
          {
            if (AppSettings.Instance.fNoErrMsgOnPassFile == false)
            {
              // 注意
              // 動作設定で指定されたパスワードファイルが見つかりません。
              // [FilePath]
              //
              // Alert
              // Password is not found that specified in setting panel.
              // [FilePath]
              DialogResult ret = MessageBox.Show(
                new Form { TopMost = true },
                Resources.DialogMessagePasswordFileNotFound + Environment.NewLine + AppSettings.Instance.PassFilePath,
              Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            return;
          }
        }

      }

    }

    /// <summary>
    /// panelStartPage
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void panelStartPage_VisibleChanged(object sender, EventArgs e)
    {
      if (panelStartPage.Visible == true)
      {
        AppSettings.Instance.FileList = null; // Clear file list
        AppSettings.Instance.EncryptionFileType = FILE_TYPE_NONE;
        this.AllowDrop = true;

        //toolStripButtonEncryptSelectFiles.Enabled = true;
        //toolStripButtonEncryptSelectFolder.Enabled = true;
        //toolStripButtonDecryptSelectAtcFiles.Enabled = true;
        //toolStripButtonOption.Enabled = true;

        this.AcceptButton = null;
        this.CancelButton = buttonExit;

        // File type for encryption. 
        int FileType = 0;
        if (AppSettings.Instance.EncryptionSameFileTypeAlways > 0)
        {
          FileType = AppSettings.Instance.EncryptionSameFileTypeAlways;
        }
        else if(AppSettings.Instance.EncryptionSameFileTypeBefore > 0)
        {
          FileType = AppSettings.Instance.EncryptionSameFileTypeBefore;
        }
        else
        {
          FileType = AppSettings.Instance.EncryptionFileType;
        }
     
        pictureBoxAtc.Image = pictureBoxAtcOff.Image;
        pictureBoxExe.Image = pictureBoxExeOff.Image;
        pictureBoxRsa.Image = pictureBoxRsaOff.Image;
        pictureBoxDec.Image = pictureBoxDecOff.Image;

        // Encryption will be the same file type always.
        if (AppSettings.Instance.EncryptionSameFileTypeAlways == 0)
        {
          // No selection
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeAlways == 1)
        {
          pictureBoxAtc.Image = pictureBoxAtcOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeAlways == 2)
        {
          pictureBoxExe.Image = pictureBoxExeOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeAlways == 3)
        {
          pictureBoxRsa.Image = pictureBoxRsaOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeBefore == 0)
        {
          // No selection
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeBefore == 1)
        {
          pictureBoxAtc.Image = pictureBoxAtcOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeBefore == 2)
        {
          pictureBoxExe.Image = pictureBoxExeOn.Image;
        }
        else if (AppSettings.Instance.EncryptionSameFileTypeBefore == 3)
        {
          pictureBoxRsa.Image = pictureBoxRsaOn.Image;
        }

        // タスクバーのリセット
        /*
        if (Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.IsPlatformSupported)
        {
          // Task bar progress
          Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager taskbarInstance;
          taskbarInstance = Microsoft.WindowsAPICodePack.Taskbar.TaskbarManager.Instance;
          taskbarInstance.SetProgressState(Microsoft.WindowsAPICodePack.Taskbar.TaskbarProgressBarState.NoProgress);
        }
        */

        // label

        // パスワード：
        // Password:
        labelPassword.Text = Resources.labelPassword;
        
        // 確認のためもう一度パスワードを入力してください：
        // Input password again to confirm:
        labelInputPasswordAgain.Text = Resources.labelInputPasswordAgainToConfirm;

        // TextBoxes
        textBoxPassword.Text = "";
        textBoxPassword.Enabled = true;
        textBoxPassword.BackColor = Color.White;

        textBoxRePassword.Text = "";
        textBoxRePassword.Enabled = true;
        textBoxRePassword.BackColor = Color.White;

        textBoxDecryptPassword.Text = "";
        textBoxDecryptPassword.Enabled = true;
        textBoxDecryptPassword.BackColor = Color.White;

        // Password files
        AppSettings.Instance.TempEncryptionPassFilePath = "";
        AppSettings.Instance.TempDecryptionPassFilePath = "";

        // Clear password input limit count
        LimitOfInputPassword = -1;
        
      }
    }

    /// <summary>
    /// panelEncrypt
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void panelEncrypt_VisibleChanged(object sender, EventArgs e)
    {
      if (panelEncrypt.Visible == true)
      {
        //toolStripButtonEncryptSelectFiles.Enabled = false;
        //toolStripButtonEncryptSelectFolder.Enabled = false;
        //toolStripButtonDecryptSelectAtcFiles.Enabled = false;
        //toolStripButtonOption.Enabled = false;

        // Not mask password character
        // 「*」で隠さずパスワードを確認しながら入力する
        if (AppSettings.Instance.fNotMaskPassword == true)
        {
          checkBoxNotMaskEncryptedPassword.Checked = true;
        }
        else
        {
          checkBoxNotMaskDecryptedPassword.Checked = false;
        }
          
        // Encryption will be the same file type always.
        // 常に同じ暗号化ファイルの種類にする
        if (AppSettings.Instance.EncryptionFileType == 0 && AppSettings.Instance.EncryptionSameFileTypeAlways > 0)
        {
          AppSettings.Instance.EncryptionFileType = AppSettings.Instance.EncryptionSameFileTypeAlways;
        }
        // Save same encryption type that was used to before.
        // 前に使った暗号化ファイルの種類にする
        else if (AppSettings.Instance.EncryptionFileType == 0 && AppSettings.Instance.EncryptionSameFileTypeBefore > 0)
        {
          AppSettings.Instance.EncryptionFileType = AppSettings.Instance.EncryptionFileType;
        }

        // Select file type
        // labelPasswordValidation.Text = "";
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC)
        {
          pictureBoxEncryption.Image = pictureBoxAtcOn.Image;
          labelEncryption.Text = labelAtc.Text;
        }
        else if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          pictureBoxEncryption.Image = pictureBoxExeOn.Image;
          labelEncryption.Text = labelExe.Text;
        }
        //else if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_PASSWORD_ZIP)
        //{
        //  pictureBoxEncryption.Image = pictureBoxRsaOn.Image;
        //  labelEncryption.Text = labelZip.Text;
        //  //pictureBoxEncryption.Image = pictureBoxZipOn.Image;
        //  //labelEncryption.Text = labelZip.Text;
        //}
        else
        {
          pictureBoxEncryption.Image = pictureBoxAtcOn.Image;
          labelEncryption.Text = labelAtc.Text;
        }

        //In the case of ZIP files, it must be more than one character of the password.
        if (pictureBoxEncryption.Image == pictureBoxRsaOn.Image)
        {
          if (textBoxPassword.Text == "")
          {
            buttonEncryptionPasswordOk.Enabled = false;
          }
          else
          {
            buttonEncryptionPasswordOk.Enabled = true;
          }
        }
        else
        {
          buttonEncryptionPasswordOk.Enabled = true;
        }

        //Show the check box in main form window
        if (AppSettings.Instance.fEncryptShowDelChkBox == true)
        {
          checkBoxDeleteOriginalFileAfterEncryption.Visible = true;
          checkBoxReDeleteOriginalFileAfterEncryption.Visible = true;

          if (AppSettings.Instance.fDelOrgFile == true)
          {
            checkBoxDeleteOriginalFileAfterEncryption.Checked = true;
          }
          else
          {
            checkBoxDeleteOriginalFileAfterEncryption.Checked = false;
          }
        }
        else
        {
          checkBoxDeleteOriginalFileAfterEncryption.Visible = false;
          checkBoxReDeleteOriginalFileAfterEncryption.Visible = false;
        }

        // Allow Drag and Drop 'password file' instead of password
        // パスワードの代わりに「パスワードファイル」のドラッグ＆ドロップを許可する
        if (AppSettings.Instance.fAllowPassFile == true)
        {
          this.AllowDrop = true;
          textBoxPassword.AllowDrop = true;
        }
        else
        {
          this.AllowDrop = false;
          textBoxPassword.AllowDrop = false;
        }

        // Enable the password strength meter
        // パスワード強度メーターを表示する
        if (AppSettings.Instance.fPasswordStrengthMeter == true)
        {
          labelPasswordStrength.Visible = true;
          pictureBoxPassStrengthMeter.Visible = true;
          textBoxPassword.Width = pictureBoxPassStrengthMeter.Left - textBoxPassword.Left - 8;
          textBoxPassword_TextChanged(sender, e);
        }
        else
        {
          labelPasswordStrength.Visible = false;
          pictureBoxPassStrengthMeter.Visible = false;
          textBoxPassword.Width = 
            pictureBoxPassStrengthMeter.Left - textBoxPassword.Left + pictureBoxPassStrengthMeter.Width;
        }

        // Turn on IMEs in all text box for password entry
        // パスワード入力用のすべてのテキストボックスでIMEをオンにする
        if (AppSettings.Instance.fTurnOnIMEsTextBoxForPasswordEntry == true)
        {
          textBoxPassword.ImeMode = ImeMode.On;
          textBoxRePassword.ImeMode = ImeMode.On;
        }
        else
        {
          textBoxPassword.ImeMode = ImeMode.NoControl;
          textBoxRePassword.ImeMode = ImeMode.NoControl;
        }

        textBoxPassword_TextChanged(sender, e);

        this.AcceptButton = buttonEncryptionPasswordOk;
        this.CancelButton = buttonEncryptCancel;
        textBoxPassword.Focus();

      }
    }

    /// <summary>
    /// panelEncryptConfirm
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void panelEncryptConfirm_VisibleChanged(object sender, EventArgs e)
    {
      if (panelEncryptConfirm.Visible == true)
      {
        pictureBoxEncryptionConfirm.Image = pictureBoxEncryption.Image;
        labelEncryptionConfirm.Text = labelEncryption.Text;
        
        if (textBoxPassword.Text == textBoxRePassword.Text)
        {
          pictureBoxCheckPasswordValidation.Image = pictureBoxValidIcon.Image;
          textBoxRePassword.BackColor = Color.Honeydew;
        }
        else
        {
          pictureBoxCheckPasswordValidation.Image = null;
          textBoxRePassword.BackColor = Color.PapayaWhip;
        }

        this.AcceptButton = buttonEncryptStart;
        this.CancelButton = buttonEncryptionConfirmCancel;
        textBoxRePassword.Focus();

      }
    }

    /// <summary>
    /// 
    /// 確認パスワード入力テキストボックスのテキスト編集イベント
    /// </summary>
    private void textBoxRePassword_TextChanged(object sender, EventArgs e)
    {
      if (AppSettings.Instance.MyEncryptPasswordBinary != null)
      {
        textBoxPassword.Enabled = false;
        textBoxRePassword.Enabled = false;           
        textBoxPassword.BackColor = SystemColors.ButtonFace;
        textBoxRePassword.BackColor = SystemColors.ButtonFace;
        // すでにパスワードファイルが入力済みです：
        // Password file is entered already:
        labelInputPasswordAgain.Text = Resources.labelPasswordFileIsEnteredAlready;
      }
      else
      {
        textBoxPassword.Enabled = true;
        textBoxRePassword.Enabled = true;
        textBoxPassword.BackColor = SystemColors.Window;
        textBoxRePassword.BackColor = SystemColors.Window;
        // 確認のためもう一度パスワードを入力してください：
        // Input password again to confirm:
        labelInputPasswordAgain.Text = Resources.labelInputPasswordAgainToConfirm;

        if (textBoxPassword.Text == textBoxRePassword.Text)
        {
          pictureBoxCheckPasswordValidation.Image = pictureBoxValidIcon.Image;
          textBoxRePassword.BackColor = Color.Honeydew;
        }
        else
        {
          pictureBoxCheckPasswordValidation.Image = pictureBoxInValidIcon.Image;
          textBoxRePassword.BackColor = Color.PapayaWhip;
        }
      }
    }

    private void textBoxRePassword_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
      {
        e.Handled = true;
        buttonEncryptStart_Click(sender, e);
      }

    }

    /// <summary>
    /// panelDecrtpt
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void panelDecrtpt_VisibleChanged(object sender, EventArgs e)
    {
      if (panelDecrypt.Visible == true)
      {
        //toolStripButtonEncryptSelectFiles.Enabled = false;
        //toolStripButtonEncryptSelectFolder.Enabled = false;
        //toolStripButtonDecryptSelectAtcFiles.Enabled = false;
        //toolStripButtonOption.Enabled = false;

        this.AcceptButton = buttonDecryptStart;
        this.CancelButton = buttonDecryptCancel;

        switch (AppSettings.Instance.DetectFileType())
        {
          case PROCESS_TYPE_DECRYPTION:
            break;

          case PROCESS_TYPE_PASSWORD_ZIP:
            // 注意
            // 現状、パスワード付きZIPファイルの復号には対応していません。
            //
            // Alert
            // Now does not correspond to the decryption of password-protected ZIP file.
            DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageNotZipDecrypted,
            Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            //スタートウィンドウ表示
            panelStartPage.Visible = true;
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = false;
            panelDecrypt.Visible = false;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;

            panelStartPage_VisibleChanged(sender, e);

            return;

          default:  // Unexpected
            // 注意
            // 想定外のファイルです。復号することができません。
            //
            // Alert
            // Unexpected decrypted files. It stopped the process.
            ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageUnexpectedDecryptedFiles,
            Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
        }

        // Not mask password character
        AppSettings.Instance.fNotMaskPassword = checkBoxNotMaskDecryptedPassword.Checked ? true : false;

        //Show the check box in main form window
        if (AppSettings.Instance.fDecryptShowDelChkBox == true)
        {
          checkBoxDeleteAtcFileAfterDecryption.Visible = true;
        }
        else
        {
          checkBoxDeleteAtcFileAfterDecryption.Visible = false;
        }

        textBoxDecryptPassword.Focus();

        //Allow Drag and Drop file instead of password
        if (AppSettings.Instance.fAllowPassFile == true)
        {
          textBoxDecryptPassword.AllowDrop = true;
        }
        else
        {
          textBoxDecryptPassword.AllowDrop = false;
        }

        // Turn on IMEs in all text box for password entry
        if (AppSettings.Instance.fTurnOnIMEsTextBoxForPasswordEntry == true)
        {
          textBoxDecryptPassword.ImeMode = ImeMode.On;
        }
        else
        {
          textBoxDecryptPassword.ImeMode = ImeMode.NoControl;
        }

        this.AcceptButton = buttonDecryptStart;
        this.CancelButton = buttonDecryptCancel;
        textBoxRePassword.Focus();

      }
    }

    private void panelProgressState_VisibleChanged(object sender, EventArgs e)
    {
      if (panelProgressState.Visible == true)
      {
        //toolStripButtonEncryptSelectFiles.Enabled = false;
        //toolStripButtonEncryptSelectFolder.Enabled = false;
        //toolStripButtonDecryptSelectAtcFiles.Enabled = false;
        //toolStripButtonOption.Enabled = false;

        this.CancelButton = buttonCancel;

        //labelCryptionType.Text = "";
        labelProgressMessageText.Text = "-";
        labelProgressPercentText.Text = "- %";
        buttonCancel.Text = Resources.ButtonTextCancel;  // Cancel button

        this.CancelButton = buttonCancel;

      }
    }
    #endregion

    //======================================================================
    // Encrypt window ( panelEncrypt )
    //======================================================================
    #region Encrypt Window
    /// <summary>
    /// 
    /// 暗号化ウィンドウでのポップアップメニューから各ファイルタイプの選択
    /// </summary>
    private void pictureBoxEncryption_Click(object sender, EventArgs e)
    {
      Point p = pictureBoxEncryption.PointToScreen(pictureBoxEncryption.ClientRectangle.Location);
      this.contextMenuStrip2.Show(p);
    }

    private void ToolStripMenuItemAtcFile_Click(object sender, EventArgs e)
    {
      // Encrypt to ATC file
      AppSettings.Instance.EncryptionFileType = FILE_TYPE_ATC;
      pictureBoxEncryption.Image = pictureBoxAtcOn.Image;
      labelEncryption.Text = labelAtc.Text;
      textBoxPassword.Focus();
      buttonEncryptionPasswordOk.Enabled = true;
    }

    private void ToolStripMenuItemExeFile_Click(object sender, EventArgs e)
    {
      // Encrypt to EXE(ATC) file
      AppSettings.Instance.EncryptionFileType = FILE_TYPE_ATC_EXE;
      pictureBoxEncryption.Image = pictureBoxExeOn.Image;
      labelEncryption.Text = labelExe.Text;
      textBoxPassword.Focus();
      buttonEncryptionPasswordOk.Enabled = true;
    }

    private void ToolStripMenuItemRsa_Click(object sender, EventArgs e)
    {
      // Encrypt to RSA
      AppSettings.Instance.EncryptionFileType = FILE_TYPE_NONE;

      // キーファイル待ち
      // Waiting for specifying key file
      fWaitingForKeyFile = true;

      // ファイルまたはフォルダーが読み込まれました。暗号化するための公開鍵をここへドラッグ＆ドロップしてください。
      // The file or folder has been loaded.Drag and drop the public key to be encrypted here.
      labelRsaMessage.Text = Resources.labelRsaFilesloaded;
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = true;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;
      panelStartPage.Visible = false;

    }

    private void textBoxPassword_TextChanged(object sender, EventArgs e)
    {
      if (AppSettings.Instance.MyEncryptPasswordBinary != null)
      {
        textBoxPassword.Enabled = false;
        textBoxRePassword.Enabled = false;
        textBoxPassword.BackColor = SystemColors.ButtonFace;
        textBoxRePassword.BackColor = SystemColors.ButtonFace;
        return;
      }

      // In the case of ZIP files, it must be more than one character of the password.
      if (pictureBoxEncryption.Image == pictureBoxRsaOn.Image)
      {
        if (textBoxPassword.Text == "")
        {
          buttonEncryptionPasswordOk.Enabled = false;
        }
        else
        {
          buttonEncryptionPasswordOk.Enabled = true;
        }
      }
      else
      {
        buttonEncryptionPasswordOk.Enabled = true;
      }

      // Processing while a memorized password is input.
      if (AppSettings.Instance.fMyEncryptPasswordKeep == true)
      {
        if (textBoxPassword.Text != AppSettings.Instance.MyEncryptPasswordString)
        {
          // "記憶パスワードを破棄して新しいパスワードを入力する："
          // "Discard memorized password and input new password:"
          labelPassword.Text = Resources.labelPasswordInputNewPassword;
        }
        else
        {
          // "記憶パスワード："
          // "The memorized password:"
          labelPassword.Text = Resources.labelPasswordMemorized;
        }
      }

      textBoxPassword.Enabled = true;
      textBoxRePassword.Enabled = true;

      if (textBoxPassword.Text == textBoxRePassword.Text)
      {
        pictureBoxCheckPasswordValidation.Image = pictureBoxValidIcon.Image;
        textBoxRePassword.BackColor = Color.Honeydew;
      }
      else
      {
        textBoxRePassword.BackColor = Color.PapayaWhip;
      }

      // Password Strength meter ( zxcvbn )
      if (pictureBoxPassStrengthMeter.Visible == true)
      {
        var result = Zxcvbn.Zxcvbn.MatchPassword(textBoxPassword.Text);

        switch (result.Score)
        {
          case 0:
            if (textBoxPassword.Text == "")
            {
              pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrengthEmpty.Image;
              labelPasswordStrength.Text = Resources.zxcvbnLabelEmpty;
            }
            else
            {
              pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrengthEmpty.Image;
              labelPasswordStrength.Text = Resources.zxcvbnLabel00;
            }
            break;

          case 1:
            pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrength01.Image;
            labelPasswordStrength.Text = Resources.zxcvbnLabel01;
            break;

          case 2:
            pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrength02.Image;
            labelPasswordStrength.Text = Resources.zxcvbnLabel02;
            break;

          case 3:
            pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrength03.Image;
            labelPasswordStrength.Text = Resources.zxcvbnLabel03;
            break;

          case 4:
            pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrength04.Image;
            labelPasswordStrength.Text = Resources.zxcvbnLabel04;
            break;

          default:
            pictureBoxPassStrengthMeter.Image = pictureBoxPasswordStrengthEmpty.Image;
            labelPasswordStrength.Text = Resources.zxcvbnLabel00;
            break;
        }

        toolTipZxcvbnWarning.ToolTipTitle = Resources.zxcvbnToolTipTitleWarning;
        switch (result.warning)
        {
          case Zxcvbn.Warning.StraightRow:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningStraightRow);
            break;
          case Zxcvbn.Warning.ShortKeyboardPatterns:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningShortKeyboardPatterns);
            break;
          case Zxcvbn.Warning.RepeatsLikeAaaEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningRepeatsLikeAaaEasy);
            break;
          case Zxcvbn.Warning.RepeatsLikeAbcSlighterHarder:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningRepeatsLikeAbcSlighterHarder);
            break;
          case Zxcvbn.Warning.SequenceAbcEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningSequenceAbcEasy);
            break;
          case Zxcvbn.Warning.RecentYearsEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningRecentYearsEasy);
            break;
          case Zxcvbn.Warning.DatesEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningDatesEasy);
            break;
          case Zxcvbn.Warning.Top10Passwords:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningTop10Passwords);
            break;
          case Zxcvbn.Warning.CommonPasswords:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningCommonPasswords);
            break;
          case Zxcvbn.Warning.SimilarCommonPasswords:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningSimilarCommonPasswords);
            break;
          case Zxcvbn.Warning.WordEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningWordEasy);
            break;
          case Zxcvbn.Warning.NameSurnamesEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningNameSurnamesEasy);
            break;
          case Zxcvbn.Warning.CommonNameSurnamesEasy:
            toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningCommonNameSurnamesEasy);
            break;
          case Zxcvbn.Warning.Empty:
            if (textBoxPassword.Text == "")
            {
              toolTipZxcvbnWarning.SetToolTip(labelPasswordStrength, Resources.zxcvbnWarningEmpty);
            }
            else
            {
              toolTipZxcvbnWarning.ToolTipTitle = "";
            }
            break;
          default:
            toolTipZxcvbnWarning.ToolTipTitle = "";
            break;
        }

        if (toolTipZxcvbnWarning.ToolTipTitle == "")
        {
          toolTipZxcvbnWarning.Active = false;
        }
        else
        {
          toolTipZxcvbnWarning.Active = true;
        }

        toolTipZxcvbnSuggestions.ToolTipTitle = Resources.zxcvbnToolTipTitleSuggestions;
        String SuggestionsList = "";
        for (int i = 0; i < result.suggestions.Count; i++)
        {
          String SuggestionText = "";
          switch (result.suggestions[i])
          {
            case Zxcvbn.Suggestion.AddAnotherWordOrTwo:
              SuggestionText = Resources.zxcvbnSuggestionAddAnotherWordOrTwo;
              break;
            case Zxcvbn.Suggestion.UseLongerKeyboardPattern:
              SuggestionText = Resources.zxcvbnSuggestionUseLongerKeyboardPattern;
              break;
            case Zxcvbn.Suggestion.AvoidRepeatedWordsAndChars:
              SuggestionText = Resources.zxcvbnSuggestionAvoidRepeatedWordsAndChars;
              break;
            case Zxcvbn.Suggestion.AvoidSequences:
              SuggestionText = Resources.zxcvbnSuggestionAvoidSequences;
              break;
            case Zxcvbn.Suggestion.AvoidYearsAssociatedYou:
              SuggestionText = Resources.zxcvbnSuggestionAvoidYearsAssociatedYou;
              break;
            case Zxcvbn.Suggestion.AvoidDatesYearsAssociatedYou:
              SuggestionText = Resources.zxcvbnSuggestionAvoidDatesYearsAssociatedYou;
              break;
            case Zxcvbn.Suggestion.CapsDontHelp:
              SuggestionText = Resources.zxcvbnSuggestionCapsDontHelp;
              break;
            case Zxcvbn.Suggestion.AllCapsEasy:
              SuggestionText = Resources.zxcvbnSuggestionAllCapsEasy;
              break;
            case Zxcvbn.Suggestion.ReversedWordEasy:
              SuggestionText = Resources.zxcvbnSuggestionReversedWordEasy;
              break;
            case Zxcvbn.Suggestion.PredictableSubstitutionsEasy:
              SuggestionText = Resources.zxcvbnSuggestionPredictableSubstitutionsEasy;
              break;
            case Zxcvbn.Suggestion.Empty:
              if (textBoxPassword.Text == "")
              {
                SuggestionText = Resources.zxcvbnSuggestionEmpty;
              }
              break;
            case Zxcvbn.Suggestion.Default:
              SuggestionText = Resources.zxcvbnSuggestionDefault;
              break;
            default:
              break;
          }
          if (SuggestionText != "")
          {
            SuggestionsList = SuggestionsList + "- " + SuggestionText + "\r\n";
          }
        }

        if (SuggestionsList == "")
        {
          toolTipZxcvbnSuggestions.SetToolTip(pictureBoxPassStrengthMeter, "");
          toolTipZxcvbnSuggestions.Active = false;
        }
        else
        {
          toolTipZxcvbnSuggestions.SetToolTip(pictureBoxPassStrengthMeter, SuggestionsList);
          toolTipZxcvbnSuggestions.Active = true;
        }
      }

    }

    private void textBoxPassword_DragEnter(object sender, DragEventArgs e)
    {
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
      {
        e.Effect = DragDropEffects.Copy;
        textBoxPassword.BackColor = Color.Honeydew;
      }
      else
      {
        e.Effect = DragDropEffects.None;
      }
    }

    private void textBoxPassword_DragDrop(object sender, DragEventArgs e)
    {
      string[] FilePaths = (string[])e.Data.GetData(DataFormats.FileDrop, false);

      if ( File.Exists(FilePaths[0]) == true)
      {
        AppSettings.Instance.TempEncryptionPassFilePath = FilePaths[0];
        AppSettings.Instance.MyEncryptPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.TempEncryptionPassFilePath);
        textBoxPassword.Text = AppSettings.BytesToHexString(AppSettings.Instance.MyEncryptPasswordBinary);
        textBoxRePassword.Text = textBoxPassword.Text;

        panelStartPage.Visible = false;
        panelEncrypt.Visible = false;
        panelEncryptConfirm.Visible = true;      // EncryptConfirm
        panelDecrypt.Visible = false;
        panelRsa.Visible = false;
        panelRsaKey.Visible = false;
        panelProgressState.Visible = false;
      }
      else
      {
        // 注意
        // パスワードファイルにフォルダーを使うことはできません。
        //
        // Alert
        // Not use the folder to the password file.
        DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageNotDirectoryInPasswordFile,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
      }
      
    }

    private void textBoxPassword_DragLeave(object sender, EventArgs e)
    {
      textBoxPassword.BackColor = SystemColors.Window;
    }

    private void textBoxPassword_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
      {
        e.Handled = true;
        buttonEncryptionPasswordOk_Click(sender, e);
      }
    }

    private void buttonEncryptionPasswordOk_Click(object sender, EventArgs e)
    {
      //-----------------------------------
      // Display enryption confirm window
      //-----------------------------------
      panelStartPage.Visible = false;
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = true;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;

    }

    private void buttonEncryptionConfirmCancel_Click(object sender, EventArgs e)
    {
      //-----------------------------------
      // Back and display enryption confirm window
      //-----------------------------------
      panelStartPage.Visible = false;
      panelEncrypt.Visible = true;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;

    }

    ///// <summary>
    /////  "Not &mask password character" checkbox click event
    ///// 「パスワードをマスクしない」チェックボックスのクリックイベント
    ///// </summary>
    private void checkBoxNotMaskEncryptedPassword_CheckedChanged(object sender, EventArgs e)
    {
      if (checkBoxNotMaskEncryptedPassword.Checked == true)
      {
        checkBoxReNotMaskEncryptedPassword.Checked = true;
        textBoxPassword.UseSystemPasswordChar = false;
        textBoxRePassword.UseSystemPasswordChar = false;
        AppSettings.Instance.fNotMaskPassword = true;
      }
      else
      {
        checkBoxReNotMaskEncryptedPassword.Checked = false;
        textBoxPassword.UseSystemPasswordChar = true;
        textBoxRePassword.UseSystemPasswordChar = true;
        AppSettings.Instance.fNotMaskPassword = false;
      }
    }

    private void checkBoxReNotMaskEncryptedPassword_CheckedChanged(object sender, EventArgs e)
    {
      if (checkBoxReNotMaskEncryptedPassword.Checked == true)
      {
        checkBoxNotMaskEncryptedPassword.Checked = true;
        textBoxPassword.UseSystemPasswordChar = false;
        textBoxRePassword.UseSystemPasswordChar = false;
        AppSettings.Instance.fNotMaskPassword = true;
      }
      else
      {
        checkBoxNotMaskEncryptedPassword.Checked = false;
        textBoxPassword.UseSystemPasswordChar = true;
        textBoxRePassword.UseSystemPasswordChar = true;
        AppSettings.Instance.fNotMaskPassword = false;
      }
    }

    /// <summary>
    /// "&Delete original files or directories after encryption" checkbox click event
    /// 「暗号化完了後に元ファイルを削除する(&D)」クリックイベント
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkBoxDeleteOriginalFileAfterEncryption_CheckedChanged(object sender, EventArgs e)
    {
      if (checkBoxDeleteOriginalFileAfterEncryption.Checked == true)
      {
        checkBoxReDeleteOriginalFileAfterEncryption.Checked = true;
      }
      else
      {
        checkBoxReDeleteOriginalFileAfterEncryption.Checked = false;
      }

    }

    private void checkBoxReDeleteOriginalFileAfterEncryption_CheckedChanged(object sender, EventArgs e)
    {
      if (checkBoxReDeleteOriginalFileAfterEncryption.Checked == true)
      {
        checkBoxDeleteOriginalFileAfterEncryption.Checked = true;
      }
      else
      {
        checkBoxDeleteOriginalFileAfterEncryption.Checked = false;
      }

    }

    private void pictureBoxEncryptConfirmBackButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxEncryptConfirmBackButton.Image = pictureBoxBackButtonOn.Image;
    }

    private void pictureBoxEncryptConfirmBackButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxEncryptConfirmBackButton.Image = pictureBoxBackButtonOff.Image;
    }

    //======================================================================
    /// <summary>
    /// 「panelEncrypt」- 暗号化「実行」のボタンが押されたイベント
    ///  Encryption "Execute" button pressed event
    /// </summary>
    //======================================================================
    private void buttonEncryptStart_Click(object sender, EventArgs e)
    {
      //Not mask password character
      AppSettings.Instance.fNotMaskPassword = checkBoxNotMaskEncryptedPassword.Checked ? true : false;

      //-----------------------------------
      // Password in TextBox
      //-----------------------------------
      if (textBoxPassword.Text != textBoxRePassword.Text)
      {
        // Invalid mkark
        // pictureBoxCheckPasswordValidation.Image = pictureBoxInValidIcon.Image;
        // labelPasswordValidation.Text = Resources.labelCaptionPasswordInvalid;
        // 注意
        // ２つのパスワードが一致しません。入力し直してください。
        //
        // Alert
        // Two Passwords do not match, it is invalid.
        // Input them again.
        DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessagePasswordsNotMatch,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        if (ret == DialogResult.OK)
        {
          textBoxPassword.Focus();
          textBoxPassword.SelectAll();
        }

        return;
      }

      // 個別に暗号化する場合は、入力されたパスを展開する処理を入れる。
      if (AppSettings.Instance.fFilesOneByOne == true)
      {
        List<string> TempFileList = new List<string>();
        foreach (string TheFileList in AppSettings.Instance.FileList)
        {
          if (Directory.Exists(TheFileList) == true)
          {
            IEnumerable<string> FileLists = GetFileList("*", TheFileList);
            foreach (string f in FileLists)
            {
              TempFileList.Add(f);
            }
          }
          else
          {
            TempFileList.Add(TheFileList);
          }
        }

        AppSettings.Instance.FileList = TempFileList;

      }

      FileIndex = 0;
      EncryptionProcess();

    }

    //======================================================================
    /// <summary>
    /// 暗号化までの前処理
    /// Preprocessing up to encryption
    /// </summary>
    private void EncryptionProcess()
    {
      //-----------------------------------
      // Display progress window
      //-----------------------------------
      panelStartPage.Visible = false;
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = true;
      labelCryptionType.Text = Resources.labelProcessNameEncrypt;
      pictureBoxProgress.Image = pictureBoxAtcOn.Image;
      labelProgress.Text = labelAtc.Text;

      if (FileIndex > AppSettings.Instance.FileList.Count - 1)
      {
        labelProgressPercentText.Text = "100%";
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = progressBar.Maximum;
        labelCryptionType.Text = "";
        labelProgressMessageText.Text = Resources.labelCaptionCompleted;  // "Completed"
        notifyIcon1.Text = "100% " + Resources.labelCaptionCompleted;

        buttonCancel.Text = Resources.ButtonTextOK;
        return;
      }
      
      //-----------------------------------
      // Directory to oputput encrypted files
      //-----------------------------------
      //string OutDirPath = Path.GetDirectoryName(AppSettings.Instance.FileList[0]);  // default
      string OutDirPath = "";
      if (AppSettings.Instance.fSaveToSameFldr == true)
      {
        OutDirPath = AppSettings.Instance.SaveToSameFldrPath;
      }
      else
      {
        string FullPath = Path.GetFullPath(AppSettings.Instance.FileList[FileIndex]);
        if (Directory.Exists(Path.GetDirectoryName(FullPath)) == true)
        {
          OutDirPath = Path.GetDirectoryName(FullPath);
        }
      }

      if (Directory.Exists(OutDirPath) == false)
      {
        // 注意
        // 保存先のフォルダーが見つかりません！　処理を中止します。
        //
        // Alert
        // The folder to save is not found! Process is aborted.
        DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageDirectoryNotFount + Environment.NewLine + OutDirPath,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        labelProgressPercentText.Text = "- %";
        progressBar.Value = 0;
        labelCryptionType.Text = Resources.labelCaptionAborted;
        notifyIcon1.Text = "- % " + Resources.labelCaptionAborted;
        AppSettings.Instance.FileList = null;
        this.Update();
        return;
      }

      //-----------------------------------
      // Encrypted files camouflage with extension
      //-----------------------------------
      string Extension = "";
      if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC || AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE)
      {
        Extension = AppSettings.Instance.fAddCamoExt == true ? AppSettings.Instance.CamoExt : ".atc";
      }
      else if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
      {
        Extension = AppSettings.Instance.fAddCamoExt == true ? AppSettings.Instance.CamoExt : ".exe";
      }
      //else if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_PASSWORD_ZIP)
      //{
      //  Extension = AppSettings.Instance.fAddCamoExt == true ? AppSettings.Instance.CamoExt : ".zip";
      //}

      //-----------------------------------
      // Encryption password
      //-----------------------------------
      string EncryptionPassword = textBoxRePassword.Text;

      //-----------------------------------
      // Password file
      //-----------------------------------
      // ※パスワードファイルは、記憶パスワードや通常の入力されたパスワードよりも優先される。
      // * This password files is priority than memorized encryption password and inputting normal password string.
      byte[] EncryptionPasswordBinary = null;

      if (AppSettings.Instance.fAllowPassFile == true && 
          (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
           AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC || // ATC(EXE) only
           AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE))
      {
        // Check specified password file for Decryption
        if (AppSettings.Instance.fCheckPassFile == true)
        {
          if (File.Exists(AppSettings.Instance.PassFilePath) == true)
          {
            EncryptionPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.PassFilePath);
          }
          else
          {
            if (AppSettings.Instance.fNoErrMsgOnPassFile == false)
            {
              // エラー
              // 暗号化時に指定されたパスワードファイルが見つかりません。
              //
              // Error
              // The specified password file is not found in encryption.
              DialogResult ret = MessageBox.Show(
                new Form { TopMost = true },
                Resources.DialogMessageEncryptionPasswordFileNotFound + Environment.NewLine + AppSettings.Instance.PassFilePath,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            labelProgressPercentText.Text = "- %";
            progressBar.Value = 0;
            labelCryptionType.Text = Resources.labelCaptionError;
            notifyIcon1.Text = "- % " + Resources.labelCaptionError;
            AppSettings.Instance.FileList = null;
            this.Update();
            return;
          }
        }

        // Drag & Drop Password file
        if (File.Exists(AppSettings.Instance.TempEncryptionPassFilePath) == true)
        {
          EncryptionPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.TempEncryptionPassFilePath);
        }
      }

      //-----------------------------------
      // Always minimize when running
      //-----------------------------------
      if (AppSettings.Instance.fMainWindowMinimize == true)
      {
        this.WindowState = FormWindowState.Minimized;
      }

      //-----------------------------------
      // Minimizing a window without displaying in the taskbar
      //-----------------------------------
      if (AppSettings.Instance.fTaskBarHide == true)
      {
        this.Hide();
      }

      //-----------------------------------
      // Display in the task tray
      //-----------------------------------
      if (AppSettings.Instance.fTaskTrayIcon == true)
      {
        notifyIcon1.Visible = true;
      }
      else
      {
        notifyIcon1.Visible = false;
      }

      //-----------------------------------
      // Save same encryption type that was used to before.
      //-----------------------------------
      AppSettings.Instance.EncryptionSameFileTypeBefore = AppSettings.Instance.EncryptionFileType;

      //-----------------------------------
      // Get directory path to output
      //-----------------------------------
      if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE || AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC)
      {
        // Save to the same directory?
        if (AppSettings.Instance.fSaveToSameFldr == true)
        {
          if (Directory.Exists(AppSettings.Instance.SaveToSameFldrPath) == true)
          {
            OutDirPath = AppSettings.Instance.SaveToSameFldrPath;
          }
        }

      }
      else if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
      {
        pictureBoxProgress.Image = pictureBoxExeOn.Image;
        labelProgress.Text = labelExe.Text;

        // Save to the same directory?
        if (AppSettings.Instance.fSaveToSameFldr == true)
        {
          if (Directory.Exists(AppSettings.Instance.SaveToSameFldrPath) == true)
          {
            OutDirPath = AppSettings.Instance.SaveToSameFldrPath;
          }
        }
      }

      string AtcFilePath = "";

      //----------------------------------------------------------------------
      // Create one encrypted file from files
      //----------------------------------------------------------------------
      if (AppSettings.Instance.fAllFilePack == true)
      {
        // 複数ファイルは一つの暗号化ファイルにまとめる
        // The multiple files are gotten one of the encrypted file together.
        //
        // 複数のファイルやフォルダーを処理すると、１つの暗号化ファイルにまとめます。 
        // このオプションを選択している場合、新しいファイル名を指定します。
        // The multiple files is gotten one of the encrypted file together. 
        // If this option is selected, you specify a new file name.
        if (AppSettings.Instance.fAutoName == true)
        {
          if (AppSettings.Instance.fSaveToSameFldr == true && Directory.Exists(AppSettings.Instance.SaveToSameFldrPath) == true)
          {
            AtcFilePath = Path.Combine(AppSettings.Instance.SaveToSameFldrPath,
                            Path.GetFileNameWithoutExtension(AppSettings.Instance.FileList[0])) + Extension;
          }
          else
          {
            AtcFilePath = Path.Combine(Path.GetDirectoryName(AppSettings.Instance.FileList[0]),
                            Path.GetFileNameWithoutExtension(AppSettings.Instance.FileList[0])) + Extension;
          }
        }
        else
        {
          if (AppSettings.Instance.fSaveToSameFldr == true && Directory.Exists(AppSettings.Instance.SaveToSameFldrPath) == true)
          {
            saveFileDialog1.FileName = Path.GetFileName(AppSettings.Instance.FileList[0]);
            saveFileDialog1.InitialDirectory = AppSettings.Instance.SaveToSameFldrPath;
          }
          else
          {
            saveFileDialog1.FileName = Path.GetFileName(AppSettings.Instance.FileList[0]);
            saveFileDialog1.InitialDirectory = AppSettings.Instance.InitDirPath;
          }

          // Input encrypted file name for putting together
          // 一つにまとめる暗号化ファイル名入力
          saveFileDialog1.Title = Resources.DialogTitleAllPackFiles;
          saveFileDialog1.OverwritePrompt = false;

          if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
          {
            saveFileDialog1.Filter = Resources.SaveDialogFilterSelfExeFiles;
          }
          else
          {
            saveFileDialog1.Filter = Resources.SaveDialogFilterAtcFiles;
          }

          if (saveFileDialog1.ShowDialog() == DialogResult.OK)
          {
            AtcFilePath = saveFileDialog1.FileName;
            AppSettings.Instance.InitDirPath = Path.GetDirectoryName(saveFileDialog1.FileName);
          }
          else
          { //キャンセル(Cancel)
            panelStartPage.Visible = false;
            panelEncrypt.Visible = false;
            panelEncryptConfirm.Visible = true;
            panelDecrypt.Visible = false;
            panelRsa.Visible = false;
            panelRsaKey.Visible = false;
            panelProgressState.Visible = false;
            return;
          }

        }

        int NumberOfFiles = 0;

        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4 = new FileEncrypt4();
          encryption4.NumberOfFiles = NumberOfFiles + 1;
          encryption4.TotalNumberOfFiles = NumberOfFiles;
        }

        //-----------------------------------
        //Set number of times to input password in encrypt files
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          char input_limit;
          if (0 < AppSettings.Instance.MissTypeLimitsNum && AppSettings.Instance.MissTypeLimitsNum < 11)
          {
            input_limit = (char)AppSettings.Instance.MissTypeLimitsNum;
          }
          else
          {
            input_limit = (char)3;
          }
          encryption4.MissTypeLimits = input_limit;
        }

        //-----------------------------------
        // Save encryption files to same folder.
        if (AppSettings.Instance.fSaveToSameFldr == false)
        {
          OutDirPath = Path.GetDirectoryName(AtcFilePath);
        }

        //-----------------------------------
        //Create encrypted file including extension
        string FileName = Path.GetFileName(AtcFilePath);
        if (AppSettings.Instance.fExtInAtcFileName == true)
        {
          FileName = Path.GetFileName(AtcFilePath) + Extension;
        }
        else
        {
          FileName = Path.GetFileNameWithoutExtension(AtcFilePath) + Extension;
        }
        AtcFilePath = Path.Combine(OutDirPath, FileName);

        //-----------------------------------
        // Specify the format of the encryption file name
        if (AppSettings.Instance.fAutoName == true)
        {
          FileName = AppSettings.Instance.getSpecifyFileNameFormat(
            AppSettings.Instance.AutoNameFormatText, AtcFilePath
          );
        }
        AtcFilePath = Path.Combine(OutDirPath, FileName);

        //-----------------------------------
        //Confirm &overwriting when same file name exists.
        if (encryption4 != null && AppSettings.Instance.fEncryptConfirmOverwrite == true)
        {
          if (File.Exists(AtcFilePath) == true)
          {
            // Show dialog for confirming to orverwrite

            if (TempOverWriteOption == SKIP_ALL) 
            {
              FileIndex = AppSettings.Instance.FileList.Count;
              EncryptionProcess();
              return;
            }
            else if (TempOverWriteOption == OVERWRITE_ALL)
            {
              // Overwrite ( Create )
            }
            else
            {
              // 問い合わせ
              // 以下のファイルはすでに存在しています。上書きして保存しますか？
              //
              // Question
              // The following file already exists. Do you overwrite the files to save?
              using (Form4 frm4 = new Form4("ComfirmToOverwriteAtc",
                Resources.labelComfirmToOverwriteFile + Environment.NewLine + AtcFilePath))
              {
                frm4.ShowDialog();

                if (frm4.OverWriteOption == USER_CANCELED)
                {
                  panelStartPage.Visible = false;
                  panelEncrypt.Visible = false;
                  panelEncryptConfirm.Visible = false;
                  panelDecrypt.Visible = false;
                  panelRsa.Visible = false;
                  panelRsaKey.Visible = false;
                  panelProgressState.Visible = true;

                  // Canceled
                  labelProgressPercentText.Text = "- %";
                  progressBar.Value = 0;
                  labelCryptionType.Text = "";
                  notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
                  AppSettings.Instance.FileList = null;

                  buttonCancel.Text = Resources.ButtonTextOK;

                  // Atc file is deleted
                  if (File.Exists(encryption4.AtcFilePath) == true)
                  {
                    FileSystem.DeleteFile(encryption4.AtcFilePath);
                  }

                  FileIndex = -1;

                  // 暗号化の処理はキャンセルされました。
                  // Encryption was canceled.
                  labelProgressMessageText.Text = Resources.labelEncryptionCanceled;
                  labelProgressPercentText.Text = "- %";
                  progressBar.Value = 0;
                  labelCryptionType.Text = Resources.labelCaptionCanceled;
                  notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
                  AppSettings.Instance.FileList = null;
                  this.Update();
                  return;
                }
                else
                {
                  TempOverWriteOption = frm4.OverWriteOption;
                  if (frm4.OverWriteOption == SKIP)
                  {
                    FileIndex++;
                    EncryptionProcess();
                    return;
                  }
                }
              }

            }

          }

        }

        //-----------------------------------
        // Self executable file
        //-----------------------------------
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4.fExecutable = true;
        }

        //-----------------------------------
        // RSA lock file string ( public key string )
        //-----------------------------------
        if (string.IsNullOrEmpty(XmlPublicKeyString) == false)
        {
          encryption4.RsaPublicKeyXmlString = XmlPublicKeyString;
        }

        //-----------------------------------
        //　Set the timestamp of encryption file to original files or directories
        //-----------------------------------
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4.fKeepTimeStamp = AppSettings.Instance.fKeepTimeStamp;
        }

        //-----------------------------------
        // View the Cancel button
        //-----------------------------------
        buttonCancel.Text = Resources.ButtonTextCancel;

        //-----------------------------------
        // Compression Level
        //-----------------------------------
        System.IO.Compression.CompressionLevel compressionLevel;
        switch (AppSettings.Instance.CompressionLevel)
        {
          case 1: // Fastest
            compressionLevel = System.IO.Compression.CompressionLevel.Fastest;
            break;
          case 0: // No compression
            compressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            break;
          case 2: // Optional
          default:
            compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            break;
        }

       //-----------------------------------
       // Encryption start
       //-----------------------------------

       // BackgroundWorker event handler
       bkg = new BackgroundWorker();

        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          bkg.DoWork += (s, d) =>
          encryption4.Encrypt(
            s, d,
            AppSettings.Instance.FileList.ToArray(),
            AtcFilePath,
            EncryptionPassword, EncryptionPasswordBinary,
            Path.GetFileNameWithoutExtension(AtcFilePath),
            compressionLevel);
        }

        FileIndex = AppSettings.Instance.FileList.Count;

        bkg.RunWorkerCompleted += backgroundWorker_Encryption_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();


      }// end if (AppSettings.Instance.EncryptionFileType == TYPE_ATC_ENCRYPT && AllPackFilePath != "");
      //----------------------------------------------------------------------
      // Encrypt or decrypt files in directory one by one
      //----------------------------------------------------------------------
      else if (AppSettings.Instance.fFilesOneByOne == true)
      {
        // フォルダ内のファイルは個別に暗号化する
        // Files in the folder are encrypted one by one.
        // 
        // フォルダーが処理された場合、サブフォルダー以下にあるファイルすべてを個別に暗号化します。
        // ただし、その中に既に暗号化ファイルか、ZIPファイルが含まれる場合は、それらを無視します。
        // If the folder has been processed, all the files in the subfolders are encrypted one by one. 
        // However, if encrypted files or ZIP files were existed already in it, they are ignored.

        int TotalNumberOfFiles = AppSettings.Instance.FileList.Count();

        // A first file of ArrayList
        string FilePath = AppSettings.Instance.FileList[FileIndex];

        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4 = new FileEncrypt4();
          encryption4.NumberOfFiles = FileIndex + 1;
          encryption4.TotalNumberOfFiles = TotalNumberOfFiles;
        }

        //-----------------------------------
        //Set number of times to input password in encrypt files
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          char input_limit;
          if (0 < AppSettings.Instance.MissTypeLimitsNum && AppSettings.Instance.MissTypeLimitsNum < 11)
          {
            input_limit = (char)AppSettings.Instance.MissTypeLimitsNum;
          }
          else
          {
            input_limit = (char)3;
          }
          encryption4.MissTypeLimits = input_limit;
        }

        //-----------------------------------
        // Save encryption files to same folder.
        if (AppSettings.Instance.fSaveToSameFldr == false)
        {
          OutDirPath = Path.GetDirectoryName(FilePath);
        }

        //-----------------------------------
        //Create encrypted file including extension
        string FileName = Path.GetFileName(FilePath);
        if (AppSettings.Instance.fExtInAtcFileName == true)
        {
          FileName = Path.GetFileName(FilePath) + Extension;
        }
        else
        {
          FileName = Path.GetFileNameWithoutExtension(FilePath) + Extension;
        }
        AtcFilePath = Path.Combine(OutDirPath, FileName);

        //-----------------------------------
        // Specify the format of the encryption file name
        if (AppSettings.Instance.fAutoName == true)
        {
          FileName = AppSettings.Instance.getSpecifyFileNameFormat(
            AppSettings.Instance.AutoNameFormatText, AtcFilePath
          );
        }
        AtcFilePath = Path.Combine(OutDirPath, FileName);

        //-----------------------------------
        //Confirm &overwriting when same file name exists.
        if (encryption4 != null && AppSettings.Instance.fEncryptConfirmOverwrite == true)
        {
          if (File.Exists(AtcFilePath) == true)
          {
            // Show dialog for confirming to orverwrite

            if (TempOverWriteOption == SKIP_ALL)
            {
              FileIndex = AppSettings.Instance.FileList.Count;
              EncryptionProcess();
              return;
            }
            else if (TempOverWriteOption == OVERWRITE_ALL)
            {
              // Overwrite ( Create )
            }
            else
            {
              // 問い合わせ
              // 以下のファイルはすでに存在しています。上書きして保存しますか？
              //
              // Question
              // The following file already exists. Do you overwrite the files to save?
              using (Form4 frm4 = new Form4("ComfirmToOverwriteAtc",
                Resources.labelComfirmToOverwriteFile + Environment.NewLine + AtcFilePath))
              {
                frm4.ShowDialog();

                if (frm4.OverWriteOption == USER_CANCELED)
                {
                  panelStartPage.Visible = false;
                  panelEncrypt.Visible = false;
                  panelEncryptConfirm.Visible = false;
                  panelDecrypt.Visible = false;
                  panelRsa.Visible = false;
                  panelRsaKey.Visible = false;
                  panelProgressState.Visible = true;

                  // Canceled
                  labelProgressPercentText.Text = "- %";
                  progressBar.Value = 0;
                  labelCryptionType.Text = "";
                  notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
                  AppSettings.Instance.FileList = null;

                  buttonCancel.Text = Resources.ButtonTextOK;

                  // Atc file is deleted
                  if (File.Exists(encryption4.AtcFilePath) == true)
                  {
                    FileSystem.DeleteFile(encryption4.AtcFilePath);
                  }

                  FileIndex = -1;

                  // 暗号化の処理はキャンセルされました。
                  // Encryption was canceled.
                  labelProgressMessageText.Text = Resources.labelEncryptionCanceled;
                  return;
                }
                else
                {
                  TempOverWriteOption = frm4.OverWriteOption;
                  if (frm4.OverWriteOption == SKIP)
                  {
                    FileIndex++;
                    EncryptionProcess();
                    return;
                  }
                }
              }

            }

          }

        }

        //-----------------------------------
        // Self executable file
        //-----------------------------------
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4.fExecutable = true;
        }

        //-----------------------------------
        // RSA lock file string ( public key string )
        //-----------------------------------
        if (string.IsNullOrEmpty(XmlPublicKeyString) == false)
        {
          encryption4.RsaPublicKeyXmlString = XmlPublicKeyString;
        }

        //-----------------------------------
        //　Set the timestamp of encryption file to original files or directories
        //-----------------------------------
        encryption4.fKeepTimeStamp = AppSettings.Instance.fKeepTimeStamp;

        //-----------------------------------
        // View the Cancel button
        //-----------------------------------
        buttonCancel.Text = Resources.ButtonTextCancel;

        //-----------------------------------
        // Compression Level
        //-----------------------------------
        System.IO.Compression.CompressionLevel compressionLevel;
        switch (AppSettings.Instance.CompressionLevel)
        {
          case 1: // Fastest
            compressionLevel = System.IO.Compression.CompressionLevel.Fastest;
            break;
          case 0: // No compression
            compressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            break;
          case 2: // Optional
          default:
            compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            break;
        }

        //-----------------------------------
        // Encryption start
        //-----------------------------------

        bkg = new BackgroundWorker();

        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          bkg.DoWork += (s, d) =>
          {
            encryption4.Encrypt(
              s, d,
              new string[] { AppSettings.Instance.FileList[FileIndex] },
              AtcFilePath,
              EncryptionPassword, EncryptionPasswordBinary,
              "",
              compressionLevel);
          };
        }

        bkg.RunWorkerCompleted += backgroundWorker_Encryption_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();
        

      }// end else if (AppSettings.Instance.fFilesOneByOne == true);
      
      //----------------------------------------------------------------------
      // Normal
      //----------------------------------------------------------------------
      else
      { // AppSettings.Instance.fNormal;

        // 何もしない
        // Nothing to do
        //
        // 複数のファイルやフォルダーを処理すると、それぞれを暗号化しファイルが生成されます。
        // フォルダーの場合は、サブフォルダーも含め、フォルダー単位でパックされます。
        // When a number of files and folders is processed, and each file is generated to encrypt files. 
        // In the case of folders ( including subfolders ) are packed in a folder unit.

        int TotalNumberOfFiles = AppSettings.Instance.FileList.Count();
        string FileListPath = AppSettings.Instance.FileList[FileIndex];

        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4 = new FileEncrypt4();
          encryption4.NumberOfFiles = FileIndex + 1;
          encryption4.TotalNumberOfFiles = TotalNumberOfFiles;
        }

        //-----------------------------------
        //Set number of times to input password in encrypt files
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          char input_limit;
          if (0 < AppSettings.Instance.MissTypeLimitsNum && AppSettings.Instance.MissTypeLimitsNum < 11)
          {
            input_limit = (char)AppSettings.Instance.MissTypeLimitsNum;
          }
          else
          {
            input_limit = (char)3;
          }
          encryption4.MissTypeLimits = input_limit;
        }

        //-----------------------------------
        // Save encryption files to same folder.
        if (AppSettings.Instance.fSaveToSameFldr == false)
        {
          OutDirPath = Path.GetDirectoryName(FileListPath);
        }
  
        if (FileListPath.EndsWith(":\\") == true) // For the root directory, such as C:\, etc.
        {
          // Extract the first drive letter only
          FileListPath = FileListPath.Substring(0, 1);
        }

        //-----------------------------------
        //Create encrypted file including extension
        string FileListName;
        if (AppSettings.Instance.fExtInAtcFileName == true)
        {
          FileListName = Path.GetFileName(FileListPath) + Extension;
        }
        else
        {
          FileListName = Path.GetFileNameWithoutExtension(FileListPath) + Extension;
        }

        AtcFilePath = Path.Combine(OutDirPath, FileListName);

        //-----------------------------------
        // Specify the format of the encryption file name
        if (AppSettings.Instance.fAutoName == true)
        {
          FileListName = AppSettings.Instance.getSpecifyFileNameFormat(
            AppSettings.Instance.AutoNameFormatText, AtcFilePath
          );
        }

        AtcFilePath = Path.Combine(OutDirPath, FileListName);

        //-----------------------------------
        //Confirm &overwriting when same file name exists.

        if (encryption4 != null && AppSettings.Instance.fEncryptConfirmOverwrite == true)
        {

          if (File.Exists(AtcFilePath) == true)
            {
            // Show dialog for confirming to orverwrite

            if (TempOverWriteOption == SKIP_ALL)
            {
              FileIndex++;
              EncryptionProcess();
              return;
            }
            else if (TempOverWriteOption == OVERWRITE_ALL)
            {
              // Overwrite ( Create )
            }
            else
            {
              // 問い合わせ
              // 以下のファイルはすでに存在しています。上書きして保存しますか？
              //
              // Question
              // The following file already exists. Do you overwrite the files to save?
              using (Form4 frm4 = new Form4("ComfirmToOverwriteAtc",
                Resources.labelComfirmToOverwriteFile + Environment.NewLine + AtcFilePath))
              {
                frm4.ShowDialog();

                if (frm4.OverWriteOption == USER_CANCELED)
                {
                  panelStartPage.Visible = false;
                  panelEncrypt.Visible = false;
                  panelEncryptConfirm.Visible = false;
                  panelDecrypt.Visible = false;
                  panelRsa.Visible = false;
                  panelRsaKey.Visible = false;
                  panelProgressState.Visible = true;

                  // Canceled
                  labelProgressPercentText.Text = "- %";
                  progressBar.Value = 0;
                  labelCryptionType.Text = "";
                  notifyIcon1.Text = "- % " + Resources.labelCaptionCanceled;
                  AppSettings.Instance.FileList = null;

                  buttonCancel.Text = Resources.ButtonTextOK;

                  // Atc file is deleted
                  if (File.Exists(encryption4.AtcFilePath) == true)
                  {
                    FileSystem.DeleteFile(encryption4.AtcFilePath);
                  }

                  FileIndex = -1;

                  // 暗号化の処理はキャンセルされました。
                  // Encryption was canceled.
                  labelProgressMessageText.Text = Resources.labelEncryptionCanceled;
                  return;
                }
                else
                {
                  TempOverWriteOption = frm4.OverWriteOption;
                  if (frm4.OverWriteOption == SKIP)
                  {
                    FileIndex++;
                    EncryptionProcess();
                    return;
                  }
                }
              }

            }
              
          }

        }
        //----------------------------------------------------------------------
        // Self executable file
        //----------------------------------------------------------------------
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4.fExecutable = true;
        }

        //-----------------------------------
        // RSA lock file string ( public key string )
        //-----------------------------------
        if (string.IsNullOrEmpty(XmlPublicKeyString) == false)
        {
          encryption4.RsaPublicKeyXmlString = XmlPublicKeyString;
        }

        //-----------------------------------
        //　Set the timestamp of encryption file to original files or directories
        //-----------------------------------
        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          encryption4.fKeepTimeStamp = AppSettings.Instance.fKeepTimeStamp;
        }

        //-----------------------------------
        // View the Cancel button
        //-----------------------------------
        buttonCancel.Text = Resources.ButtonTextCancel;

        //-----------------------------------
        // Compression Level
        //-----------------------------------
        System.IO.Compression.CompressionLevel compressionLevel;
        switch (AppSettings.Instance.CompressionLevel)
        {
          case 0: // No compression
            compressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            break;
          case 1: // Fastest
            compressionLevel = System.IO.Compression.CompressionLevel.Fastest;
            break;
          case 2: // Optional
          default:
            compressionLevel = System.IO.Compression.CompressionLevel.Optimal;
            break;
        }

        //----------------------------------------------------------------------
        // Encrypt
        //----------------------------------------------------------------------

        // BackgroundWorker event handler
        bkg = new BackgroundWorker();

        if (AppSettings.Instance.EncryptionFileType == FILE_TYPE_NONE ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC ||
            AppSettings.Instance.EncryptionFileType == FILE_TYPE_ATC_EXE)
        {
          bkg.DoWork += (s, d) =>
          encryption4.Encrypt(
            s, d,
            new string[] { AppSettings.Instance.FileList[FileIndex] },
            AtcFilePath,
            EncryptionPassword, EncryptionPasswordBinary,
            "",
            compressionLevel);
        }

        bkg.RunWorkerCompleted += backgroundWorker_Encryption_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();

      }

    }// end EncryptionProcess();


    /// <summary>
    /// 
    /// panelEncrypt「キャンセル」ボタンのクリックイベント
    /// </summary>
    private void buttonEncryptCancel_Click(object sender, EventArgs e)
    {
      textBoxPassword.Text = "";
      textBoxRePassword.Text = "";
      AppSettings.Instance.TempEncryptionPassFilePath = "";
      //
      //スタートウィンドウへ戻る
      panelStartPage.Visible = true;
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;

      // ファイルリストをクリアする
      AppSettings.Instance.FileList = new List<string>();

      panelStartPage_VisibleChanged(sender, e);

    }

    private void pictureBoxEncryptBackButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxEncryptBackButton.Image = pictureBoxBackButtonOn.Image; 
    }

    private void pictureBoxEncryptBackButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxEncryptBackButton.Image = pictureBoxBackButtonOff.Image;
    }


    #endregion

    //======================================================================
    // Decrypt window ( panelDecrypt )
    //======================================================================
    #region Decrypt window

    /// <summary>
    /// 「パスワードマスクをしない(&M)」クリックイベント
    /// 'Not mask password character' checkbox click event.
    /// </summary>
    private void checkBoxNotMaskDecryptedPassword_CheckedChanged(object sender, EventArgs e)
    {
      if (checkBoxNotMaskDecryptedPassword.Checked == true)
      {
        textBoxDecryptPassword.PasswordChar = (char)0;
        textBoxDecryptPassword.UseSystemPasswordChar = false;
      }
      else
      {
        textBoxDecryptPassword.UseSystemPasswordChar = true;
      }
    }

    /// <summary>
    /// 「復号後に暗号化ファイルを削除する(&D)」クリックイベント
    /// 'Delete &encrypted file after decryption' checkbox click event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void checkBoxDeleteAtcFileAfterDecryption_CheckedChanged(object sender, EventArgs e)
    {
    }

    private void textBoxDecryptPassword_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
      {
        buttonDecryptStart.PerformClick();
      }
    }

    private void textBoxDecryptPassword_DragDrop(object sender, DragEventArgs e)
    {
      string[] FilePaths = (string[])e.Data.GetData(DataFormats.FileDrop, false);

      if (File.Exists(FilePaths[0]) == true)
      {
        AppSettings.Instance.TempDecryptionPassFilePath = FilePaths[0];
        AppSettings.Instance.MyDecryptPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.TempDecryptionPassFilePath);
        textBoxDecryptPassword.Text = AppSettings.BytesToHexString(AppSettings.Instance.MyDecryptPasswordBinary);
        textBoxDecryptPassword.BackColor = SystemColors.ButtonFace;
        textBoxDecryptPassword.Enabled = false;
      }
      else
      {
        // 注意
        // パスワードファイルにフォルダーを使うことはできません。
        //
        // Alert
        // Not use the folder to the password file.
        DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageNotDirectoryInPasswordFile,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
      }
    }

    private void textBoxDecryptPassword_DragEnter(object sender, DragEventArgs e)
    {
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
      {
        e.Effect = DragDropEffects.Copy;
        textBoxDecryptPassword.BackColor = Color.Honeydew;
      }
      else
      {
        e.Effect = DragDropEffects.None;
      }
    }

    private void textBoxDecryptPassword_DragLeave(object sender, EventArgs e)
    {
      textBoxDecryptPassword.BackColor = SystemColors.Window;
    }

    private void textBoxDecryptPassword_TextChanged(object sender, EventArgs e)
    {
      if (AppSettings.Instance.MyEncryptPasswordBinary != null)
      {
        textBoxDecryptPassword.Enabled = false;
        textBoxDecryptPassword.BackColor = SystemColors.ButtonFace;
        // すでにパスワードファイルが入力済みです：
        // Password file is entered already:
        labelDecryptionPassword.Text = Resources.labelPasswordFileIsEnteredAlready;
        return;
      }
      
      textBoxDecryptPassword.Enabled = true;
      textBoxDecryptPassword.BackColor = SystemColors.Window;

      // パスワード：
      // Password:
      labelDecryptionPassword.Text = Resources.labelPassword;

      if (AppSettings.Instance.fMyDecryptPasswordKeep == true)
      {
        if (textBoxDecryptPassword.Text != AppSettings.Instance.MyDecryptPasswordString)
        {
          // "記憶パスワードを破棄して新しいパスワードを入力する："
          // "Discard memorized password and input new password:"
          labelDecryptionPassword.Text = Resources.labelPasswordInputNewPassword;
        }
      }

    }

    //======================================================================
    /// <summary>
    ///  Decryption button 'Click' event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    //======================================================================
    private void buttonDecryptStart_Click(object sender, EventArgs e)
    {
      FileIndex = 0;
      OutputFileList = new List<string>();

      DecryptionProcess();
    }


    //======================================================================
    /// <summary>
    /// DecryptionProcess
    /// </summary>
    //======================================================================
    private void DecryptionProcess()
    {
      //-----------------------------------
      // Display progress window
      //-----------------------------------
      panelStartPage.Visible = false;
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = true;

      labelProgress.Text = labelDecryption.Text;
      pictureBoxProgress.Image = pictureBoxDecOn.Image;
      labelCryptionType.Text = Resources.labelProcessNameDecrypt;
      buttonCancel.Text = Resources.ButtonTextCancel;

      this.Update();

      if (FileIndex > AppSettings.Instance.FileList.Count - 1)
      {
        labelProgressPercentText.Text = "100%";
        progressBar.Style = ProgressBarStyle.Continuous;
        progressBar.Value = progressBar.Maximum;
        labelCryptionType.Text = "";
        labelProgressMessageText.Text = Resources.labelCaptionCompleted;  // "Completed"
        notifyIcon1.Text = "100% " + Resources.labelCaptionCompleted;
        buttonCancel.Text = Resources.ButtonTextOK;

        DecryptionEndProcess();

        return;
      }

      //-----------------------------------
      // Directory to oputput decrypted files
      //-----------------------------------
      string OutDirPath = "";
      if (AppSettings.Instance.fDecodeToSameFldr == true)
      {
        OutDirPath = AppSettings.Instance.DecodeToSameFldrPath;
      }

      if (Directory.Exists(OutDirPath) == false)
      {
        OutDirPath = Path.GetDirectoryName((string)AppSettings.Instance.FileList[0]);
      }

      //-----------------------------------
      // Decryption password
      //-----------------------------------
      string DecryptionPassword = textBoxDecryptPassword.Text;
      
      //-----------------------------------
      // Always minimize when running
      //-----------------------------------
      if (AppSettings.Instance.fMainWindowMinimize == true)
      {
        this.WindowState = FormWindowState.Minimized;
      }

      //-----------------------------------
      // Minimizing a window without displaying in the taskbar
      //-----------------------------------
      if (AppSettings.Instance.fTaskBarHide == true)
      {
        this.Hide();
      }

      //-----------------------------------
      // Display in the task tray
      //-----------------------------------
      if (AppSettings.Instance.fTaskTrayIcon == true)
      {
        notifyIcon1.Visible = true;
      }
      else
      {
        notifyIcon1.Visible = false;
      }
        
      //-----------------------------------
      // Preparing for decrypting
      // 
      //-----------------------------------
      string AtcFilePath = AppSettings.Instance.FileList[FileIndex];

      progressBar.Style = ProgressBarStyle.Marquee;
      progressBar.MarqueeAnimationSpeed = 50;
      // 復号するための準備をしています...
      // Getting ready for decryption...
      labelProgressMessageText.Text = Resources.labelGettingReadyForDecryption;

      decryption4 = new FileDecrypt4(AtcFilePath);

      if (decryption4.DataFileVersion < 130)
      {
        decryption2 = new FileDecrypt2(AtcFilePath);
      }
      else if (decryption4.DataFileVersion < 140)
      {
        decryption3 = new FileDecrypt3(AtcFilePath);
      }

      if (decryption4.TokenStr == "_AttacheCaseData" || decryption4.TokenStr == "_AttacheCase_Rsa")
      {
        // Encryption data ( O.K. )
      }
      else if (decryption4.TokenStr == "_Atc_Broken_Data")
      {
        // 注意
        // この暗号化ファイルは破壊されています。処理を中止します。
        //
        // Alert
        // This encrypted file is broken. The process is aborted.
        MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageAtcFileBroken + Environment.NewLine + AtcFilePath,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        labelProgressPercentText.Text = "- %";
        labelProgressMessageText.Text = Resources.labelCaptionAborted;
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Continuous;
        buttonCancel.Text = Resources.ButtonTextOK;
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;
        return;

      }
      else
      {
        // 注意
        // 暗号化ファイルではありません。処理を中止します。
        //
        // Alert
        // The file is not encrypted file. The process is aborted.
        MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageNotAtcFile + Environment.NewLine + AtcFilePath,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        labelProgressPercentText.Text = "- %";
        labelProgressMessageText.Text = Resources.labelCaptionAborted;
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Continuous;
        buttonCancel.Text = Resources.ButtonTextOK;
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;
        return;

      }

      //-----------------------------------
      // Password file
      //-----------------------------------

      // ※パスワードファイルは、記憶パスワードよりも優先される。
      // * This password files is priority than memorized encryption password.

      byte[] DecryptionPasswordBinary = null;
      if (AppSettings.Instance.fAllowPassFile == true)
      {
        // Check specified password file for Decryption
        if (AppSettings.Instance.fCheckPassFileDecrypt == true)
        {
          if (File.Exists(AppSettings.Instance.PassFilePathDecrypt) == true)
          {
            if (decryption4.DataFileVersion < 130)
            {
              DecryptionPasswordBinary = GetPasswordFileHash2(AppSettings.Instance.PassFilePathDecrypt);
            }
            else
            {
              DecryptionPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.PassFilePathDecrypt);
            }
          }
          else
          {
            if (AppSettings.Instance.fNoErrMsgOnPassFile == false)
            {
              // エラー
              // 復号時の指定されたパスワードファイルが見つかりません。処理を中止します。
              //
              // Error
              // The specified password file is not found in decryption. The process is aborted.
              DialogResult ret = MessageBox.Show(
                new Form { TopMost = true },
                Resources.DialogMessageDecryptionPasswordFileNotFound + Environment.NewLine + AppSettings.Instance.PassFilePathDecrypt,
                Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

              labelProgressPercentText.Text = "- %";
              labelProgressMessageText.Text = Resources.labelCaptionAborted;
              progressBar.Value = 0;
              progressBar.Style = ProgressBarStyle.Continuous;
              buttonCancel.Text = Resources.ButtonTextOK;
              notifyIcon1.Text = "- % " + Resources.labelCaptionError;
              return;
            }
          }
        }

        // Drag & Drop Password file
        if (File.Exists(AppSettings.Instance.TempDecryptionPassFilePath) == true)
        {
          if (decryption4.DataFileVersion < 130)
          {
            DecryptionPasswordBinary = GetPasswordFileHash2(AppSettings.Instance.TempDecryptionPassFilePath);
            DecryptionPassword = "";
          }
          else
          {
            DecryptionPasswordBinary = GetPasswordFileSha256(AppSettings.Instance.TempDecryptionPassFilePath);
            DecryptionPassword = "";
          }
        }

        // コマンドラインからのパスワードがさらに優先される
        // The password from command line option that is still more priority.
        if (AppSettings.Instance.DecryptPasswordStringFromCommandLine != null ||
            AppSettings.Instance.DecryptPasswordStringFromCommandLine != null)
        {
          DecryptionPassword = AppSettings.Instance.DecryptPasswordStringFromCommandLine;
          DecryptionPasswordBinary = null;
        }

      }

      // BackgroundWorker event handler
      bkg = new BackgroundWorker();

      //-----------------------------------
      // Old version 
      if (decryption4.DataFileVersion < 130)
      {
        decryption3 = null; // ver.3 is null
        decryption4 = null; // ver.4 is null
        decryption2 = new FileDecrypt2(AtcFilePath);
        decryption2.fNoParentFolder = AppSettings.Instance.fNoParentFldr;
        decryption2.NumberOfFiles = FileIndex + 1;
        decryption2.fSameTimeStamp = AppSettings.Instance.fSameTimeStamp;
        decryption2.TotalNumberOfFiles = AppSettings.Instance.FileList.Count;
        decryption2.TempOverWriteOption = (AppSettings.Instance.fDecryptConfirmOverwrite == false ? OVERWRITE_ALL : 0);
        if (LimitOfInputPassword == -1)
        {
          LimitOfInputPassword = decryption2.MissTypeLimits;
        }
        toolStripStatusLabelDataVersion.Text = "Data ver.2";
        this.Update();

        //======================================================================
        // Decryption start
        // 復号開始
        // http://stackoverflow.com/questions/4807152/sending-arguments-to-background-worker
        //======================================================================
        bkg.DoWork += (s, d) =>
        {
          decryption2.Decrypt(
            s, d,
            AtcFilePath, OutDirPath, DecryptionPassword, DecryptionPasswordBinary,
            DialogMessageForOverWrite, DialogMessageInvalidChar);
        };

        bkg.RunWorkerCompleted += backgroundWorker_Decryption_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();

      }
      //-----------------------------------
      // Ver.3
      else if (decryption4.DataFileVersion < 140)
      {
        decryption2 = null;
        decryption4 = null;
        decryption3.fNoParentFolder = AppSettings.Instance.fNoParentFldr;
        decryption3.NumberOfFiles = FileIndex + 1;
        decryption3.TotalNumberOfFiles = AppSettings.Instance.FileList.Count;
        decryption3.fSameTimeStamp = AppSettings.Instance.fSameTimeStamp;
        decryption3.TempOverWriteOption = (AppSettings.Instance.fDecryptConfirmOverwrite == false ? OVERWRITE_ALL : 0);
        if (LimitOfInputPassword == -1)
        {
          LimitOfInputPassword = decryption3.MissTypeLimits;
        }
        decryption3.fSalvageIgnoreHashCheck = AppSettings.Instance.fSalvageIgnoreHashCheck;
        toolStripStatusLabelDataVersion.Text = "Data ver.3";
        this.Update();

        //======================================================================
        // Decryption start
        // 復号開始
        // http://stackoverflow.com/questions/4807152/sending-arguments-to-background-worker
        //======================================================================
        bkg.DoWork += (s, d) =>
        {
          decryption3.Decrypt(
            s, d,
            AtcFilePath, OutDirPath, DecryptionPassword, DecryptionPasswordBinary,
            DialogMessageForOverWrite);
        };

        bkg.RunWorkerCompleted += backgroundWorker_Decryption_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();

      }
      //-----------------------------------
      // Ver.4 ( Current version )
      else if (decryption4.DataFileVersion < 150)
      {
        decryption2 = null;
        decryption3 = null;
        decryption4.fNoParentFolder = AppSettings.Instance.fNoParentFldr;
        decryption4.NumberOfFiles = FileIndex + 1;
        decryption4.TotalNumberOfFiles = AppSettings.Instance.FileList.Count;
        decryption4.fSameTimeStamp = AppSettings.Instance.fSameTimeStamp;
        if (string.IsNullOrEmpty(XmlPrivateKeyString) == false)
        { // RSA decryption
          decryption4.RsaPrivateKeyXmlString = XmlPrivateKeyString;
        }
        decryption4.TempOverWriteOption = (AppSettings.Instance.fDecryptConfirmOverwrite == false ? OVERWRITE_ALL : 0);
        if (LimitOfInputPassword == -1)
        {
          LimitOfInputPassword = decryption4.MissTypeLimits;
        }
        decryption4.fSalvageIgnoreHashCheck = AppSettings.Instance.fSalvageIgnoreHashCheck;
        toolStripStatusLabelDataVersion.Text = "Data ver.4";
        this.Update();

        //======================================================================
        // Decryption start
        // 復号開始
        // http://stackoverflow.com/questions/4807152/sending-arguments-to-background-worker
        //======================================================================
        bkg.DoWork += (s, d) =>
        {
          decryption4.Decrypt(
            s, d,
            AtcFilePath, OutDirPath, DecryptionPassword, DecryptionPasswordBinary,
            DialogMessageForOverWrite);
        };

        bkg.RunWorkerCompleted += backgroundWorker_Decryption_RunWorkerCompleted;
        bkg.ProgressChanged += backgroundWorker_ProgressChanged;
        bkg.WorkerReportsProgress = true;
        bkg.WorkerSupportsCancellation = true;

        bkg.RunWorkerAsync();

      }
      //-----------------------------------
      // Higher version 
      else
      {
        // 警告
        // このファイルはアタッシェケースの上位バージョンで暗号化されています。
        // 復号できません。処理を中止します。
        //
        // Alert
        // This file has been encrypted with a higher version.
        // It can not be decrypted. The process is aborted.
        MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageHigherVersion + Environment.NewLine + AtcFilePath,
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        labelProgressPercentText.Text = "- %";
        labelProgressMessageText.Text = Resources.labelCaptionAborted;
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Continuous;
        buttonCancel.Text = Resources.ButtonTextOK;
        notifyIcon1.Text = "- % " + Resources.labelCaptionError;

        return;

      }

    }
    //======================================================================
    /// <summary>
    /// DecryptionEndProcess
    /// </summary>
    private void DecryptionEndProcess()
    {
      bool fOpen = false;

      if (AppSettings.Instance.fOpenFile == true)
      {
        if (OutputFileList.Count() > AppSettings.Instance.ShowDialogWhenMultipleFilesNum)
        {
          // 問い合わせ
          // 復号したファイルが○個以上あります。
          // それでもすべてのファイルを関連付けられたアプリケーションで開きますか？
          //
          // Question
          // There decrypted file is * or more.
          // But, open all of the files associated with application?
          DialogResult ret = 
            MessageBox.Show(new Form { TopMost = true }, 
              string.Format(Resources.DialogMessageOpenMultipleFiles, 
              AppSettings.Instance.ShowDialogWhenMultipleFilesNum),
          Resources.DialogTitleQuestion, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

          if (ret == DialogResult.Yes)
          {
            fOpen = true;
          }
          else
          {
            fOpen = false;
          }
        }
        else
        {
          fOpen = true;
        }

        if (fOpen == true)
        {
          foreach (string path in OutputFileList)
          {
            if (Path.GetExtension(path).ToLower() == ".exe" || Path.GetExtension(path).ToLower() == ".bat" || Path.GetExtension(path).ToLower() == ".cmd")
            {
              if (AppSettings.Instance.fShowDialogWhenExeFile == true)
              {
                // 問い合わせ
                // 復号したファイルに実行ファイルが含まれています。以下のファイルを実行しますか？
                //
                // Question
                // It contains the executable files in the decrypted file.
                // Do you run the following file?
                DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageExecutableFile + Environment.NewLine + path,
                Resources.DialogTitleQuestion, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

                if (ret == DialogResult.No)
                {
                  continue;
                }
                else
                { // Executable
                  System.Diagnostics.Process p = System.Diagnostics.Process.Start(path);
                }
              }
            }
            else if (File.Exists(path) == true)
            {
              System.Diagnostics.Process p = System.Diagnostics.Process.Start(path);
            }
            else if (Directory.Exists(path) == true)
            {
              // Open the folder by Explorer
              System.Diagnostics.Process.Start("EXPLORER.EXE", path);
            }

          }// end foreach;

        }// end if (fOpen == true);

      }
       
      // Set the timestamp of files or directories to decryption time.
      if (AppSettings.Instance.fSameTimeStamp == true)
      {
        OutputFileList.ForEach(delegate (String FilePath)
        {
          DateTime dtNow = DateTime.Now;
          File.SetCreationTime(FilePath, dtNow);
          File.SetLastWriteTime(FilePath, dtNow);
          File.SetLastAccessTime(FilePath, dtNow);
        });
      }
        
      // Delete file or directories
      if (AppSettings.Instance.fDelEncFile == true || checkBoxDeleteAtcFileAfterDecryption.Checked == true)
      {
        if (AppSettings.Instance.fConfirmToDeleteAfterDecryption == true)
        {
          // 問い合わせ
          // 復号の元になった暗号化ファイルを削除しますか？
          //
          // Question
          // Are you sure to delete the encypted file(s) that are the source of the decryption?
          DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageDeleteEncryptedFiles,
            Resources.DialogTitleQuestion, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
          if (ret == DialogResult.Yes)
          {
            buttonCancel.Text = Resources.ButtonTextCancel;
            DeleteData(AppSettings.Instance.FileList);
          }
        }
        else
        {
          DeleteData(AppSettings.Instance.FileList);
        }
      }

      if (AppSettings.Instance.fEndToExit == true)
      {
        Application.Exit();
      }

    }

    //----------------------------------------------------------------------
    // Cancel button click event.                
    private void buttonDecryptCancel_Click(object sender, EventArgs e)
    {
      // Cancel, If decryption is not being processed 
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;
      panelStartPage.Visible = true;
      textBoxDecryptPassword.Text = "";
      AppSettings.Instance.TempDecryptionPassFilePath = "";
      // ファイルリストをクリアする
      AppSettings.Instance.FileList = new List<string>();

    }

    private void pictureBoxDecryptBackButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxDecryptBackButton.Image = pictureBoxBackButtonOn.Image;
    }

    private void pictureBoxDecryptBackButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxDecryptBackButton.Image = pictureBoxBackButtonOff.Image;
    }


    #endregion

    //======================================================================
    // Progress window ( panelProgressState )
    //======================================================================
    #region Progress

    /// <summary>
    ///  "Back" button
    ///  「戻る」ボタン
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void buttonCancel_Click(object sender, EventArgs e)
    {

      if (buttonCancel.Text == Resources.ButtonTextOK)
      {
        //スタートウィンドウ表示
        panelStartPage.Visible = true;
        panelEncrypt.Visible = false;
        panelEncryptConfirm.Visible = false;
        panelDecrypt.Visible = false;
        panelRsa.Visible = false;
        panelRsaKey.Visible = false;
        panelProgressState.Visible = false;

        panelStartPage_VisibleChanged(sender, e);

        buttonCancel.Text = Resources.ButtonTextCancel;

        // RSAページを初期値へ戻す
        // Return RSA pages to their initial values
        // ---
        // ここに、暗号化または復号したいファイル・フォルダーをドラッグ＆ドロップしてください。先に公開鍵、秘密鍵をドラッグ＆ドロップすることもできます。
        // Drag and drop the file or folder you want to encrypt or decrypt here. You can also drag and drop the public key and private key first.
        labelRsaMessage.Text = Resources.labelRsaMessage;
        // 公開鍵暗号
        // Public key Encrypt
        labelRsa.Text = Resources.labelRsa;
        pictureBoxRsaPage.Image = pictureBoxPublicAndPrivateKey.Image;
        AppSettings.Instance.TempDecryptionPassFilePath = "";
        XmlPublicKeyString = "";
        XmlPrivateKeyString = "";
        fWaitingForKeyFile = false;
        buttonGenerateKey.Visible = true;

        // ファイルリストをクリアする
        AppSettings.Instance.FileList = null;

        return;
      }
      else
      {
        //-----------------------------------
        // Canceling
        // キャンセル処理
        //-----------------------------------
        if (bkg != null && bkg.IsBusy == true)
        {
          bkg.CancelAsync();
        }

        if (cts != null)
        {
          cts.Cancel();
        }

        buttonCancel.Text = Resources.ButtonTextOK;

      }

    }

    private void pictureBoxProgressStateBackButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxProgressStateBackButton.Image = pictureBoxBackButtonOn.Image;
    }

    private void pictureBoxProgressStateBackButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxProgressStateBackButton.Image = pictureBoxBackButtonOff.Image;
    }

    private void pictureBoxProgressStateBackButton_Click(object sender, EventArgs e)
    {
      if (buttonCancel.Text == Resources.ButtonTextOK)
      {
        //スタートウィンドウ表示
        panelStartPage.Visible = true;
        panelEncrypt.Visible = false;
        panelEncryptConfirm.Visible = false;
        panelDecrypt.Visible = false;
        panelRsa.Visible = false;
        panelRsaKey.Visible = false;
        panelProgressState.Visible = false;

        panelStartPage_VisibleChanged(sender, e);

        return;
      }
      else
      {
        return;
      }
    }

    #endregion

    //======================================================================
    /// <summary>
    /// Notify Icon Mouse Click event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    //======================================================================
    private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
    {
      if (this.WindowState == FormWindowState.Minimized)
      {
        this.WindowState = FormWindowState.Normal;
      }
      if (this.Visible == false)
      {
        this.Show();
      }
      this.Activate();
    }
      
    //======================================================================
    /// <summary>
    /// ファイルを破壊して、当該内部トークンを「破壊」ステータスに書き換える
    /// Break a specified file, and rewrite the token of broken status
    /// </summary>
    /// <param name="FilePath"></param>
    /// <returns></returns>
    //======================================================================
    public bool BreakTheFile(string FilePath)
    {
      try
      {
        using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite))
        {
          byte[] byteArray = new byte[16];
          fs.Seek(4, SeekOrigin.Begin);
          if (fs.Read(byteArray, 0, 16) == 16)
          {
            string TokenStr = System.Text.Encoding.ASCII.GetString(byteArray);
            if (TokenStr == "_AttacheCaseData")
            {
              // Rewriting Token
              fs.Seek(4, SeekOrigin.Begin);
              byteArray = System.Text.Encoding.ASCII.GetBytes("_Atc_Broken_Data");
              fs.Write(byteArray, 0, 16);

              // Break IV of the file
              byteArray = new byte[32];
              RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
              rng.GetNonZeroBytes(byteArray);

              fs.Seek(32, SeekOrigin.Begin);
              fs.Write(byteArray, 0, 32);

              // 警告
              // パスワード入力制限を超えたため、暗号化ファイルは破壊されました。
              //
              // Alert
              // Because it exceeded the limit number of inputting password, the encrypted file has been broken.
              DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageBroken,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            }
            else if (TokenStr == "_Atc_Broken_Data")
            {
              // broken already

              // 警告
              // この暗号化ファイルはすでに破壊されています。
              //
              // Alert
              // The encrypted file has already been destroyed.
              DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageBrokenAlready,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

              return (true);

            }
            else
            {  // Token is not found.

              // 警告
              // 破壊トークンを見つけられませんでした。暗号化ファイルではない可能性があります。
              //
              // Alert
              // The broken token could not found. The file may not be an encrypted file.
              DialogResult ret = MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageBrokenDestroyNotFount,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

              return (false);
            }
          }
          else
          {
            return (false);
          }

        }// end using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read));

        return (true);

      }
      catch(Exception e)
      {
#if(DEBUG)
        System.Windows.Forms.MessageBox.Show(new Form { TopMost = true }, e.Message);
#endif
        return (false);
      }
    }

    //======================================================================
    /// <summary>
    /// 開発用ディベロッパーコンソールウィンドウの表示と復号ファイルのヘッダ情報の表示
    /// Display the Developer Console window for development and 
    /// display header information of decrypted file
    /// </summary>
    /// <returns></returns>
    //======================================================================
    private void showDeveloperConsoleWindowDecrypt()
    {
      if (frm5 == null || frm5.IsDisposed)
      {
        frm5 = new Form5();
      }
      frm5.Show();

      if (AppSettings.Instance.DeveloperConsolePosX < 0 || AppSettings.Instance.DeveloperConsolePosY < 0)
      {
        // 位置がマイナス値の場合（デフォルト値も含む）は、画面中央に表示する
        frm5.Left = Screen.GetBounds(this).Width / 2 - AppSettings.Instance.DeveloperConsoleWidth / 2;
        frm5.Top = Screen.GetBounds(this).Height / 2 - AppSettings.Instance.DeveloperConsoleHeight / 2;
        frm5.Width = AppSettings.Instance.DeveloperConsoleWidth;
        frm5.Height = AppSettings.Instance.DeveloperConsoleHeight;
      }
      else
      {
        frm5.Left = AppSettings.Instance.DeveloperConsolePosX;
        frm5.Top = AppSettings.Instance.DeveloperConsolePosY;
        frm5.Width = AppSettings.Instance.DeveloperConsoleWidth;
        frm5.Height = AppSettings.Instance.DeveloperConsoleHeight;
      }

      if (decryption3 != null)
      {
        // AttacheCase3 data
        Form5.Instance.textBoxAppFileVersionText = decryption3.DataFileVersion.ToString();
        Form5.Instance.textBrokenText3 = decryption3.fBroken.ToString();
        Form5.Instance.textBoxFileSignature3Text = decryption3.TokenStr;
        Form5.Instance.textBoxMissTypeLimit3Text = ((int)decryption3.MissTypeLimits).ToString();
        Form5.Instance.textBoxDataFileVersion3Text = decryption3.DataFileVersion.ToString();
        Form5.Instance.textBoxAtcHeaderSizeText = decryption3.AtcHeaderSize.ToString();
        Form5.Instance.textSaltText = BitConverter.ToString(decryption3.salt).Replace("-", string.Empty);
        Form5.Instance.textBoxRfc2898DeriveBytesText = BitConverter.ToString(decryption3.deriveBytes.GetBytes(32)).Replace("-", string.Empty);
        Form5.Instance.textBoxOutputFileListText = string.Join(", ", decryption3.FileList.ToArray());
        Form5.Instance.toolStripStatusLabelDecryptionTimeText = "Decryption Time: " + decryption3.DecryptionTimeString;
      }
      else if (decryption2 != null)
      {
        // AttacheCase2 data
        Form5.Instance.textBoxDataSubVersionText = decryption2.DataSebVersion.ToString();
        Form5.Instance.textBoxReservedText = BitConverter.ToString(decryption2.reserved);
        Form5.Instance.textBoxMissTypeLimit2Text = ((int)decryption2.MissTypeLimits).ToString();
        Form5.Instance.textBoxfBroken2Text = decryption2.fBroken.ToString();
        Form5.Instance.textBoxlFileSignature2Text = decryption2.TokenStr;
        Form5.Instance.textBoxDataFileVersion2Text = decryption2.DataFileVersion.ToString();
        Form5.Instance.textBoxTypeAlgorismText = decryption2.TypeAlgorism.ToString();
        Form5.Instance.textBoxAtcHeaderSize2Text = decryption2.AtcHeaderSize.ToString();
        Form5.Instance.textBoxOutputFileList2Text = string.Join(", ", decryption2.OutputFileList.ToArray());
        Form5.Instance.toolStripStatusLabelDecryptionTimeText = "Decryption Time: " + decryption2.DecryptionTimeString;

      }
    }
    //----------------------------------------------------------------------

    private void panelStartPage_MouseEnter(object sender, EventArgs e)
    {
      if (this.BackColor == SystemColors.Control)
      { // light theme
        panelStartPage.BackColor = Color.WhiteSmoke;
      }
      else
      { // dark theme
        panelStartPage.BackColor = Color.FromArgb(255, 40, 40, 40);
      }
    }

    private void panelStartPage_MouseLeave(object sender, EventArgs e)
    {
      if (this.BackColor == SystemColors.Control)
      { // light theme
        panelStartPage.BackColor = Color.White;
      }
      else
      { // dark theme
        panelStartPage.BackColor = Color.Black;
      }
    }
   
    private void Form1_MouseDown(object sender, MouseEventArgs e)
    {
      if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
      {
        //マウスダウンした位置を記憶する
        MouseDownPoint = new Point(e.X, e.Y);
      }
    }

    private void Form1_MouseMove(object sender, MouseEventArgs e)
    {
      if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
      {
        this.Left += e.X - MouseDownPoint.X;
        this.Top += e.Y - MouseDownPoint.Y;
      }
    }

    private void buttonExit_Click(object sender, EventArgs e)
    {
      Application.Exit();
    }

    private void Form1_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Escape)
      {
        this.Close();
      }
    }

    private void panelStartPage_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Escape)
      {
        this.Close();
      }
    }

    private void pictureBoxAtc_Click(object sender, EventArgs e)
    {
      if (pictureBoxAtc.Image == pictureBoxAtcChk.Image)
      {
        pictureBoxAtc.Image = pictureBoxAtcOn.Image;
        AppSettings.Instance.EncryptionFileType = FILE_TYPE_NONE;
        AppSettings.Instance.EncryptionSameFileTypeBefore = FILE_TYPE_NONE;
      }
      else
      {
        pictureBoxAtc.Image = pictureBoxAtcChk.Image;
        pictureBoxExe.Image = pictureBoxExeOff.Image;
        pictureBoxRsa.Image = pictureBoxRsaOff.Image;
        pictureBoxDec.Image = pictureBoxDecOff.Image;
        AppSettings.Instance.EncryptionFileType = FILE_TYPE_ATC;
        AppSettings.Instance.EncryptionSameFileTypeBefore = FILE_TYPE_ATC;
      }
    }

    private void pictureBoxAtc_MouseEnter(object sender, EventArgs e)
    {
      if (pictureBoxAtc.Image != pictureBoxAtcChk.Image)
      {
        pictureBoxAtc.Image = pictureBoxAtcOn.Image;
      }
    }

    private void pictureBoxAtc_MouseLeave(object sender, EventArgs e)
    {
      if (pictureBoxAtc.Image != pictureBoxAtcChk.Image)
      {
        pictureBoxAtc.Image = pictureBoxAtcOff.Image;
      }
    }

    private void pictureBoxExe_Click(object sender, EventArgs e)
    {
      if (pictureBoxExe.Image == pictureBoxExeChk.Image)
      {
        pictureBoxExe.Image = pictureBoxExeOn.Image;
        AppSettings.Instance.EncryptionFileType = FILE_TYPE_NONE;
        AppSettings.Instance.EncryptionSameFileTypeBefore = FILE_TYPE_NONE;
      }
      else
      {
        pictureBoxAtc.Image = pictureBoxAtcOff.Image;
        pictureBoxExe.Image = pictureBoxExeChk.Image;
        pictureBoxRsa.Image = pictureBoxRsaOff.Image;
        //pictureBoxZip.Image = pictureBoxZipOff.Image;
        pictureBoxDec.Image = pictureBoxDecOff.Image;
        AppSettings.Instance.EncryptionFileType = FILE_TYPE_ATC_EXE;
        AppSettings.Instance.EncryptionSameFileTypeBefore = FILE_TYPE_ATC_EXE;
      }
    }

    private void pictureBoxExe_MouseEnter(object sender, EventArgs e)
    {
      if (pictureBoxExe.Image != pictureBoxExeChk.Image)
      {
        pictureBoxExe.Image = pictureBoxExeOn.Image;
      }
    }

    private void pictureBoxExe_MouseLeave(object sender, EventArgs e)
    {
      if (pictureBoxExe.Image != pictureBoxExeChk.Image)
      {
        pictureBoxExe.Image = pictureBoxExeOff.Image;
      }
    }

    private void pictureBoxRsa_Click(object sender, EventArgs e)
    {
      if (pictureBoxRsa.Image == pictureBoxRsaChk.Image)
      {
        pictureBoxRsa.Image = pictureBoxRsaOn.Image;
      }
      else
      {
        pictureBoxAtc.Image = pictureBoxAtcOff.Image;
        pictureBoxExe.Image = pictureBoxExeOff.Image;
        pictureBoxDec.Image = pictureBoxDecOff.Image;
        pictureBoxRsa.Image = pictureBoxRsaChk.Image;
      }

      panelStartPage.Visible = false;
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = true;            // RSA
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;

    }

    private void pictureBoxRsa_MouseEnter(object sender, EventArgs e)
    {
      if (pictureBoxRsa.Image != pictureBoxRsaChk.Image)
      {
        pictureBoxRsa.Image = pictureBoxRsaOn.Image;
      }
    }

    private void pictureBoxRsa_MouseLeave(object sender, EventArgs e)
    {
      if (pictureBoxRsa.Image != pictureBoxRsaChk.Image)
      {
        pictureBoxRsa.Image = pictureBoxRsaOff.Image;
      }
    }

    private void pictureBoxDec_Click(object sender, EventArgs e)
    {
      AppSettings.Instance.FileList = null;
      openFileDialog1.Title = Resources.DialogTitleEncryptSelectFiles;
      openFileDialog1.Filter = Resources.SaveDialogFilterAtcFiles;
      openFileDialog1.InitialDirectory = AppSettings.Instance.InitDirPath;
      openFileDialog1.Multiselect = true;

      pictureBoxAtc.Image = pictureBoxAtcOff.Image;
      pictureBoxExe.Image = pictureBoxExeOff.Image;
      pictureBoxRsa.Image = pictureBoxRsaOff.Image;
      pictureBoxDec.Image = pictureBoxDecChk.Image;

      if (openFileDialog1.ShowDialog() == DialogResult.OK)
      {
        foreach (string filname in openFileDialog1.FileNames)
        {
          if (AppSettings.Instance.FileList == null)
          {
            AppSettings.Instance.FileList = new List<string>();
          }
          AppSettings.Instance.FileList.Add(filname);
        }

        // Check memorized password
        if (AppSettings.Instance.fMyDecryptPasswordKeep == true)
        {
          textBoxDecryptPassword.Text = AppSettings.Instance.MyDecryptPasswordString;
        }

        // Encrypt by memorized password without confirming
        if (AppSettings.Instance.fMemPasswordExe)
        {
          buttonDecryptStart.PerformClick();
        }
        else
        {
          panelStartPage.Visible = false;
          panelEncrypt.Visible = false;
          panelEncryptConfirm.Visible = false;
          panelDecrypt.Visible = true;    //Decrypt
          panelRsa.Visible = false;
          panelRsaKey.Visible = false;
          panelProgressState.Visible = false;
        }
      }
      else
      {
        pictureBoxDec.Image = pictureBoxDecOff.Image;
      }
    }

    private void pictureBoxDec_MouseEnter(object sender, EventArgs e)
    {
      if (pictureBoxDec.Image != pictureBoxDecChk.Image)
      {
        pictureBoxDec.Image = pictureBoxDecOn.Image;
      }
    }

    private void pictureBoxDec_MouseLeave(object sender, EventArgs e)
    {
      if (pictureBoxDec.Image != pictureBoxDecChk.Image)
      {
        pictureBoxDec.Image = pictureBoxDecOff.Image;
      }
    }

    private void pictureBoxOptionButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxOptionButton.Image = pictureBoxOptionOn.Image;

      if (this.BackColor == SystemColors.Control)
      { // light theme
        panelStartPage.BackColor = Color.WhiteSmoke;
      }
      else
      { // dark theme
        panelStartPage.BackColor = Color.FromArgb(255, 40, 40, 40);
      }

    }

    private void pictureBoxOptionButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxOptionButton.Image = pictureBoxOptionOff.Image;
    }

    //----------------------------------------------------------------------
    // Change theme color ( "dark" or "light" )
    // テーマカラーの変更（ダークテーマ、ライトテーマ）
    // ref. https://stackoverflow.com/questions/61145347/c-how-to-make-a-dark-mode-theme-in-windows-forms-separate-form-as-select-the
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private void ChangeTheme(Control.ControlCollection container, bool fDark)
    {
      // キャプションバーのダークモード表示
      // ref. https://stackoverflow.com/questions/57124243/winforms-dark-title-bar-on-windows-10
      if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 17763)
      {
        var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
        if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 18985)
        {
          attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
        }
        int useImmersiveDarkMode = (fDark == true ? 1 : 0);
        DwmSetWindowAttribute(this.Handle, (int)attribute, ref useImmersiveDarkMode, sizeof(int));
      }

      // This form
      this.BackColor = (fDark == true ? Color.Black : SystemColors.Control);

      // 各コントロールのダークモード表示
      foreach (Control component in container)
      {
        if (component is Panel)
        {
          ChangeTheme(component.Controls, fDark);
          component.BackColor = (fDark == true ? Color.Black : SystemColors.Control);
          component.ForeColor = (fDark == true ? Color.White : SystemColors.ControlText);
        }
        else if (component is Button)
        {
          component.BackColor = (fDark == true ? Color.Black : SystemColors.Control);
          component.ForeColor = (fDark == true ? Color.White : SystemColors.ControlText);
        }
        else if (component is TextBox)
        {
          component.BackColor = (fDark == true ? Color.Black : SystemColors.Control);
          component.ForeColor = (fDark == true ? Color.White : SystemColors.ControlText);
        }
        else if (component is ToolStrip)
        {
          component.BackColor = (fDark == true ? Color.Black : SystemColors.Control);
          component.ForeColor = (fDark == true ? Color.White : SystemColors.ControlText);
        }

        if (fDark == true)
        {
          ToolStripProfessionalRenderer renderer = new MyToolStripRenderer(Color.FromArgb(241, 241, 241), new MyDarkColorTable());
          renderer.RoundedEdges = false;
          ToolStripManager.Renderer = renderer;
          ToolStripManager.VisualStylesEnabled = true;
          // テキストボックスだけ例外
          // Only text box is an exception.
          textBoxPassword.ForeColor = Color.Black;
          textBoxRePassword.ForeColor = Color.Black;
          textBoxDecryptPassword.ForeColor = Color.Black;
        }
        else
        {
          ToolStripProfessionalRenderer renderer = new MyToolStripRenderer(SystemColors.ControlText, new MyLightColorTable());
          renderer.RoundedEdges = true;
          ToolStripManager.Renderer = renderer;
          ToolStripManager.VisualStylesEnabled = true;

        }

        // 各タブページの左側のパネル
        if (fDark == true)
        {
          panel1.BackColor = Color.FromArgb(255, 30, 30, 30);
          panel2.BackColor = Color.FromArgb(255, 30, 30, 30);
          panel3.BackColor = Color.FromArgb(255, 30, 30, 30);
          panel4.BackColor = Color.FromArgb(255, 30, 30, 30);
          panel5.BackColor = Color.FromArgb(255, 30, 30, 30);
        }
        else
        {
          panel1.BackColor = Color.WhiteSmoke;
          panel2.BackColor = Color.WhiteSmoke;
          panel3.BackColor = Color.WhiteSmoke;
          panel4.BackColor = Color.WhiteSmoke;
          panel5.BackColor = Color.WhiteSmoke;
        }

      }

    }
    //----------------------------------------------------------------------
    // ToolStripのダークテーマカラーテーブル
    // ref. https://stackoverflow.com/questions/36767478/color-change-for-menuitem
    private class MyDarkColorTable : ProfessionalColorTable
    {
      public override Color ToolStripDropDownBackground
      {
        get { return Color.FromArgb(255, 30, 30, 30); }
      }
      public override Color ImageMarginGradientBegin
      {
        get { return Color.FromArgb(255, 30, 30, 30); }
      }
      public override Color ImageMarginGradientMiddle
      {
        get { return Color.FromArgb(255, 30, 30, 30); }
      }
      public override Color ImageMarginGradientEnd
      {
        get { return Color.FromArgb(255, 30, 30, 30); }
      }
      public override Color MenuBorder
      {
        get { return Color.Black; }
      }
      public override Color MenuItemBorder
      {
        get { return Color.Black; }
      }
      public override Color MenuItemSelected
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
      public override Color MenuStripGradientBegin
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
      public override Color MenuStripGradientEnd
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
      public override Color MenuItemSelectedGradientBegin
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
      public override Color MenuItemSelectedGradientEnd
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
      public override Color MenuItemPressedGradientBegin
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
      public override Color MenuItemPressedGradientEnd
      {
        get { return Color.FromArgb(255, 60, 60, 60); }
      }
    }
    //----------------------------------------------------------------------
    // ToolStripの「ライト」テーマカラーテーブル
    private class MyLightColorTable : ProfessionalColorTable
    {
      public override Color ToolStripDropDownBackground
      {
        get { return Color.FromArgb(255, 253, 253, 253); }
      }
      public override Color ImageMarginGradientBegin
      {
        get { return Color.FromArgb(255, 248, 248, 248); }
      }
      public override Color ImageMarginGradientMiddle
      {
        get { return Color.FromArgb(255, 248, 248, 248); }
      }
      public override Color ImageMarginGradientEnd
      {
        get { return Color.FromArgb(255, 248, 248, 248); }
      }
      public override Color MenuBorder
      {
        get { return Color.FromArgb(255, 128, 128, 128); }
      }
      public override Color MenuItemBorder
      {
        get { return Color.FromArgb(255, 189, 189, 189); }
      }
      public override Color MenuItemSelected
      {
        get { return Color.FromArgb(255, 181, 215, 243); }
      }
      public override Color MenuStripGradientBegin
      {
        get { return SystemColors.Control; }
      }
      public override Color MenuStripGradientEnd
      {
        get { return SystemColors.Control; }
      }
      public override Color MenuItemSelectedGradientBegin
      {
        get { return Color.FromArgb(255, 181, 215, 243); }
      }
      public override Color MenuItemSelectedGradientEnd
      {
        get { return Color.FromArgb(255, 181, 215, 243); }
      }
      public override Color MenuItemPressedGradientBegin
      {
        get { return Color.FromArgb(255, 181, 215, 243); }
      }
      public override Color MenuItemPressedGradientEnd
      {
        get { return Color.FromArgb(255, 181, 215, 243); }
      }
    }
    //----------------------------------------------------------------------
    // ref. https://tzeditor.blogspot.com/2019/10/c_30.html
    private class MyToolStripRenderer : ToolStripProfessionalRenderer
    {
      private Color foreColor;
      /// <summary>
      /// コンストラクタ
      /// </summary>
      public MyToolStripRenderer()
          : this(null)
      {
      }
      /// <summary>
      /// コンストラクタ
      /// </summary>
      /// <param name="colorTable"></param>
      public MyToolStripRenderer(ProfessionalColorTable colorTable)
          : this(SystemColors.ControlText, colorTable)
      {
      }
      /// <summary>
      /// コンストラクタ
      /// </summary>
      /// <param name="foreColor"></param>
      /// <param name="colorTable"></param>
      public MyToolStripRenderer(Color foreColor, ProfessionalColorTable colorTable)
          : base(colorTable)
      {
        this.foreColor = foreColor;
      }
      /// <summary>
      /// メニューの色設定
      /// </summary>
      /// <param name="e"></param>
      protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
      {
        e.TextColor = this.foreColor;
        base.OnRenderItemText(e);
      }
      /// <summary>
      /// OnRenderMenuItemBackground
      /// </summary>
      /// <param name="e"></param>
      protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
      {
        e.Item.ForeColor = this.foreColor;
        base.OnRenderMenuItemBackground(e);
      }
      /// <summary>
      /// OnRenderToolStripBorder
      /// </summary>
      /// <param name="e"></param>
      protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
      {
        base.OnRenderToolStripBorder(e);

        ToolStrip toolStrip = e.ToolStrip;
        if (toolStrip is StatusStrip)
        {
          e.Graphics.DrawLine(new Pen(Color.FromArgb(45, 45, 48)), 0, 0, e.ToolStrip.Width, 0);
        }
      }
    }

    private void button3_Click(object sender, EventArgs e)
    {
      if (File.Exists(AppSettings.Instance.SaveToIniDirPath) == false)
      {
        // Default foloder is Desktop
        saveFileDialog2.InitialDirectory =
          Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
      }
      else
      {
        saveFileDialog2.InitialDirectory = AppSettings.Instance.SaveToIniDirPath;
      }

      // Save a lock file (public key) and key file (private key)
      // ロックファイル（公開鍵）とキーファイル（秘密鍵）の保存
      saveFileDialog2.Title = Resources.DialogTitleSavePublicAndPrivateKey;
      if (saveFileDialog2.ShowDialog() == DialogResult.OK)
      {
        CreateKeyPair(saveFileDialog2.FileName, "");
        DirectoryInfo diParent = Directory.GetParent(saveFileDialog2.FileName);
        AppSettings.Instance.SaveToIniDirPath = diParent.FullName;
      }

    }

    //----------------------------------------------------------------------
    // ペアの公開鍵・暗号鍵を生成する
    //----------------------------------------------------------------------
    private static string CreateKeyPair(string filePath, string guidString)
    {

      if (string.IsNullOrEmpty(guidString))
      {
        // GUIDを生成する
        var guid = Guid.NewGuid();
        guidString = guid.ToString();
      }

      DirectoryInfo diParent = Directory.GetParent(filePath);
      string DirPath = diParent.FullName;
      string FileName = Path.GetFileNameWithoutExtension(filePath);

      // 公開鍵・秘密鍵のファイルパス
      var publicKeyFilePath = Path.Combine(DirPath, FileName + ".atcpub");
      var privateFilePath = Path.Combine(DirPath, FileName + ".atcpvt");

      //-----------------------------------
      //RSACryptoServiceProviderオブジェクトの作成
      var rsa = new RSACryptoServiceProvider(2048);

      //公開鍵をXML形式で取得
      var publicKey = rsa.ToXmlString(false);
      //秘密鍵をXML形式で取得
      var privateKey = rsa.ToXmlString(true);

      //-----------------------------------
      // 公開鍵XMLファイルの編集
      var xml = XElement.Parse(publicKey);

      // アップロード日時（UTC日時）
      //XElement xmlUploadDateTime = new XElement("upload", "");
      //xml.AddFirst(xmlUploadDateTime);
      // 生成日時（UTC日時）
      //XElement xmlDateTime = new XElement("datetime", DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss"));
      //xml.AddFirst(xmlDateTime);
      // From ( メールアドレスなど )
      //XElement xmlFrom = new XElement("from", sendToString);
      //xml.AddFirst(xmlFrom);
      // ラベル（鍵管理用キーワード）
      //XElement xmlTo = new XElement("to", fromString);
      //xml.AddFirst(xmlTo);

      // 種別
      XElement xmlType = new XElement("type", "public");
      xml.AddFirst(xmlType);
      // GUID
      XElement xmlGuid = new XElement("id", guidString);
      xml.AddFirst(xmlGuid);
      // Token
      XElement xmlToken = new XElement("token", "AttacheCase");
      xml.AddFirst(xmlToken);
      // 公開鍵として保存する
      xml.Save(publicKeyFilePath);

      //-----------------------------------
      // 秘密鍵XMLファイルの編集
      xml = XElement.Parse(privateKey);

      //xml.AddFirst(xmlUploadDateTime); // アップロード日時（UTC日時）
      //xml.AddFirst(xmlDateTime);       // 作成日時（UTC日時）
      //xml.AddFirst(xmlFrom);           // 送り主情報（メールアドレスなど格納）
      //xml.AddFirst(xmlTo);             // 鍵管理用キーワード

      // 種別
      xmlType = new XElement("type", "private");
      xml.AddFirst(xmlType);
      // GUID
      xml.AddFirst(xmlGuid);
      // Token
      xml.AddFirst(xmlToken);
      // 秘密鍵として保存する
      xml.Save(privateFilePath);

      rsa.Clear();

      return guidString;

    }

    private void buttonRsaCancel_Click(object sender, EventArgs e)
    {
      // Cancel
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;
      panelStartPage.Visible = true;
      // ここに、暗号化または復号したいファイル・フォルダーをドラッグ＆ドロップしてください。先に公開鍵、秘密鍵をドラッグ＆ドロップすることもできます。
      // Drag and drop the file or folder you want to encrypt or decrypt here. You can also drag and drop the public key and private key first.
      labelRsaMessage.Text = Resources.labelRsaMessage;
      // 公開鍵暗号
      // Public key Encrypt
      labelRsa.Text = Resources.labelRsa;
      pictureBoxRsaPage.Image = pictureBoxPublicAndPrivateKey.Image;
      AppSettings.Instance.TempDecryptionPassFilePath = "";
      XmlPublicKeyString = "";
      XmlPrivateKeyString = "";
      fWaitingForKeyFile = false;
      buttonGenerateKey.Visible = true;
      // ファイルリストをクリアする
      AppSettings.Instance.FileList = new List<string>();

    }

    private void pictureBoxRsaBackButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxRsaBackButton.Image = pictureBoxBackButtonOn.Image;
    }

    private void pictureBoxRsaBackButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxRsaBackButton.Image = pictureBoxBackButtonOff.Image;
    }

    private void panelRsa_VisibleChanged(object sender, EventArgs e)
    {
      if (panelRsa.Visible == true)
      {
        if (AppSettings.Instance.FileList == null || AppSettings.Instance.FileList.Count == 0)
        {
          buttonGenerateKey.Visible = true;
        }
        else
        {
          buttonGenerateKey.Visible = false;
        }

        this.AcceptButton = buttonGenerateKey;
        this.CancelButton = buttonRsaCancel;

      }
    }

    private void buttonRsaKeyCancel_Click(object sender, EventArgs e)
    {
      // Cancel
      panelEncrypt.Visible = false;
      panelEncryptConfirm.Visible = false;
      panelDecrypt.Visible = false;
      panelRsa.Visible = false;
      panelRsaKey.Visible = false;
      panelProgressState.Visible = false;
      panelStartPage.Visible = true;
      // ここに、暗号化または復号したいファイル・フォルダーをドラッグ＆ドロップしてください。先に公開鍵、秘密鍵をドラッグ＆ドロップすることもできます。
      // Drag and drop the file or folder you want to encrypt or decrypt here. You can also drag and drop the public key and private key first.
      labelRsaMessage.Text = Resources.labelRsaMessage;
      // 公開鍵暗号
      // Public key Encrypt
      labelRsa.Text = Resources.labelRsa;
      pictureBoxRsaPage.Image = pictureBoxPublicAndPrivateKey.Image;
      AppSettings.Instance.TempDecryptionPassFilePath = "";
      XmlPublicKeyString = "";
      XmlPrivateKeyString = "";
      fWaitingForKeyFile = false;
      buttonGenerateKey.Visible = true;
      // ファイルリストをクリアする
      AppSettings.Instance.FileList = new List<string>();

      this.CancelButton = buttonRsaKeyCancel;

    }

    private void pictureBoxRsaKeyBackButton_MouseEnter(object sender, EventArgs e)
    {
      pictureBoxRsaKeyBackButton.Image = pictureBoxBackButtonOn.Image;
    }

    private void pictureBoxRsaKeyBackButton_MouseLeave(object sender, EventArgs e)
    {
      pictureBoxRsaKeyBackButton.Image = pictureBoxBackButtonOff.Image;
    }

    private void getXmlFileHash(string FilePath)
    {
 
      XmlHashStringList = new Dictionary<string, string>();

      // GUID
      XElement xmlElement = XElement.Load(FilePath);
      XmlHashStringList.Add("GUID", xmlElement.Element("id").Value);

      using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        // MD5
        var md5 = new MD5CryptoServiceProvider();
        byte[] bs = md5.ComputeHash(fs);
        md5.Clear();
        XmlHashStringList.Add("MD5", BitConverter.ToString(bs).ToLower().Replace("-", ""));
      }

      using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        // SHA-1
        var sha1 = new SHA1CryptoServiceProvider();
        byte[] bs = sha1.ComputeHash(fs);
        sha1.Clear();
        XmlHashStringList.Add("SHA-1", BitConverter.ToString(bs).ToLower().Replace("-", ""));
      }

      using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        // SHA-256
        var sha256 = new SHA256CryptoServiceProvider();
        byte[] bs = sha256.ComputeHash(fs);
        sha256.Clear();
        XmlHashStringList.Add("SHA-256", BitConverter.ToString(bs).ToLower().Replace("-", ""));
      }

    }

    private void comboBoxHashList_SelectedIndexChanged(object sender, EventArgs e)
    {
      // GUID
      // MD5
      // SHA-1
      // SHA-256
      string selectedItem = comboBoxHashList.SelectedItem.ToString();
      textBoxHashString.Text = XmlHashStringList[selectedItem];
    }


  }// end public partial class Form1 : Form;

}// end namespace AttacheCase;
