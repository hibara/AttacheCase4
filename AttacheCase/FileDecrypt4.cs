//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttacheCase4 )" -- File encryption software.
// Copyright (C) 2016-2025  Mitsuhiro Hibara
//
// * Required .NET Framework 4.6 or later
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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
#if __MACOS__
using AppKit;
#endif

namespace AttacheCase
{
  internal class FileDecrypt4
  {
    private struct FileData
    {
      public string FilePath;
      public Int64 FileSize;
      public int FileAttribute;
      public DateTime LastWriteDateTime;
      public DateTime CreationDateTime;
      public byte[] Hash;
    }

    // Status code
    private const int ENCRYPT_SUCCEEDED = 1; // Encrypt is succeeded.
    private const int DECRYPT_SUCCEEDED = 2; // Decrypt is succeeded.
    private const int DELETE_SUCCEEDED = 3; // Delete is succeeded.
    private const int READY_FOR_ENCRYPT = 4; // Getting ready for encryption or decryption.
    private const int READY_FOR_DECRYPT = 5; // Getting ready for encryption or decryption.
    private const int ENCRYPTING = 6; // Encrypting.
    private const int DECRYPTING = 7; // Decrypting.
    private const int DELETING = 8; // Deleting.

    // Error code
    private const int USER_CANCELED = -1;   // User cancel.
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

    // Overwrite Option
    //private const int USER_CANCELED = -1;
    private const int OVERWRITE = 1;
    private const int OVERWRITE_ALL = 2;
    private const int KEEP_NEWER = 3;
    private const int KEEP_NEWER_ALL = 4;
    // ---
    // Skip Option
    private const int SKIP = 5;
    private const int SKIP_ALL = 6;
    // MD5 hash mismatch
    private const int CONTINUE = 7;

    private readonly byte[] GuidData = new byte[16];
    private byte[] RsaPassword = new byte[32];
    private readonly byte[] RsaEncryptedPassword = new byte[256];

    private const int BUFFER_SIZE = 4096;

    // Header data variables
    private const string STRING_TOKEN_NORMAL = "_AttacheCaseData";
    private const string STRING_TOKEN_BROKEN = "_Atc_Broken_Data";
    private const string STRING_TOKEN_RSA = "_AttacheCase_Rsa";
    private const char DATA_FILE_VERSION = (char)140;
    private const string ATC_ENCRYPTED_TOKEN = "atc4";

    // Atc data size of self executable file
    private readonly Int64 _ExeOutSize;
    private Int64 _TotalSize;
    //private Int64 _TotalFileSize = 0;

#if __MACOS__
    private bool _fCancel = false;
    public bool fCancel
    {
        get { return this._fCancel; }
        set { this._fCancel = value; }
    }

#endif

    //----------------------------------------------------------------------
    // For this file list after description, open associated with file or folder.
    public List<string> OutputFileList { get; } = new List<string>();

    // Temporary option for overwriting
    // private const int USER_CANCELED  = -1;
    // private const int OVERWRITE      = 1;
    // private const int OVERWRITE_ALL  = 2;
    // private const int KEEP_NEWER     = 3;
    // private const int KEEP_NEWER_ALL = 4;
    // private const int SKIP           = 5;
    // private const int SKIP_ALL       = 6;
    public int TempOverWriteOption { get; set; } = USER_CANCELED;

    // MD5ハッシュ不一致でも処理を続行するか
    // private const int USER_CANCELED  = -1;
    // private const int CONTINUE       = 7;
    public int TempMd5HashMismatchContinueOption { get; set; } = USER_CANCELED;

    // 処理した暗号化ファイルの数
    // The number of ATC files to be decrypted
    public int NumberOfFiles { get; set; } = 0;

    // 処理する暗号化ファイルの合計数
    // Total number of ATC files to be decrypted
    public int TotalNumberOfFiles { get; set; } = 1;

    // 親フォルダーを生成しないか否か
    //Create no parent folder in decryption
    public bool fNoParentFolder { get; set; } = false;

    // パスワード入力回数制限（読み取り専用）
    //Get limit of times to input password in encrypt files ( readonly ).
    public char MissTypeLimits { get; } = (char)3;

    // ファイル、フォルダーのタイムスタンプを復号時に合わせる
    //Set the timestamp to decrypted files or directories
    public bool fSameTimeStamp { get; set; } = false;

    // 一つずつ親フォルダーを確認、生成しながら復号する（サルベージモード）
    // Decrypt one by one while creating the parent folder ( Salvage mode ).
    public bool fSalvageToCreateParentFolderOneByOne { get; set; } = false;

    // すべてのファイルを同じ階層のディレクトリーに復号する（サルベージモード）
    // Decrypt all files into the directory of the same hierarchy ( Salvage mode ). 
    public bool fSalvageIntoSameDirectory { get; set; } = false;

    // 自己実行形式タイプのファイルか
    // Self-executable file
    public bool fExecutableType { get; } = false;

    // ファイルのハッシュ値チェックを無視する
    // ignore checking hash data of files
    public bool fSalvageIgnoreHashCheck { get; set; } = false;

    // 公開鍵暗号（RSA暗号）によるデータ
    // RSA encrypted data
    public bool fRsaEncryptionType { get; } = false;

    //----------------------------------------------------------------------
    // The return value of error ( ReadOnly)
    //----------------------------------------------------------------------
    // Input "Error code" for value
    public int ReturnCode { get; private set; } = 0;

    // File path that caused the error
    public string ErrorFilePath { get; private set; } = "";

    // Drive name to decrypt
    public string DriveName { get; private set; } = "";

    // Total file number
    public Int64 TotalFileNumber { get; } = 0;

    // Total file size of files to be decrypted
    public Int64 TotalFileSize { get; private set; } = -1;

    // Free space on the drive to decrypt the file
    public Int64 AvailableFreeSpace { get; private set; } = -1;

    // Error message by the exception
    public string ErrorMessage { get; private set; } = "";

    public ArrayList MessageList { get; private set; }

    //----------------------------------------------------------------------
    // The plain text header data of encrypted file ( ReadOnly)
    //----------------------------------------------------------------------
    // Data Sub Version ( ver.2.00~ = "5", ver.2.70~ = "6" )
    public int DataSebVersion { get; } = 0;

    // The broken status of file
    public bool fBroken { get; } = false;

    // Internal token string ( Whether the file is broken )  = "_AttacheCaseData" or "_Atc_Broken_Data"
    public string TokenStr { get; } = "";

    // Data File Version ( DATA_FILE_VERSION = 140 )
    public int DataFileVersion { get; } = 0;

    // TYPE_ALGORITHM = 1:Rijndael	
    public int TypeAlgorism { get; } = 0;

    // The size of encrypted header data
    private readonly int _AtcHeaderSize;

    // Just this ATC file to decrypt
    public string AtcFilePath { get; }

    // App version
    public Int16 AppVersion { get; } = 0;

    // The salt used in Rfc2898DeriveBytes
    public byte[] salt { get; } = new byte[8];

    public Rfc2898DeriveBytes deriveBytes { get; private set; }

    // List of files to decrypt
    public List<string> FileList { get; }

    // Decryption time
    public string DecryptionTimeString { get; private set; }

    // Guid
    public string GuidString { get; set; }

    // RSA decryption private key XML string
    private string _RsaPrivateKeyXmlString;
    public string RsaPrivateKeyXmlString
    {
      get => this._RsaPrivateKeyXmlString;
      set
      {
        this._RsaPrivateKeyXmlString = value;
        this.fRsaEncryption = true;
      }
    }
    // RSA Decryption
    public bool fRsaEncryption { get; private set; } = false;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="FilePath"></param>
    /// <param name="fileList"></param>
    public FileDecrypt4(string FilePath)
    {
      AtcFilePath = FilePath;

      // _AttacheCaseData
      //byte[] AtcTokenByte = { 0x5F, 0x41, 0x74, 0x74, 0x61, 0x63, 0x68, 0x65, 0x43, 0x61, 0x73, 0x65, 0x44, 0x61, 0x74, 0x61};
      int[] AtcTokenByte = { 95, 65, 116, 116, 97, 99, 104, 101, 67, 97, 115, 101, 68, 97, 116, 97 };

      // _Atc_Broken_Data
      //byte[] AtcBrokenTokenByte = { 0x5F, 0x41, 0x74, 0x63, 0x5F, 0x42, 0x72, 0x6F, 0x6B, 0x65, 0x6E, 0x5F, 0x44, 0x61, 0x74, 0x61 };
      int[] AtcBrokenTokenByte = { 95, 65, 116, 99, 95, 66, 114, 111, 104, 101, 110, 95, 68, 97, 116, 97 };

      // _AttacheCase_Rsa
      //byte[] AtcRsaTokenByte = { 0x5F, 0x41, 0x74, 0x74, 0x61, 0x63, 0x68, 0x65, 0x43, 0x61, 0x73, 0x65, 0x5F, 0x52, 0x73, 0x61 };
      int[] AtcRsaTokenByte = { 95, 65, 116, 116, 97, 99, 104, 101, 67, 97, 115, 101, 95, 82, 115, 97 };

      try
      {
        using (var fs = new FileStream(AtcFilePath, FileMode.Open, FileAccess.Read))
        {
          int b;
          while ((b = fs.ReadByte()) > -1)
          {
            //-----------------------------------
            // Check the token "_AttacheCaseData"
            var fToken = false;
            if (b == AtcTokenByte[0])
            {
              fToken = true;
              for (var i = 1; i < AtcTokenByte.Length; i++)
              {
                if (fs.ReadByte() != AtcTokenByte[i])
                {
                  fToken = false;
                  break;
                }
              }
              if (fToken)
              {
                if (fs.Position > 20)
                { // Self executable file
                  fExecutableType = true;
                  _ExeOutSize = fs.Position - 20;
                }
                break;
              }
            }

            //-----------------------------------
            // Check the token "_Atc_Broken_Data"
            if (b == AtcBrokenTokenByte[0])
            {
              fToken = true;
              for (var i = 1; i < AtcBrokenTokenByte.Length; i++)
              {
                if (fs.ReadByte() != AtcBrokenTokenByte[i])
                {
                  fToken = false;
                  break;
                }
              }

              if (fToken == true)
              {
                if (fs.Position > 20)
                { // Self executable file
                  fExecutableType = true;
                  fBroken = true;
                  _ExeOutSize = fs.Position - 20;
                }
                break;
              }
            }

            //-----------------------------------

          }// end while();

        }// end using (FileStream fs = new FileStream(_AtcFilePath, FileMode.Open, FileAccess.Read));

        using (var fs = new FileStream(AtcFilePath, FileMode.Open, FileAccess.Read))
        {
          if (fs.Length < 16)
          {
            ReturnCode = NOT_ATC_DATA;
            ErrorFilePath = AtcFilePath;
            return;
          }

          if (fExecutableType == true)
          {
            fs.Seek(_ExeOutSize, SeekOrigin.Begin);
          }

          // Plain text header
          var byteArray = new byte[2];
          fs.Read(byteArray, 0, 2);
          AppVersion = BitConverter.ToInt16(byteArray, 0);      // AppVersion

          byteArray = new byte[1];
          fs.Read(byteArray, 0, 1);
          MissTypeLimits = (char)byteArray[0];                  // MissTypeLimits

          byteArray = new byte[1];
          fs.Read(byteArray, 0, 1);
          fBroken = BitConverter.ToBoolean(byteArray, 0);       // fBroken

          byteArray = new byte[16];
          fs.Read(byteArray, 0, 16);
          TokenStr = Encoding.ASCII.GetString(byteArray);       // TokenStr（"_AttacheCaseData" or "_Atc_Broken_Data"）

          byteArray = new byte[4];
          fs.Read(byteArray, 0, 4);
          DataFileVersion = BitConverter.ToInt32(byteArray, 0); // DATA_FILE_VERSION = 140

          byteArray = new byte[4];
          fs.Read(byteArray, 0, 4);
          _AtcHeaderSize = BitConverter.ToInt32(byteArray, 0);   // AtcHeaderSize ( encrypted header data size )

          fs.Read(GuidData, 0, 16);                              // GUID

          // salt
          fs.Read(salt, 0, 8);
#if (DEBUG)
          Debug.WriteLine("salt: " + BitConverter.ToString(salt));
#endif
          // RSA encryption
          if (TokenStr.Trim() == "_AttacheCase_Rsa")
          {
            fs.Read(RsaEncryptedPassword, 0, 256);               // Encrypted 
          }

#if (DEBUG)
          //System.Windows.Forms.MessageBox.Show("_TokenStr: " + _TokenStr);
#endif

        } // end using (FileStream fs = new FileStream(_AtcFilePath, FileMode.Open, FileAccess.Read));

        ReturnCode = DECRYPT_SUCCEEDED;
        return;

      }
      catch (UnauthorizedAccessException)
      {
        //オペレーティング システムが I/O エラーまたは特定の種類のセキュリティエラーのためにアクセスを拒否する場合、スローされる例外
        //The exception that is thrown when the operating system denies access
        //because of an I/O error or a specific type of security error.
        ReturnCode = OS_DENIES_ACCESS;
        ErrorFilePath = AtcFilePath;
        return;
      }
      catch (DirectoryNotFoundException ex)
      {
        //ファイルまたはディレクトリの一部が見つからない場合にスローされる例外
        //The exception that is thrown when part of a file or directory cannot be found
        ReturnCode = DIRECTORY_NOT_FOUND;
        ErrorMessage = ex.Message;
        return;
      }
      catch (DriveNotFoundException ex)
      {
        //使用できないドライブまたは共有にアクセスしようとするとスローされる例外
        //The exception that is thrown when trying to access a drive or share that is not available
        ReturnCode = DRIVE_NOT_FOUND;
        ErrorMessage = ex.Message;
        return;
      }
      catch (FileLoadException ex)
      {
        //マネージド アセンブリが見つかったが、読み込むことができない場合にスローされる例外
        //The exception that is thrown when a managed assembly is found but cannot be loaded
        ReturnCode = FILE_NOT_LOADED;
        ErrorFilePath = ex.FileName;
        return;
      }
      catch (FileNotFoundException ex)
      {
        //ディスク上に存在しないファイルにアクセスしようとして失敗したときにスローされる例外
        //The exception that is thrown when an attempt to access a file that does not exist on disk fails
        ReturnCode = FILE_NOT_FOUND;
        ErrorFilePath = ex.FileName;
        return;
      }
      catch (PathTooLongException)
      {
        //パス名または完全修飾ファイル名がシステム定義の最大長を超えている場合にスローされる例外
        //The exception that is thrown when a path or fully qualified file name is longer than the system-defined maximum length
        ReturnCode = PATH_TOO_LONG;
        return;
      }
      catch (IOException ex)
      {
        //I/Oエラーが発生したときにスローされる例外。現在の例外を説明するメッセージを取得します。
        //The exception that is thrown when an I/O error occurs. Gets a message that describes the current exception.
        ReturnCode = IO_EXCEPTION;
        ErrorMessage = ex.Message;
        return;
      }
      catch (Exception ex)
      {
        ReturnCode = ERROR_UNEXPECTED;
        ErrorMessage = ex.Message;
        return;
      }

    } // end public FileDecrypt4(string FilePath);
      //----------------------------------------------------------------------

    /// <summary>
    /// The encrypted file by AES to the original file or folder by user's password.
    /// ユーザーが設定したパスワードによって、AESによって暗号化されたファイルを元のファイル、またはフォルダーに復号して戻す。
    /// </summary>
    /// <param name="FilePath">File path or directory path is encrypted</param>
    /// <param name="OutDirPath">The directory of outputting encryption file.</param>
    /// <param name="Password">Encryption password string</param>
    /// <param name="PasswordBinary"></param>
    /// <param name="sender"></param>
    /// <param name="dialog"></param>
    /// <returns>bool true: Success, false: Failed</returns>
#if __MACOS__
    public bool Decrypt(
      object sender, DoWorkEventArgs e,
      string FilePath, string OutDirPath, string Password, byte[] PasswordBinary,
      Action<int, string> dialog, bool fSuppressConfirmation)
    {
#else
    public bool Decrypt(
      object sender, DoWorkEventArgs e,
      string FilePath, string OutDirPath, string Password, byte[] PasswordBinary,
      Action<int, string, IReadOnlyList<string>> dialog)
    {
#endif
      var worker = sender as BackgroundWorker;
      bool cancelCheck() => worker.CancellationPending;

      //-----------------------------------
      // Header data is starting.
      // Progress event handler
      var messageList = new ArrayList
      {
        READY_FOR_DECRYPT,
        Path.GetFileName(FilePath)
      };

      worker?.ReportProgress(0, messageList);

      var swDecrypt = new Stopwatch();
      var swProgress = new Stopwatch();

      swDecrypt.Start();
      swProgress.Start();

      var RsaBlock = 0;

      // _FileList = new List<string>();
      // Dictionary<int, FileListData> dic = new Dictionary<int, FileListData>();
      var FileDataList = new List<FileData>();

      if (TokenStr.Trim() == "_AttacheCaseData")
      {
        // Atc data
        RsaBlock = 0;
      }
#if (EXEOUT)
#else
      else if (TokenStr.Trim() == "_AttacheCase_Rsa")
      {
        var xmlElement = XElement.Parse(_RsaPrivateKeyXmlString);
        var guidString = xmlElement.Element("id")?.Value;
        var guid = Guid.Parse(guidString!);
        if (GuidData.SequenceEqual(guid.ToByteArray()) == false)
        {
          ReturnCode = RSA_KEY_GUID_NOT_MATCH;
          ErrorFilePath = _RsaPrivateKeyXmlString;
          return (false);
        }

        var rsa = new RSACryptoServiceProvider(2048);
        rsa.FromXmlString(_RsaPrivateKeyXmlString); //秘密鍵を指定
        var RsaPasswordData = rsa.Decrypt(RsaEncryptedPassword, RSAEncryptionPadding.OaepSHA1);
        //string debugString = BitConverter.ToString(RsaPasswordData);
        //Console.WriteLine(debugString);
        // RSA decrypted password binary data
        PasswordBinary = RsaPasswordData;
        RsaBlock = 256;
      }
#endif
      else if (TokenStr.Trim() == "_Atc_Broken_Data")
      {
        // Atc file is broken
        ReturnCode = ATC_BROKEN_DATA;
        ErrorFilePath = FilePath;
        RsaBlock = 0;
        return (false);
      }
      else
      {
        // not AttacheCase data
        ReturnCode = NOT_ATC_DATA;
        ErrorFilePath = FilePath;
        RsaBlock = 0;
        return (false);
      }

      //Rfc2898DeriveBytes _deriveBytes;
      if (PasswordBinary == null)
      {
        deriveBytes = new Rfc2898DeriveBytes(Password, salt, 1000);
      }
      else
      {
        deriveBytes = new Rfc2898DeriveBytes(PasswordBinary, salt, 1000);
      }

      var key = deriveBytes.GetBytes(32);
      var iv = deriveBytes.GetBytes(16);

#if (DEBUG)
      var debugString = BitConverter.ToString(salt);
      Debug.WriteLine("salt: " + debugString);

      debugString = BitConverter.ToString(key);
      Debug.WriteLine("key: " + debugString);

      debugString = BitConverter.ToString(iv);
      Debug.WriteLine("iv: " + debugString);
#endif

      try
      {
        byte[] byteArray;
        var len = 0;
        using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          if (fs.Length < 16)
          {
            // not AttacheCase data
            ReturnCode = NOT_ATC_DATA;
            ErrorFilePath = FilePath;
            return (false);
          }
          else
          {
            if (_ExeOutSize > 0)
            {
              // self-executable file
              fs.Seek(_ExeOutSize + 52 + RsaBlock, SeekOrigin.Begin);
            }
            else
            {
              fs.Seek(52 + RsaBlock, SeekOrigin.Begin);
            }
          }

          using (var ms = new MemoryStream())
          {
            // The Header of MemoryStream is encrypted
            using (var aes = new AesManaged())
            {
              aes.BlockSize = 128;             // BlockSize = 16 bytes
              aes.KeySize = 256;               // KeySize = 32 bytes
              aes.Mode = CipherMode.CBC;       // CBC mode
              aes.Padding = PaddingMode.Zeros; // Padding mode is "PKCS7".

              aes.Key = key;
              aes.IV = iv;

              // Decryption interface.
              var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
              using (var cse = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
              {
                byteArray = new byte[_AtcHeaderSize];
                len = cse.Read(byteArray, 0, _AtcHeaderSize);
                ms.Write(byteArray, 0, _AtcHeaderSize);
              }
            }

            // Encrypted token
            ms.Position = 0;
            byteArray = new byte[4];
            ms.Read(byteArray, 0, 4);

            // Check Password Token
            var Token = Encoding.UTF8.GetString(byteArray);
            if (Token.IndexOf(ATC_ENCRYPTED_TOKEN, StringComparison.Ordinal) > -1)
            {
              // Decryption is succeeded.
            }
            else
            {
              // Token is not match ( Password is not correct )
              ReturnCode = PASSWORD_TOKEN_NOT_FOUND;
              ErrorFilePath = FilePath;
              return (false);
            }

            TotalFileSize = 0;

            var ParentFolder = "";
            var fDirectoryTraversal = false;
            var InvalidFilePath = "";

            Int64 FileNum = 0;

            while (true)
            {

              byteArray = new byte[2];

              if (ms.Read(byteArray, 0, 2) < 2)
              {
                break;
              }

              // FileNameSize
              var fd = new FileData();
              var FileNameSize = BitConverter.ToInt16(byteArray, 0);
              // FileName
              byteArray = new byte[FileNameSize];
              ms.Read(byteArray, 0, FileNameSize);
              fd.FilePath = Encoding.UTF8.GetString(byteArray);

#if __MACOS__
              fd.FilePath = fd.FilePath.Replace("\\", "/");
#else
              fd.FilePath = fd.FilePath.Replace("/", "\\");
#endif

              //-----------------------------------
              // Parent folder is not created.
              //
              if (fNoParentFolder)
              {
                // root directory
                if (FileNum == 0)
                {
                  ParentFolder = fd.FilePath;
                }
                // not root directory ( files )
                else
                {
                  var sb = new StringBuilder(fd.FilePath);
                  len = ParentFolder.Length;
                  fd.FilePath = sb.Replace(ParentFolder, "", 0, len).ToString();
                }
              }

              //-----------------------------------
              // File path
              //
              var OutFilePath = "";
              if (Path.IsPathRooted(fd.FilePath))
              {
                OutFilePath = OutDirPath + fd.FilePath;
              }
              else
              {
                OutFilePath = Path.Combine(OutDirPath, fd.FilePath);
              }

              //-----------------------------------
              // ディレクトリ・トラバーサル対策
              // Directory traversal countermeasures

              // 余計な ":" が含まれている
              // Extra ":" is included
              var FilePathSplits = fd.FilePath.Split(':');
              if (FilePathSplits.Length > 1)
              {
                fDirectoryTraversal = true;
                InvalidFilePath = OutFilePath;
              }

              try
              {
                // ファイルパスを正規化
                // Canonicalize file path.
                OutFilePath = Path.GetFullPath(OutFilePath);
              }
              catch
              {
                fDirectoryTraversal = true;
                InvalidFilePath = OutFilePath;
              }

              // 正規化したパスが保存先と一致するか
              // Whether the canonicalized path matches the save destination
              if (fDirectoryTraversal == false && OutFilePath.StartsWith(OutDirPath))
              {
                fd.FilePath = OutFilePath;
              }
              else
              {
                fDirectoryTraversal = true;
                InvalidFilePath = OutFilePath;
              }

              // Error: Directory traversal countermeasures
              if (fDirectoryTraversal)
              {
                ReturnCode = INVALID_FILE_PATH;
                ErrorFilePath = InvalidFilePath;
                return (false);
              }

              //-----------------------------------
              // FileSize
              byteArray = new byte[8];
              ms.Read(byteArray, 0, 8);
              fd.FileSize = BitConverter.ToInt64(byteArray, 0);
              TotalFileSize += fd.FileSize;

              // File Attribute
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              fd.FileAttribute = BitConverter.ToInt32(byteArray, 0);

              // Last write DateTime;
              var tzi = TimeZoneInfo.Local;  // UTC to Local time

              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              var LastWriteDateTimeString = BitConverter.ToInt32(byteArray, 0).ToString("0000/00/00");
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              LastWriteDateTimeString = LastWriteDateTimeString + BitConverter.ToInt32(byteArray, 0).ToString(" 00:00:00");
              DateTime.TryParse(LastWriteDateTimeString, out fd.LastWriteDateTime);
              fd.LastWriteDateTime = TimeZoneInfo.ConvertTimeFromUtc(fd.LastWriteDateTime, tzi);

              // Creation DateTime
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              var CreationDateTimeString = BitConverter.ToInt32(byteArray, 0).ToString("0000/00/00");
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              CreationDateTimeString = CreationDateTimeString + BitConverter.ToInt32(byteArray, 0).ToString(" 00:00:00");
              DateTime.TryParse(CreationDateTimeString, out fd.CreationDateTime);
              fd.CreationDateTime = TimeZoneInfo.ConvertTimeFromUtc(fd.CreationDateTime, tzi);

              if (fd.FileSize > 0)
              {
                // Check sum ( MD5 hash )
                fd.Hash = new byte[16];
                ms.Read(fd.Hash, 0, 16);
              }

              // Insert to 'Key-Value' type array data.
              // dic.Add(FileNum, fd);
              FileDataList.Add(fd);
              FileNum++;

              // Next FileNameSize
              //byteArray = new byte[2];
            }

          }//end using (MemoryStream ms = new MemoryStream())

        }//end using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read));

#if DEBUG
#if __MACOS__
#else
        //----------------------------------------------------------------------
        // Output debug log
        //----------------------------------------------------------------------
        var DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        using (var sw = new StreamWriter(Path.Combine(DesktopDir, "_decrypt_header.txt"), false, Encoding.UTF8))
        {
          for (var i = 0; i < FileDataList.Count; i++)
          {
            var OneLine = i + "\t";
            OneLine += FileDataList[i].FilePath + "\t";
            OneLine += FileDataList[i].FileSize + "\t";
            OneLine += FileDataList[i].FileAttribute + "\t";
            OneLine += FileDataList[i].LastWriteDateTime.ToString("yyyy/MM/dd H:mm:s") + "\t";
            OneLine += FileDataList[i].CreationDateTime.ToString("yyyy/MM/dd H:mm:s") + "\t";
            if (FileDataList[i].FileSize > 0)   // MD5 hash
            {
              OneLine += BitConverter.ToString(FileDataList[i].Hash).Replace("-", string.Empty);
            }
            sw.WriteLine(OneLine);
          }
        }
#endif
#endif

        //----------------------------------------------------------------------
        // Check the disk space
        //----------------------------------------------------------------------
        var RootDriveLetter = Path.GetPathRoot(OutDirPath).Substring(0, 1);

        if (RootDriveLetter == "\\")
        {
          // Network
        }
        else
        {
          var drive = new DriveInfo(RootDriveLetter);

          var driveType = drive.DriveType;
          switch (driveType)
          {
            case DriveType.CDRom:
            case DriveType.NoRootDirectory:
            case DriveType.Unknown:
              break;
            case DriveType.Fixed:     // Local Drive
            case DriveType.Network:   // Mapped Drive
            case DriveType.Ram:       // Ram Drive
            case DriveType.Removable: // Usually a USB Drive
                                      // The drive is not available, or not enough free space.
              if (drive.IsReady == false || drive.AvailableFreeSpace < TotalFileSize)
              {
                ReturnCode = NO_DISK_SPACE;
                DriveName = drive.ToString();
                //_TotalFileSize = _TotalFileSize;
                AvailableFreeSpace = drive.AvailableFreeSpace;
                return (false);
              }
              break;
            default:
              throw new ArgumentOutOfRangeException();
          }
        }

        //-----------------------------------
        // Decrypt file main data.
        //-----------------------------------
        using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          //-----------------------------------
          // Adjust the header data in 16 bytes ( Block size )
          var mod = _AtcHeaderSize % 16;
          if (fExecutableType)
          {
            fs.Seek(_ExeOutSize + 52 + RsaBlock + _AtcHeaderSize + 16 - mod, SeekOrigin.Begin);
          }
          else
          {
            fs.Seek(52 + RsaBlock + _AtcHeaderSize + 16 - mod, SeekOrigin.Begin);
          }

          //-----------------------------------
          // Decryption
          using (var aes = new AesManaged())
          {
            aes.BlockSize = 128;             // BlockSize = 16bytes
            aes.KeySize = 256;               // KeySize = 32bytes
            aes.Mode = CipherMode.CBC;       // CBC mode
            aes.Padding = PaddingMode.PKCS7; // Padding mode
            aes.Key = key;
            aes.IV = iv;
#if DEBUG
            //System.Windows.Forms.MessageBox.Show("dic.Count: " + dic.Count);
#endif
            //Decryption interface.
            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (var cse = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
            {
              using (var ds = new DeflateStream(cse, CompressionMode.Decompress))
              {
                FileStream outfs = null;
                Int64 FileSize = 0;
                var FileIndex = 0;

                var fSkip = false;

                if (fNoParentFolder)
                {
                  if (FileDataList[0].FilePath.EndsWith("\\") || FileDataList[0].FilePath.EndsWith("/"))
                  {
                    FileIndex = 1;  // Ignore parent folder.
                  }
                }

                //----------------------------------------------------------------------
                byteArray = new byte[BUFFER_SIZE];

                //while ((len = ds.Read(byteArray, 0, BUFFER_SIZE)) > 0)
                while (true)
                {

                  len = ds.Read(byteArray, 0, BUFFER_SIZE);

                  // 末尾の0バイトファイル、またはフォルダ生成対策
                  if (len == 0) len = 1;

                  var buffer_size = len;

                  while (len > 0)
                  {
                    //----------------------------------------------------------------------
                    // 書き込み中のファイルまたはフォルダが無い場合は作る
                    // Create them if there is no writing file or folder.
                    //----------------------------------------------------------------------
                    if (outfs == null)
                    {
                      var processPath = string.Empty;

                      //-----------------------------------
                      // Create file or directories.
                      if (FileIndex > FileDataList.Count - 1)
                      {
                        ReturnCode = DECRYPT_SUCCEEDED;
                        return (true);
                      }
                      else
                      {
                        //-----------------------------------
                        // Create directory
                        //-----------------------------------
                        if (FileDataList[FileIndex].FilePath.EndsWith("\\") ||
                            FileDataList[FileIndex].FilePath.EndsWith("/"))
                        {
                          processPath = Path.Combine(OutDirPath, FileDataList[FileIndex].FilePath);
                          var di = new DirectoryInfo(processPath);

                          // File already exists.
                          if (Directory.Exists(processPath))
                          {
                            // Temporary option for overwriting
                            // private const int USER_CANCELED  = -1;
                            // private const int OVERWRITE      = 1;
                            // private const int OVERWRITE_ALL  = 2;
                            // private const int KEEP_NEWER     = 3;
                            // private const int KEEP_NEWER_ALL = 4;
                            // private const int SKIP           = 5;
                            // private const int SKIP_ALL       = 6;
                            if (TempOverWriteOption == OVERWRITE_ALL)
                            {
                              // Overwrite ( New create )
                            }
                            else if (TempOverWriteOption == SKIP_ALL)
                            {
                              fSkip = true;
                            }
                            else if (TempOverWriteOption == KEEP_NEWER_ALL)
                            {
                              if (di.LastWriteTime > FileDataList[FileIndex].LastWriteDateTime)
                              {
                                fSkip = true; // old directory
                              }
                            }
                            else
                            {
                              // Show dialog of confirming to overwrite. 
                              dialog(0, processPath, null);

                              // Cancel
                              if (TempOverWriteOption == USER_CANCELED)
                              {
                                ReturnCode = USER_CANCELED;
                                return (false);
                              }
                              else if (TempOverWriteOption == OVERWRITE || TempOverWriteOption == OVERWRITE_ALL)
                              {
                                // Overwrite ( New create )
                              }
                              // Skip, or Skip All
                              else if (TempOverWriteOption == SKIP_ALL)
                              {
                                fSkip = true;
                                ReturnCode = DECRYPT_SUCCEEDED;
                                return (true);
                              }
                              else if (TempOverWriteOption == SKIP)
                              {
                                fSkip = true;
                              }
                              else if (TempOverWriteOption is KEEP_NEWER or KEEP_NEWER_ALL)
                              { // New file?
                                if (di.LastWriteTime > FileDataList[FileIndex].LastWriteDateTime)
                                {
                                  fSkip = true;
                                }
                              }
                            }

                            if (fSkip == false)
                            {
                              //隠し属性を削除する
                              di.Attributes &= ~FileAttributes.Hidden;
                              //読み取り専用を削除
                              di.Attributes &= ~FileAttributes.ReadOnly;
                            }

                            //すべての属性を解除
                            //File.SetAttributes(path, FileAttributes.Normal);

                          } // end if ( Directory.Exists )

                          Directory.CreateDirectory(FileDataList[FileIndex].FilePath);
                          OutputFileList.Add(FileDataList[FileIndex].FilePath);
                          FileSize = 0;
                          FileIndex++;

                          if (FileIndex > FileDataList.Count - 1)
                          {
                            ReturnCode = DECRYPT_SUCCEEDED;
                            return (true);
                          }

                          continue;

                        }
                        //-----------------------------------
                        // Create file
                        //-----------------------------------
                        else
                        {
                          if (FileDataList[FileIndex].FilePath.StartsWith(OutDirPath, StringComparison.OrdinalIgnoreCase))
                          {
                            // そのまま、FileDataList[FileIndex].FilePath を使用
                            processPath = FileDataList[FileIndex].FilePath;
                          }
                          else
                          {
                            // StartsWith が false の場合は Path.Combine を使用
                            processPath = Path.Combine(OutDirPath, FileDataList[FileIndex].FilePath);
                          }

                          // path変数を使用して FileInfo を生成
                          var fi = new FileInfo(processPath);

                          // File already exists.
                          if (File.Exists(processPath))
                          {
                            // Salvage Data Mode
                            if (fSalvageIntoSameDirectory)
                            {
                              var SerialNum = 0;
                              while (File.Exists(processPath))
                              {
                                processPath = getFileNameWithSerialNumber(processPath, SerialNum);
                                SerialNum++;
                              }
                            }
                            else
                            {
                              // Temporary option for overwriting
                              // private const int USER_CANCELED  = -1;
                              // private const int OVERWRITE      = 1;
                              // private const int OVERWRITE_ALL  = 2;
                              // private const int KEEP_NEWER     = 3;
                              // private const int KEEP_NEWER_ALL = 4;
                              // private const int SKIP           = 5;
                              // private const int SKIP_ALL       = 6;
                              if (TempOverWriteOption == OVERWRITE_ALL)
                              {
                                // Overwrite ( New create )
                              }
                              else if (TempOverWriteOption == SKIP_ALL)
                              {
                                fSkip = true;
                              }
                              else if (TempOverWriteOption == KEEP_NEWER_ALL)
                              {
                                if (fi.LastWriteTime > FileDataList[FileIndex].LastWriteDateTime)
                                {
                                  fSkip = true;
                                }
                              }
                              else
                              {
                                // Show dialog of confirming to overwrite. 
                                dialog(1, processPath, null);

                                // Cancel
                                if (TempOverWriteOption == USER_CANCELED)
                                {
                                  ReturnCode = USER_CANCELED;
                                  return (false);
                                }
                                else if (TempOverWriteOption is OVERWRITE or OVERWRITE_ALL)
                                {
                                  // Overwrite ( New create )
                                }
                                // Skip, or Skip All
                                else if (TempOverWriteOption is SKIP or SKIP_ALL)
                                {
                                  fSkip = true;
                                }
                                else if (TempOverWriteOption is KEEP_NEWER or KEEP_NEWER_ALL)
                                { // New file?
                                  if (fi.LastWriteTime > FileDataList[FileIndex].LastWriteDateTime)
                                  {
                                    fSkip = true; // old directory
                                  }
                                }
                              }

                              if (fSkip == false)
                              {
                                //隠し属性を削除する
                                //fi.Attributes &= ~FileAttributes.Hidden;
                                //読み取り専用を削除
                                //fi.Attributes &= ~FileAttributes.ReadOnly;

                                //すべての属性を解除
                                File.SetAttributes(processPath, FileAttributes.Normal);
                              }

                            }

                          }// end if ( File.Exists );

                          // Salvage data mode
                          // サルベージ・モード
                          if (fSalvageToCreateParentFolderOneByOne)
                          {
                            // Decrypt one by one while creating the parent folder.
                            Directory.CreateDirectory(Path.GetDirectoryName(processPath));
                          }

                          if (fSkip)
                          {
                            // Not create file
                          }
                          else
                          {
                            try
                            {
                              outfs = new FileStream(processPath, FileMode.Create, FileAccess.Write);
                            }
                            catch
                            {
                              // フォルダが通っていない場合は例外が発生するので親フォルダーを作成して改めてファイルを開く
                              // If there is no parent folders, an exception will occur, so create a parent folders and open the file again
                              var fileInfo = new FileInfo(processPath);
                              if (!fileInfo.Directory.Exists)
                              {
                                fileInfo.Directory.Create();
                              }
                              outfs = new FileStream(processPath, FileMode.Create, FileAccess.Write);

                            }
                          }

                          OutputFileList.Add(processPath);
                          FileSize = 0;

                        }

                      }

                    }// end if (outfs == null);

                    //----------------------------------------------------------------------
                    // Write data
                    //----------------------------------------------------------------------
                    if (FileSize + len < FileDataList[FileIndex].FileSize)
                    {
                      if (outfs != null || fSkip)
                      {
                        // まだまだ書き込める
                        // can write more
                        if (fSkip == false)
                        {
                          outfs.Write(byteArray, buffer_size - len, len);
                        }
                        FileSize += len;
                        _TotalSize += len;
                        len = 0;
                      }
                    }
                    else
                    {
                      // ファイルの境界を超えて読み込んでいる
                      // Reading beyond file boundaries
                      var rest = (int)(FileDataList[FileIndex].FileSize - FileSize);

                      if (fSkip == false)
                      {
                        // 書き込み完了
                        // Write completed
                        outfs.Write(byteArray, buffer_size - len, rest);
                      }

                      _TotalSize += rest;

                      len -= rest;

                      if (outfs != null)
                      {
                        // 生成したファイルを閉じる
                        // File close
                        outfs.Close();
                        outfs = null;
                      }

                      //----------------------------------------------------------------------
                      // ファイル属性の復元
                      // Restore file attributes

                      if (fSkip == false)
                      {
                        var fi = new FileInfo(FileDataList[FileIndex].FilePath)
                        {
                          // タイムスタンプの復元
                          // Restore the timestamp of a file
                          CreationTime = FileDataList[FileIndex].CreationDateTime,
                          LastWriteTime = FileDataList[FileIndex].LastWriteDateTime,
                          // ファイル属性の復元
                          // Restore file attribute.
                          Attributes = (FileAttributes)FileDataList[FileIndex].FileAttribute
                        };

                        // ハッシュ値のチェック
                        // Check the hash of a file
                        if (fSalvageIgnoreHashCheck == false && FileSize > 0)
                        {
                          var hash = GetMd5Hash(FileDataList[FileIndex].FilePath, cancelCheck);
                          // ハッシュ値が異なるか
                          // Do the hash values differ?
                          if (hash.SequenceEqual(FileDataList[FileIndex].Hash) == false)
                          {
                            var hashList = new[]
                            {
                              BitConverter.ToString(FileDataList[FileIndex].Hash).Replace("-", "").ToLower(),
                              BitConverter.ToString(hash).Replace("-", "").ToLower()
                            };

                            // ハッシュ値が異なるので、処理を続行するかキャンセルするかを問い合わせる
                            // Confirm whether to continue or cancel the process because the hash values are different
                            dialog(2, FileDataList[FileIndex].FilePath, hashList);

                            // キャンセルが返ってきたときのみ、処理を抜ける
                            // Exit the process only when a cancellation is returned.
                            if (TempMd5HashMismatchContinueOption == USER_CANCELED)
                            {
                              ReturnCode = NOT_CORRECT_HASH_VALUE;
                              ErrorFilePath = FileDataList[FileIndex].FilePath;
                              return (false);
                            }
                            else
                            {
                              TempMd5HashMismatchContinueOption = CONTINUE;
                            }

                          }
                        }

                      }

                      FileSize = 0;
                      FileIndex++;

                      fSkip = false;

                      if (FileIndex > FileDataList.Count - 1)
                      {
                        ReturnCode = DECRYPT_SUCCEEDED;
                        return (true);
                      }

                    }
                    //----------------------------------------------------------------------
                    //進捗の表示
                    var MessageText = "";
                    if (TotalNumberOfFiles > 1)
                    {
                      MessageText = FilePath + " ( " + NumberOfFiles + "/" + TotalNumberOfFiles + " files" + " )";
                    }
                    else
                    {
                      MessageText = FilePath;
                    }

                    this.MessageList = new ArrayList
                    {
                      DECRYPTING,
                      MessageText
                    };

                    // プログレスバーの更新間隔を100msに調整
                    if (swProgress.ElapsedMilliseconds > 100)
                    {
                      var percent = ((float)_TotalSize / TotalFileSize);
                      worker.ReportProgress((int)(percent * 10000), this.MessageList);
                      swProgress.Restart();
                    }

                    // User cancel
                    if (worker.CancellationPending)
                    {
                      if (outfs != null)
                      {
                        outfs.Close();
                        outfs = null;
                      }
                      e.Cancel = true;
                      return (false);
                    }

                  }// end while(len > 0);

                }// end while ((len = ds.Read(byteArray, 0, BUFFER_SIZE)) > 0); 

              }// end using (DeflateStream ds = new DeflateStream(cse, CompressionMode.Decompress));

            }// end using (CryptoStream cse = new CryptoStream(fs, decryptor, CryptoStreamMode.Read));

          }// end using (Rijndael aes = new RijndaelManaged());

        }// end using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read));

      }
      catch (UnauthorizedAccessException)
      {
        //オペレーティング システムが I/O エラーまたは特定の種類のセキュリティエラーのためにアクセスを拒否する場合、スローされる例外
        //The exception that is thrown when the operating system denies access
        //because of an I/O error or a specific type of security error.
        ReturnCode = OS_DENIES_ACCESS;
        return (false);
      }
      catch (DirectoryNotFoundException ex)
      {
        //ファイルまたはディレクトリの一部が見つからない場合にスローされる例外
        //The exception that is thrown when part of a file or directory cannot be found
        ReturnCode = DIRECTORY_NOT_FOUND;
        ErrorMessage = ex.Message;
        return (false);
      }
      catch (DriveNotFoundException ex)
      {
        //使用できないドライブまたは共有にアクセスしようとするとスローされる例外
        //The exception that is thrown when trying to access a drive or share that is not available
        ReturnCode = DRIVE_NOT_FOUND;
        ErrorMessage = ex.Message;
        return (false);
      }
      catch (FileLoadException ex)
      {
        //マネージド アセンブリが見つかったが、読み込むことができない場合にスローされる例外
        //The exception that is thrown when a managed assembly is found but cannot be loaded
        ReturnCode = FILE_NOT_LOADED;
        ErrorFilePath = ex.FileName;
        return (false);
      }
      catch (FileNotFoundException ex)
      {
        //ディスク上に存在しないファイルにアクセスしようとして失敗したときにスローされる例外
        //The exception that is thrown when an attempt to access a file that does not exist on disk fails
        ReturnCode = FILE_NOT_FOUND;
        ErrorFilePath = ex.FileName;
        return (false);
      }
      catch (PathTooLongException)
      {
        //パス名または完全修飾ファイル名がシステム定義の最大長を超えている場合にスローされる例外
        //The exception that is thrown when a path or fully qualified file name is longer than the system-defined maximum length
        ReturnCode = PATH_TOO_LONG;
        return (false);
      }
      catch (IOException ex)
      {
        //I/Oエラーが発生したときにスローされる例外。現在の例外を説明するメッセージを取得します。
        //The exception that is thrown when an I/O error occurs. Gets a message that describes the current exception.
        ReturnCode = IO_EXCEPTION;
        ErrorMessage = ex.Message;
        return (false);
      }
      catch (Exception ex)
      {
        // ユーザーキャンセルを行うタイミングで例外が発生してしまうため、エラーコードはユーザーキャンセルをそのまま返す。
        // The error code returns the user cancel as it is because the exception occurs at the time of user cancel.
        if (ReturnCode == USER_CANCELED)
        {
          ReturnCode = USER_CANCELED;
          ErrorMessage = "";
          return (false);
        }
        else if (ReturnCode == NOT_CORRECT_HASH_VALUE)
        {
          ReturnCode = NOT_CORRECT_HASH_VALUE;
          ErrorMessage = ErrorFilePath;
          return (false);
        }
        else if (ReturnCode == DECRYPT_SUCCEEDED)
        {
          ReturnCode = DECRYPT_SUCCEEDED;
          return (true);
        }
        else
        {
          ReturnCode = IO_EXCEPTION;
          ErrorMessage = ex.Message;
          return (false);
        }

      }
      finally
      {
        swProgress.Stop();
        swDecrypt.Stop();
        // 計測時間
        TimeSpan ts = swDecrypt.Elapsed;
        DecryptionTimeString =
          Convert.ToString(ts.Hours) + "h" + Convert.ToString(ts.Minutes) + "m" +
          Convert.ToString(ts.Seconds) + "s" + Convert.ToString(ts.Milliseconds) + "ms";
      }

    }// end Decrypt();

    /// ファイル名に連番を振る
    /// Put a serial number to the file name
    private static string getFileNameWithSerialNumber(string FilePath, int SerialNum)
    {
      var DirPath = Path.GetDirectoryName(FilePath);
      var FileName = Path.GetFileNameWithoutExtension(FilePath) + SerialNum.ToString("0000") + Path.GetExtension(FilePath);

      return DirPath == null ? FileName : Path.Combine(DirPath, FileName);
    }

    /// Get a check sum (MD5) to calculate
    private static byte[] GetMd5Hash(string FilePath)
    {
      using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      //MD5CryptoServiceProviderオブジェクト
      var md5 = new MD5CryptoServiceProvider();
      var bs = md5.ComputeHash(fs);
      md5.Clear();
      return (bs);
    }

    /// <summary>
    /// MD5ハッシュを取得する（キャンセル可能版）
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="cancelCheck"></param>
    /// <returns></returns>
    private static byte[] GetMd5Hash(string filePath, Func<bool> cancelCheck)
    {
      using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      var md5 = new MD5CryptoServiceProvider();
      var buffer = new byte[8192];
      int bytesRead;
      while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
      {
        if (cancelCheck()) // キャンセルチェック
        {
          md5.Clear();
          return null; // キャンセルされた場合はnullを返す
        }
        md5.TransformBlock(buffer, 0, bytesRead, null, 0);
      }
      md5.TransformFinalBlock(buffer, 0, 0);
      var bs = md5.Hash;
      md5.Clear();
      return bs;
    }


  }//end class FileDecrypt2;

}//end namespace AttacheCase;
