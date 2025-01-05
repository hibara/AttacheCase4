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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
#if __MACOS__
using AppKit;
#endif

namespace AttacheCase
{
  public partial class FileEncrypt4
  {
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

    private byte[] buffer;
    private const int BUFFER_SIZE = 4096;

    // Header data variables
    private static bool fBrocken = false;
    private const string STRING_TOKEN_NORMAL = "_AttacheCaseData";
    private const string STRING_TOKEN_BROKEN = "_Atc_Broken_Data";
    private const string STRING_TOKEN_RSA = "_AttacheCase_Rsa";
    private const int DATA_FILE_VERSION = 140;  // ver.4
    private const string ATC_ENCRYPTED_TOKEN = "atc4";

    //Encrypted header data size
    private int _AtcHeaderSize = 0;
    private long _TotalSize = 0;
    //private Int64 _TotalFileSize = 0;
    private long _StartPos = 0;

#if __MACOS__
    private bool _fCancel = false;
    public bool fCancel
    {
        get { return this._fCancel; }
        set { this._fCancel = value; }
    }
#endif

    // The number of files or folders to be encrypted
    public int NumberOfFiles { get; set; } = 0;

    // Total number of files or folders to be encrypted
    public int TotalNumberOfFiles { get; set; } = 1;

    // Set number of times to input password in encrypt files:
    public char MissTypeLimits { get; set; } = (char)3;

    // Self-executable file
    public bool fExecutable { get; set; } = false;

    // .NET Framework Version of Self-executable file  
    public string ExeToolVersionString { get; set; } = "4.6.2";

    // Set the timestamp of encryption file to original files or directories
    public bool fKeepTimeStamp { get; set; } = false;

    // Set the Compression Option ( 0: Optimal, 1: Fastest, 2: NoCompression)
    public int CompressionOption { get; set; } = 1;

    // ATC file ( encrypted file name ) path to output
    public string AtcFilePath { get; private set; } = "";

    // List of files and folders for encryption
    public List<string> FileList { get; private set; }

#if DEBUG
    // 処理するファイル数、ディレクトリ数
    private int fileCount = 0;
    private int dirCount = 0;
#endif

    // Encryption time
    public string EncryptionTimeString { get; private set; }

    // Guid
    public string GuidString { get; set; }

    // RSA Encryption public key XML string
    private string _RsaPublicKeyXmlString;
    public string RsaPublicKeyXmlString
    {
      get => this._RsaPublicKeyXmlString;
      set
      {
        this._RsaPublicKeyXmlString = value;
        this.fRsaEncryption = true;
      }
    }
    // RSA Encryption
    public bool fRsaEncryption { get; private set; } = false;

    //----------------------------------------------------------------------
    // The return value of error ( ReadOnly)
    //----------------------------------------------------------------------
    // Input "Error code" for value
    public int ReturnCode { get; private set; } = -1;

    // File path that caused the error
    public string ErrorFilePath { get; private set; } = "";

    // Drive name to decrypt
    public string DriveName { get; private set; } = "";

    // Total file size of files to be encrypted
    public long TotalFileSize { get; private set; } = 0;

    // Free space on the drive to encrypt the file
    public long AvailableFreeSpace { get; private set; } = -1;

    // Error message by the exception
    public string ErrorMessage { get; private set; } = "";

    private volatile bool _isCancelled;
    public void CancelEncryption()
    {
      _isCancelled = true;
    }

    // Constructor
    public FileEncrypt4()
    {
    }

    /// <summary>
    /// 指定されたファイルを、提供された設定に基づいて暗号化されたアーカイブに変換します。
    /// </summary>
    /// <param name="sender">イベントの発生元。通常は <see cref="BackgroundWorker"/> です。</param>
    /// <param name="e">イベントデータを含む <see cref="DoWorkEventArgs"/>。</param>
    /// <param name="FilePaths">暗号化対象のファイルパスの配列。</param>
    /// <param name="OutFilePath">暗号化されたアーカイブの出力先ファイルパス。</param>
    /// <param name="Password">暗号化に使用するパスワード（文字列形式）。</param>
    /// <param name="PasswordBinary">暗号化に使用するパスワード（バイナリ形式）。</param>
    /// <param name="NewArchiveName">作成される新しいアーカイブの名前。</param>
    /// <param name="compressionLevel">アーカイブに適用する圧縮レベル。<see cref="CompressionLevel"/> で指定します。</param>
    /// <returns>
    /// 暗号化処理が成功した場合は <c>true</c>、それ以外の場合は <c>false</c> を返します。
    /// </returns>
    public bool Encrypt(
      object sender, DoWorkEventArgs e,
      string[] FilePaths, string OutFilePath, string Password, byte[] PasswordBinary,
      string NewArchiveName, CompressionLevel compressionLevel)
    {

#if (DEBUG)
      var lg = new Logger();
      lg.Info("-----------------------------------");
      lg.Info(OutFilePath);
      lg.Info("Encryption start.");
      lg.StopWatchStart();
#endif

      AtcFilePath = OutFilePath;

      var worker = sender as BackgroundWorker;
      var cancelCheck = () => worker is { CancellationPending: true };

      // The timestamp of original file
      var dtCreate = File.GetCreationTime(FilePaths[0]);
      var dtUpdate = File.GetLastWriteTime(FilePaths[0]);
      var dtAccess = File.GetLastAccessTime(FilePaths[0]);

      // Create Header data.
      var MessageList = new ArrayList
      {
        READY_FOR_ENCRYPT,
        Path.GetFileName(AtcFilePath)
      };

      worker?.ReportProgress(0, MessageList);

      // Stopwatch for measuring time and adjusting the progress bar display
      var swEncrypt = new Stopwatch();
      var swProgress = new Stopwatch();
      swEncrypt.Start();
      swProgress.Start();

      FileList = [];

      // RSA Encryption password
      var byteRsaPassword = new byte[32]; // Key size
      if (fRsaEncryption == true)
      {
        using var rng = new RNGCryptoServiceProvider();
        rng.GetBytes(byteRsaPassword);
        PasswordBinary = byteRsaPassword;
      }

      // Salt
      Rfc2898DeriveBytes deriveBytes;
      if (PasswordBinary == null)
      {
        // String Password
        deriveBytes = new Rfc2898DeriveBytes(Password, 8, 1000);
      }
      else
      {
        // Binary Password
        var random_salt = new byte[8];
        var rng = new RNGCryptoServiceProvider();
        rng.GetBytes(random_salt);
        deriveBytes = new Rfc2898DeriveBytes(PasswordBinary, random_salt, 1000);
      }

      var salt = deriveBytes.Salt;
      var key = deriveBytes.GetBytes(32);
      var iv = deriveBytes.GetBytes(16);

#if (DEBUG)
      var debugString = BitConverter.ToString(salt);
      Console.WriteLine($@"salt: {debugString}");
      debugString = BitConverter.ToString(key);
      Console.WriteLine($@"key: {debugString}");
      debugString = BitConverter.ToString(iv);
      Console.WriteLine($@"iv: {debugString}");
#endif

      try
      {
        using (var outfs = new FileStream(AtcFilePath, FileMode.Create, FileAccess.Write))
        {
          // 自己実行形式ファイル（Self-executable file）
          if (fExecutable == true)
          {
            // public partial class FileEncrypt4
            // Read from ExeOut4.cs
            if (ExeToolVersionString == "4.0")
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
          var ver = AppInfo.Version;
          var vernum = short.Parse(ver.ToString().Replace(".", ""));
          var byteArray = BitConverter.GetBytes(vernum);
          outfs.Write(byteArray, 0, 2);
          // Input password limit
          byteArray = BitConverter.GetBytes(MissTypeLimits);
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
          {
            // Read GUID binary from RSA public key string
            var xmlElement = XElement.Parse(RsaPublicKeyXmlString);
            GuidString = xmlElement.Element("id")!.Value;
            guid = Guid.Parse(GuidString);
          }
          else
          {
            // New GUID
            guid = Guid.NewGuid();
            GuidString = guid.ToString();
          }

          outfs.Write(guid.ToByteArray(), 0, 16);

          // Salt
          outfs.Write(salt, 0, 8);

          // RSA encryption password
          if (fRsaEncryption == true)
          {
            // パスワードを暗号化して書き込む
            //RSACryptoServiceProviderオブジェクトの作成
            var rsa = new RSACryptoServiceProvider(2048);
            rsa.FromXmlString(_RsaPublicKeyXmlString); //公開鍵を指定
            //byte[] outbuffer = new byte[214];  // 剰余サイズ(256bytes) -2 -2 * hLen(SHA-1) = 214 Max 
            //string debugString = BitConverter.ToString(byteRsaPassword);
            //Console.WriteLine(debugString);
            var encryptedData = rsa.Encrypt(byteRsaPassword, RSAEncryptionPadding.OaepSHA1); //OAEPパディング=trueでRSA復号
            outfs.Write(encryptedData, 0, encryptedData.Length); // 256 byte
          }

          //-----------------------------------
          // 暗号化ヘッダー
          // Cipher text header
          //-----------------------------------
          var ms = new MemoryStream();
          try
          {
            // Token to refer to when decryption is successful
            byteArray = Encoding.ASCII.GetBytes(ATC_ENCRYPTED_TOKEN);
            ms.Write(byteArray, 0, 4);

            var DebugList = new List<string>();
            var OneLine = "";

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
              var FileLen = Encoding.UTF8.GetByteCount(NewArchiveName);
              byteArray = BitConverter.GetBytes((short)FileLen);
              ms.Write(byteArray, 0, 2);

              // File name
              byteArray = Encoding.UTF8.GetBytes(NewArchiveName);
              ms.Write(byteArray, 0, FileLen);

              // File size (Directory)
              const int fileSize = 0;
              byteArray = BitConverter.GetBytes((long)0);
              ms.Write(byteArray, 0, 8);

              // File attribute (Directory)
              const int fileAttr = 16;
              byteArray = BitConverter.GetBytes((int)16);
              ms.Write(byteArray, 0, 4);

              var LastWriteDateString = DateTime.UtcNow.ToString("yyyyMMdd");
              var LastWriteTimeString = DateTime.UtcNow.ToString("HHmmss");
              // Last write date
              byteArray = BitConverter.GetBytes(int.Parse(LastWriteDateString));
              ms.Write(byteArray, 0, 4);
              // Last write time
              byteArray = BitConverter.GetBytes(int.Parse(LastWriteTimeString));
              ms.Write(byteArray, 0, 4);

              var CreationDateString = DateTime.UtcNow.ToString("yyyyMMdd");
              var CreationTimeString = DateTime.UtcNow.ToString("HHmmss");
              // Creation date
              byteArray = BitConverter.GetBytes(int.Parse(CreationDateString));
              ms.Write(byteArray, 0, 4);
              // Creation time
              byteArray = BitConverter.GetBytes(int.Parse(CreationTimeString));
              ms.Write(byteArray, 0, 4);
#if DEBUG
              OneLine += $@"{FileLen}\t";
              OneLine += $@"{NewArchiveName}\t";
              OneLine += $@"{fileSize}\t";
              OneLine += $@"{fileAttr}\t";
              OneLine += $@"{LastWriteDateString}\t";
              OneLine += $@"{LastWriteTimeString}\t";
              OneLine += $@"{CreationDateString}\t";
              OneLine += $@"{CreationTimeString}";
              DebugList.Add(OneLine);
              OneLine = "";
              dirCount++;
#endif
            }

            //----------------------------------------------------------------------
            // When encrypt multiple files
            // 複数のファイルを暗号化する場合
            foreach (var FilePath in FilePaths)
            {
              var ParentPath = Path.GetDirectoryName(FilePath);
#if __MACOS__
                if (ParentPath.EndsWith("/") == false)
                {
                  ParentPath = ParentPath + "/";
                }
#else
              if (ParentPath == null)
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

              //-----------------------------------
              // 暗号化リストを生成（ファイル）
              // Create file to encrypt list ( File )
              //-----------------------------------
              if (File.Exists(FilePath))
              {
                var entry = GetFileInfo(ParentPath, FilePath, cancelCheck);

                if (_isCancelled || worker.CancellationPending)
                {
                  // キャンセル待ち
                  e.Cancel = true;
                  return (false);
                }

                TotalFileSize += FileInfoStreamWriter(ref ms, entry, NewArchiveName);
                FileList.Add(entry.FullPath);
#if DEBUG
                OneLine += $"{entry.FullPath}\t";
                OneLine += $"{entry.Size}\t";
                OneLine += $"{entry.Attributes}\t";
                OneLine += $"{entry.LastWriteDate}\t";
                OneLine += $"{entry.LastWriteTime}\t";
                OneLine += $"{entry.CreationDate}\t";
                OneLine += $"{entry.CreationTime}";
                DebugList.Add(OneLine);
                OneLine = "";
                fileCount++;
#endif
              }
              //-----------------------------------
              // 暗号化リストを生成（ディレクトリ）
              // Create file to encrypt list ( Directory )
              //-----------------------------------
              else
              {
                // Directory
                foreach (var entry in GetFileList(ParentPath, FilePath, cancelCheck))
                {
                  if (_isCancelled || worker.CancellationPending)
                  {
                    e.Cancel = true;
                    return false;
                  }

                  if (entry.IsDirectory)
                  {
                    TotalFileSize += FileInfoStreamWriter(ref ms, entry, NewArchiveName);
                    FileList.Add(entry.FullPath);
#if DEBUG
                    OneLine += $"{entry.FullPath}\t";
                    OneLine += $"{entry.Size}\t";
                    OneLine += $"{entry.Attributes}\t";
                    OneLine += $"{entry.LastWriteDate}\t";
                    OneLine += $"{entry.LastWriteTime}\t";
                    OneLine += $"{entry.CreationDate}\t";
                    OneLine += $"{entry.CreationTime}";
                    DebugList.Add(OneLine);
                    OneLine = "";
                    fileCount++;
#endif
                  }
                  else
                  {
                    TotalFileSize += FileInfoStreamWriter(ref ms, entry, NewArchiveName);
                    // files only ( Add Files list for encryption )
                    FileList.Add(entry.FullPath);
#if DEBUG
                    OneLine += $"{entry.FullPath}\t";
                    OneLine += $"{entry.Size}\t";
                    OneLine += $"{entry.Attributes}\t";
                    OneLine += $"{entry.LastWriteDate}\t";
                    OneLine += $"{entry.LastWriteTime}\t";
                    OneLine += $"{entry.CreationDate}\t";
                    OneLine += $"{entry.CreationTime}";
                    DebugList.Add(OneLine);
                    OneLine = "";
                    dirCount++;
#endif
                  }

                } // end foreach (ArrayList Item in GetFilesList(ParentPath, FilePath));

              } // if (File.Exists(FilePath) == true);

            } // end foreach (string FilePath in FilePaths);
#if DEBUG
            var DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            using var sw = new StreamWriter(Path.Combine(DesktopDir, "_encrypt_header.txt"), false, Encoding.UTF8);
            foreach (var line in DebugList)
            {
              sw.WriteLine(line);
            }
#endif

            //----------------------------------------------------------------------
            // Check the disk space
            //----------------------------------------------------------------------
            var RootDriveLetter = Path.GetPathRoot(AtcFilePath).Substring(0, 1);

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
                case DriveType.Fixed: // Local Drive
                case DriveType.Network: // Mapped Drive
                case DriveType.Ram: // Ram Drive
                case DriveType.Removable: // Usually a USB Drive

                  // The drive is not available, or not enough free space.
                  if (drive.IsReady == false || drive.AvailableFreeSpace < TotalFileSize)
                  {
                    // not available free space
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

            //----------------------------------------------------------------------
            // The Header of MemoryStream is encrypted
            // ヘッダーの暗号化
            //----------------------------------------------------------------------
            // Back to current position of 'encrypted file size'
            if (fExecutable == true)
            {
              if (ExeToolVersionString == "4.0")
              {
                outfs.Seek(ExeOutFileSize[0] + 24, SeekOrigin.Begin); // self executable file
              }
              else
              {
                outfs.Seek(ExeOutFileSize[1] + 24, SeekOrigin.Begin); // self executable file
              }
            }
            else
            {
              outfs.Seek(24, SeekOrigin.Begin);
            }

            using var aesManaged = new AesManaged();
            aesManaged.BlockSize = 128; // BlockSize = 8bytes
            aesManaged.KeySize = 256; // KeySize = 16bytes
            aesManaged.Mode = CipherMode.CBC; // CBC mode
            aesManaged.Padding = PaddingMode.PKCS7; // Padding mode is "PKCS7".
            aesManaged.Key = key;
            aesManaged.IV = iv;

            ms.Position = 0;
            //Encryption interface.
            var encryptor = aesManaged.CreateEncryptor(aesManaged.Key, aesManaged.IV);
            using CryptoStream cse = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write);
            //----------------------------------------------------------------------
            // ヘッダーの暗号化
            //----------------------------------------------------------------------
            int atcHeaderSize = 0;
            _AtcHeaderSize = 0; // exclude IV of header
            buffer = new byte[BUFFER_SIZE];
            while ((atcHeaderSize = ms.Read(buffer, 0, BUFFER_SIZE)) > 0)
            {
              cse.Write(buffer, 0, atcHeaderSize);
              _AtcHeaderSize += atcHeaderSize;
            }

            //----------------------------------------------------------------------
            // 本体データの暗号化
            //----------------------------------------------------------------------
            byteArray = BitConverter.GetBytes(_AtcHeaderSize);
            outfs.Write(byteArray, 0, 4);

            // Out file stream position move to end
            outfs.Seek(0, SeekOrigin.End);

            using var aes = new AesManaged();
            aes.BlockSize = 128; // BlockSize = 8bytes
            aes.KeySize = 256; // KeySize = 16bytes
            aes.Mode = CipherMode.CBC; // CBC mode
            aes.Padding = PaddingMode.PKCS7; // Padding mode

            aes.Key = key;
            aes.IV = iv;

            // Encryption interface.
            encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ces = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write);
            using var ds = new DeflateStream(cse, compressionLevel);
            foreach (var path in FileList)
            {
              // Only file is encrypted
              buffer = new byte[BUFFER_SIZE];
              using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
              var len = 0;
              while ((len = fs.Read(buffer, 0, BUFFER_SIZE)) > 0)
              {
                ds.Write(buffer, 0, len);
                _TotalSize += len;

                string MessageText;
                if (TotalNumberOfFiles > 1)
                {
                  MessageText = path + $" ( {NumberOfFiles} / {TotalNumberOfFiles} files )";
                }
                else
                {
                  MessageText = path;
                }

                MessageList = new ArrayList
                {
                  ENCRYPTING,
                  MessageText
                };

                // Adjusted the progress bar update interval to 100ms
                if (swProgress.ElapsedMilliseconds > 100)
                {
                  var percent = ((float)_TotalSize / TotalFileSize);
                  worker.ReportProgress((int)(percent * 10000), MessageList);
                  swProgress.Restart();
                }

                if (worker.CancellationPending == true)
                {
                  e.Cancel = true;
                  return (false);
                }
              }
            } // end foreach (string path in _FileList);
          }
          catch (OperationCanceledException)
          {
            e.Cancel = true;
            return (false);
          }
          catch (Exception ex)
          {
            Debug.WriteLine(ex.Message);
            return (false);
          }

        } // end using (FileStream outfs = new FileStream(_AtcFilePath, FileMode.Create, FileAccess.Write));

        // Set the timestamp of encryption file to original files or directories
        if (fKeepTimeStamp == true)
        {
          File.SetCreationTime(AtcFilePath, dtCreate);
          File.SetLastWriteTime(AtcFilePath, dtUpdate);
          File.SetLastAccessTime(AtcFilePath, dtAccess);
        }
        else
        {
          dtUpdate = DateTime.Now;
          File.SetLastWriteTime(AtcFilePath, dtUpdate);
        }

        //Encryption succeed.
        ReturnCode = ENCRYPT_SUCCEEDED;
        return (true);

      }
      catch (UnauthorizedAccessException)
      {
        //オペレーティング システムが I/O エラーまたは特定の種類のセキュリティエラーのためにアクセスを拒否する場合、スローされる例外
        //The exception that is thrown when the operating system denies access
        //because of an I/O error or a specific type of security error.
        ReturnCode = OS_DENIES_ACCESS;
        ErrorFilePath = AtcFilePath;
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
      catch (CryptographicException ex)
      {
        //xmlString パラメーターの形式が正しくありません。
        //The format of the xmlString parameter is not valid.
        ReturnCode = CRYPTOGRAPHIC_EXCEPTION;
        ErrorMessage = ex.Message;
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
        ReturnCode = ERROR_UNEXPECTED;
        ErrorMessage = ex.Message;
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
        var ts = swEncrypt.Elapsed;
        EncryptionTimeString = $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s {ts.Milliseconds}ms";
      }

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentPath"></param>
    /// <param name="rootFolderPath"></param>
    /// <param name="cancelCheck"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    public IEnumerable<FileSystemEntry> GetFileList(string parentPath, string rootFolderPath, Func<bool> cancelCheck)
    {
      var pending = new ConcurrentQueue<string>();
      var results = new ConcurrentBag<FileSystemEntry>();
      pending.Enqueue(rootFolderPath);

      while (!pending.IsEmpty)
      {
        if (cancelCheck()) throw new OperationCanceledException();

        if (pending.TryDequeue(out var currentPath))
        {
          results.Add(GetDirectoryInfo(parentPath, currentPath));

          foreach (var entry in ProcessCurrentDirectory(parentPath, currentPath, cancelCheck))
          {
            results.Add(entry);
          }
        }
      }

      return results.OrderBy(e => e.RelativePath);
    }

    /// <summary>
    /// 暗号化ファイルに格納する各ファイル情報をMemoryStreamに書き込む
    /// </summary>
    /// <param name="ms">参照するMemoryStreamインスタンス</param>
    /// <param name="entry">各ファイル情報</param>
    /// <param name="newArchiveName">親ディレクトリがあればその名前</param>
    /// <returns></returns>
    private static long FileInfoStreamWriter(ref MemoryStream ms, FileSystemEntry entry, string newArchiveName)
    {
      var fileLen = Encoding.UTF8.GetByteCount(newArchiveName + entry.RelativePath);
      var byteArray = BitConverter.GetBytes((short)fileLen);
      ms.Write(byteArray, 0, 2);

      // Directory or file name
      byteArray = Encoding.UTF8.GetBytes(newArchiveName + entry.RelativePath);
      ms.Write(byteArray, 0, fileLen);

      // Size
      byteArray = BitConverter.GetBytes(entry.Size);
      ms.Write(byteArray, 0, 8);

      // File attribute
      byteArray = BitConverter.GetBytes((int)entry.Attributes);
      ms.Write(byteArray, 0, 4);

      // Last write date (UTC)
      byteArray = BitConverter.GetBytes(entry.LastWriteDate);
      ms.Write(byteArray, 0, 4);

      // Last write time (UTC)
      byteArray = BitConverter.GetBytes(entry.LastWriteTime);
      ms.Write(byteArray, 0, 4);

      // Creation date (UTC)
      byteArray = BitConverter.GetBytes(entry.CreationDate);
      ms.Write(byteArray, 0, 4);

      // Creation time (UTC)
      byteArray = BitConverter.GetBytes(entry.CreationTime);
      ms.Write(byteArray, 0, 4);

      // MD5ハッシュ（ディレクトリでない場合のみ）
      if (entry is { IsDirectory: false, Size: > 0 })
      {
        ms.Write(entry.Md5Hash, 0, 16);
      }

      return entry.Size;
    }

    public struct FileSystemEntry
    {
      public bool IsDirectory;
      public string FullPath;
      public string RelativePath;
      public long Size;
      public FileAttributes Attributes;
      public int LastWriteDate;
      public int LastWriteTime;
      public int CreationDate;
      public int CreationTime;
      public byte[] Md5Hash;
    }

    private IEnumerable<FileSystemEntry> ProcessCurrentDirectory(string parentPath, string currentPath, Func<bool> cancelCheck)
    {
      if (cancelCheck()) throw new OperationCanceledException();

      var entries = new ConcurrentBag<FileSystemEntry>();
      try
      {
        var files = Directory.EnumerateFiles(currentPath).ToList();
        if (files.Any())
        {
          Parallel.ForEach(files, new ParallelOptions
          {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = new CancellationToken(cancelCheck())
          }, file =>
          {
            if (cancelCheck()) throw new OperationCanceledException();
            var entry = GetFileInfo(parentPath, file, cancelCheck);
            entries.Add(entry);
          });
        }

        foreach (var dir in Directory.EnumerateDirectories(currentPath))
        {
          if (cancelCheck()) throw new OperationCanceledException();
          entries.Add(GetDirectoryInfo(parentPath, dir));
        }
      }
      catch (UnauthorizedAccessException) { /* スキップ */ }
      catch (DirectoryNotFoundException) { /* スキップ */ }

      return entries.OrderBy(e => e.RelativePath);
    }

    private static FileSystemEntry GetDirectoryInfo(string parentPath, string dirPath)
    {
#if __MACOS__
      if (ParentPath.EndsWith('/') == false)
      {
        ParentPath = ParentPath + "/";
      }
#endif
      var di = new DirectoryInfo(dirPath);
      var relativePath = CreateRelativePath(parentPath, dirPath);

      // ディレクトリの場合のみ末尾にデリミタを追加
      if (relativePath.EndsWith("\\") == false)
      {
        relativePath += "\\";
      }

      return new FileSystemEntry
      {
        IsDirectory = true,                              // File flag
        FullPath = dirPath,                              // Absolute file path
        RelativePath = relativePath,                     // (string)Remove parent directory path.
        Size = 0,                                        // (Int64)File size
        Attributes = di.Attributes,                      // (int)File attribute
        LastWriteDate = GetDateInt(di.LastWriteTimeUtc), // Last write Date (UTC)
        LastWriteTime = GetTimeInt(di.LastWriteTimeUtc), // Last write Time (UTC)
        CreationDate = GetDateInt(di.CreationTimeUtc),   // Creation Date (UTC)
        CreationTime = GetTimeInt(di.CreationTimeUtc),   // Creation Time (UTC)
        Md5Hash = []     // Check Sum (MD5)
      };
    }

    private static FileSystemEntry GetFileInfo(string parentPath, string filePath, Func<bool> cancelCheck)
    {
      try
      {
#if __MACOS__
      if (ParentPath.EndsWith('/') == false)
      {
        ParentPath = ParentPath + "/";
      }
#endif
        var fi = new FileInfo(filePath);
        return new FileSystemEntry
        {
          IsDirectory = false, // Directory flag
          FullPath = filePath, // Absolute file path
          RelativePath = CreateRelativePath(parentPath, filePath), // (string)Remove parent directory path.
          Size = fi.Length, // File size = 0
          Attributes = fi.Attributes, // (int)File attribute
          LastWriteDate = GetDateInt(fi.LastWriteTimeUtc), // Last write Date (UTC)
          LastWriteTime = GetTimeInt(fi.LastWriteTimeUtc), // Last write Time (UTC)
          CreationDate = GetDateInt(fi.CreationTimeUtc), // Creation Date (UTC)
          CreationTime = GetTimeInt(fi.CreationTimeUtc), // Creation Time (UTC)
          Md5Hash = fi.Length == 0 ? null : GetMd5Hash(filePath, cancelCheck) // Check Sum (MD5)
        };
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        throw new FileProcessingException($"Error processing file: {filePath}", filePath, ex);
      }
    }

    public class FileProcessingException : Exception
    {
      public string FilePath { get; }
      public FileProcessingException(string message, string filePath, Exception inner)
        : base(message, inner)
      {
        FilePath = filePath;
      }
    }


    private static string CreateRelativePath(string parentPath, string fullPath)
    {
      var relativeLength = fullPath.Length - parentPath.Length;
      if (relativeLength <= 0) return "";

      var relativePath = fullPath.Substring(parentPath.Length, relativeLength);

      // ディレクトリの場合のみ末尾にデリミタを追加
      if (!relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
      {
        relativePath += Path.DirectorySeparatorChar;
      }

      return relativePath;
    }

    private static int GetDateInt(DateTime dt)
    {
      return (dt.Year * 10000) + (dt.Month * 100) + dt.Day;
    }

    private static int GetTimeInt(DateTime dt)
    {
      return (dt.Hour * 10000) + (dt.Minute * 100) + dt.Second;
    }

    /// <summary>
    /// 指定されたファイルのMD5ハッシュ値を計算します。
    /// </summary>
    /// <param name="filePath">ハッシュ値を計算する対象のファイルのパス。</param>
    /// <param name="cancelCheck">
    /// 計算処理中に定期的に呼び出されるデリゲート関数。
    /// この関数が<c>true</c>を返した場合、処理がキャンセルされ、<c>null</c>が返されます。
    /// </param>
    /// <returns>
    /// ファイルのMD5ハッシュ値を格納したバイト配列。処理がキャンセルされた場合は<c>null</c>を返します。
    /// </returns>
    private static byte[] GetMd5Hash(string filePath, Func<bool> cancelCheck)
    {
      using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var md5 = new MD5CryptoServiceProvider();
      var buffer = new byte[8192];
      int bytesRead;
      while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
      {
        if (cancelCheck())
        {
          return null;
        }
        md5.TransformBlock(buffer, 0, bytesRead, null, 0);
      }
      md5.TransformFinalBlock(buffer, 0, 0);
      return md5.Hash;
    }

    /// <summary>
    /// アセンブリ情報を取得するクラス
    /// Get assembly information
    /// </summary>
    private static class AppInfo
    {
      private static readonly Assembly _assembly = Assembly.GetCallingAssembly();

      public static Version Version => _assembly.GetName().Version;

      public static string Title
      {
        get
        {
          var title = GetAttribute<AssemblyTitleAttribute>()?.Title;
          return !string.IsNullOrEmpty(title)
            ? title
            : Path.GetFileNameWithoutExtension(_assembly.CodeBase);
        }
      }

      public static string ProductName => GetAttribute<AssemblyProductAttribute>()?.Product ?? "";

      public static string Description => GetAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

      public static string CopyrightHolder => GetAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

      public static string CompanyName => GetAttribute<AssemblyCompanyAttribute>()?.Company ?? "";

      private static T GetAttribute<T>() where T : Attribute
      {
        return _assembly.GetCustomAttributes(typeof(T), false).FirstOrDefault() as T;
      }
    }

  }// end class FileEncrypt()

}// end namespace

