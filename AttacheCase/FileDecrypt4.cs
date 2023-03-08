//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttachéCase4 )" -- File encryption software.
// Copyright (C) 2016-2023  Mitsuhiro Hibara
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
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.IO.Compression;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
#if __MACOS__
using AppKit;
#endif

namespace AttacheCase
{
  class FileDecrypt4
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
    private const int DELETE_SUCCEEDED  = 3; // Delete is succeeded.
    private const int READY_FOR_ENCRYPT = 4; // Getting ready for encryption or decryption.
    private const int READY_FOR_DECRYPT = 5; // Getting ready for encryption or decryption.
    private const int ENCRYPTING        = 6; // Ecrypting.
    private const int DECRYPTING        = 7; // Decrypting.
    private const int DELETING          = 8; // Deleting.

    // Error code
    private const int USER_CANCELED            = -1;   // User cancel.
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

    // Overwrite Option
    //private const int USER_CANCELED = -1;
    private const int OVERWRITE      = 1;
    private const int OVERWRITE_ALL  = 2;
    private const int KEEP_NEWER     = 3;
    private const int KEEP_NEWER_ALL = 4;
    // ---
    // Skip Option
    private const int SKIP = 5;
    private const int SKIP_ALL = 6;

    byte[] GuidData = new byte[16];
    byte[] RsaPassword = new byte[32];
    byte[] RsaEncryptedPassword = new byte[256];

    private const int BUFFER_SIZE = 4096;

    // Header data variables
    private const string STRING_TOKEN_NORMAL = "_AttacheCaseData";
    private const string STRING_TOKEN_BROKEN = "_Atc_Broken_Data";
    private const string STRING_TOKEN_RSA    = "_AttacheCase_Rsa";
    private const char DATA_FILE_VERSION = (char)140;
    private const string ATC_ENCRYPTED_TOKEN = "atc4";

    // Atc data size of self executable file
    private Int64 _ExeOutSize = 0;
    private Int64 _TotalSize = 0;
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
    // For thie file list after description, open associated with file or folder.
    private readonly List<string> _OutputFileList = new List<string>();
    public List<string> OutputFileList
    {
      get { return _OutputFileList; }
    }

    // Temporary option for overwriting
    // private const int USER_CANCELED  = -1;
    // private const int OVERWRITE      = 1;
    // private const int OVERWRITE_ALL  = 2;
    // private const int KEEP_NEWER     = 3;
    // private const int KEEP_NEWER_ALL = 4;
    // private const int SKIP           = 5;
    // private const int SKIP_ALL       = 6;
    private int _TempOverWriteOption = -1;
    public int TempOverWriteOption
    {
      get { return this._TempOverWriteOption; }
      set { this._TempOverWriteOption = value; }
    }
    // 処理した暗号化ファイルの数
    // The number of ATC files to be decrypted
    private int _NumberOfFiles = 0;
    public int NumberOfFiles
    {
      get { return this._NumberOfFiles; }
      set { this._NumberOfFiles = value; }
    }
    // 処理する暗号化ファイルの合計数
    // Total number of ATC files to be decrypted
    private int _TotalNumberOfFiles = 1;
    public int TotalNumberOfFiles
    {
      get { return this._TotalNumberOfFiles; }
      set { this._TotalNumberOfFiles = value; }
    }
    // 親フォルダーを生成しないか否か
    //Create no parent folder in decryption
    private bool _fNoParentFolder = false;
    public bool fNoParentFolder
    {
      get { return this._fNoParentFolder; }
      set { this._fNoParentFolder = value; }
    }
    // パスワード入力回数制限（読み取り専用）
    //Get limit of times to input password in encrypt files ( readonly ).
    private char _MissTypeLimits = (char)3;
    public char MissTypeLimits
    {
      get { return this._MissTypeLimits; }
    }
    // ファイル、フォルダーのタイムスタンプを復号時に合わせる
    private bool _fSameTimeStamp = false;
    //Set the timestamp to decrypted files or directories
    public bool fSameTimeStamp
    {
      get { return this._fSameTimeStamp; }
      set { this._fSameTimeStamp = value; }
    }
    // 一つずつ親フォルダーを確認、生成しながら復号する（サルベージモード）
    private bool _fSalvageToCreateParentFolderOneByOne = false;
    // Decrypt one by one while creating the parent folder ( Salvage mode ).
    public bool fSalvageToCreateParentFolderOneByOne
    {
      get { return this._fSalvageToCreateParentFolderOneByOne; }
      set { this._fSalvageToCreateParentFolderOneByOne = value; }
    }
    // すべてのファイルを同じ階層のディレクトリーに復号する（サルベージモード）
    private bool _fSalvageIntoSameDirectory = false;
    // Decrypt all files into the directory of the same hierarchy ( Salvage mode ). 
    public bool fSalvageIntoSameDirectory
    {
      get { return this._fSalvageIntoSameDirectory; }
      set { this._fSalvageIntoSameDirectory = value; }
    }
    // 自己実行形式タイプのファイルか
    // Self-executable file
    private bool _fExecutableType = false;
    public bool fExecutableType
    {
      get { return this._fExecutableType; }
    }
    // ファイルのハッシュ値チェックを無視する
    // ignore checking hash data of files
    private bool _fSalvageIgnoreHashCheck = false;
    public bool fSalvageIgnoreHashCheck
    {
      get { return this._fSalvageIgnoreHashCheck; }
      set { this._fSalvageIgnoreHashCheck = value; }
    }
    // 公開鍵暗号（RSA暗号）によるデータ
    // RSA encrypted data
    private bool _fRsaEncryptionType = false;
    public bool fRsaEncryptionType
    {
      get { return this._fRsaEncryptionType; }
    }

    //----------------------------------------------------------------------
    // The return value of error ( ReadOnly)
    //----------------------------------------------------------------------
    // Input "Error code" for value
    private int _ReturnCode = 0;
    public int ReturnCode
    {
      get { return this._ReturnCode; }
    }
    // File path that caused the error
    private string _ErrorFilePath = "";
    public string ErrorFilePath
    {
      get { return this._ErrorFilePath; }
    }
    // Drive name to decrypt
    private string _DriveName = "";
    public string DriveName
    {
      get { return this._DriveName; }
    }
    // Total file number
    private Int64 _TotalFileNumber = 0;
    public Int64 TotalFileNumber
    {
      get { return this._TotalFileNumber; }
    }
    // Total file size of files to be decrypted
    private Int64 _TotalFileSize = -1;
    public Int64 TotalFileSize
    {
      get { return this._TotalFileSize; }
    }
    // Free space on the drive to decrypt the file
    private Int64 _AvailableFreeSpace = -1;
    public Int64 AvailableFreeSpace
    {
      get { return this._AvailableFreeSpace; }
    }
    // Error message by the exception
    private string _ErrorMessage = "";
    public string ErrorMessage
    {
      get { return this._ErrorMessage; }
    }
    private ArrayList _MessageList;
    public ArrayList MessageList
    {
      get { return this._MessageList; }
    }

    //----------------------------------------------------------------------
    // The plain text header data of encrypted file ( ReadOnly)
    //----------------------------------------------------------------------
    // Data Sub Version ( ver.2.00~ = "5", ver.2.70~ = "6" )
    private int _DataSebVersion = 0;
    public int DataSebVersion
    {
      get { return this._DataSebVersion; }
    }
    // The broken status of file
    private bool _fBroken = false;
    public bool fBroken
    {
      get { return this._fBroken; }
    }
    // Internal token string ( Whether or not the file is broken )  = "_AttacheCaseData" or "_Atc_Broken_Data"
    private string _TokenStr = "";
    public string TokenStr
    {
      get { return this._TokenStr; }
    }
    // Data File Version ( DATA_FILE_VERSION = 140 )
    private int _DataFileVersion = 0;
    public int DataFileVersion
    {
      get { return this._DataFileVersion; }
    }
    // TYPE_ALGORISM = 1:Rijndael	
    private int _TypeAlgorism = 0;
    public int TypeAlgorism
    {
      get { return this._TypeAlgorism; }
    }
    // The size of encrypted header data
    private int _AtcHeaderSize = 0;
    public int AtcHeaderSize
    {
      get { return this._AtcHeaderSize; }
    }

    // Just this ATC file to decrypt
    private string _AtcFilePath = "";
    public string AtcFilePath
    {
      get { return this._AtcFilePath; }
    }

    // App version
    private Int16 _AppVersion = 0;
    public Int16 AppVersion
    {
      get { return this._AppVersion; }
    }

    // The salt used in Rfc2898DeriveBytes
    private byte[] _salt = new byte[8];
    public byte[] salt
    {
      get { return this._salt; }
    }

    private Rfc2898DeriveBytes _deriveBytes;
    public Rfc2898DeriveBytes deriveBytes
    {
      get { return this._deriveBytes; }
    }

    // List of files to decrypt
    private List<string> _FileList;
    public List<string> FileList
    {
      get { return this._FileList; }
    }

    // Decryption time
    private string _DecryptionTimeString;
    public string DecryptionTimeString
    {
      get { return this._DecryptionTimeString; }
    }

    // Guid
    private string _GuidString;
    public string GuidString
    {
      get { return this._GuidString; }
      set { this._GuidString = value; }
    }

    // RSA decryption private key XML string
    private string _RsaPrivateKeyXmlString;
    public string RsaPrivateKeyXmlString
    {
      get { return this._RsaPrivateKeyXmlString; }
      set
      {
        this._RsaPrivateKeyXmlString = value;
        this._fRsaEncryption = true;
      }
    }
    // RSA Decryption
    private bool _fRsaEncryption = false;
    public bool fRsaEncryption
    {
      get { return this._fRsaEncryption; }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="FilePath"></param>
    public FileDecrypt4(string FilePath)
    {
      _AtcFilePath = FilePath;

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
        using (FileStream fs = new FileStream(_AtcFilePath, FileMode.Open, FileAccess.Read))
        {
          bool fToken = false;
          int b;
          while ((b = fs.ReadByte()) > -1)
          {
            //-----------------------------------
            // Check the token "_AttacheCaseData"
            if (b == AtcTokenByte[0])
            {
              fToken = true;
              for (int i = 1; i < AtcTokenByte.Length; i++)
              {
                if (fs.ReadByte() != AtcTokenByte[i])
                {
                  fToken = false;
                  break;
                }
              }
              if (fToken == true)
              {
                if (fs.Position > 20)
                { // Self executabel file
                  _fExecutableType = true;
                  _ExeOutSize = fs.Position - 20;
                }
                break;
              }
            }

            //-----------------------------------
            // Check the token "_Atc_Broken_Data"
            if (fToken == false && b == AtcBrokenTokenByte[0])
            {
              fToken = true;
              for (int i = 1; i < AtcBrokenTokenByte.Length; i++)
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
                { // Self executabel file
                  _fExecutableType = true;
                  _fBroken = true;
                  _ExeOutSize = fs.Position - 20;
                }
                break;
              }
            }

            //-----------------------------------
            if (fToken == true)
            {
              break;
            }
            //-----------------------------------

          }// end while();

        }// end using (FileStream fs = new FileStream(_AtcFilePath, FileMode.Open, FileAccess.Read));

        byte[] byteArray;

        using (FileStream fs = new FileStream(_AtcFilePath, FileMode.Open, FileAccess.Read))
        {
          if (fs.Length < 16)
          {
            _ReturnCode = NOT_ATC_DATA;
            _ErrorFilePath = _AtcFilePath;
            return;
          }

          if (_fExecutableType == true)
          {
            fs.Seek(_ExeOutSize, SeekOrigin.Begin);
          }

          // Plain text header
          byteArray = new byte[2];
          fs.Read(byteArray, 0, 2);
          _AppVersion = BitConverter.ToInt16(byteArray, 0);      // AppVersion

          byteArray = new byte[1];
          fs.Read(byteArray, 0, 1);
          _MissTypeLimits = (char)byteArray[0];                  // MissTypeLimits

          byteArray = new byte[1];
          fs.Read(byteArray, 0, 1);
          _fBroken = BitConverter.ToBoolean(byteArray, 0);       // fBroken

          byteArray = new byte[16];
          fs.Read(byteArray, 0, 16);
          _TokenStr = Encoding.ASCII.GetString(byteArray);       // TokenStr（"_AttacheCaseData" or "_Atc_Broken_Data"）

          byteArray = new byte[4];
          fs.Read(byteArray, 0, 4);
          _DataFileVersion = BitConverter.ToInt32(byteArray, 0); // DATA_FILE_VERSION = 140

          byteArray = new byte[4];
          fs.Read(byteArray, 0, 4);
          _AtcHeaderSize = BitConverter.ToInt32(byteArray, 0);   // AtcHeaderSize ( encrypted header data size )

          fs.Read(GuidData, 0, 16);                              // GUID

          // salt
          fs.Read(_salt, 0, 8);
#if (DEBUG)
          Console.WriteLine("salt: " + BitConverter.ToString(_salt));
#endif
          // RSA encryption
          if (_TokenStr.Trim() == "_AttacheCase_Rsa")
          {
            fs.Read(RsaEncryptedPassword, 0, 256);               // Encrypted 
          }

#if (DEBUG)
          //System.Windows.Forms.MessageBox.Show("_TokenStr: " + _TokenStr);
#endif

        } // end using (FileStream fs = new FileStream(_AtcFilePath, FileMode.Open, FileAccess.Read));

        _ReturnCode = DECRYPT_SUCCEEDED;
        return;

      }
      catch (UnauthorizedAccessException)
      {
        //オペレーティング システムが I/O エラーまたは特定の種類のセキュリティエラーのためにアクセスを拒否する場合、スローされる例外
        //The exception that is thrown when the operating system denies access
        //because of an I/O error or a specific type of security error.
        _ReturnCode = OS_DENIES_ACCESS;
        _ErrorFilePath = _AtcFilePath;
        return;
      }
      catch (DirectoryNotFoundException ex)
      {
        //ファイルまたはディレクトリの一部が見つからない場合にスローされる例外
        //The exception that is thrown when part of a file or directory cannot be found
        _ReturnCode = DIRECTORY_NOT_FOUND;
        _ErrorMessage = ex.Message;
        return;
      }
      catch (DriveNotFoundException ex)
      {
        //使用できないドライブまたは共有にアクセスしようとするとスローされる例外
        //The exception that is thrown when trying to access a drive or share that is not available
        _ReturnCode = DRIVE_NOT_FOUND;
        _ErrorMessage = ex.Message;
        return;
      }
      catch (FileLoadException ex)
      {
        //マネージド アセンブリが見つかったが、読み込むことができない場合にスローされる例外
        //The exception that is thrown when a managed assembly is found but cannot be loaded
        _ReturnCode = FILE_NOT_LOADED;
        _ErrorFilePath = ex.FileName;
        return;
      }
      catch (FileNotFoundException ex)
      {
        //ディスク上に存在しないファイルにアクセスしようとして失敗したときにスローされる例外
        //The exception that is thrown when an attempt to access a file that does not exist on disk fails
        _ReturnCode = FILE_NOT_FOUND;
        _ErrorFilePath = ex.FileName;
        return;
      }
      catch (PathTooLongException)
      {
        //パス名または完全修飾ファイル名がシステム定義の最大長を超えている場合にスローされる例外
        //The exception that is thrown when a path or fully qualified file name is longer than the system-defined maximum length
        _ReturnCode = PATH_TOO_LONG;
        return;
      }
      catch (IOException ex)
      {
        //I/Oエラーが発生したときにスローされる例外。現在の例外を説明するメッセージを取得します。
        //The exception that is thrown when an I/O error occurs. Gets a message that describes the current exception.
        _ReturnCode = IO_EXCEPTION;
        _ErrorMessage = ex.Message;
        return;
      }
      catch (Exception ex)
      {
        _ReturnCode = ERROR_UNEXPECTED;
        _ErrorMessage = ex.Message;
        return;
      }

    } // end public FileDecrypt4(string FilePath);
      //----------------------------------------------------------------------

    /// <summary>
    /// The encrypted file by AES to the original file or folder by user's password.
    /// ユーザーが設定したパスワードによって、AESによって暗号化されたファイルを元のファイル、またはフォルダーに復号して戻す。
    /// </summary>
    /// <param name="FilePath">File path or directory path is encrypted</param>
    /// <param name="OutFileDir">The directory of outputing encryption file.</param>
    /// <param name="Password">Encription password string</param>
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
      Action<int, string> dialog)
    {
#endif
      string LastWriteDateTimeString;
      string CreationDateTimeString;

      BackgroundWorker worker = sender as BackgroundWorker;

      //-----------------------------------
      // Header data is starting.
      // Progress event handler
      ArrayList MessageList = new ArrayList();
      MessageList.Add(READY_FOR_DECRYPT);
      MessageList.Add(Path.GetFileName(FilePath));

      worker.ReportProgress(0, MessageList);

      Stopwatch swDecrypt = new Stopwatch();
      Stopwatch swProgress = new Stopwatch();

      swDecrypt.Start();
      swProgress.Start();
      float percent = 0;

      int len = 0;
      byte[] byteArray;
      int RsaBlock = 0;

      // _FileList = new List<string>();
      // Dictionary<int, FileListData> dic = new Dictionary<int, FileListData>();
      List<FileData> FileDataList = new List<FileData>();

      if (_TokenStr.Trim() == "_AttacheCaseData")
      {
        // Atc data
        RsaBlock = 0;
      }
#if (EXEOUT)
#else
      else if (_TokenStr.Trim() == "_AttacheCase_Rsa")
      {
        XElement xmlElement = XElement.Parse(_RsaPrivateKeyXmlString);
        string GuidString = xmlElement.Element("id").Value;
        Guid guid = Guid.Parse(GuidString);
        if (GuidData.SequenceEqual(guid.ToByteArray()) == false)
        {
          _ReturnCode = RSA_KEY_GUID_NOT_MATCH;
          _ErrorFilePath = _RsaPrivateKeyXmlString;
          return (false);
        }

        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048);
        rsa.FromXmlString(_RsaPrivateKeyXmlString); //秘密鍵を指定
        byte[] RsaPasswordData = rsa.Decrypt(RsaEncryptedPassword, RSAEncryptionPadding.OaepSHA1);
        //string debugString = BitConverter.ToString(RsaPasswordData);
        //Console.WriteLine(debugString);
        // RSA decrypted password binary data
        PasswordBinary = RsaPasswordData;
        RsaBlock = 256;
      }
#endif
      else if (_TokenStr.Trim() == "_Atc_Broken_Data")
      {
        // Atc file is broken
        _ReturnCode = ATC_BROKEN_DATA;
        _ErrorFilePath = FilePath;
        RsaBlock = 0;
        return (false);
      }
      else
      {
        // not AttacheCase data
        _ReturnCode = NOT_ATC_DATA;
        _ErrorFilePath = FilePath;
        RsaBlock = 0;
        return (false);
      }

      //Rfc2898DeriveBytes _deriveBytes;
      if (PasswordBinary == null)
      {
        _deriveBytes = new Rfc2898DeriveBytes(Password, _salt, 1000);
      }
      else
      {
        _deriveBytes = new Rfc2898DeriveBytes(PasswordBinary, _salt, 1000);
      }

      byte[] key = _deriveBytes.GetBytes(32);
      byte[] iv = _deriveBytes.GetBytes(16);

#if (DEBUG)
      string debugString = BitConverter.ToString(_salt);
      Console.WriteLine("salt: " + debugString);

      debugString = BitConverter.ToString(key);
      Console.WriteLine("key: " + debugString);

      debugString = BitConverter.ToString(iv);
      Console.WriteLine("iv: " + debugString);
#endif

      try
      {
        using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          if (fs.Length < 16)
          {
            // not AttacheCase data
            _ReturnCode = NOT_ATC_DATA;
            _ErrorFilePath = FilePath;
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

          using (MemoryStream ms = new MemoryStream())
          {
            // The Header of MemoryStream is encrypted
            using (AesManaged aes = new AesManaged())
            {
              aes.BlockSize = 128;             // BlockSize = 16 bytes
              aes.KeySize = 256;               // KeySize = 32 bytes
              aes.Mode = CipherMode.CBC;       // CBC mode
              aes.Padding = PaddingMode.Zeros; // Padding mode is "PKCS7".

              aes.Key = key;
              aes.IV = iv;

              // Decryption interface.
              ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
              using (CryptoStream cse = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
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
            string Token = Encoding.UTF8.GetString(byteArray);
            if (Token.IndexOf(ATC_ENCRYPTED_TOKEN) > -1)
            {
              // Decryption is succeeded.
            }
            else
            {
              // Token is not match ( Password is not correct )
              _ReturnCode = PASSWORD_TOKEN_NOT_FOUND;
              _ErrorFilePath = FilePath;
              return (false);
            }

            _TotalFileSize = 0;
            Int16 FileNameSize = 0;

            string ParentFolder = "";
            bool fDirectoryTraversal = false;
            string InvalidFilePath = "";

            Int64 FileNum = 0;

            while (true) {

              byteArray = new byte[2];

              if (ms.Read(byteArray, 0, 2) < 2 )
              {
                break;
              }

              // FileNameSize
              FileData fd = new FileData();
              FileNameSize = BitConverter.ToInt16(byteArray, 0);
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
             if (_fNoParentFolder == true)
             {
                // root directory
                if (FileNum == 0)
                {
                  ParentFolder = fd.FilePath;
                }
                // not root directroy ( files )
                else
                {
                  StringBuilder sb = new StringBuilder(fd.FilePath);
                  len = ParentFolder.Length;
                  fd.FilePath = sb.Replace(ParentFolder, "", 0, len).ToString();
                }
              }

              //-----------------------------------
              // File path
              //
              string OutFilePath = "";
              if (Path.IsPathRooted(fd.FilePath) == true)
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
              string[] FilePathSplits = fd.FilePath.Split(':');
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
              if (fDirectoryTraversal == true)
              {
                _ReturnCode = INVALID_FILE_PATH;
                _ErrorFilePath = InvalidFilePath;
                return (false);
              }

              //-----------------------------------
              // FileSize
              byteArray = new byte[8];
              ms.Read(byteArray, 0, 8);
              fd.FileSize = BitConverter.ToInt64(byteArray, 0);
              _TotalFileSize += fd.FileSize;

              // File Attribute
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              fd.FileAttribute = BitConverter.ToInt32(byteArray, 0);

              // Last write DateTime;
              TimeZoneInfo tzi = TimeZoneInfo.Local;  // UTC to Local time
                  
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              LastWriteDateTimeString = BitConverter.ToInt32(byteArray, 0).ToString("0000/00/00");
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              LastWriteDateTimeString = LastWriteDateTimeString + BitConverter.ToInt32(byteArray, 0).ToString(" 00:00:00");
              DateTime.TryParse(LastWriteDateTimeString, out fd.LastWriteDateTime);
              fd.LastWriteDateTime = TimeZoneInfo.ConvertTimeFromUtc(fd.LastWriteDateTime, tzi);

              // Creation DateTime
              byteArray = new byte[4];
              ms.Read(byteArray, 0, 4);
              CreationDateTimeString = BitConverter.ToInt32(byteArray, 0).ToString("0000/00/00");
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
        string DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        using (var sw = new StreamWriter(Path.Combine(DesktopDir, "_decrypt_header.txt"), false, Encoding.UTF8))
        {
            for (int i = 0; i < FileDataList.Count; i++)
            {
                string OneLine = i.ToString() + "\t";
                OneLine += FileDataList[i].FilePath + "\t";
                OneLine += FileDataList[i].FileSize.ToString() + "\t";
                OneLine += FileDataList[i].FileAttribute.ToString() + "\t";
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
        string RootDriveLetter = Path.GetPathRoot(OutDirPath).Substring(0, 1);

        if (RootDriveLetter == "\\")
        {
          // Network
        }
        else
        {
          DriveInfo drive = new DriveInfo(RootDriveLetter);

          DriveType driveType = drive.DriveType;
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
              if (drive.IsReady == false || drive.AvailableFreeSpace < _TotalFileSize)
              {
                _ReturnCode = NO_DISK_SPACE;
                _DriveName = drive.ToString();
                //_TotalFileSize = _TotalFileSize;
                _AvailableFreeSpace = drive.AvailableFreeSpace;
                return (false);
              }
              break;
          }
        }

        //-----------------------------------
        // Decrypt file main data.
        //-----------------------------------
        using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
          //-----------------------------------
          // Adjust the header data in 16 bytes ( Block size )
          int mod = _AtcHeaderSize % 16;
          if (_fExecutableType == true)
          {
            fs.Seek(_ExeOutSize + 52 + RsaBlock + _AtcHeaderSize + 16 - mod, SeekOrigin.Begin);
          }
          else
          {
            fs.Seek(52 + RsaBlock + _AtcHeaderSize + 16 - mod, SeekOrigin.Begin);
          }

          //-----------------------------------
          // Decyption
          using (AesManaged aes = new AesManaged())
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
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (CryptoStream cse = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
            {
              using (DeflateStream ds = new DeflateStream(cse, CompressionMode.Decompress))
              {

                FileStream outfs = null;
                Int64 FileSize = 0;
                int FileIndex = 0;

                bool fSkip = false;

                if (_fNoParentFolder == true)
                {
                  if (FileDataList[0].FilePath.EndsWith("\\") == true || FileDataList[0].FilePath.EndsWith("/") == true)
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

                  int buffer_size = len;

                  while (len > 0)
                  {
                    //----------------------------------------------------------------------
                    // 書き込み中のファイルまたはフォルダが無い場合は作る
                    // Create them if there is no writing file or folder.
                    //----------------------------------------------------------------------
                    if (outfs == null)
                    {
                      //-----------------------------------
                      // Create file or dirctories.
                      if (FileIndex > FileDataList.Count - 1)
                      {
                        _ReturnCode = DECRYPT_SUCCEEDED;
                        return (true);
                      }
                      else
                      {
                        //-----------------------------------
                        // Create directory
                        //-----------------------------------
                        if (FileDataList[FileIndex].FilePath.EndsWith("\\") == true ||
                            FileDataList[FileIndex].FilePath.EndsWith("/") == true)
                        {
                          string path = Path.Combine(OutDirPath, FileDataList[FileIndex].FilePath);
                          DirectoryInfo di = new DirectoryInfo(path);

                          // File already exists.
                          if (Directory.Exists(path) == true)
                          {
                            // Temporary option for overwriting
                            // private const int USER_CANCELED  = -1;
                            // private const int OVERWRITE      = 1;
                            // private const int OVERWRITE_ALL  = 2;
                            // private const int KEEP_NEWER     = 3;
                            // private const int KEEP_NEWER_ALL = 4;
                            // private const int SKIP           = 5;
                            // private const int SKIP_ALL       = 6;
                            if (_TempOverWriteOption == OVERWRITE_ALL)
                            {
                              // Overwrite ( New create )
                            }
                            else if (_TempOverWriteOption == SKIP_ALL)
                            {
                              fSkip = true;
                            }
                            else if (_TempOverWriteOption == KEEP_NEWER_ALL)
                            {
                              if (di.LastWriteTime > FileDataList[FileIndex].LastWriteDateTime)
                              {
                                fSkip = true; // old directory
                              }
                            }
                            else
                            {

                              // Show dialog of comfirming to overwrite. 
                              dialog(0, path);

                              // Cancel
                              if (_TempOverWriteOption == USER_CANCELED)
                              {
                                _ReturnCode = USER_CANCELED;
                                return (false);
                              }
                              else if (_TempOverWriteOption == OVERWRITE || _TempOverWriteOption == OVERWRITE_ALL)
                              {
                                // Overwrite ( New create )
                              }
                              // Skip, or Skip All
                              else if (_TempOverWriteOption == SKIP_ALL)
                              {
                                fSkip = true;
                                _ReturnCode = DECRYPT_SUCCEEDED;
                                return (true);
                              }
                              else if (_TempOverWriteOption == SKIP)
                              {
                                fSkip = true;
                              }
                              else if (_TempOverWriteOption == KEEP_NEWER || _TempOverWriteOption == KEEP_NEWER_ALL)
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
                          _OutputFileList.Add(FileDataList[FileIndex].FilePath);
                          FileSize = 0;
                          FileIndex++;

                          if (FileIndex > FileDataList.Count - 1)
                          {
                            _ReturnCode = DECRYPT_SUCCEEDED;
                            return (true);
                          }

                          continue;

                        }
                        //-----------------------------------
                        // Create file
                        //-----------------------------------
                        else
                        {
                          string path = Path.Combine(OutDirPath, FileDataList[FileIndex].FilePath);
                          FileInfo fi = new FileInfo(path);

                          // File already exists.
                          if (File.Exists(path) == true)
                          {
                            // Salvage Data Mode
                            if (_fSalvageIntoSameDirectory == true)
                            {
                              int SerialNum = 0;
                              while (File.Exists(path) == true)
                              {
                                path = getFileNameWithSerialNumber(path, SerialNum);
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
                              if (_TempOverWriteOption == OVERWRITE_ALL)
                              {
                                // Overwrite ( New create )
                              }
                              else if (_TempOverWriteOption == SKIP_ALL)
                              {
                                fSkip = true;
                              }
                              else if (_TempOverWriteOption == KEEP_NEWER_ALL)
                              {
                                if (fi.LastWriteTime > FileDataList[FileIndex].LastWriteDateTime)
                                {
                                  fSkip = true;
                                }
                              }
                              else
                              {
                                // Show dialog of comfirming to overwrite. 
                                dialog(1, path);

                                // Cancel
                                if (_TempOverWriteOption == USER_CANCELED)
                                {
                                  _ReturnCode = USER_CANCELED;
                                  return (false);
                                }
                                else if (_TempOverWriteOption == OVERWRITE || _TempOverWriteOption == OVERWRITE_ALL)
                                {
                                  // Overwrite ( New create )
                                }
                                // Skip, or Skip All
                                else if (_TempOverWriteOption == SKIP || _TempOverWriteOption == SKIP_ALL)
                                {
                                  fSkip = true;
                                }
                                else if (_TempOverWriteOption == KEEP_NEWER || _TempOverWriteOption == KEEP_NEWER_ALL)
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
                                File.SetAttributes(path, FileAttributes.Normal);
                              }

                            }

                          }// end if ( File.Exists );

                          // Salvage data mode
                          // サルベージ・モード
                          if (_fSalvageToCreateParentFolderOneByOne == true)
                          {
                            // Decrypt one by one while creating the parent folder.
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                          }

                          if (fSkip == true)
                          {
                            // Not create file
                          }
                          else
                          {
                            try
                            {
                              outfs = new FileStream(path, FileMode.Create, FileAccess.Write);
                            }
                            catch
                            {
                              // フォルダが通っていない場合は例外が発生するので親フォルダーを作成して改めてファイルを開く
                              // If there is no parent folders, an exception will occur, so create a parent folders and open the file again
                              FileInfo fileInfo = new FileInfo(path);
                              if (!fileInfo.Directory.Exists)
                              {
                                fileInfo.Directory.Create();
                              }
                              outfs = new FileStream(path, FileMode.Create, FileAccess.Write);

                            }
                          }

                          _OutputFileList.Add(path);
                          FileSize = 0;

                        }

                      }

                    }// end if (outfs == null);

                    //----------------------------------------------------------------------
                    // Write data
                    //----------------------------------------------------------------------
                    if (FileSize + len < FileDataList[FileIndex].FileSize)
                    {
                      if (outfs != null || fSkip == true)
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
                      int rest = (int)(FileDataList[FileIndex].FileSize - FileSize);

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
                        FileInfo fi = new FileInfo(FileDataList[FileIndex].FilePath);

                        // タイムスタンプの復元
                        // Restore the timestamp of a file
                        fi.CreationTime = (DateTime)FileDataList[FileIndex].CreationDateTime;
                        fi.LastWriteTime = (DateTime)FileDataList[FileIndex].LastWriteDateTime;

                        // ファイル属性の復元
                        // Restore file attribute.
                        fi.Attributes = (FileAttributes)FileDataList[FileIndex].FileAttribute;

                        // ハッシュ値のチェック
                        // Check the hash of a file
                        if (_fSalvageIgnoreHashCheck == false && FileSize > 0)
                        {
                          byte[] hash = getMd5Hash(FileDataList[FileIndex].FilePath);
                          if (System.Linq.Enumerable.SequenceEqual(hash, FileDataList[FileIndex].Hash) == false )
                          {
                            _ReturnCode = NOT_CORRECT_HASH_VALUE;
                            _ErrorFilePath = FileDataList[FileIndex].FilePath;
                            return (false);
                          }
                        }

                      }

                      FileSize = 0;
                      FileIndex++;

                      fSkip = false;

                      if (FileIndex > FileDataList.Count - 1)
                      {
                        _ReturnCode = DECRYPT_SUCCEEDED;
                        return (true);
                      }

                    }
                    //----------------------------------------------------------------------
                    //進捗の表示
                    string MessageText = "";
                    if (_TotalNumberOfFiles > 1)
                    {
                      MessageText = FilePath + " ( " + _NumberOfFiles.ToString() + "/" + _TotalNumberOfFiles.ToString() + " files" + " )";
                    }
                    else
                    {
                      MessageText = FilePath;
                    }

                    _MessageList = new ArrayList();
                    _MessageList.Add(DECRYPTING);
                    _MessageList.Add(MessageText);

                    // プログレスバーの更新間隔を100msに調整
                    if (swProgress.ElapsedMilliseconds > 100)
                    {
                      percent = ((float)_TotalSize / _TotalFileSize);
                      worker.ReportProgress((int)(percent * 10000), _MessageList);
                      swProgress.Restart();
                    }

                    // User cancel
                    if (worker.CancellationPending == true)
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
        _ReturnCode = OS_DENIES_ACCESS;
        return (false);
      }
      catch (DirectoryNotFoundException ex)
      {
        //ファイルまたはディレクトリの一部が見つからない場合にスローされる例外
        //The exception that is thrown when part of a file or directory cannot be found
        _ReturnCode = DIRECTORY_NOT_FOUND;
        _ErrorMessage = ex.Message;
        return (false);
      }
      catch (DriveNotFoundException ex)
      {
        //使用できないドライブまたは共有にアクセスしようとするとスローされる例外
        //The exception that is thrown when trying to access a drive or share that is not available
        _ReturnCode = DRIVE_NOT_FOUND;
        _ErrorMessage = ex.Message;
        return (false);
      }
      catch (FileLoadException ex)
      {
        //マネージド アセンブリが見つかったが、読み込むことができない場合にスローされる例外
        //The exception that is thrown when a managed assembly is found but cannot be loaded
        _ReturnCode = FILE_NOT_LOADED;
        _ErrorFilePath = ex.FileName;
        return (false);
      }
      catch (FileNotFoundException ex)
      {
        //ディスク上に存在しないファイルにアクセスしようとして失敗したときにスローされる例外
        //The exception that is thrown when an attempt to access a file that does not exist on disk fails
        _ReturnCode = FILE_NOT_FOUND;
        _ErrorFilePath = ex.FileName;
        return (false);
      }
      catch (PathTooLongException)
      {
        //パス名または完全修飾ファイル名がシステム定義の最大長を超えている場合にスローされる例外
        //The exception that is thrown when a path or fully qualified file name is longer than the system-defined maximum length
        _ReturnCode = PATH_TOO_LONG;
        return (false);
      }
      catch (IOException ex)
      {
        //I/Oエラーが発生したときにスローされる例外。現在の例外を説明するメッセージを取得します。
        //The exception that is thrown when an I/O error occurs. Gets a message that describes the current exception.
        _ReturnCode = IO_EXCEPTION;
        _ErrorMessage = ex.Message;
        return (false);
      }
      catch (Exception ex)
      {
        // ユーザーキャンセルを行うタイミングで例外が発生してしまうため、エラーコードはユーザーキャンセルをそのまま返す。
        // The error code returns the user cancel as it is because the exception occurs at the time of user cancel.
        if (_ReturnCode == USER_CANCELED)
        {
          _ReturnCode = USER_CANCELED;
          _ErrorMessage = "";
          return (false);
        }
        else if (_ReturnCode == DECRYPT_SUCCEEDED)
        {
          _ReturnCode = DECRYPT_SUCCEEDED;
          return (true);
        }

        _ReturnCode = IO_EXCEPTION;
        _ErrorMessage = ex.Message;
        return (false);
      }
      finally
      {
        swProgress.Stop();
        swDecrypt.Stop();
        // 計測時間
        TimeSpan ts = swDecrypt.Elapsed;
        _DecryptionTimeString = 
          Convert.ToString(ts.Hours) + "h" + Convert.ToString(ts.Minutes) + "m" + 
          Convert.ToString(ts.Seconds) + "s" +Convert.ToString(ts.Milliseconds) + "ms";
      }

    }// end Decrypt();

    /// ファイル名に連番を振る
    /// Put a serial number to the file name
    private string getFileNameWithSerialNumber(string FilePath, int SerialNum)
    {
      string DirPath = Path.GetDirectoryName(FilePath);
      string FileName = Path.GetFileNameWithoutExtension(FilePath) + SerialNum.ToString("0000") + Path.GetExtension(FilePath);

      return Path.Combine(DirPath, FileName);

    }

    /// Get a check sum (MD5) to calculate
    private static byte[] getMd5Hash(string FilePath)
    {
      byte[] bs;
      using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        //MD5CryptoServiceProviderオブジェクト
        MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
        bs = md5.ComputeHash(fs);
        md5.Clear();
      }
      return (bs);
    }


  }//end class FileDecrypt2;

}//end namespace AttacheCase;
