﻿//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttacheCase4 )" -- File encryption software.
// Copyright (C) 2016-2025  Mitsuhiro Hibara
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
using System.ComponentModel;
using System.Windows.Forms;
using AttacheCase;
using System.IO;
using System.Security.Cryptography;
using System.Drawing;
using System.Collections;
using System.Collections.ObjectModel;
using Exeout.Properties;
using System.Text;

namespace Exeout
{
  public partial class Form1 : Form
  {
    // Status code
    //private const int ENCRYPT_SUCCEEDED   = 1; // Encrypt is succeeded.
    private const int DECRYPT_SUCCEEDED = 2; // Decrypt is succeeded.
    //private const int DELETE_SUCCEEDED    = 3; // Delete is succeeded.
    private const int HEADER_DATA_READING = 4; // Header data is reading.
    //private const int ENCRYPTING          = 5; // Ecrypting.
    private const int DECRYPTING = 6; // Decrypting.
                                      //private const int DELETING            = 7; // Deleting.

    // Error Code
    private const int USER_CANCELED = -1;
    private const int ERROR_UNEXPECTED = -100;
    private const int NOT_ATC_DATA = -101;
    private const int ATC_BROKEN_DATA = -102;
    private const int NO_DISK_SPACE = -103;
    private const int FILE_INDEX_NOT_FOUND = -104;
    private const int PASSWORD_TOKEN_NOT_FOUND = -105;
    private const int NOT_CORRECT_HASH_VALUE = -106;
    private const int INVALID_FILE_PATH = -107;
    private const int OS_DENIES_ACCESS = -108;
    private const int DATA_NOT_FOUND = -109;
    private const int DIRECTORY_NOT_FOUND = -110;
    private const int DRIVE_NOT_FOUND = -111;
    private const int FILE_NOT_LOADED = -112;
    private const int FILE_NOT_FOUND = -113;
    private const int PATH_TOO_LONG = -114;
    private const int CRYPTOGRAPHIC_EXCEPTION = -115;
    private const int RSA_KEY_GUID_NOT_MATCH = -116;
    private const int IO_EXCEPTION = -117;

    public static BackgroundWorker bkg;
    public int LimitOfInputPassword = -1;
    private FileDecrypt4 decryption = null;
    string TempDecryptionPassFilePath = "";

    public Form1()
    {
      InitializeComponent();
      this.Text = Path.GetFileName(Application.ExecutablePath);
    }

    private void Form1_Load(object sender, EventArgs e)
    {
      // Copyright info
      toolStripStatusLabel1.Text = ApplicationInfo.CopyrightHolder;
    }

    private void Form1_Shown(object sender, EventArgs e)
    {
      textBox1.Focus();
      textBox1.SelectAll();
    }

    private void checkBoxNotMaskPassword_CheckedChanged(object sender, EventArgs e)
    {
      if (checkBoxNotMaskPassword.Checked == true)
      {
        textBox1.PasswordChar = (char)0;
        textBox1.UseSystemPasswordChar = false;
      }
      else
      {
        textBox1.UseSystemPasswordChar = true;
      }
    }

    private void textBox1_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter)
      {
        buttonDecrypt_Click(sender, e);
      }
    }

    private void buttonExit_Click(object sender, EventArgs e)
    {
      Application.Exit();
    }

    private void toolStripStatusLabel1_Click(object sender, EventArgs e)
    {
      // Show dialog for confirming to orverwrite
      Form2 frm2 = new Form2();
      frm2.ShowDialog();
      frm2.Dispose();
    }

    private void textBox1_DragDrop(object sender, DragEventArgs e)
    {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

      if (File.Exists(files[0]) == true)
      {
        TempDecryptionPassFilePath = files[0];
        buttonDecrypt.PerformClick(); // Decryption start.
      }
    }

    private void textBox1_DragEnter(object sender, DragEventArgs e)
    {
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
      {
        e.Effect = DragDropEffects.Copy;
        textBox1.BackColor = Color.Honeydew;
      }
      else
      {
        e.Effect = DragDropEffects.None;
      }
    }

    private void textBox1_DragLeave(object sender, EventArgs e)
    {
      textBox1.BackColor = SystemColors.Window;
    }

    /// <summary>
    /// Decryption start.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void buttonDecrypt_Click(object sender, EventArgs e)
    {
      buttonDecrypt.Enabled = false;
      checkBoxNotMaskPassword.Visible = false;
      progressBar1.Location = textBox1.Location;
      progressBar1.Width = textBox1.Width;
      progressBar1.Visible = true;
      labelPercent.Text = "- %";

      //-----------------------------------
      // Directory to oputput decrypted files
      //-----------------------------------
      string OutDirPath = Path.GetDirectoryName(Application.ExecutablePath);

      //-----------------------------------
      // Decryption password
      //-----------------------------------
      string DecryptionPassword = textBox1.Text;

      //-----------------------------------
      // Password file
      //-----------------------------------

      // Drag & Drop Password file
      byte[] DecryptionPasswordBinary = null;
      if (File.Exists(TempDecryptionPassFilePath) == true)
      {
        DecryptionPasswordBinary = GetSha256FromFile(TempDecryptionPassFilePath);
      }

      //-----------------------------------
      // Preparing for decrypting
      // 
      //-----------------------------------
      decryption = new FileDecrypt4(Application.ExecutablePath);

      if (decryption.TokenStr == "_AttacheCaseData")
      {
        // Encryption data ( O.K. )
      }
      else if (decryption.TokenStr == "_Atc_Broken_Data")
      {
        //
        // エラー
        // この暗号化ファイルは壊れています。処理を中止します。
        //
        // Alert
        // This encrypted file is broken. The process is aborted.
        //
        MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageAtcFileBroken,
          Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        labelMessage.Text = Resources.labelCaptionAborted;
        //labelMessage.Text = "Process of decryption has been aborted.";

        return;
      }
      else
      {
        // 
        // エラー
        // 暗号化ファイルではありません。処理を中止します。
        //
        // Alert
        // The file is not encrypted file. The process is aborted.
        // 
        MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageNotAtcFile,
          Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

        labelMessage.Text = Resources.labelCaptionAborted;
        //labelMessage.Text = "Process of decryption has been aborted.";
        labelPercent.Text = "- %";

        return;
      }

      if (LimitOfInputPassword == -1)
      {
        LimitOfInputPassword = decryption.MissTypeLimits;
      }

#if (DEBUG)
      System.Windows.Forms.MessageBox.Show("BackgroundWorker event handler.");
#endif
      //======================================================================
      // BackgroundWorker event handler
      bkg = new BackgroundWorker();
      bkg.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
      bkg.ProgressChanged += backgroundWorker_ProgressChanged;
      bkg.WorkerReportsProgress = true;

#if (DEBUG)
      System.Windows.Forms.MessageBox.Show("Decryption start.");
#endif
      //======================================================================
      // Decryption start
      // 復号開始
      // Refer：http://stackoverflow.com/questions/4807152/sending-arguments-to-background-worker
      //======================================================================
      bkg.DoWork += (s, d) =>
      {
        decryption.Decrypt(
          s, d,
          Application.ExecutablePath, OutDirPath, DecryptionPassword, DecryptionPasswordBinary,
          DialogMessageForOverWrite);
      };

      bkg.RunWorkerAsync();

    }

    //======================================================================
    // Backgroundworker
    //======================================================================
    #region

    private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
      if (e.ProgressPercentage > 0)
      {
        ArrayList MessageList = (ArrayList)e.UserState;

        progressBar1.Style = ProgressBarStyle.Continuous;
        progressBar1.Value = e.ProgressPercentage / 100;
        labelPercent.Text = ((float)e.ProgressPercentage / 100).ToString("F") + "%";

        // ((int)MessageList[0] == DECRYPTING )
        labelMessage.Text = (string)MessageList[1];
        this.Update();
      }
      else
      {
        progressBar1.Style = ProgressBarStyle.Marquee;
        progressBar1.Value = 0;
        // 復号するための準備をしています...
        // Getting ready for decryption...
        labelMessage.Text = Resources.labelGettingReadyForDecryption;
        labelPercent.Text = "- %";
      }

    }

    private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {

#if (DEBUG)
      System.Windows.Forms.MessageBox.Show("backgroundWorker_RunWorkerCompleted");
#endif

      if (e.Cancelled)
      {
        // Canceled
        labelPercent.Text = "- %";
        progressBar1.Value = 0;
        labelMessage.Text = Resources.labelCaptionCanceled;
        return;

      }
      else if (e.Error != null)
      {
        //e.Error.Message;
        // "エラー： " 
        // "Error occurred: "
        labelMessage.Text = Resources.labelCaptionError + e.Error.Message;
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
        switch (decryption.ReturnCode)
        {
          //-----------------------------------
          case DECRYPT_SUCCEEDED:
            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.Value = progressBar1.Maximum;
            labelPercent.Text = "100%";
            labelMessage.Text = Resources.labelCaptionCompleted;

            this.Update();

            decryption = null;
            return;

          //-----------------------------------
          case USER_CANCELED:
            // Canceled
            labelPercent.Text = "- %";
            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.Value = 0;
            // キャンセルされました。
            // Canceled.
            labelMessage.Text = Resources.labelCaptionCanceled;

            this.Update();

            decryption = null;

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
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case NOT_ATC_DATA:
            // エラー
            // 暗号化ファイルではありません。処理を中止します。
            //
            // Error
            // The file is not encrypted file. The process is aborted.
            MessageBox.Show(new Form { TopMost = true },
              Resources.DialogMessageNotAtcFile + Environment.NewLine + decryption.ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case ATC_BROKEN_DATA:
            // エラー
            // 暗号化ファイル(.atc)は壊れています。処理を中止します。
            //
            // Error
            // Encrypted file ( atc ) is broken. The process is aborted.
            MessageBox.Show(new Form { TopMost = true },
              Resources.DialogMessageAtcFileBroken + Environment.NewLine + decryption.ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case NO_DISK_SPACE:
            // 警告
            // ドライブに空き容量がありません。処理を中止します。
            //
            // Alert
            // No free space on the disk. The process is aborted.
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageNoDiskSpace,
              Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case FILE_INDEX_NOT_FOUND:
            // エラー
            // 暗号化ファイル内部で、不正なファイルインデックスがありました。
            //
            // Error
            // Internal file index is invalid in encrypted file.
            MessageBox.Show(Resources.DialogMessageFileIndexInvalid,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case NOT_CORRECT_HASH_VALUE:
            // エラー
            // ファイルのハッシュ値が異なります。ファイルが壊れたか、改ざんされた可能性があります。
            // 処理を中止します。
            //
            // Error
            // The file is not the same hash value. Whether the file is corrupted, it may have been made the falsification.
            // The process is aborted.
            MessageBox.Show(new Form { TopMost = true },
              Resources.DialogMessageNotSameHash + Environment.NewLine + decryption.ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            decryption = null;
            return;

          //-----------------------------------
          case INVALID_FILE_PATH:
            // エラー
            // ファイル、またはフォルダーパスが不正です。処理を中止します。
            //
            // Error
            // The path of files or folders are invalid. The process is aborted.
            MessageBox.Show(new Form { TopMost = true },
              Resources.DialogMessageInvalidFilePath + Environment.NewLine + decryption.ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          case RSA_KEY_GUID_NOT_MATCH:
            // エラー
            // 暗号化されたペアの秘密鍵ではありません。復号できませんでした。
            // Error
            // This is not the private key of the encrypted pair. Could not decrypt.
            MessageBox.Show(new Form { TopMost = true },
              Resources.DialogMessageInvalidPrivateKey + Environment.NewLine + decryption.ErrorFilePath,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

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
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case ERROR_UNEXPECTED:
            // エラー
            // 予期せぬエラーが発生しました。処理を中止します。
            //
            // Error
            // An unexpected error has occurred. And stops processing.
            MessageBox.Show(new Form { TopMost = true }, Resources.DialogMessageUnexpectedError,
              Resources.DialogTitleError, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            labelMessage.Text = Resources.labelCaptionAborted;
            labelPercent.Text = "- %";
            decryption = null;
            return;

          //-----------------------------------
          case PASSWORD_TOKEN_NOT_FOUND:

          default:
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
              progressBar1.Visible = false;
              textBox1.Focus();
              textBox1.SelectAll();
              buttonDecrypt.Enabled = true;
              // "パスワードを入力してください：";
              // "Input Password:";
              labelMessage.Text = Resources.labelInputPassword;
            }
            else
            {
              // パスワード回数を超過
              // Exceed times limit of inputting password
              if (decryption.fBroken == true)
              {
                BreakTheFile(Application.ExecutablePath);
              }
              else
              {
                Application.Exit();
              }

            }
            decryption = null;
            return;

        }// end switch();

      }

    }

    #endregion

    //----------------------------------------------------------------------
    /// <summary>
    /// 上書きの確認をするダイアログ表示とユーザー応答内容の受け渡し
    /// Show dialog for confirming to overwrite, and passing user command. 
    /// </summary>
    //----------------------------------------------------------------------
    System.Threading.ManualResetEvent _busy = new System.Threading.ManualResetEvent(false);
    private void DialogMessageForOverWrite(int FileType, string FilePath)
    {
      if (decryption.TempOverWriteOption == 2)
      { // Overwrite all
        return;
      }

      if (!bkg.IsBusy)
      {
        bkg.RunWorkerAsync();
        // Unblock the worker 
        _busy.Set();
      }

      string DialogMessageText;
      if (File.Exists(FilePath))
      {
        // file name
        DialogMessageText = Resources.labelComfirmToOverwriteFile;
      }
      else
      {
        // dirctory name
        DialogMessageText = Resources.labelComfirmToOverwriteDir;
      }

      // 問い合わせ
      // 以下のファイル（フォルダー）はすでに存在しています。上書きして保存しますか？
      // 
      // Question
      // The following file(folder) already exists. Do you overwrite the files to save?
      //
      if (MessageBox.Show(DialogMessageText + "\n" + FilePath,
            Resources.DialogTitleQuestion, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
      {
        decryption.TempOverWriteOption = 2; //Overwrite all
      }

      _busy.Reset();

    }

    /// <summary>
    /// Update to decrypt progress display.
    /// 復号処理の進捗状況を更新する
    /// </summary>
    /// <param name="size"></param>
    /// <param name="TotalSize"></param>
    private void UpdateDecryptProgress(Int64 TotalSize, Int64 TotalFileSize, int StatusCode, string MessageText)
    {
      float percent = (float)TotalSize / TotalFileSize;
      bkg.ReportProgress((int)(percent * 10000), MessageText);
    }

    /// 計算してチェックサム（SHA-256）を得る
    /// Get a check sum (SHA-256) to calculate
    private static byte[] GetSha256FromFile(string FilePath)
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

    //----------------------------------------------------------------------
    /// <summary>
    /// ファイルを破壊して、当該内部トークンを「破壊」ステータスに書き換える
    /// Break a specified file, and rewrite the token of broken status
    /// </summary>
    /// <param name="FilePath"></param>
    /// <returns></returns>
    //----------------------------------------------------------------------
    public bool BreakTheFile(string FilePath)
    {
      using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite))
      {
        byte[] byteArray = new byte[16];
        if (fs.Read(byteArray, 4, 16) == 16)
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
          }
          else if (TokenStr == "_Atc_Broken_Data")
          {
            // broken already
            return (true);
          }
          else
          { // Token is not found.
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

  }


}
