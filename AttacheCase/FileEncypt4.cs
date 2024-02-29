//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttachéCase4 )" -- File encryption software.
// Copyright (C) 2016-2024  Mitsuhiro Hibara
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
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
#if __MACOS__
using AppKit;
#endif

namespace AttacheCase
{
  public partial class FileEncrypt4
  {
    // Status code
    private const int ENCRYPT_SUCCEEDED  = 1; // Encrypt is succeeded.
    private const int DECRYPT_SUCCEEDED  = 2; // Decrypt is succeeded.
    private const int DELETE_SUCCEEDED   = 3; // Delete is succeeded.
    private const int READY_FOR_ENCRYPT  = 4; // Getting ready for encryption or decryption.
    private const int READY_FOR_DECRYPT  = 5; // Getting ready for encryption or decryption.
    private const int ENCRYPTING         = 6; // Ecrypting.
    private const int DECRYPTING         = 7; // Decrypting.
    private const int DELETING           = 8; // Deleting.

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

    private byte[] buffer;
    private const int BUFFER_SIZE = 4096;

    // Header data variables
    private static bool fBrocken = false;
    private const string STRING_TOKEN_NORMAL = "_AttacheCaseData";
    private const string STRING_TOKEN_BROKEN = "_Atc_Broken_Data";
    private const string STRING_TOKEN_RSA    = "_AttacheCase_Rsa";
    private const int DATA_FILE_VERSION = 140;  // ver.4
    private const string ATC_ENCRYPTED_TOKEN = "atc4";

    //Encrypted header data size
    private int _AtcHeaderSize = 0;
    private Int64 _TotalSize = 0;
    //private Int64 _TotalFileSize = 0;
    private Int64 _StartPos = 0;

#if __MACOS__
    private bool _fCancel = false;
    public bool fCancel
    {
        get { return this._fCancel; }
        set { this._fCancel = value; }
    }
#endif

    // The number of files or folders to be encrypted
    private int _NumberOfFiles = 0;
    public int NumberOfFiles
    {
      get { return this._NumberOfFiles; }
      set { this._NumberOfFiles = value; }
    }
    // Total number of files or folders to be encrypted
    private int _TotalNumberOfFiles = 1;
    public int TotalNumberOfFiles
    {
      get { return this._TotalNumberOfFiles; }
      set { this._TotalNumberOfFiles = value; }
    }
    // Set number of times to input password in encrypt files:
    private char _MissTypeLimits = (char)3;
    public char MissTypeLimits
    {
      get { return this._MissTypeLimits; }
      set { this._MissTypeLimits = value; }
    }
    // Self-executable file
    private bool _fExecutable = false;
    public bool fExecutable
    {
      get { return this._fExecutable; }
      set { this._fExecutable = value; }
    }
    // .NET Framework Version of Self-executable file  
    private string _ExeToolVersionString = "4.6.2";
    public string ExeToolVersionString
    {
      get { return this._ExeToolVersionString; }
      set { this._ExeToolVersionString = value; }
    }

    // Set the timestamp of encryption file to original files or directories
    private bool _fKeepTimeStamp = false;
    public bool fKeepTimeStamp
    {
      get { return this._fKeepTimeStamp; }
      set { this._fKeepTimeStamp = value; }
    }
    // Set the Compression Option ( 0: Optimal, 1: Fastest, 2: NoCompression)
    private int _CompressionOption = 0;
    public int CompressionOption
    {
      get { return this._CompressionOption; }
      set { this._CompressionOption = value; }
    }

    // ATC file ( encrypted file name ) path to output
    private string _AtcFilePath = "";
    public string AtcFilePath
    {
      get { return this._AtcFilePath; }
    }
    // List of files and folders for encryption
    private List<string> _FileList;
    public List<string> FileList
    {
      get { return this._FileList; }
    }

    // Encryption time
    private string _EncryptionTimeString;
    public string EncryptionTimeString
    {
      get { return this._EncryptionTimeString; }
    }

    // Guid
    private string _GuidString;
    public string GuidString
    {
      get { return this._GuidString; }
    }

    // RSA Encryption public key XML string
    private string _RsaPublicKeyXmlString;
    public string RsaPublicKeyXmlString
    {
      get { return this._RsaPublicKeyXmlString; }
      set { 
        this._RsaPublicKeyXmlString = value;
        this._fRsaEncryption = true;
      }
    }
    // RSA Encryption
    private bool _fRsaEncryption = false;
    public bool fRsaEncryption
    {
      get { return this._fRsaEncryption; }
    }

    //----------------------------------------------------------------------
    // The return value of error ( ReadOnly)
    //----------------------------------------------------------------------
    // Input "Error code" for value
    private int _ReturnCode = -1;
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
    // Total file size of files to be encrypted
    private Int64 _TotalFileSize = 0;
    public Int64 TotalFileSize
    {
      get { return this._TotalFileSize; }
    }
    // Free space on the drive to encrypt the file
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

    // Constructor
    public FileEncrypt4()
    {
    }

    /// <summary>
    /// Multiple files or directories is encrypted by AES (exactly Rijndael) to use password string.
    /// 複数のファイル、またはディレクトリをAES（正確にはRijndael）を使って指定のパスワードで暗号化する
    /// </summary>
    /// <param name="FilePath">File path or directory path is encrypted</param>
    /// <param name="OutFilePath">Output encryption file name</param>
    /// <param name="Password">Encription password string</param>
    /// <returns>Encryption success(true) or failed(false)</returns>
    public bool Encrypt(
      object sender, DoWorkEventArgs e,
      string[] FilePaths, string OutFilePath, string Password, byte[] PasswordBinary, 
      string NewArchiveName, CompressionLevel compressionLevel)
    {

#if (DEBUG)
      Logger lg = new Logger();
      lg.Info("-----------------------------------");
      lg.Info(OutFilePath);
      lg.Info("Encryotion satrt.");
      lg.StopWatchStart();
#endif

      _AtcFilePath = OutFilePath;

      BackgroundWorker worker = sender as BackgroundWorker;
      // The timestamp of original file
      DateTime dtCreate = File.GetCreationTime(FilePaths[0]);
      DateTime dtUpdate = File.GetLastWriteTime(FilePaths[0]);
      DateTime dtAccess = File.GetLastAccessTime(FilePaths[0]);

      // Create Header data.
      ArrayList MessageList = new ArrayList
      {
        READY_FOR_ENCRYPT,
        Path.GetFileName(_AtcFilePath)
      };

      worker.ReportProgress(0, MessageList);

      // Stopwatch for measuring time and adjusting the progress bar display
      Stopwatch swEncrypt = new Stopwatch();
      Stopwatch swProgress = new Stopwatch();
      swEncrypt.Start();
      swProgress.Start();

      float percent = 0;

      _FileList = new List<string>();
      byte[] byteArray = null;

      // RSA Encryption password
      var byteRsaPassword = new byte[32]; // Key size
      if (_fRsaEncryption == true)
      {
        using (var rng = new RNGCryptoServiceProvider())
        {
          rng.GetBytes(byteRsaPassword);
          PasswordBinary = byteRsaPassword;
        }
      }

      // Salt
      Rfc2898DeriveBytes deriveBytes;
      if(PasswordBinary == null)
      { // String Password
        deriveBytes = new Rfc2898DeriveBytes(Password, 8, 1000);
      }
      else
      { // Binary Password
        byte[] random_salt = new byte[8];
        RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        rng.GetBytes(random_salt);
        deriveBytes = new Rfc2898DeriveBytes(PasswordBinary, random_salt, 1000);
      }
      byte[] salt = deriveBytes.Salt;
      byte[] key = deriveBytes.GetBytes(32);
      byte[] iv = deriveBytes.GetBytes(16);

#if (DEBUG)
      string debugString = BitConverter.ToString(salt);
      Console.WriteLine("salt: " + debugString);
      debugString = BitConverter.ToString(key);
      Console.WriteLine("key: " + debugString);
      debugString = BitConverter.ToString(iv);
      Console.WriteLine("iv: " + debugString);
#endif

      try
      {
        using (FileStream outfs = new FileStream(_AtcFilePath, FileMode.Create, FileAccess.Write))
        {
          // 自己実行形式ファイル（Self-executable file）
          if (_fExecutable == true)
          {
            // public partial class FileEncrypt4
            // Read from ExeOut4.cs
            if (_ExeToolVersionString == "4.0")
            {
              ExeOutFileSize[0] = rawData[0].Length;
              outfs.Write(rawData[0], 0, ExeOutFileSize[0]);
            }
            else // 4.6.2
            {
              ExeOutFileSize[1] = rawData[1].Length;
              outfs.Write(rawData[1], 0, ExeOutFileSize[1]);
            }
          }

          _StartPos = outfs.Seek(0, SeekOrigin.End);

          // Application version
          Version ver = AppInfo.Version;
          Int16 vernum = Int16.Parse(ver.ToString().Replace(".", ""));
          byteArray = BitConverter.GetBytes(vernum);
          outfs.Write(byteArray, 0, 2);
          // Input password limit
          byteArray = BitConverter.GetBytes(_MissTypeLimits);
          outfs.Write(byteArray, 0, 1);
          // Exceed the password input limit, destroy the file?
          byteArray = BitConverter.GetBytes(fBrocken);
          outfs.Write(byteArray, 0, 1);
          // Token that this is the AttacheCase file
          if (fRsaEncryption == true)
          {
            byteArray = Encoding.ASCII.GetBytes(STRING_TOKEN_RSA);
          }
          else
          {
            byteArray = Encoding.ASCII.GetBytes(STRING_TOKEN_NORMAL);
          }
          outfs.Write(byteArray, 0, 16);
          // File sub version
          byteArray = BitConverter.GetBytes(DATA_FILE_VERSION);
          outfs.Write(byteArray, 0, 4);
          // The size of encrypted Atc header size ( reserved ) 
          byteArray = BitConverter.GetBytes((int)0);
          outfs.Write(byteArray, 0, 4);

          // GUID
          Guid guid;
          if (string.IsNullOrEmpty(RsaPublicKeyXmlString) == false)
          { // Read GUID binary from RSA public key string
            XElement xmlElement = XElement.Parse(RsaPublicKeyXmlString);
            string GuidString = xmlElement.Element("id").Value;
            guid = Guid.Parse(GuidString);
          }
          else
          {
            // New GUID
            guid = Guid.NewGuid();
          }
          outfs.Write(guid.ToByteArray(), 0, 16);

          // Salt
          outfs.Write(salt, 0, 8);

          // RSA encryption password
          if (_fRsaEncryption == true)
          {
            // パスワードを暗号化して書き込む
            //RSACryptoServiceProviderオブジェクトの作成
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048);
            rsa.FromXmlString(_RsaPublicKeyXmlString); //公開鍵を指定
           //byte[] outbuffer = new byte[214];  // 剰余サイズ(256bytes) -2 -2 * hLen(SHA-1) = 214 Max 
            //string debugString = BitConverter.ToString(byteRsaPassword);
            //Console.WriteLine(debugString);
            byte[] encryptedData = rsa.Encrypt(byteRsaPassword, RSAEncryptionPadding.OaepSHA1); //OAEPパディング=trueでRSA復号
            outfs.Write(encryptedData, 0, encryptedData.Length);  // 256 byte
          }

          // Cipher text header.
          using (MemoryStream ms = new MemoryStream())
          {
            // Token to refer to when decryption is successful
            byteArray = Encoding.ASCII.GetBytes(ATC_ENCRYPTED_TOKEN);
            ms.Write(byteArray, 0, 4);

            string ParentPath;
            List<string> DebugList = new List<string>();
            string OneLine = "";

            //----------------------------------------------------------------------
            // Put together files in one ( Save as the name ).
            // 複数ファイルを一つにまとめる（ファイルに名前をつけて保存）
            if (NewArchiveName != "")
            {
#if __MACOS__
              NewArchiveName = NewArchiveName + "/";
#else
              if (NewArchiveName.EndsWith("\\") == false)
              {
                NewArchiveName = NewArchiveName + "\\";
              }
#endif
              // File name length
              int FileLen = Encoding.UTF8.GetByteCount(NewArchiveName);
              byteArray = BitConverter.GetBytes((Int16)FileLen);
#if DEBUG
              OneLine += FileLen.ToString() + "\t";
#endif
              ms.Write(byteArray, 0, 2);
              
              // File name
              byteArray = Encoding.UTF8.GetBytes(NewArchiveName);
#if DEBUG
              OneLine += NewArchiveName + "\t";
#endif
              ms.Write(byteArray, 0, FileLen);
              
              // File size
              byteArray = BitConverter.GetBytes((Int64)0);
#if DEBUG
              OneLine += "0\t";
#endif
              ms.Write(byteArray, 0, 8);
              
              // File attribute
              byteArray = BitConverter.GetBytes((int)16);
#if DEBUG
              OneLine += BitConverter.ToInt16(byteArray, 0).ToString() + "\t";
#endif
              ms.Write(byteArray, 0, 4);

              string DateString = DateTime.UtcNow.ToString("yyyyMMdd");
              string TimeString = DateTime.UtcNow.ToString("HHmmss");
              // Last write date
              byteArray = BitConverter.GetBytes(int.Parse(DateString));
#if DEBUG
              OneLine += DateString + "\t";
#endif
              ms.Write(byteArray, 0, 4);
              // Last write time
              byteArray = BitConverter.GetBytes(int.Parse(TimeString));
#if DEBUG
              OneLine += TimeString + "\t";
#endif
              ms.Write(byteArray, 0, 4);

              // Creation date
              byteArray = BitConverter.GetBytes(int.Parse(DateString));
#if DEBUG
              OneLine += DateString + "\t";
#endif
              ms.Write(byteArray, 0, 4);
              // Creation time
              byteArray = BitConverter.GetBytes(int.Parse(TimeString));
#if DEBUG
              OneLine += TimeString;
#endif
              ms.Write(byteArray, 0, 4);
#if DEBUG
              DebugList.Add(OneLine);
#endif
            }

            //----------------------------------------------------------------------
            // When encrypt multiple files
            // 複数のファイルを暗号化する場合
            foreach (string FilePath in FilePaths)
            {
              ParentPath = Path.GetDirectoryName(FilePath);

#if __MACOS__
              if (ParentPath.EndsWith("/") == false)
              {
                ParentPath = ParentPath + "/";
              }

#else
              if (ParentPath == null)  // In case of 'C:\\' root direcroy.
              {
                ParentPath = FilePath;
              }

              if (ParentPath.EndsWith("\\") == false)
              {
                ParentPath = ParentPath + "\\";
              }

#endif

              if ((worker.CancellationPending == true))
              {
                e.Cancel = true;
                return (false);
              }

              //----------------------------------------------------------------------
              // 暗号化リストを生成（ファイル）
              // Create file to encrypt list ( File )
              //----------------------------------------------------------------------
              if (File.Exists(FilePath) == true)
              {
                ArrayList Items = GetFileInfo(ParentPath, FilePath);

                // File name length
                int FileLen = Encoding.UTF8.GetByteCount(NewArchiveName + Items[2]);
                byteArray = BitConverter.GetBytes((Int16)FileLen);
                ms.Write(byteArray, 0, 2);
#if DEBUG
                OneLine = FileLen.ToString() + "\t";
#endif

                // File name
                byteArray = Encoding.UTF8.GetBytes(NewArchiveName + Items[2]);
                ms.Write(byteArray, 0, FileLen);
#if DEBUG
                OneLine += NewArchiveName + Items[2] + "\t";
#endif

                // File size
                Int64 FileSize = Convert.ToInt64(Items[3]);
                byteArray = BitConverter.GetBytes(FileSize);
                ms.Write(byteArray, 0, 8);
#if DEBUG
                OneLine += FileSize.ToString() + "\t";
#endif
                // File attribute
                byteArray = BitConverter.GetBytes((int)Items[4]);
                ms.Write(byteArray, 0, 4);
#if DEBUG
                OneLine += ((int)Items[4]).ToString() + "\t";
#endif
                // Last write date (UTC)
                byteArray = BitConverter.GetBytes((int)Items[5]);
                ms.Write(byteArray, 0, 4);
#if DEBUG
                OneLine += ((int)Items[5]).ToString() + "\t";
#endif
                // Last write time (UTC)
                byteArray = BitConverter.GetBytes((int)Items[6]);
                ms.Write(byteArray, 0, 4);
#if DEBUG
                OneLine += ((int)Items[6]).ToString() + "\t";
#endif
                // Creation date (UTC)
                byteArray = BitConverter.GetBytes((int)Items[7]);
                ms.Write(byteArray, 0, 4);
#if DEBUG
                OneLine += ((int)Items[7]).ToString() + "\t";
#endif
                // Creation time (UTC)
                byteArray = BitConverter.GetBytes((int)Items[8]);
                ms.Write(byteArray, 0, 4);
#if DEBUG
                OneLine += ((int)Items[8]).ToString() + "\t";
#endif
                if (FileSize > 0)
                {
                  // Check sum (MD5)
                  byteArray = (byte[])Items[9];
                  ms.Write(byteArray, 0, 16);
                }
#if DEBUG
                OneLine += BitConverter.ToString((byte[])Items[9]).Replace("-", "");
                DebugList.Add(OneLine);   // デバッグ
#endif
                // Files list for encryption
                _FileList.Add(Items[1].ToString());  // Absolute file path
                // Total file size
                _TotalFileSize += FileSize;
              }
              //----------------------------------------------------------------------
              // 暗号化リストを生成（ディレクトリ）
              // Create file to encrypt list ( Directory )
              //----------------------------------------------------------------------
              else
              {
                // Directory
                // _FileList.Add(FilePath);  // No need to put it in the file list

                foreach (ArrayList Items in GetFileList(ParentPath, FilePath))
                {
                  if ((worker.CancellationPending == true))
                  {
                    e.Cancel = true;
                    return (false);
                  }

                  //-----------------------------------
                  // ディレクトリ
                  if ((int)Items[0] == 0)
                  {
                    // Directory name length
                    int FileLen = Encoding.UTF8.GetByteCount(NewArchiveName + Items[2]);
                    byteArray = BitConverter.GetBytes((Int16)FileLen);
                    ms.Write(byteArray, 0, 2);
#if DEBUG
                    OneLine = FileLen.ToString() + "\t";
#endif
                    // Directroy name
                    byteArray = Encoding.UTF8.GetBytes(NewArchiveName + Items[2]);
                    ms.Write(byteArray, 0, (int)FileLen);
#if DEBUG
                    OneLine += NewArchiveName + Items[2] + "\t";
#endif
                    // File size
                    Int64 FileSize = Convert.ToInt64(0);
                    byteArray = BitConverter.GetBytes(FileSize);
                    ms.Write(byteArray, 0, 8);
#if DEBUG
                    OneLine += FileSize.ToString() + "\t";
#endif
                    // File attribute
                    byteArray = BitConverter.GetBytes((int)Items[4]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[4]).ToString() + "\t";
#endif
                    // Last write date (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[5]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[5]).ToString() + "\t";
#endif
                    // Last write time (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[6]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[6]).ToString() + "\t";
#endif
                    // Creation date (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[7]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[7]).ToString() + "\t";
#endif
                    // Creation time (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[8]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[8]).ToString();
                    // Check sum (MD5)
                    // None

                    DebugList.Add(OneLine); // デバッグ
#endif

                    // Directory
                    //_FileList.Add(Item[1].ToString());  // Absolute file path
                  }
                  //-----------------------------------
                  // ファイル
                  else
                  {
                    // File name length
                    int FileLen = Encoding.UTF8.GetByteCount(NewArchiveName + Items[2]);
                    byteArray = BitConverter.GetBytes((Int16)FileLen);
                    ms.Write(byteArray, 0, 2);
#if DEBUG
                    OneLine = FileLen.ToString() + "\t";
#endif
                    // File name
                    byteArray = Encoding.UTF8.GetBytes(NewArchiveName + Items[2]);
                    ms.Write(byteArray, 0, FileLen);
#if DEBUG
                    OneLine += NewArchiveName + Items[2] + "\t";
#endif
                    // File size
                    Int64 FileSize = Convert.ToInt64((Int64)Items[3]);
                    byteArray = BitConverter.GetBytes(FileSize);
                    ms.Write(byteArray, 0, 8);
#if DEBUG
                    OneLine += FileSize.ToString() + "\t";
#endif
                    // File attribute
                    byteArray = BitConverter.GetBytes((int)Items[4]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[4]).ToString() + "\t";
#endif
                    // Last write date (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[5]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[5]).ToString() + "\t";
#endif
                    // Last write time (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[6]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[6]).ToString() + "\t";
#endif
                    // Creation date (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[7]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[7]).ToString() + "\t";
#endif
                    // Creation time (UTC)
                    byteArray = BitConverter.GetBytes((int)Items[8]);
                    ms.Write(byteArray, 0, 4);
#if DEBUG
                    OneLine += ((int)Items[8]).ToString() + "\t";
#endif
                    if (FileSize > 0)
                    {
                      // Check sum (MD5)
                      byteArray = (Byte[])Items[9];
                      ms.Write(byteArray, 0, 16);
                    }
#if DEBUG
                    OneLine += BitConverter.ToString((Byte[])Items[9]).Replace("-", "");
                    DebugList.Add(OneLine);
#endif
                    // files only ( Add Files list for encryption )
                    _FileList.Add(Items[1].ToString());  // Absolute file path
                    // Total file size
                    _TotalFileSize += FileSize;

                  }

                }// end foreach (ArrayList Item in GetFilesList(ParentPath, FilePath));

              }// if (File.Exists(FilePath) == true);

            }// end foreach (string FilePath in FilePaths);

#if DEBUG
            string DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            using (var sw = new StreamWriter(Path.Combine(DesktopDir, "_encrypt_header.txt"), false, Encoding.UTF8))
            {
              foreach (string line in DebugList)
              {
                sw.WriteLine(line);
              }
            }
#endif
            // Write File number
            // ms.positon = 4;
            // byteArray = BitConverter.GetBytes((Int64)FileNumber);
            // ms.Write(byteArray, 0, 8);

            //----------------------------------------------------------------------
            // Check the disk space
            //----------------------------------------------------------------------
            string RootDriveLetter = Path.GetPathRoot(_AtcFilePath).Substring(0, 1);

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
                    // not available free space
                    _ReturnCode = NO_DISK_SPACE;
                    _DriveName = drive.ToString();
                    //_TotalFileSize = _TotalFileSize;
                    _AvailableFreeSpace = drive.AvailableFreeSpace;
                    return (false);
                  }
                  break;
              }
            }

            //----------------------------------------------------------------------
            // Create header data
#if (DEBUG)
            string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string FileListTextPath = Path.Combine(DesktopPath, "_header_text.txt");
            var FileListText = String.Join("\n", _FileList);
            File.WriteAllText(FileListTextPath, FileListText, Encoding.UTF8);
#endif
            //----------------------------------------------------------------------
            // The Header of MemoryStream is encrypted
            using (AesManaged aes = new AesManaged())
            {
              aes.BlockSize = 128;              // BlockSize = 8bytes
              aes.KeySize = 256;                // KeySize = 16bytes
              aes.Mode = CipherMode.CBC;        // CBC mode
              aes.Padding = PaddingMode.PKCS7;  // Padding mode is "PKCS7".
              aes.Key = key;
              aes.IV = iv;

              ms.Position = 0;
              //Encryption interface.
              ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
              using (CryptoStream cse = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write))
              {
                //----------------------------------------------------------------------
                // ヘッダーの暗号化
                //----------------------------------------------------------------------
                int len = 0;
                _AtcHeaderSize = 0;   // exclude IV of header
                buffer = new byte[BUFFER_SIZE];
                while ((len = ms.Read(buffer, 0, BUFFER_SIZE)) > 0)
                {
                  cse.Write(buffer, 0, len);
                  _AtcHeaderSize += len;
                }
              }

            }// end using (Rijndael aes = new RijndaelManaged());

          }// end  using (MemoryStream ms = new MemoryStream());

        }// end using (FileStream outfs = new FileStream(_AtcFilePath, FileMode.Create, FileAccess.Write));


        //----------------------------------------------------------------------
        // 本体データの暗号化
        //----------------------------------------------------------------------
        using (FileStream outfs = new FileStream(_AtcFilePath, FileMode.OpenOrCreate, FileAccess.Write))
        {
          byteArray = new byte[4];
          // Back to current positon of 'encrypted file size'
          if (_fExecutable == true)
          {
            if (_ExeToolVersionString == "4.0")
            {
              outfs.Seek(ExeOutFileSize[0] + 24, SeekOrigin.Begin);  // self executable file
            }
            else
            {
              outfs.Seek(ExeOutFileSize[1] + 24, SeekOrigin.Begin);  // self executable file
            }
          }
          else
          {
            outfs.Seek(24, SeekOrigin.Begin);
          }

          byteArray = BitConverter.GetBytes(_AtcHeaderSize);
          outfs.Write(byteArray, 0, 4);

          // Out file stream postion move to end
          outfs.Seek(0, SeekOrigin.End);

          // The Header of MemoryStream is encrypted
          using (AesManaged aes = new AesManaged())
          {
            aes.BlockSize = 128;              // BlockSize = 8bytes
            aes.KeySize = 256;                // KeySize = 16bytes
            aes.Mode = CipherMode.CBC;        // CBC mode
            aes.Padding = PaddingMode.PKCS7;  // Padding mode

            aes.Key = key;
            aes.IV = iv;

            // Encryption interface.
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (CryptoStream cse = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write))
            {
              using (DeflateStream ds = new DeflateStream(cse, compressionLevel))
              {
                int len = 0;
                foreach (string path in _FileList)
                {
                  // Only file is encrypted
                  buffer = new byte[BUFFER_SIZE];
                  using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                  {
                    len = 0;
                    while ((len = fs.Read(buffer, 0, BUFFER_SIZE)) > 0)
                    {
                      ds.Write(buffer, 0, len);
                      _TotalSize += len;

                      string MessageText = "";
                      if (_TotalNumberOfFiles > 1)
                      {
                        MessageText = path + " ( " + _NumberOfFiles.ToString() + " / " + _TotalNumberOfFiles.ToString() + " files )";
                      }
                      else
                      {
                        MessageText = path;
                      }

                      MessageList = new ArrayList();
                      MessageList.Add(ENCRYPTING);
                      MessageList.Add(MessageText);

                      // Adjusted the progress bar update interval to 100ms
                      if (swProgress.ElapsedMilliseconds > 100)
                      {
                        percent = ((float)_TotalSize / _TotalFileSize);
                        worker.ReportProgress((int)(percent * 10000), MessageList);
                        swProgress.Restart();
                      }

                      if (worker.CancellationPending == true)
                      {
                        fs.Dispose();
                        e.Cancel = true;
                        return (false);
                      }
                    }
                  }
                } // end foreach (string path in _FileList);

              } // end using ( DeflateStream ds);

            } // end using (CryptoStream cse);

          } // end using (AES aes = new AESManaged());

        } // end using (FileStream outfs = new FileStream(_AtcFilePath, FileMode.Create, FileAccess.Write));

        // Set the timestamp of encryption file to original files or directories
        if (_fKeepTimeStamp == true)
        {
          File.SetCreationTime(_AtcFilePath, dtCreate);
          File.SetLastWriteTime(_AtcFilePath, dtUpdate);
          File.SetLastAccessTime(_AtcFilePath, dtAccess);
        }
        else
        {
          dtUpdate = DateTime.Now;
          File.SetLastWriteTime(_AtcFilePath, dtUpdate);
        }

        //Encryption succeed.
        _ReturnCode = ENCRYPT_SUCCEEDED;
        return (true);

      }
      catch (UnauthorizedAccessException)
      {
        //オペレーティング システムが I/O エラーまたは特定の種類のセキュリティエラーのためにアクセスを拒否する場合、スローされる例外
        //The exception that is thrown when the operating system denies access
        //because of an I/O error or a specific type of security error.
        _ReturnCode = OS_DENIES_ACCESS;
        _ErrorFilePath = _AtcFilePath;
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
      catch (CryptographicException ex)
      {
        //xmlString パラメーターの形式が正しくありません。
        //The format of the xmlString parameter is not valid.
        _ReturnCode = CRYPTOGRAPHIC_EXCEPTION;
        _ErrorMessage = ex.Message;
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
        _ReturnCode = ERROR_UNEXPECTED;
        _ErrorMessage = ex.Message;
        return (false);
      }
      finally
      {
#if (DEBUG)
        lg.StopWatchStop();
        lg.Info("encryption finished!");
#endif
        swEncrypt.Stop();
        swProgress.Stop();
        // 計測時間
        TimeSpan ts = swEncrypt.Elapsed;
        _EncryptionTimeString =
          Convert.ToString(ts.Hours) + "h" + Convert.ToString(ts.Minutes) + "m" +
          Convert.ToString(ts.Seconds) + "s" + Convert.ToString(ts.Milliseconds) + "ms";
      }
    } // encrypt();

    /// <summary>
    /// 指定したルートディレクトリのファイルリストを並列処理で取得する
    /// Get a list of files from the specified root directory in parallel processing
    /// </summary>
    /// <remarks>http://stackoverflow.com/questions/2106877/is-there-a-faster-way-than-this-to-find-all-the-files-in-a-directory-and-all-sub</remarks>
    /// <param name="fileSearchPattern"></param>
    /// <param name="rootFolderPath"></param>
    /// <returns></returns>
    private static IEnumerable<ArrayList> GetFileList(string ParentPath, string rootFolderPath)
    {
      Queue<string> pending = new Queue<string>();
      pending.Enqueue(rootFolderPath);
      string[] tmp;
      while (pending.Count > 0)
      {
        rootFolderPath = pending.Dequeue();
        //-----------------------------------
        // Directory
        ArrayList list = new ArrayList();
        list = GetDirectoryInfo(ParentPath, rootFolderPath);
        yield return list;
        tmp = Directory.GetFiles(rootFolderPath);
        for (int i = 0; i < tmp.Length; i++)
        {
          //-----------------------------------
          // File
          list = GetFileInfo(ParentPath, (string)tmp[i]);
          yield return list;
        }
        tmp = Directory.GetDirectories(rootFolderPath);
        for (int i = 0; i < tmp.Length; i++)
        {
          pending.Enqueue(tmp[i]);
        }
      }
    }

    /// <summary>
    /// 指定のディレクトリ情報をリストで取得する
    /// Get the information of specific DIRECTORY in the ArrayList
    /// </summary>
    /// <param name="ParentPath"></param>
    /// <param name="DirPath"></param>
    /// <returns></returns>
    private static ArrayList GetDirectoryInfo(string ParentPath, string DirPath)
    {
      ArrayList List = new ArrayList();
      //DirectoryInfo di = new DirectoryInfo(ParentPath + Path.GetFileName(DirPath));
      DirectoryInfo di = new DirectoryInfo(DirPath);
      List.Add(0);                                      // Directory flag
      List.Add(DirPath);                                // Absolute file path
#if __MACOS__
      List.Add(DirPath.Replace(ParentPath, "") + "/"); // (string)Remove parent directory path.
#else
      List.Add(DirPath.Replace(ParentPath, "") + "\\"); // (string)Remove parent directory path.
#endif
      List.Add(0);                                      // File size = 0
      List.Add((int)di.Attributes);                     // (int)File attribute
      List.Add(int.Parse((di.LastWriteTimeUtc.ToString("yyyyMMdd")))); // Last write Date (UTC)
      List.Add(int.Parse((di.LastWriteTimeUtc.ToString("HHmmss"))));   // Last write Time (UTC)
      List.Add(int.Parse(di.CreationTimeUtc.ToString("yyyyMMdd")));    // Creation Date (UTC)
      List.Add(int.Parse(di.CreationTimeUtc.ToString("HHmmss")));      // Creation Time (UTC)
      List.Add("");                                     // Check Sum (MD5)
      return (List);
    }

    /// <summary>
    /// 指定のファイル情報をリストで取得する
    /// Get the information of specific FILE in the ArrayList
    /// </summary>
    /// <param name="ParentPath"></param>
    /// <param name="FilePath"></param>
    /// <returns></returns>
    private static ArrayList GetFileInfo(string ParentPath, string FilePath)
    {
      ArrayList List = new ArrayList();
#if __MACOS__
      if (ParentPath.EndsWith('/') == false)
      {
        ParentPath = ParentPath + "/";
      }
#endif
      string AbsoluteFilePath = FilePath;
      FileInfo fi = new FileInfo(FilePath);
      List.Add(1);                                // File flag
      List.Add(FilePath);                         // Absolute file path
      List.Add(FilePath.Replace(ParentPath, "")); // (string)Remove parent directory path.
      List.Add(fi.Length);                        // (Int64)File size
      List.Add((int)fi.Attributes);               // (int)File attribute
      List.Add(int.Parse((fi.LastWriteTimeUtc.ToString("yyyyMMdd")))); // Last write Date (UTC)
      List.Add(int.Parse((fi.LastWriteTimeUtc.ToString("HHmmss"))));   // Last write Time (UTC)
      List.Add(int.Parse(fi.CreationTimeUtc.ToString("yyyyMMdd")));    // Creation Date (UTC)
      List.Add(int.Parse(fi.CreationTimeUtc.ToString("HHmmss")));      // Creation Time (UTC)
      List.Add(getMd5Hash(AbsoluteFilePath));     // Check Sum (MD5)
      return (List);
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

    /// <summary>
    /// アセンブリ情報を取得する
    /// Get assembly infomations
    /// http://stackoverflow.com/questions/909555/how-can-i-get-the-assembly-file-version
    /// </summary>
    static private class AppInfo
    {
      public static Version Version { get { return Assembly.GetCallingAssembly().GetName().Version; } }
      public static string Title
      {
        get
        {
          object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
          if (attributes.Length > 0)
          {
            AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
            if (titleAttribute.Title.Length > 0) return titleAttribute.Title;
          }
          return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
        }
      }

      public static string ProductName
      {
        get
        {
          object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
          return attributes.Length == 0 ? "" : ((AssemblyProductAttribute)attributes[0]).Product;
        }
      }

      public static string Description
      {
        get
        {
          object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
          return attributes.Length == 0 ? "" : ((AssemblyDescriptionAttribute)attributes[0]).Description;
        }
      }

      public static string CopyrightHolder
      {
        get
        {
          object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
          return attributes.Length == 0 ? "" : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
        }
      }

      public static string CompanyName
      {
        get
        {
          object[] attributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
          return attributes.Length == 0 ? "" : ((AssemblyCompanyAttribute)attributes[0]).Company;
        }
      }

    }

  }// end class FileEncrypt()

}// end namespace

