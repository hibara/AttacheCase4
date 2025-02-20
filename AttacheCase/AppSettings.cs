//---------------------------------------------------------------------- 
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
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AttacheCase.Properties;
using System.Text;
using System.Security;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace AttacheCase
{
  /// <summary>
  /// Save AttacheCase setting to registry class by Singleton pattern
  /// </summary>
  internal class AppSettings
  {
    [SuppressUnmanagedCodeSecurityAttribute]
    internal static class UnsafeNativeMethods
    {
      //
      // An INI file handling class using C#
      // http://www.codeproject.com/Articles/1966/An-INI-file-handling-class-using-C
      //
      [DllImport("kernel32", BestFitMapping = false, ThrowOnUnmappableChar = true)]
      internal static extern uint WritePrivateProfileString(string section, string key, string val, string filePath);
      [DllImport("kernel32", BestFitMapping = false, ThrowOnUnmappableChar = true)]
      internal static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
      [DllImport("kernel32", BestFitMapping = false, ThrowOnUnmappableChar = true)]
      internal static extern uint GetVolumeInformation(string PathName, StringBuilder VolumeNameBuffer, UInt32 VolumeNameSize, ref UInt32 VolumeSerialNumber, ref UInt32 MaximumComponentLength, ref UInt32 FileSystemFlags, StringBuilder FileSystemNameBuffer, UInt32 FileSystemNameSize);
    }

    // File type
    private const int FILE_TYPE_ERROR = -1;
    private const int FILE_TYPE_NONE = 0;
    private const int FILE_TYPE_ATC = 1;
    private const int FILE_TYPE_ATC_EXE = 2;
    private const int FILE_TYPE_PASSWORD_ZIP = 3;
    private const int FILE_TYPE_RSA_DATA = 4;
    private const int FILE_TYPE_RSA_PRIVATE_KEY = 5;
    private const int FILE_TYPE_RSA_PUBLIC_KEY = 6;

    // Process Type
    private const int PROCESS_TYPE_ERROR = -1;
    private const int PROCESS_TYPE_NONE = 0;
    private const int PROCESS_TYPE_ATC = 1;
    private const int PROCESS_TYPE_ATC_EXE = 2;
    private const int PROCESS_TYPE_PASSWORD_ZIP = 3;
    private const int PROCESS_TYPE_DECRYPTION = 4;
    private const int PROCESS_TYPE_RSA_ENCRYPTION = 5;
    private const int PROCESS_TYPE_RSA_DECRYPTION = 6;
    // Dialog Button
    private const int MESSAGE_BOX_BUTTONS_YES = 7;
    private const int MESSAGE_BOX_BUTTONS_NO = 8;

    //-----------------------------------
    // メンバ変数(Member Variable)
    //-----------------------------------

    // Self instance
    private static AppSettings _Instance;

    private readonly string RegistryRootPath3 = $@"Software\Hibara\{"AttacheCase3"}";
    private readonly string RegistryRootPath4 = $@"Software\Hibara\{"AttacheCase4"}";

    private readonly string RegistryPathAppInfo = $@"Software\Hibara\{"AttacheCase4"}\AppInfo";
    private readonly string RegistryPathWindowPos = $@"Software\Hibara\{"AttacheCase4"}\WindowPos";
    private readonly string RegistryPathMyKey = $@"Software\Hibara\{"AttacheCase4"}\MyKey";
    private readonly string RegistryPathOption = $@"Software\Hibara\{"AttacheCase4"}\Option";

    // Static instance ( Singleton pattern )
    public static AppSettings Instance
    {
      get
      {
        if (_Instance == null)
        {
          _Instance = new AppSettings();
        }
        return _Instance;
      }
      set => _Instance = value;
    }

    //----------------------------------------------------------------------
    // File List
    private List<string> _FileList = new List<string>();
    public List<string> FileList
    {
      get => this._FileList;
      set => this._FileList = value;
    }

    //----------------------------------------------------------------------
    // 一時的な設定ファイルパス（INIファイル）
    // Temporary setting file path ( INI file )
    #region

    public string IniFilePath { get; set; }

    #endregion

    //----------------------------------------------------------------------
    // Window Pos
    #region
    //----------------------------------------------------------------------
    private int _FormTop;
    public int FormTop
    {
      get => this._FormTop;
      set => this._FormTop = value;
    }
    private int _FormLeft;
    public int FormLeft
    {
      get => this._FormLeft;
      set => this._FormLeft = value;
    }
    private int _FormWidth;
    public int FormWidth
    {
      get => this._FormWidth;
      set => this._FormWidth = value;
    }
    private int _FormHeight;
    public int FormHeight
    {
      get => this._FormHeight;
      set => this._FormHeight = value;
    }
    private int _FormStyle;
    public int FormStyle
    {
      get => this._FormStyle;
      set => this._FormStyle = value;
    }

    public int TabSelectedIndex { get; set; }

    private string _InitDirPath;
    public string InitDirPath
    {
      get => this._InitDirPath;
      set => this._InitDirPath = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // General
    #region
    //----------------------------------------------------------------------

    private bool _fEndToExit;             // 処理後に終了するか
    //Exit AttacheCase after process.
    public bool fEndToExit
    {
      get => this._fEndToExit;
      set => this._fEndToExit = value;
    }

    private bool _fOpenFile;              // 復号したファイルを関連付けされたソフトで開く
    //Open decrypted files by associated application
    public bool fOpenFile
    {
      get => this._fOpenFile;
      set => this._fOpenFile = value;
    }

    private bool _fShowDialogWhenExeFile; // 復号したファイルに実行ファイルが含まれるとき警告ダイアログを出す
    //Show dialog when containing the executable file.
    public bool fShowDialogWhenExeFile
    {
      get => this._fShowDialogWhenExeFile;
      set => this._fShowDialogWhenExeFile = value;
    }

    private int _ShowDialogWhenMultipleFilesNum; // 復号したファイルが複数個あるとき警告ダイアログを出す
    //Show dialog when more than multiple files
    public int ShowDialogWhenMultipleFilesNum
    {
      get => this._ShowDialogWhenMultipleFilesNum;
      set => this._ShowDialogWhenMultipleFilesNum = value;
    }

    private bool _fAskEncDecode;                 // 暗号/復号処理かを問い合わせる
    //Ask to encrypt or decrypt regardless of file content
    public bool fAskEncDecode
    {
      get => this._fAskEncDecode;
      set => this._fAskEncDecode = value;
    }

    //Always output to Executable file
    private bool _fSaveToExeout;                // 常に自己実行形式で出力する
    public bool fSaveToExeout
    {
      get => this._fSaveToExeout;
      set => this._fSaveToExeout = value;
    }

    private bool _fShowExeoutChkBox;            // メインフォームにチェックボックスを表示する
    //Always display checkbox of this option
    public bool fShowExeoutChkBox
    {
      get => this._fShowExeoutChkBox;
      set => this._fShowExeoutChkBox = value;
    }

    private string _ThemeColorName;            // テーマカラー名
    // Window theme color option ( name )
    public string ThemeColorName
    {
      get => this._ThemeColorName;
      set => this._ThemeColorName = value;
    }

    // 「自動」が選択されているときのテーマカラー名
    // Window theme color option ( name )
    public string CurrentThemeColorName => _ThemeColorName == "auto" ? GetSystemThemeColor() : _ThemeColorName;

    #endregion

    //----------------------------------------------------------------------
    // Passwords
    #region
    //----------------------------------------------------------------------
    private bool _fMyEncryptPasswordKeep;            // 暗号化パスワードを記憶するか
    //Remember password for encryption
    public bool fMyEncryptPasswordKeep
    {
      get => this._fMyEncryptPasswordKeep;
      set => this._fMyEncryptPasswordKeep = value;
    }

    public string MyEncryptPasswordString { get; set; }

    private byte[] _MyEncryptPasswordBinary;          // 暗号化パスワード（バイナリ）
    public byte[] MyEncryptPasswordBinary
    {
      get => this._MyEncryptPasswordBinary;
      set => this._MyEncryptPasswordBinary = value;
    }

    // コマンドラインからの暗号化パスワード（文字列）
    public string EncryptPasswordStringFromCommandLine { get; set; }

    private bool _fMyDecryptPasswordKeep;             // 復号パスワードを記憶するか
    //Remember &Decryption password
    public bool fMyDecryptPasswordKeep
    {
      get => this._fMyDecryptPasswordKeep;
      set => this._fMyDecryptPasswordKeep = value;
    }

    // 復号パスワード（文字列）
    public string MyDecryptPasswordString { get; set; }

    // 復号パスワード（バイナリ）
    public byte[] MyDecryptPasswordBinary { get; set; }

    private string _DecryptPasswordStringFromCommandLine; // コマンドラインからの復号パスワード（文字列）
    public string DecryptPasswordStringFromCommandLine
    {
      get => this._DecryptPasswordStringFromCommandLine;
      set => this.EncryptPasswordStringFromCommandLine = value;
    }

    private bool _fMemPasswordExe;                 //記憶パスワードで確認なく実行する
    //Encrypt/Decrypt by &memorized password without confirming
    public bool fMemPasswordExe
    {
      get => this._fMemPasswordExe;
      set => this._fMemPasswordExe = value;
    }

    // Not mask password character
    private bool _fNotMaskPassword;
    public bool fNotMaskPassword
    {
      get => this._fNotMaskPassword;
      set => this._fNotMaskPassword = value;
    }

    // Enable password strength meter display
    private bool _fPasswordStrengthMeter;
    public bool fPasswordStrengthMeter
    {
      get => this._fPasswordStrengthMeter;
      set => this._fPasswordStrengthMeter = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Window
    #region
    //----------------------------------------------------------------------
    private bool _fMainWindowMinimize;     // 常にウィンドウを最小化して処理する
    //Always execute by minimize the window
    public bool fMainWindowMinimize
    {
      get => this._fMainWindowMinimize;
      set => this._fMainWindowMinimize = value;
    }

    private bool _fTaskBarHide;           // タスクバーに表示しない
    //Minimizing a window without appearing in the taskbar
    public bool fTaskBarHide
    {
      get => this._fTaskBarHide;
      set => this._fTaskBarHide = value;
    }

    private bool _fTaskTrayIcon;          // タスクトレイにアイコンを表示する
    //Display in the task tray
    public bool fTaskTrayIcon
    {
      get => this._fTaskTrayIcon;
      set => this._fTaskTrayIcon = value;
    }

    private bool _fOpenFolder;              // フォルダの場合に復号後に開くか
    //In the case of the folder, open it in Explorer after decrypting
    public bool fOpenFolder
    {
      get => this._fOpenFolder;
      set => this._fOpenFolder = value;
    }

    private bool _fWindowForeground;       // デスクトップで最前面にウィンドウを表示する
    //Bring AttacheCase window in front of Desktop
    public bool fWindowForeground
    {
      get => this._fWindowForeground;
      set => this._fWindowForeground = value;
    }

    private bool _fNoMultipleInstance;    // 複数起動しない
    //Not Allow multiple in&stance of AttacheCase
    public bool fNoMultipleInstance
    {
      get => this._fNoMultipleInstance;
      set => this._fNoMultipleInstance = value;
    }

    private bool _fTurnOnIMEsTextBoxForPasswordEntry; // パスワード入力用のすべてのテキストボックスでIMEをオンにする
    // Turn on IMEs in all text box for password entry
    public bool fTurnOnIMEsTextBoxForPasswordEntry
    {
      get => this._fTurnOnIMEsTextBoxForPasswordEntry;
      set => this._fTurnOnIMEsTextBoxForPasswordEntry = value;
    }

    private bool _fNotMaximizedInTabletMode;  // PCがタブレットモードで動作しているとき、最大化モードで起動しない
    // Doesn't start in maximized mode when PC is operating in tablet mode
    public bool fNotMaximizedInTabletMode
    {
      get => this._fNotMaximizedInTabletMode;
      set => this._fNotMaximizedInTabletMode = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Save 
    #region
    //----------------------------------------------------------------------
    // Temporary option for overwriting
    public int TempOverWriteOption { get; set; }

    // Temporay option for overwriting for new date only.
    public bool OverWriteForNewDate { get; set; }

    private int _EncryptionFileType = 0;              // 暗号化ファイルの種類
                                                      // Encryption type
    public int EncryptionFileType
    {
      get => this._EncryptionFileType;
      set => this._EncryptionFileType = value;
    }

    private bool _fEncryptionSameFileTypeAlways;      // 常に同じ暗号化ファイルの種類にする
                                                      // Save same encryption type always.
    public bool fEncryptionSameFileTypeAlways
    {
      get => this._fEncryptionSameFileTypeAlways;
      set => this._fEncryptionSameFileTypeAlways = value;
    }

    private int _EncryptionSameFileTypeAlways;        // 常に同じ暗号化ファイルの種類
                                                      // Same encryption type always.
    public int EncryptionSameFileTypeAlways
    {
      get => this._EncryptionSameFileTypeAlways;
      set => this._EncryptionSameFileTypeAlways = value;
    }

    // 4GBを超えるファイルサイズの作成を許可する
    // Allow more than 4GB of file size to be created
    private bool _fOver4GBok = false;
    public bool fOver4GBok
    {
      get => this._fOver4GBok;
      set => this._fOver4GBok = value;
    }

    // 作成される自己実行可能形式ファイルが 4GiB を超えそうなとき問い合わせる
    // Ask when a self-executable file to be created is about to exceed 4GiB
    private bool _fAskAboutToExceed4Gib = true;
    public bool fAskAboutToExceed4Gib
    {
      get => this._fAskAboutToExceed4Gib;
      set => this._fAskAboutToExceed4Gib = value;
    }

    private bool _fEncryptionSameFileTypeBefore;      // 前に使った暗号化ファイルの種類にする
    // Save same encryption type that was used to before.
    public bool fEncryptionSameFileTypeBefore
    {
      get => this._fEncryptionSameFileTypeBefore;
      set => this._fEncryptionSameFileTypeBefore = value;
    }

    private int _EncryptionSameFileTypeBefore;       // 前に使った暗号化ファイルの種類
                                                     // Same encryption type that was used to before.
    public int EncryptionSameFileTypeBefore
    {
      get => this._EncryptionSameFileTypeBefore;
      set => this._EncryptionSameFileTypeBefore = value;
    }

    private bool _fOutputExeForOlderOS = false;       // 古いOSでも復号化できるように自己実行ファイルを出力する
                                                      // Output self-executable files so that they can be decrypted on older operating systems
    public bool fOutputExeForOlderOS
    {
      get => this._fOutputExeForOlderOS;
      set => this._fOutputExeForOlderOS = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Save Encrypt
    #region
    //----------------------------------------------------------------------

    private bool _fSaveToSameFldr;                 // 暗号化ファイルを常に同じ場所に保存するか
    //Save to same folder in &encryption
    public bool fSaveToSameFldr
    {
      get => this._fSaveToSameFldr;
      set => this._fSaveToSameFldr = value;
    }

    private string _SaveToSameFldrPath;           // その保存場所
    // The folder path
    public string SaveToSameFldrPath
    {
      get => this._SaveToSameFldrPath;
      set => this._SaveToSameFldrPath = value;
    }

    private bool _fEncryptConfirmOverwrite;      // 同名ファイルの上書きを確認するか
    //Confirm overwriting when same filename exists
    public bool fEncryptConfirmOverwrite
    {
      get => this._fEncryptConfirmOverwrite;
      set => this._fEncryptConfirmOverwrite = value;
    }

    private bool _fNormal;                // 何もしない
    //Normal
    public bool fNormal
    {
      get => this._fNormal;
      set
      {
        if ((this._fNormal = value) == true)
        {
          _fAllFilePack = false;
          _fFilesOneByOne = false;
        }
      }
    }

    private bool _fAllFilePack;           // 複数のファイルを暗号化する際は一つにまとめる
    //Create one encrypted file from files
    public bool fAllFilePack
    {
      get => this._fAllFilePack;
      set
      {
        if ((this._fAllFilePack = value) == true)
        {
          _fNormal = false;
          _fFilesOneByOne = false;
        }
      }
    }

    private bool _fFilesOneByOne;         // フォルダ内のファイルは個別に暗号化/復号する
    //Encrypt or decrypt files in directory one by one
    public bool fFilesOneByOne
    {
      get => this._fFilesOneByOne;
      set
      {
        if ((this._fFilesOneByOne = value) == true)
        {
          _fNormal = false;
          _fAllFilePack = false;
        }
      }
    }

    private bool _fKeepTimeStamp;         // 暗号化ファイルのタイムスタンプを元ファイルに合わせる
    //Set the timestamp of encryption file to original files or directories
    public bool fKeepTimeStamp
    {
      get => this._fKeepTimeStamp;
      set => this._fKeepTimeStamp = value;
    }

    private bool _fExtInAtcFileName;      // 暗号化ファイル名に拡張子を含める
    //Create encrypted file &including extension
    public bool fExtInAtcFileName
    {
      get => this._fExtInAtcFileName;
      set => this._fExtInAtcFileName = value;
    }

    private bool _fAutoName;              // 自動で暗号化ファイル名を付加する
    //Specify the format of the encryption file name
    public bool fAutoName
    {
      get => this._fAutoName;
      set => this._fAutoName = value;
    }

    private string _AutoNameFormatText;    // 自動で付加するファイル名書式
    public string AutoNameFormatText
    {
      get => this._AutoNameFormatText;
      set => this._AutoNameFormatText = value;
    }

    private bool _fAutoNameAlphabets;      // アルファベットを使う
    // Use alphabets
    public bool fAutoNameAlphabets
    {
      get => this._fAutoNameAlphabets;
      set => this._fAutoNameAlphabets = value;
    }

    private bool _fAutoNameLowerCase;      // アルファベットの小文字を使う
    // Use alphabets for lower case
    public bool fAutoNameLowerCase
    {
      get => this._fAutoNameLowerCase;
      set => this._fAutoNameLowerCase = value;
    }

    private bool _fAutoNameUpperCase;      // アルファベットの大文字を使う
    // Use alphabets for upper case
    public bool fAutoNameUpperCase
    {
      get => this._fAutoNameUpperCase;
      set => this._fAutoNameUpperCase = value;
    }

    private bool _fAutoNameNumbers;       // 数字を使う
    // Use string of numbers
    public bool fAutoNameNumbers
    {
      get => this._fAutoNameNumbers;
      set => this._fAutoNameNumbers = value;
    }

    private bool _fAutoNameSymbols;       // 記号を使う
    // Use symbols
    public bool fAutoNameSymbols
    {
      get => this._fAutoNameSymbols;
      set => this._fAutoNameSymbols = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Save Decrypt
    #region
    //----------------------------------------------------------------------

    //Save to same folder in decryption
    private bool _fDecodeToSameFldr;                  // 常に同じ場所へファイルを復号するか
    public bool fDecodeToSameFldr
    {
      get => this._fDecodeToSameFldr;
      set => this._fDecodeToSameFldr = value;
    }

    private string _DecodeToSameFldrPath;             // その保存場所
    // The folder path
    public string DecodeToSameFldrPath
    {
      get => this._DecodeToSameFldrPath;
      set => this._DecodeToSameFldrPath = value;
    }

    private bool _fDecryptConfirmOverwrite;          // 同名ファイルの上書きを確認するか
    //Confirm overwriting when same filename exists
    public bool fDecryptConfirmOverwrite
    {
      get => this._fDecryptConfirmOverwrite;
      set => this._fDecryptConfirmOverwrite = value;
    }

    private bool _fNoParentFldr;                     // 復号するときに親フォルダを生成しない
    //Create no parent folder in decryption
    public bool fNoParentFldr
    {
      get => this._fNoParentFldr;
      set => this._fNoParentFldr = value;
    }

    private bool _fSameTimeStamp;                   // ファイル、フォルダーのタイムスタンプを復号時に合わせる
    //Set the timestamp to decrypted files or directories
    public bool fSameTimeStamp
    {
      get => this._fSameTimeStamp;
      set => this._fSameTimeStamp = value;
    }

    private bool _fCompareFile;                    // 暗号処理後にファイルコンペアを行うか
    //Com&pare data files after encryption
    public bool fCompareFile
    {
      get => this._fCompareFile;
      set => this._fCompareFile = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Password ZIP 
    #region
    //----------------------------------------------------------------------
    //Save to the same folder in decryption
    private bool _fZipToSameFldr;                // 常に同じ場所へファイルを復号するか
    public bool fZipToSameFldr
    {
      get => this._fZipToSameFldr;
      set => this._fZipToSameFldr = value;
    }

    private string _ZipToSameFldrPath;           // その保存場所
    public string ZipToSameFldrPath              // The folder path
    {
      get => this._ZipToSameFldrPath;
      set => this._ZipToSameFldrPath = value;
    }

    private bool _fZipConfirmOverwrite;          // 同名ファイルの上書きを確認するか
    public bool fZipConfirmOverwrite             // Confirm overwriting when the same filename exists
    {
      get => this._fZipConfirmOverwrite;
      set => this._fZipConfirmOverwrite = value;
    }

    private int _ZipEncryptionAlgorithm;           // 暗号アルゴリズム
    public int ZipEncryptionAlgorithm              // 0: PkzipWeak, 1: WinZipAes128, 2: WinZipAes256 
    {
      get => this._ZipEncryptionAlgorithm;
      set => this._ZipEncryptionAlgorithm = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Delete
    #region
    //----------------------------------------------------------------------

    private bool _fDelOrgFile;                  // 暗号化した後、元ファイルを削除する
    //Delete original files or directories after encryption
    public bool fDelOrgFile
    {
      get => this._fDelOrgFile;
      set => this._fDelOrgFile = value;
    }

    private bool _fEncryptShowDelChkBox;            // メインフォームにチェックボックスを表示する
    //Show the check box in main form window
    public bool fEncryptShowDelChkBox
    {
      get => this._fEncryptShowDelChkBox;
      set => this._fEncryptShowDelChkBox = value;
    }

    private bool _fConfirmToDeleteAfterEncryption;  // 暗号化後に元ファイルまたはフォルダの削除確認メッセージを表示するか
    //Show confirmation dialog to delete file or directories
    public bool fConfirmToDeleteAfterEncryption
    {
      get => this._fConfirmToDeleteAfterEncryption;
      set => this._fConfirmToDeleteAfterEncryption = value;
    }

    private bool _fDelEncFile;                     // 復号した後、暗号化ファイルを削除する
    //Delete encrypted file after decryption
    public bool fDelEncFile
    {
      get => this._fDelEncFile;
      set => this._fDelEncFile = value;
    }

    private bool _fDecryptShowDelChkBox;            // メインフォームにチェックボックスを表示する
    //Show the check box in main form window
    public bool fDecryptShowDelChkBox
    {
      get => this._fDecryptShowDelChkBox;
      set => this._fDecryptShowDelChkBox = value;
    }

    private bool _fConfirmToDeleteAfterDecryption;  // 復号後に元の暗号化ファイルを削除確認メッセージを表示するか
    //Show confirmation dialog to delete file or directories
    public bool fConfirmToDeleteAfterDecryption
    {
      get => this._fConfirmToDeleteAfterDecryption;
      set => this._fConfirmToDeleteAfterDecryption = value;
    }

    private int _fCompleteDelFile;                 // 完全削除を行うか(0:通常，1:ごみ箱, 2:完全削除）
    //Advanced Delete Option [0: Normal Delete, 1: Complete erase, 2: Send to Trash ]
    public int fCompleteDelFile
    {
      get => this._fCompleteDelFile;
      set
      {
        if ((this._fCompleteDelFile = value) < 0 || (this._fCompleteDelFile = value) > 3)
        {
          this._fCompleteDelFile = 2;
        }
      }
    }

    //Set the number of time to overwrite and completely delete
    private int _DelRandNum;                 // 乱数を何回書き込み消去するか
    //Number of Random data
    public int DelRandNum
    {
      get => this._DelRandNum;
      set => this._DelRandNum = value;
    }

    private int _DelZeroNum;                 // NULLを何回書き込み消去するか
    //Number of Zeros 
    public int DelZeroNum
    {
      get => this._DelZeroNum;
      set => this._DelZeroNum = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Compression
    #region
    //----------------------------------------------------------------------

    private int _CompressRate;               // 圧縮率
    // Enable compression
    public int CompressRate
    {
      get => this._CompressRate;
      set
      {
        if ((this._CompressRate = value) < 0 || (this._CompressRate = value) > 9)
        {
          this._CompressRate = 6;
        }
      }
    }

    // ver.4 or later
    private int _CompressionLevel;          // 圧縮サイズ
    public int CompressionLevel
    {
      get => this._CompressionLevel;
      set
      {
        if ((this._CompressionLevel = value) < 0 || (this._CompressionLevel = value) > 2)
        {
          this._CompressionLevel = 0;
        }
      }
    }

    #endregion

    //----------------------------------------------------------------------
    // System
    #region
    //----------------------------------------------------------------------

    private int _fAssociationFile;                //関連付け設定
    //Association with AttacheCase files ( *.atc )
    public int fAssociationFile
    {
      get => this._fAssociationFile;
      set => this._fAssociationFile = value;
    }

    // ファイルアイコン番号
    private int _AtcsFileIconIndex;
    // Number of preset icon
    public int AtcsFileIconIndex
    {
      get => this._AtcsFileIconIndex;
      set
      {
        if ((this._AtcsFileIconIndex = value) < 0 || (this._AtcsFileIconIndex = value) > 4)
        {
          this._AtcsFileIconIndex = 1;
        }
      }
    }

    private string _UserRegIconFilePath;       // ユーザー指定のファイルアイコンパス
                                               // Specify my file icon path
    public string UserRegIconFilePath
    {
      get => this._UserRegIconFilePath;
      set => this._UserRegIconFilePath = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Import / Export
    #region

    // 常に、設定ファイル「_AtcCase.ini」を読み込む
    // Always read the setting file "_AtcCase.ini"
    public bool fAlwaysReadIniFile { get; set; }

    // 毎回、確認のためのダイアログ ボックスを表示する
    // Show a dialog box to confirm always.
    //private bool _fShowDialogToConfirmToReadIniFile;
    //public bool fShowDialogToConfirmToReadIniFile
    //{
    //  get { return this._fShowDialogToConfirmToReadIniFile; }
    //  set { this._fShowDialogToConfirmToReadIniFile = value; }
    //}

    // 毎回、確認のためのダイアログ ボックスを表示する
    // Show a dialog box to confirm always.
    // ver.4.0.2.7～
    public bool fShowConfirmationDialogToReadIniFile { get; set; }

    #endregion

    //----------------------------------------------------------------------
    // Advanced
    #region

    // 実行ファイルの場合にMOTWが伝播する
    // MOTW propagates in the case of executable files
    private bool _isCheckMotwExecutableFiles;

    public bool isCheckMotwExecutableFiles
    {
      get => this._isCheckMotwExecutableFiles;
      set => this._isCheckMotwExecutableFiles = value;
    }
    public string[] MotwExecutableFilesExtensions = [".exe", ".bat", ".cmd", ".hta", ".lnk", ".msi", ".pif", ".ps1", ".scr", ".vbs"];

    // Officeファイルの場合にMOTWが伝播する
    // MOTW propagates in the case of Office files
    private bool _isCheckMotwOfficeFiles;
    public bool isCheckMotwOfficeFiles
    {
      get => this._isCheckMotwOfficeFiles;
      set => this._isCheckMotwOfficeFiles = value;
    }
    public string[] MotwOfficeFilesExtensions = [
      ".doc",".docb",".docm",".docx",".dot",".dotm",".dotx",".wbk",
      ".ppa",".ppam",".pot",".potm",".potx",".pps",".ppsm",".ppsx",".ppt",".pptm",".pptx",".sldm",".sldx",
      ".xls",".xlsb",".xlsm",".xlsx",".xlm",".xlt",".xltm",".xltx"
    ];

    // ユーザー定義型ファイルの場合にMOTWが伝播する
    // MOTW propagates in the case of user defined types
    private bool _isCheckMotwUserDefinedTypes;
    public bool isCheckMotwUserDefinedTypes
    {
      get => this._isCheckMotwUserDefinedTypes;
      set => this._isCheckMotwUserDefinedTypes = value;
    }
    // ユーザー定義型ファイルの拡張子リスト
    // List of extensions for user-defined type files
    public string[] MotwUserDefinedTypesExtensions = [];
    // ユーザー定義型ファイルリスト
    // User-defined file list
    private string[] _MotwUserDefinedTypes;

    public string[] MotwUserDefinedTypes
    {
      get => this._MotwUserDefinedTypes;
      set => this._MotwUserDefinedTypes = value;
    }

    // すべてのファイルにMOTWが伝播する
    // MOTW propagation to all files
    private bool _isCheckMotwAllFiles;
    public bool isCheckMotwAllFiles
    {
      get => this._isCheckMotwAllFiles;
      set => this._isCheckMotwAllFiles = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Password file
    #region
    //----------------------------------------------------------------------

    private bool _fAllowPassFile;                  // パスワードファイルのドラッグ＆ドロップを許す
    //Allow a password file to drag and drop
    public bool fAllowPassFile
    {
      get => this._fAllowPassFile;
      set => this._fAllowPassFile = value;
    }

    private bool _fCheckPassFile;                  // 暗号化時にパスワードファイルを自動チェックする
    //Check password file for Encryption
    public bool fCheckPassFile
    {
      get => this._fCheckPassFile;
      set => this._fCheckPassFile = value;
    }

    private string _PassFilePath;                 // 暗号時のパスワードファイルパス
                                                  //Password file path for encryption
    public string PassFilePath
    {
      get => this._PassFilePath;
      set => this._PassFilePath = value;
    }

    private string _TempEncryptionPassFilePath;    // 暗号時の一時パスワードファイルパス（保存されない）
    //The path of the password file that is dragged and dropped by user
    public string TempEncryptionPassFilePath
    {
      get => this._TempEncryptionPassFilePath;
      set => this._TempEncryptionPassFilePath = value;
    }


    private bool _fCheckPassFileDecrypt;          // 復号時にパスワードファイルを自動チェックする
    //Check password file for Decryption
    public bool fCheckPassFileDecrypt
    {
      get => this._fCheckPassFileDecrypt;
      set => this._fCheckPassFileDecrypt = value;
    }

    private string _PassFilePathDecrypt;          // 復号時のパスワードファイルパス
    //Password file path for decryption
    public string PassFilePathDecrypt
    {
      get => this._PassFilePathDecrypt;
      set => this._PassFilePathDecrypt = value;
    }

    //The path of the password file that is dragged and dropped by user
    public string TempDecryptionPassFilePath { get; set; }

    private bool _fNoErrMsgOnPassFile;            // パスワードファイルがない場合エラーを出さない
    public bool fNoErrMsgOnPassFile               // //It's not issued an error message when password file doesn't exists
    {
      get => this._fNoErrMsgOnPassFile;
      set => this._fNoErrMsgOnPassFile = value;
    }

    private bool _fPasswordFileExe;               //パスワードファイルで確認なく実行する
    public bool fPasswordFileExe                  //Encrypt/Decrypt by the password of Password file without confirming.
    {
      get => this._fPasswordFileExe;
      set => this._fPasswordFileExe = value;
    }

    public bool PasswordFilePriority { get; set; }

    #endregion

    //----------------------------------------------------------------------
    // Camouflage Extension
    #region
    //----------------------------------------------------------------------
    private bool _fAddCamoExt;            // 暗号化ファイルの拡張子を偽装する
                                          // Encrypted files camouflage with extension
    public bool fAddCamoExt
    {
      get => this._fAddCamoExt;
      set => this._fAddCamoExt = value;
    }

    private string _CamoExt;              // その拡張子
                                          // It's extension string
    public string CamoExt
    {
      get => this._CamoExt;
      set => this._CamoExt = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Input Password limit
    #region
    //----------------------------------------------------------------------

    private int _MissTypeLimitsNum;          // パスワードのタイプミス制限回数（ver.2.70～）
    //Set number of times to input password in encrypt files:
    public int MissTypeLimitsNum
    {
      get => this._MissTypeLimitsNum;
      set
      {
        if ((this._MissTypeLimitsNum = value) < 0 || (this._MissTypeLimitsNum = value) > 10)
        {
          this._MissTypeLimitsNum = 3; // default value
        }
      }
    }

    private bool _fBroken;                // タイプミス回数を超えたときにファイルを破壊するか否か（ver.2.70～）
    //If input wrong password to the number of times, destroy it
    public bool fBroken
    {
      get => this._fBroken;
      set => this._fBroken = value;
    }

    #endregion

    //----------------------------------------------------------------------
    // Salvage 
    #region
    private bool _fSalvageToCreateParentFolderOneByOne;
    // Decrypt one by one while creating the parent folder.
    public bool fSalvageToCreateParentFolderOneByOne
    {
      get => this._fSalvageToCreateParentFolderOneByOne;
      set => this._fSalvageToCreateParentFolderOneByOne = value;
    }

    private bool _fSalvageIntoSameDirectory;
    // Decrypt all files into the directory of the same hierarchy.
    public bool fSalvageIntoSameDirectory
    {
      get => this._fSalvageIntoSameDirectory;
      set => this._fSalvageIntoSameDirectory = value;
    }

    // Ignore file hash value check.
    public bool fSalvageIgnoreHashCheck { get; set; }

    #endregion

    //----------------------------------------------------------------------
    // Develop mode
    #region
    private bool _fDeveloperConsole;
    // Ignore file hash value check.
    public bool fDeveloperConsole
    {
      get => this._fDeveloperConsole;
      set => this._fDeveloperConsole = value;
    }

    private int _DeveloperConsolePosX;
    public int DeveloperConsolePosX
    {
      get => this._DeveloperConsolePosX;
      set => this._DeveloperConsolePosX = value;
    }

    private int _DeveloperConsolePosY;
    public int DeveloperConsolePosY
    {
      get => this._DeveloperConsolePosY;
      set => this._DeveloperConsolePosY = value;
    }

    private int _DeveloperConsoleWidth;
    public int DeveloperConsoleWidth
    {
      get => this._DeveloperConsoleWidth;
      set => this._DeveloperConsoleWidth = value;
    }

    private int _DeveloperConsoleHeight;
    public int DeveloperConsoleHeight
    {
      get => this._DeveloperConsoleHeight;
      set => this._DeveloperConsoleHeight = value;
    }
    #endregion

    //----------------------------------------------------------------------
    // Command line Only
    #region
    //----------------------------------------------------------------------

    // The sum of the command line arguments
    private int _CommandLineArgsNum = 0;          // コマンドライン引数の合計
    public int CommandLineArgsNum => this._CommandLineArgsNum;

    // メインフォームを非表示
    //When running on the command line, do not display the main form
    public bool fHideMainForm { get; set; } = false;

    // エラーメッセージ表示の抑制
    //When running on the command line, do not display error message
    public bool fNoErrorMsg { get; set; } = false;

    private int _ProcTypeWithoutAsk = 0;         // 暗号/復号処理か（動作設定にはない。コマンドラインのみ）
    //On the command line, specify encryption or decryption ( 1: Encrypt, 2: Decrypt )
    public int ProcTypeWithoutAsk
    {
      get => this._ProcTypeWithoutAsk;
      set => this._ProcTypeWithoutAsk = value;
    }

    // 1: ATC, 2: EXE(ATC), 3: ZIP, 0: Others(Encrypt file?);
    public int[] FileType { get; private set; } = [0, 0, 0, 0];

    #endregion

    //----------------------------------------------------------------------
    // Others
    #region
    //----------------------------------------------------------------------

    private string _Language;                    //使用言語
    //Language
    public string Language
    {
      get => this._Language;
      set => this._Language = value;
    }

    //現在の設定
    //Language
    public string CurrentConfiguration { get; set; }

    private string _ApplicationPath;              //アタッシェケース本体（EXE）の場所
    public string ApplicationPath
    {
      get => this._ApplicationPath;
      set => this._ApplicationPath = value;
    }

    private int _AppVersion;                      //アタッシェケースのバージョン
    // Get this application version
    public int AppVersion
    {
      get => this._AppVersion;
      set => this._AppVersion = value;
    }

    private int _ActiveTreeNode;                  // 開いていたツリービュー項目
    // Active option panel 
    public int ActiveTreeNode
    {
      get => this._ActiveTreeNode;
      set => this._ActiveTreeNode = value;
    }

    private string _SaveToIniDirPath;             // 保存ダイアログの初期ディレクトリ
    // Active option panel 
    public string SaveToIniDirPath
    {
      get => this._SaveToIniDirPath;
      set => this._SaveToIniDirPath = value;
    }

    #endregion

    //======================================================================
    /// <summary>
    /// Constructor（コンストラクタ）
    /// </summary>
    //======================================================================
    private AppSettings()
    {
      _FileList = [];
    }

    public AppSettings(string registryPathMyKey)
    {
      RegistryPathMyKey = registryPathMyKey;
    }

    //======================================================================
    /// <summary>
    /// アタッシェケースの設定を読み込まれたソースから取得する
    /// Get the AttacheCase configuration from the settings of reading sources on the beginning
    /// </summary>
    //======================================================================
    public void ReadOptions()
    {
      //----------------------------------------------------------------------
      // アタッシェケースのすべての設定をレジストリから読み込む
      // Load ALL settings of AttacheCase from registry
      this.ReadOptionsFromRegistry();

      //----------------------------------------------------------------------
      // アタッシェケース本体のある場所に設定用INIファイルがあるか？
      // Is there INI file in the location where AttacheCase Application exists?
      var FilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? string.Empty, "_AtcCase.ini");
      if (File.Exists(FilePath) == true)
      {
        IniFilePath = FilePath;
        ReadOptionFromIniFile(IniFilePath);
      }

      //----------------------------------------------------------------------
      // 起動時のコマンドライン引数に渡されたファイルパスに、設定用INIファイルがあるか？ 
      // Is there INI file in the file path of startup command line arguments?
      var cmds = Environment.GetCommandLineArgs();
      for (var i = 0; i < cmds.Length; i++)
      {
        // File list processed
        if (i > 0 && cmds[i].IndexOf("/", StringComparison.Ordinal) != 0)
        {
          if (File.Exists(cmds[i]) == true || Directory.Exists(cmds[i]) == true)
          {
            FilePath = Path.Combine(Path.GetDirectoryName(cmds[i]) ?? string.Empty, "_AtcCase.ini");
            if (File.Exists(FilePath) == true)
            {
              IniFilePath = FilePath;
              ReadOptionFromIniFile(IniFilePath);
              break;
            }
          }
        }
      }

      //----------------------------------------------------------------------
      // 起動時のコマンドラインオプションから設定を読み込む（上書き）
      // Load the settings from startup command line option ( Overwrite )
      _CommandLineArgsNum = this.ParserArguments();

    }

    //======================================================================
    /// <summary>
    /// アタッシェケースの設定を読み込まれたソースへ保存する（または保存しない）
    /// Save the setting of AttacheCase to source of reading on the beginning (or not to save)
    /// </summary>
    //======================================================================
    public void SaveOptions(bool fTemporarySettings)
    {
      //コマンドラインからの設定が読み込まれているときは保存しない
      if (_CommandLineArgsNum > 0 && fTemporarySettings == true)
      {
        // Do not save when the setting from the command line argument is loaded
        return;
      }

      if (File.Exists(IniFilePath) == true && fTemporarySettings == true)
      {
        // INIファイルへの保存
        // If there is to read INI file, save to it.
        WriteOptionToIniFile(IniFilePath);
        return;
      }

      //レジストリへの保存
      SaveOptionsToRegistry();

    }

    //======================================================================
    /// <summary>
    /// Read all options of AttacheCase from sysytem registry
    /// アタッシェケースの設定をレジストリから読み込む
    /// </summary>
    //======================================================================
    public void ReadOptionsFromRegistry()
    {
      //「アタッシェケース4」の設定がなく、「アタッシェケース#3」の設定がある
      // "AttacheCase4" setting not be found, but "AttacheCase#3" setting be found
      using (var reg3 = Registry.CurrentUser.OpenSubKey(RegistryRootPath3, false))
      using (var reg4 = Registry.CurrentUser.OpenSubKey(RegistryRootPath4, true))
      {
        if (reg3 != null && reg4 == null)
        {
          // Copy 3 to 4
          RegCopyTo(reg3, Registry.CurrentUser.CreateSubKey(RegistryRootPath4));
        }
      }

      using (var reg = Registry.CurrentUser.OpenSubKey(RegistryPathAppInfo, false))
      {
        if (reg == null)
        {
          Registry.CurrentUser.CreateSubKey(RegistryPathAppInfo);
          Registry.CurrentUser.CreateSubKey(RegistryPathWindowPos);
          Registry.CurrentUser.CreateSubKey(RegistryPathMyKey);
          Registry.CurrentUser.CreateSubKey(RegistryPathOption);
        }
      }

      //----------------------------------------------------------------------
      // Options has been stored in the registry for 'string type' in old version of AttacheCase,
      // so when you take it out, you need to cast values to the type as necessary.
      // 旧バージョンのアタッシェケースでは、レジストリへ文字列型で格納していたため、
      // レジストリから取り出すときには、必要に応じてその型へキャストする必要があります。
      //
      //-----------------------------------
      // Open the key (HKEY_CURRENT_USER\Software\Hibara\AttacheCase）by ReadOnly
      using (var reg = Registry.CurrentUser.OpenSubKey(RegistryPathAppInfo, true))
      {
        //Application information
        //_ApplicationPath = (string)reg.GetValue("AppPath", "");
        _ApplicationPath = Application.ExecutablePath;
        _AppVersion = (int)reg.GetValue("AppVersion", 0);
      }

      //----------------------------------------------------------------------
      // Windows Positions and size
      using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(RegistryPathWindowPos, true))
      {
        _FormTop = int.Parse((string)reg.GetValue("WindowTop", "-1"));
        _FormLeft = int.Parse((string)reg.GetValue("WindowLeft", "-1"));
        _FormWidth = int.Parse((string)reg.GetValue("WindowWidth", "420"));
        _FormHeight = int.Parse((string)reg.GetValue("WindowHeight", "380"));
        // Main form state
        _FormStyle = int.Parse((string)reg.GetValue("FormStyle", "0"));     // WindowState = Normal;
        // Selected node index
        _ActiveTreeNode = int.Parse((string)reg.GetValue("ActiveTreeNode", "0"));
        // Initial directory path in dialog
        _InitDirPath = (string)reg.GetValue("InitDirPath", "");

      }

      //----------------------------------------------------------------------
      // Passwords
      using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(RegistryPathMyKey, true))
      {
        _fMyEncryptPasswordKeep = (string)reg.GetValue("fMyEncryptPasswordKeep", "0") == "1";
        _fMyDecryptPasswordKeep = (string)reg.GetValue("fMyDecryptPasswordKeep", "0") == "1";

        _MyEncryptPasswordBinary = (byte[])reg.GetValue("MyEncryptPasswordString", null);
        if (_MyEncryptPasswordBinary == null)
        {
        }
        else
        {
          MyEncryptPasswordString = DecryptMyPassword(_MyEncryptPasswordBinary);
        }

        _MyEncryptPasswordBinary = null;

        MyDecryptPasswordBinary = (byte[])reg.GetValue("MyDecryptPasswordString", null);
        if (MyDecryptPasswordBinary == null)
        {
        }
        else
        {
          MyDecryptPasswordString = DecryptMyPassword(MyDecryptPasswordBinary);
        }

        MyDecryptPasswordBinary = null;

        _fMemPasswordExe = (string)reg.GetValue("fMemPasswordExe", "0") == "1";
        _fNotMaskPassword = (string)reg.GetValue("fNotMaskPassword", "0") == "1";

        _fPasswordStrengthMeter = (string)reg.GetValue("fPasswordStrengthMeter", "1") == "1";

      }

      //----------------------------------------------------------------------
      // Options
      using (var reg = Registry.CurrentUser.OpenSubKey(RegistryPathOption, true))
      {
        //-----------------------------------
        // General
        _fEndToExit = (string)reg.GetValue("fEndToExit", "0") == "1";
        _fOpenFile = (string)reg.GetValue("fOpenFile", "0") == "1";
        _fShowDialogWhenExeFile = ((string)reg.GetValue("fShowDialogWhenExeFile", "1") == "1");
        _ShowDialogWhenMultipleFilesNum = int.Parse((string)reg.GetValue("ShowDialogWhenMultipleFilesNum", "5"));
        _fAskEncDecode = (string)reg.GetValue("fAskEncDecode", "0") == "1";
        _fSaveToExeout = (string)reg.GetValue("fSaveToExeout", "0") == "1";
        _fShowExeoutChkBox = (string)reg.GetValue("fShowExeoutChkBox", "1") == "1";
        _ThemeColorName = (string)reg.GetValue("ThemeColorName", "auto");

        //-----------------------------------
        // Window
        _fMainWindowMinimize = (string)reg.GetValue("fMainWindowMinimize", "0") == "1";
        _fTaskBarHide = (string)reg.GetValue("fTaskBarHide", "0") == "1";
        _fTaskTrayIcon = (string)reg.GetValue("fTaskTrayIcon", "0") == "1";
        _fOpenFolder = (string)reg.GetValue("fOpenFolder", "0") == "1";
        _fWindowForeground = (string)reg.GetValue("fWindowForeground", "1") == "1";
        _fNoMultipleInstance = (string)reg.GetValue("fNoMultipleInstance", "0") == "1";
        _fTurnOnIMEsTextBoxForPasswordEntry = (string)reg.GetValue("fTurnOnIMEsTextBoxForPasswordEntry", "0") == "1";
        _fNotMaximizedInTabletMode = (string)reg.GetValue("fNotMaximizedInTabletMode", "0") == "1";

        //-----------------------------------
        // Save
        // 1: ATC, 2: EXE(ATC), 3: ZIP, 0: Others(Encrypt file?)
        _EncryptionFileType = int.Parse((string)reg.GetValue("EncryptionFileType", "0"));
        _fEncryptionSameFileTypeAlways = (string)reg.GetValue("fEncryptionSameFileTypeAlways", "1") == "1";
        _EncryptionSameFileTypeAlways = int.Parse((string)reg.GetValue("EncryptionSameFileTypeAlways", "0"));
        _fEncryptionSameFileTypeBefore = (string)reg.GetValue("fEncryptionSameFileTypeBefore", "1") == "1";
        _EncryptionSameFileTypeBefore = int.Parse((string)reg.GetValue("EncryptionSameFileTypeBefore", "0"));
        _fOver4GBok = (string)reg.GetValue("fOver4GBok", "0") == "1";
        _fAskAboutToExceed4Gib = (string)reg.GetValue("fAskAboutToExceed4Gib", "1") == "1";
        _fOutputExeForOlderOS = (string)reg.GetValue("fOutputExeForOlderOS", "0") == "1";

        //-----------------------------------
        // Save Encrypt
        _fSaveToSameFldr = (string)reg.GetValue("fSaveToSameFldr", "0") == "1";
        _SaveToSameFldrPath = (string)reg.GetValue("SaveToSameFldrPath", "");

        _fEncryptConfirmOverwrite = ((string)reg.GetValue("fEncryptConfirmOverwrite", "1") == "1");
        _fAllFilePack = (string)reg.GetValue("fAllFilePack", "0") == "1";
        _fFilesOneByOne = (string)reg.GetValue("fFilesOneByOne", "0") == "1";
        _fNormal = _fAllFilePack == false && _fFilesOneByOne == false;
        _fKeepTimeStamp = (string)reg.GetValue("fKeepTimeStamp", "0") == "1";
        _fExtInAtcFileName = (string)reg.GetValue("fExtInAtcFileName", "0") == "1";
        _fAutoName = (string)reg.GetValue("fAutoName", "0") == "1";
        _AutoNameFormatText = (string)reg.GetValue("AutoNameFormatText", "<filename>_<date:yyyy_MM_dd><ext>");

        _fAutoNameAlphabets = (string)reg.GetValue("fAutoNameAlphabets", "1") == "1";
        _fAutoNameLowerCase = (string)reg.GetValue("fAutoNameLowerCase", "1") == "1";
        _fAutoNameUpperCase = (string)reg.GetValue("fAutoNameUpperCase", "1") == "1";
        _fAutoNameNumbers = (string)reg.GetValue("fAutoNameNumbers", "1") == "1";
        _fAutoNameSymbols = (string)reg.GetValue("fAutoNameSymbols", "0") == "1";

        //-----------------------------------
        // Save Decrypt
        _fDecodeToSameFldr = (string)reg.GetValue("fDecodeToSameFldr", "0") == "1";
        _DecodeToSameFldrPath = (string)reg.GetValue("DecodeToSameFldrPath", "");
        _fDecryptConfirmOverwrite = (string)reg.GetValue("fDecryptConfirmOverwrite", "1") == "1";
        _fNoParentFldr = (string)reg.GetValue("fNoParentFldr", "0") == "1";
        _fSameTimeStamp = (string)reg.GetValue("fSameTimeStamp", "0") == "1";
        _fCompareFile = (string)reg.GetValue("fCompareFile", "0") == "1";

        //-----------------------------------
        // Password ZIP
        _fZipToSameFldr = (string)reg.GetValue("fZipToSameFldr", "0") == "1";
        _ZipToSameFldrPath = (string)reg.GetValue("ZipToSameFldrPath", "");
        _fZipConfirmOverwrite = (string)reg.GetValue("fZipConfirmOverwrite", "1") == "1";
        _ZipEncryptionAlgorithm = int.Parse((string)reg.GetValue("ZipEncryptionAlgorithm", "0"));

        //-----------------------------------
        // Delete
        _fDelOrgFile = (string)reg.GetValue("fDelOrgFile", "0") == "1";
        _fEncryptShowDelChkBox = (string)reg.GetValue("fEncryptShowDelChkBox", "0") == "1";
        _fConfirmToDeleteAfterEncryption = (string)reg.GetValue("fConfirmToDeleteAfterEncryption", "1") == "1";

        _fDelEncFile = (string)reg.GetValue("fDelEncFile", "0") == "1";
        _fDecryptShowDelChkBox = (string)reg.GetValue("fDecryptShowDelChkBox", "0") == "1";
        _fConfirmToDeleteAfterDecryption = (string)reg.GetValue("fConfirmToDeleteAfterDecryption", "1") == "1";

        _fCompleteDelFile = int.Parse((string)reg.GetValue("fCompleteDelFile", "0"));
        _DelRandNum = int.Parse((string)reg.GetValue("DelRandNum", "0"));
        _DelZeroNum = int.Parse((string)reg.GetValue("DelZeroNum", "1"));

        //-----------------------------------
        // Compression
        _CompressRate = int.Parse((string)reg.GetValue("CompressRate", "6"));
        _CompressionLevel = int.Parse((string)reg.GetValue("CompressionLevel", "0"));

        //-----------------------------------
        // System
        _fAssociationFile = int.Parse((string)reg.GetValue("fAssociationFile", "1"));
        _AtcsFileIconIndex = int.Parse((string)reg.GetValue("AtcsFileIconIndex", "1"));
        _UserRegIconFilePath = (string)reg.GetValue("UserRegIconFilePath", "");

        //-----------------------------------
        // Import / Export
        fAlwaysReadIniFile = (string)reg.GetValue("fAlwaysReadIniFile", "0") == "1";
        fShowConfirmationDialogToReadIniFile = (string)reg.GetValue("fShowConfirmationDialogToReadIniFile", "1") == "1";

        //-----------------------------------
        // Advanced
        _isCheckMotwExecutableFiles = (string)reg.GetValue("fMotwExecutableFiles", "1") == "1";
        _isCheckMotwOfficeFiles = (string)reg.GetValue("fMotwOfficeFiles", "1") == "1";
        _isCheckMotwUserDefinedTypes = (string)reg.GetValue("fMotwUserDefinedTypes", "0") == "1";
        _isCheckMotwAllFiles = (string)reg.GetValue("fMotwAllFiles", "0") == "1";
        var value = reg.GetValue("MotwUserDefinedTypes", null);
        _MotwUserDefinedTypes = value as string[] ?? [];

        //-----------------------------------
        // Password file 
        _fAllowPassFile = (string)reg.GetValue("fAllowPassFile", "0") == "1";
        _fCheckPassFile = (string)reg.GetValue("fCheckPassFile", "0") == "1";
        _fCheckPassFileDecrypt = (string)reg.GetValue("fCheckPassFileDecrypt", "0") == "1";
        _PassFilePath = (string)reg.GetValue("PassFilePath", "");
        _PassFilePathDecrypt = (string)reg.GetValue("PassFilePathDecrypt", "");
        _fNoErrMsgOnPassFile = (string)reg.GetValue("fNoErrMsgOnPassFile", "0") == "1";
        _fPasswordFileExe = (string)reg.GetValue("fPasswordFileExe", "0") == "1";

        //-----------------------------------
        // Camouflage Extension 
        _fAddCamoExt = ((string)reg.GetValue("fAddCamoExt", "0") == "1") ? true : false;
        _CamoExt = (string)reg.GetValue("CamoExt", ".jpg");

        //-----------------------------------
        // Input Password limit
        _MissTypeLimitsNum = int.Parse((string)reg.GetValue("MissTypeLimitsNum", "3"));
        _fBroken = ((string)reg.GetValue("fBroken", "0") == "1") ? true : false;

        //-----------------------------------
        // Salvage
        _fSalvageToCreateParentFolderOneByOne = (string)reg.GetValue("fSalvageToCreateParentFolderOneByOne", "0") == "1";
        _fSalvageIntoSameDirectory = (string)reg.GetValue("fSalvageIntoSameDirectory", "0") == "1";

        //-----------------------------------
        // Developer mode
        _fDeveloperConsole = ((string)reg.GetValue("fDeveloperConsole", "0") == "1");
        _DeveloperConsolePosX = int.Parse((string)reg.GetValue("DeveloperConsolePosX", "-1"));
        _DeveloperConsolePosY = int.Parse((string)reg.GetValue("DeveloperConsolePosY", "-1"));
        _DeveloperConsoleWidth = int.Parse((string)reg.GetValue("DeveloperConsoleWidth", "640"));
        _DeveloperConsoleHeight = int.Parse((string)reg.GetValue("DeveloperConsoleHeight", "480"));

        //-----------------------------------
        // Others
        _Language = (string)reg.GetValue("Language", "");
        _SaveToIniDirPath = (string)reg.GetValue("SaveToIniDirPath", "");
      }

    }

    //======================================================================
    /// <summary>
    /// Save options of AttacheCase to system registry
    /// アタッシェケースの設定をレジストリに書き込む
    /// </summary>
    //======================================================================
    public void SaveOptionsToRegistry()
    {
      //-----------------------------------
      // Open the registry key (AppInfo).
      using (var reg = Registry.CurrentUser.CreateSubKey(RegistryPathAppInfo))
      {
        reg.SetValue("AppPath", Application.ExecutablePath);
        reg.SetValue("AppVersion", _AppVersion);
      }

      //-----------------------------------
      // Window
      using (var reg = Registry.CurrentUser.CreateSubKey(RegistryPathWindowPos))
      {
        //-----------------------------------
        // Windows Positions and size
        reg.SetValue("WindowTop", _FormTop.ToString());
        reg.SetValue("WindowLeft", _FormLeft.ToString());
        reg.SetValue("WindowWidth", _FormWidth.ToString());
        reg.SetValue("WindowHeight", _FormHeight.ToString());
        reg.SetValue("FormStyle", _FormStyle.ToString());
        reg.SetValue("ActiveTreeNode", _ActiveTreeNode.ToString());
        reg.SetValue("InitDirPath", _InitDirPath);
      }

      //----------------------------------------------------------------------
      // These registry is stored in a 'string' type 
      // for compatibility with old versions of AttacheCase.
      //
      // 旧バージョンとの互換性を保つため、極力、設定のレジストリへの保存は
      //「文字列型」で格納します（※記憶パスワードだけ例外）。
      //     
      //----------------------------------------------------------------------
      // Passwords
      using (var reg = Registry.CurrentUser.CreateSubKey(RegistryPathMyKey))
      {
        reg.SetValue("fMyEncryptPasswordKeep", _fMyEncryptPasswordKeep == true ? "1" : "0");
        reg.SetValue("fMyDecryptPasswordKeep", _fMyDecryptPasswordKeep == true ? "1" : "0");
        reg.SetValue("MyEncryptPasswordString", EncryptMyPassword(MyEncryptPasswordString), RegistryValueKind.Binary);
        reg.SetValue("MyDecryptPasswordString", EncryptMyPassword(MyDecryptPasswordString), RegistryValueKind.Binary);
        reg.SetValue("fMemPasswordExe", _fMemPasswordExe == true ? "1" : "0");
        reg.SetValue("fNotMaskPassword", _fNotMaskPassword == true ? "1" : "0");
        reg.SetValue("fPasswordStrengthMeter", _fPasswordStrengthMeter == true ? "1" : "0");
      }

      //----------------------------------------------------------------------
      // Options
      using (var reg = Registry.CurrentUser.CreateSubKey(RegistryPathOption))
      {
        //-----------------------------------
        // General
        reg.SetValue("fEndToExit", _fEndToExit == true ? "1" : "0");
        reg.SetValue("fOpenFile", _fOpenFile == true ? "1" : "0");
        reg.SetValue("fShowDialogWhenExeFile", _fShowDialogWhenExeFile == true ? "1" : "0");
        reg.SetValue("ShowDialogWhenMultipleFilesNum", _ShowDialogWhenMultipleFilesNum.ToString());  // int

        reg.SetValue("fAskEncDecode", _fAskEncDecode == true ? "1" : "0");
        reg.SetValue("fSaveToExeout", _fSaveToExeout == true ? "1" : "0");
        reg.SetValue("fShowExeoutChkBox", _fShowExeoutChkBox == true ? "1" : "0");
        reg.SetValue("ThemeColorName", _ThemeColorName);

        //-----------------------------------
        // Window
        reg.SetValue("fMainWindowMinimize", _fMainWindowMinimize == true ? "1" : "0");
        reg.SetValue("fTaskBarHide", _fTaskBarHide == true ? "1" : "0");
        reg.SetValue("fTaskTrayIcon", _fTaskTrayIcon == true ? "1" : "0");
        reg.SetValue("fOpenFolder", _fOpenFolder == true ? "1" : "0");
        reg.SetValue("fWindowForeground", _fWindowForeground == true ? "1" : "0");
        reg.SetValue("fNoMultipleInstance", _fNoMultipleInstance == true ? "1" : "0");
        reg.SetValue("fTurnOnIMEsTextBoxForPasswordEntry", _fTurnOnIMEsTextBoxForPasswordEntry == true ? "1" : "0");
        reg.SetValue("fNotMaximizedInTabletMode", _fNotMaximizedInTabletMode == true ? "1" : "0");

        //-----------------------------------
        // Save
        reg.SetValue("EncryptionFileType", _EncryptionFileType.ToString());
        reg.SetValue("fEncryptionSameFileTypeAlways", _fEncryptionSameFileTypeAlways == true ? "1" : "0");
        reg.SetValue("EncryptionSameFileTypeAlways", _EncryptionSameFileTypeAlways.ToString());
        reg.SetValue("fEncryptionSameFileTypeBefore", _fEncryptionSameFileTypeBefore == true ? "1" : "0");
        reg.SetValue("EncryptionSameFileTypeBefore", _EncryptionSameFileTypeBefore.ToString());
        reg.SetValue("fOver4GBok", _fOver4GBok == true ? "1" : "0");
        reg.SetValue("fAskAboutToExceed4Gib", _fAskAboutToExceed4Gib == true ? "1" : "0");
        reg.SetValue("fOutputExeForOlderOS", _fOutputExeForOlderOS == true ? "1" : "0");

        //-----------------------------------
        // Save Encrypt
        reg.SetValue("fSaveToSameFldr", _fSaveToSameFldr == true ? "1" : "0");
        reg.SetValue("SaveToSameFldrPath", _SaveToSameFldrPath);
        reg.SetValue("fEncryptConfirmOverwrite", _fEncryptConfirmOverwrite == true ? "1" : "0");
        reg.SetValue("fAllFilePack", _fAllFilePack == true ? "1" : "0");
        reg.SetValue("fFilesOneByOne", _fFilesOneByOne == true ? "1" : "0");
        reg.SetValue("fNormal", _fNormal == true ? "1" : "0");
        reg.SetValue("fKeepTimeStamp", _fKeepTimeStamp == true ? "1" : "0");
        reg.SetValue("fExtInAtcFileName", _fExtInAtcFileName == true ? "1" : "0");
        reg.SetValue("fAutoName", _fAutoName == true ? "1" : "0");
        reg.SetValue("AutoNameFormatText", _AutoNameFormatText);
        reg.SetValue("fAutoNameAlphabets", _fAutoNameAlphabets == true ? "1" : "0");
        reg.SetValue("fAutoNameLowerCase", _fAutoNameLowerCase == true ? "1" : "0");
        reg.SetValue("fAutoNameUpperCase", _fAutoNameUpperCase == true ? "1" : "0");
        reg.SetValue("fAutoNameNumbers", _fAutoNameNumbers == true ? "1" : "0");
        reg.SetValue("fAutoNameSymbols", _fAutoNameSymbols == true ? "1" : "0");

        //-----------------------------------
        // Save Decrypt
        reg.SetValue("fDecodeToSameFldr", _fDecodeToSameFldr == true ? "1" : "0");
        reg.SetValue("DecodeToSameFldrPath", _DecodeToSameFldrPath);
        reg.SetValue("fDecryptConfirmOverwrite", _fDecryptConfirmOverwrite == true ? "1" : "0");
        reg.SetValue("fNoParentFldr", _fNoParentFldr == true ? "1" : "0");
        reg.SetValue("fSameTimeStamp", _fSameTimeStamp == true ? "1" : "0");
        reg.SetValue("fCompareFile", _fCompareFile == true ? "1" : "0");

        //-----------------------------------
        // Password ZIP
        reg.SetValue("fZipToSameFldr", _fZipToSameFldr == true ? "1" : "0");
        reg.SetValue("ZipToSameFldrPath", _ZipToSameFldrPath);
        reg.SetValue("fZipConfirmOverwrite", _fZipConfirmOverwrite == true ? "1" : "0");
        reg.SetValue("ZipEncryptionAlgorithm", _ZipEncryptionAlgorithm.ToString()); // int

        //-----------------------------------
        // Delete
        reg.SetValue("fDelOrgFile", _fDelOrgFile == true ? "1" : "0");
        reg.SetValue("fEncryptShowDelChkBox", _fEncryptShowDelChkBox == true ? "1" : "0");
        reg.SetValue("fDelEncFile", _fDelEncFile == true ? "1" : "0");
        reg.SetValue("fDecryptShowDelChkBox", _fDecryptShowDelChkBox == true ? "1" : "0");
        reg.SetValue("fConfirmToDeleteAfterEncryption", _fConfirmToDeleteAfterEncryption == true ? "1" : "0");
        reg.SetValue("fDecryptShowDelChkBox", _fDecryptShowDelChkBox == true ? "1" : "0");
        reg.SetValue("fConfirmToDeleteAfterDecryption", _fConfirmToDeleteAfterDecryption == true ? "1" : "0");
        reg.SetValue("fCompleteDelFile", _fCompleteDelFile.ToString());  //int 
        reg.SetValue("DelRandNum", _DelRandNum.ToString());
        reg.SetValue("DelZeroNum", _DelZeroNum.ToString());

        //-----------------------------------
        //Compression
        reg.SetValue("CompressRate", _CompressRate.ToString());
        reg.SetValue("CompressionLevel", _CompressionLevel.ToString());

        //-----------------------------------
        // System
        reg.SetValue("fAssociationFile", _fAssociationFile.ToString());  //int
        reg.SetValue("AtcsFileIconIndex", _AtcsFileIconIndex.ToString());  //int
        reg.SetValue("UserRegIconFilePath", _UserRegIconFilePath);

        //----------------------------------------------------------------------
        // Import / Export
        reg.SetValue("fAlwaysReadIniFile", fAlwaysReadIniFile == true ? "1" : "0");
        //reg.SetValue("fShowDialogToConfirmToReadIniFile", _fShowDialogToConfirmToReadIniFile == true ? "1" : "0");
        reg.SetValue("fShowConfirmationDialogToReadIniFile", fShowConfirmationDialogToReadIniFile == true ? "1" : "0");

        //-----------------------------------
        // Advanced
        reg.SetValue("fMotwExecutableFiles", _isCheckMotwExecutableFiles ? "1" : "0");
        reg.SetValue("fMotwOfficeFiles", _isCheckMotwOfficeFiles ? "1" : "0");
        reg.SetValue("fMotwUserDefinedTypes", _isCheckMotwUserDefinedTypes ? "1" : "0");
        reg.SetValue("fMotwAllFiles", _isCheckMotwAllFiles ? "1" : "0");

        reg.SetValue("MotwUserDefinedTypes", MotwUserDefinedTypes);

        //-----------------------------------
        // Password file
        reg.SetValue("fAllowPassFile", _fAllowPassFile == true ? "1" : "0");
        reg.SetValue("fCheckPassFile", _fCheckPassFile == true ? "1" : "0");
        reg.SetValue("PassFilePath", _PassFilePath);

        reg.SetValue("fCheckPassFileDecrypt", _fCheckPassFileDecrypt == true ? "1" : "0");
        reg.SetValue("PassFilePathDecrypt", _PassFilePathDecrypt);
        reg.SetValue("fNoErrMsgOnPassFile", _fNoErrMsgOnPassFile == true ? "1" : "0");
        reg.SetValue("fPasswordFileExe", _fPasswordFileExe == true ? "1" : "0");

        //-----------------------------------
        // Camouflage Extension
        reg.SetValue("fAddCamoExt", _fAddCamoExt == true ? "1" : "0");
        reg.SetValue("CamoExt", _CamoExt);

        //-----------------------------------
        // Input Password limit
        reg.SetValue("MissTypeLimitsNum", _MissTypeLimitsNum.ToString());
        reg.SetValue("fBroken", _fBroken == true ? "1" : "0");

        //-----------------------------------
        // Salvage
        reg.SetValue("fSalvageToCreateParentFolderOneByOne", _fSalvageToCreateParentFolderOneByOne == true ? "1" : "0");
        reg.SetValue("fSalvageIntoSameDirectory", _fSalvageIntoSameDirectory == true ? "1" : "0");

        //-----------------------------------
        // Developer mode
        reg.SetValue("fDeveloperConsole", _fDeveloperConsole == true ? "1" : "0");
        reg.SetValue("DeveloperConsolePosX", _DeveloperConsolePosX.ToString());
        reg.SetValue("DeveloperConsolePosY", _DeveloperConsolePosY.ToString());
        reg.SetValue("DeveloperConsoleWidth", _DeveloperConsoleWidth.ToString());
        reg.SetValue("DeveloperConsoleHeight", _DeveloperConsoleHeight.ToString());

        //-----------------------------------
        // Others
        reg.SetValue("Language", _Language);
        reg.SetValue("SaveToIniDirPath", _SaveToIniDirPath);
      }
    }

    //======================================================================
    /// <summary>
    /// 指定のINIファイルから設定を読み込む
    /// Read options from specified INI file
    /// </summary>
    /// <param name="IniFilePath">Specified INI file</param>
    //======================================================================
    public void ReadOptionFromIniFile(string IniFilePath)
    {
      var ReturnValue = "";

      // Whether to read the found setting file (_AtcCase.ini)?
      if (fShowConfirmationDialogToReadIniFile == true)
      {
        var frm4 = new Form4("ConfirmToReadIniFile", IniFilePath);
        frm4.ShowDialog();

        var _fReadIniFile = frm4.fReadIniFile;

        if (_fReadIniFile == true)
        {
          // Read INI file
          frm4.Dispose();
        }
        else
        {
          this.IniFilePath = "";
          frm4.Dispose();
          return;
        }
      }
      else
      {
        if (fAlwaysReadIniFile == true)
        {
          // Read INI file
        }
        else
        {
          this.IniFilePath = "";
          return;
        }
      }

      //-----------------------------------
      // Application information
      ReadIniFile(IniFilePath, ref _ApplicationPath, "AppInfo", "AppPath", "");
      ReadIniFile(IniFilePath, ref _AppVersion, "AppInfo", "AppVersion", "0");

      //-----------------------------------
      // Window positions
      //-----------------------------------
      ReadIniFile(IniFilePath, ref _FormStyle, "WindowPos", "FormStyle", "0");
      ReadIniFile(IniFilePath, ref _FormTop, "WindowPos", "WindowTop", "-1");
      ReadIniFile(IniFilePath, ref _FormLeft, "WindowPos", "WindowLeft", "-1");
      ReadIniFile(IniFilePath, ref _FormWidth, "WindowPos", "WindowWidth", "420");
      ReadIniFile(IniFilePath, ref _FormHeight, "WindowPos", "WindowHeight", "380");
      ReadIniFile(IniFilePath, ref _InitDirPath, "WindowPos", "InitDirPath", "");

      ReadIniFile(IniFilePath, ref _ActiveTreeNode, "WindowPos", "ActiveTreeNode", "0");
      ReadIniFile(IniFilePath, ref _InitDirPath, "WindowPos", "InitDirPath", "");

      //-----------------------------------
      // Passwords
      //-----------------------------------
      ReadIniFile(IniFilePath, ref _fMyEncryptPasswordKeep, "MyKey", "fMyEncryptPasswordKeep", "");
      ReadIniFile(IniFilePath, ref ReturnValue, "MyKey", "MyEncryptPasswordString", "");
      if (ReturnValue != "")
      {
        _MyEncryptPasswordBinary = HexStringToByteArray(ReturnValue.ToString());
        MyEncryptPasswordString = DecryptMyPassword(_MyEncryptPasswordBinary);
      }

      ReadIniFile(IniFilePath, ref _fMyDecryptPasswordKeep, "MyKey", "fMyDecryptPasswordKeep", "");
      ReadIniFile(IniFilePath, ref ReturnValue, "MyKey", "MyDecryptPasswordString", "");
      if (ReturnValue != "")
      {
        MyDecryptPasswordBinary = HexStringToByteArray(ReturnValue.ToString());
        MyDecryptPasswordString = DecryptMyPassword(MyDecryptPasswordBinary);
      }
      ReadIniFile(IniFilePath, ref _fMemPasswordExe, "MyKey", "fMemPasswordExe", "0");
      ReadIniFile(IniFilePath, ref _fNotMaskPassword, "MyKey", "fNotMaskPassword", "0");

      ReadIniFile(IniFilePath, ref _fPasswordStrengthMeter, "MyKey", "fPasswordStrengthMeter", "1");

      //-----------------------------------
      // Options
      //-----------------------------------

      //-----------------------------------
      // General
      ReadIniFile(IniFilePath, ref _fEndToExit, "Option", "fEndToExit", "0");
      ReadIniFile(IniFilePath, ref _fOpenFile, "Option", "fOpenFile", "0");
      ReadIniFile(IniFilePath, ref _fShowDialogWhenExeFile, "Option", "fShowDialogWhenExeFile", "1");
      ReadIniFile(IniFilePath, ref _ShowDialogWhenMultipleFilesNum, "Option", "ShowDialogWhenMultipleFilesNum", "5");
      ReadIniFile(IniFilePath, ref _fAskEncDecode, "Option", "fAskEncDecode", "0");
      ReadIniFile(IniFilePath, ref _fSaveToExeout, "Option", "fSaveToExeout", "0");
      ReadIniFile(IniFilePath, ref _fShowExeoutChkBox, "Option", "fShowExeoutChkBox", "1");
      ReadIniFile(IniFilePath, ref _ThemeColorName, "Option", "ThemeColorName", "auto");

      //-----------------------------------
      // Window
      ReadIniFile(IniFilePath, ref _fMainWindowMinimize, "Option", "fMainWindowMinimize", "0");
      ReadIniFile(IniFilePath, ref _fTaskBarHide, "Option", "fTaskBarHide", "0");
      ReadIniFile(IniFilePath, ref _fTaskTrayIcon, "Option", "fTaskTrayIcon", "0");
      ReadIniFile(IniFilePath, ref _fOpenFolder, "Option", "fOpenFolder", "0");
      ReadIniFile(IniFilePath, ref _fWindowForeground, "Option", "fWindowForeground", "1");
      ReadIniFile(IniFilePath, ref _fNoMultipleInstance, "Option", "fNoMultipleInstance", "1");
      ReadIniFile(IniFilePath, ref _fTurnOnIMEsTextBoxForPasswordEntry, "Option", "fTurnOnIMEsTextBoxForPasswordEntry", "0");
      ReadIniFile(IniFilePath, ref _fNotMaximizedInTabletMode, "Option", "fNotMaximizedInTabletMode", "0");

      //-----------------------------------
      // Save

      // 1: ATC, 2: EXE(ATC), 3: ZIP, 0: Others(Encrypt file?)
      ReadIniFile(IniFilePath, ref _EncryptionFileType, "Option", "EncryptionFileType", "0");
      ReadIniFile(IniFilePath, ref _fEncryptionSameFileTypeAlways, "Option", "fEncryptionSameFileTypeAlways", "0");
      ReadIniFile(IniFilePath, ref _EncryptionSameFileTypeAlways, "Option", "EncryptionSameFileTypeAlways", "-1");
      ReadIniFile(IniFilePath, ref _fEncryptionSameFileTypeBefore, "Option", "fEncryptionSameFileTypeBefore", "0");
      ReadIniFile(IniFilePath, ref _EncryptionSameFileTypeBefore, "Option", "EncryptionSameFileTypeBefore", "-1");
      ReadIniFile(IniFilePath, ref _fOver4GBok, "Option", "fOver4GBok", "0");
      ReadIniFile(IniFilePath, ref _fAskAboutToExceed4Gib, "Option", "fAskAboutToExceed4Gib", "1");
      ReadIniFile(IniFilePath, ref _fOutputExeForOlderOS, "Option", "fOutputExeForOlderOS", "0");

      //-----------------------------------
      // Save Encrypt
      ReadIniFile(IniFilePath, ref _fSaveToSameFldr, "Option", "fSaveToSameFldr", "0");
      ReadIniFile(IniFilePath, ref _SaveToSameFldrPath, "Option", "SaveToSameFldrPath", "");
      ReadIniFile(IniFilePath, ref _fEncryptConfirmOverwrite, "Option", "fEncryptConfirmOverwrite", "1");
      ReadIniFile(IniFilePath, ref _fAllFilePack, "Option", "fAllFilePack", "0");
      ReadIniFile(IniFilePath, ref _fFilesOneByOne, "Option", "fFilesOneByOne", "0");
      _fNormal = (_fAllFilePack == false && _fFilesOneByOne == false) ? true : false;
      ReadIniFile(IniFilePath, ref _fKeepTimeStamp, "Option", "fKeepTimeStamp", "0");
      ReadIniFile(IniFilePath, ref _fExtInAtcFileName, "Option", "fExtInAtcFileName", "0");
      ReadIniFile(IniFilePath, ref _fAutoName, "Option", "fAutoName", "0");
      ReadIniFile(IniFilePath, ref _AutoNameFormatText, "Option", "AutoNameFormatText", "<filename>_<date:yyyy_MM_dd><ext>");
      ReadIniFile(IniFilePath, ref _fAutoNameAlphabets, "Option", "fAutoNameAlphabets", "1");
      ReadIniFile(IniFilePath, ref _fAutoNameLowerCase, "Option", "fAutoNameLowerCase", "1");
      ReadIniFile(IniFilePath, ref _fAutoNameUpperCase, "Option", "fAutoNameUpperCase", "1");
      ReadIniFile(IniFilePath, ref _fAutoNameNumbers, "Option", "fAutoNameNumbers", "1");
      ReadIniFile(IniFilePath, ref _fAutoNameSymbols, "Option", "fAutoNameSymbols", "0");

      //-----------------------------------
      // Save Decrypt
      ReadIniFile(IniFilePath, ref _fDecodeToSameFldr, "Option", "fDecodeToSameFldr", "0");
      ReadIniFile(IniFilePath, ref _DecodeToSameFldrPath, "Option", "DecodeToSameFldrPath", "");
      ReadIniFile(IniFilePath, ref _fDecryptConfirmOverwrite, "Option", "fDecryptConfirmOverwrite", "1");
      ReadIniFile(IniFilePath, ref _fNoParentFldr, "Option", "fNoParentFldr", "0");
      ReadIniFile(IniFilePath, ref _fSameTimeStamp, "Option", "fSameTimeStamp", "0");
      ReadIniFile(IniFilePath, ref _fCompareFile, "Option", "fCompareFile", "0");

      //-----------------------------------
      // Password ZIP
      ReadIniFile(IniFilePath, ref _fZipToSameFldr, "Option", "fZipToSameFldr", "0");
      ReadIniFile(IniFilePath, ref _ZipToSameFldrPath, "Option", "ZipToSameFldrPath", "");
      ReadIniFile(IniFilePath, ref _fZipConfirmOverwrite, "Option", "fZipConfirmOverwrite", "1");
      ReadIniFile(IniFilePath, ref _ZipEncryptionAlgorithm, "Option", "ZipEncryptionAlgorithm", "0"); // int

      //-----------------------------------
      // Delete
      ReadIniFile(IniFilePath, ref _fDelOrgFile, "Option", "fDelOrgFile", "0");
      ReadIniFile(IniFilePath, ref _fEncryptShowDelChkBox, "Option", "fEncryptShowDelChkBox", "0");
      ReadIniFile(IniFilePath, ref _fConfirmToDeleteAfterEncryption, "Option", "fConfirmToDeleteAfterEncryption", "1");
      ReadIniFile(IniFilePath, ref _fDelEncFile, "Option", "fDelEncFile", "0");
      ReadIniFile(IniFilePath, ref _fDecryptShowDelChkBox, "Option", "fDecryptShowDelChkBox", "0");
      ReadIniFile(IniFilePath, ref _fConfirmToDeleteAfterDecryption, "Option", "fConfirmToDeleteAfterDecryption", "1");
      ReadIniFile(IniFilePath, ref _fCompleteDelFile, "Option", "fCompleteDelFile", "1");
      ReadIniFile(IniFilePath, ref _DelRandNum, "Option", "DelRandNum", "0");
      ReadIniFile(IniFilePath, ref _DelZeroNum, "Option", "DelZeroNum", "1");

      //-----------------------------------
      //Compression
      ReadIniFile(IniFilePath, ref _CompressRate, "Option", "CompressRate", "6");
      ReadIniFile(IniFilePath, ref _CompressionLevel, "Option", "CompressionLevel", "0");

      //-----------------------------------
      // System
      ReadIniFile(IniFilePath, ref _fAssociationFile, "Option", "fAssociationFile", "1");
      ReadIniFile(IniFilePath, ref _AtcsFileIconIndex, "Option", "AtcsFileIconIndex", "1");
      ReadIniFile(IniFilePath, ref _UserRegIconFilePath, "Option", "UserRegIconFilePath", "");

      //-----------------------------------
      // Import / Export (depends on the registry)
      //ReadIniFile(IniFilePath, ref _fAlwaysReadIniFile, "Option", "fAlwaysReadIniFile", "0");
      //ReadIniFile(IniFilePath, ref _fShowConfirmationDialogToReadIniFile, "Option", "fShowConfirmationDialogToReadIniFile", "1");

      //-----------------------------------
      // Advanced
      ReadIniFile(IniFilePath, ref _isCheckMotwExecutableFiles, "Option", "fMotwExecutableFiles", "1");
      ReadIniFile(IniFilePath, ref _isCheckMotwOfficeFiles, "Option", "fMotwOfficeFiles", "1");
      ReadIniFile(IniFilePath, ref _isCheckMotwUserDefinedTypes, "Option", "fMotwUserDefinedTypes", "0");
      ReadIniFile(IniFilePath, ref _isCheckMotwAllFiles, "Option", "fMotwAllFiles", "0");
      var motwUserDefinedTypesString = "";
      ReadIniFile(IniFilePath, ref motwUserDefinedTypesString, "Option", "MotwUserDefinedTypes", "");
      _MotwUserDefinedTypes = motwUserDefinedTypesString.Split(',');

      //-----------------------------------
      //Password file 
      ReadIniFile(IniFilePath, ref _fAllowPassFile, "Option", "fAllowPassFile", "0");
      ReadIniFile(IniFilePath, ref _fCheckPassFile, "Option", "fCheckPassFile", "0");
      ReadIniFile(IniFilePath, ref _PassFilePath, "Option", "PassFilePath", "");

      ReadIniFile(IniFilePath, ref _fCheckPassFileDecrypt, "Option", "fCheckPassFileDecrypt", "0");
      ReadIniFile(IniFilePath, ref _PassFilePathDecrypt, "Option", "PassFilePathDecrypt", "");
      ReadIniFile(IniFilePath, ref _fNoErrMsgOnPassFile, "Option", "fNoErrMsgOnPassFile", "0");
      ReadIniFile(IniFilePath, ref _fPasswordFileExe, "Option", "fPasswordFileExe", "0");

      //-----------------------------------
      //Camouflage Extension
      ReadIniFile(IniFilePath, ref _fAddCamoExt, "Option", "fAddCamoExt", "0");
      ReadIniFile(IniFilePath, ref _CamoExt, "Option", "CamoExt", ".jpg");

      //-----------------------------------
      // Input Password limit
      ReadIniFile(IniFilePath, ref _MissTypeLimitsNum, "Option", "MissTypeLimitsNum", "3");
      ReadIniFile(IniFilePath, ref _fBroken, "Option", "fBroken", "0");

      //-----------------------------------
      // Salvage
      ReadIniFile(IniFilePath, ref _fSalvageToCreateParentFolderOneByOne, "Option", "fSalvageToCreateParentFolderOneByOne", "0");
      ReadIniFile(IniFilePath, ref _fSalvageIntoSameDirectory, "Option", "fSalvageIntoSameDirectory", "0");

      //-----------------------------------
      // Developer mode
      ReadIniFile(IniFilePath, ref _fDeveloperConsole, "Option", "fDeveloperConsole", "0");
      ReadIniFile(IniFilePath, ref _DeveloperConsolePosX, "Option", "DeveloperConsolePosX", "-1");
      ReadIniFile(IniFilePath, ref _DeveloperConsolePosY, "Option", "DeveloperConsolePosY", "-1");
      ReadIniFile(IniFilePath, ref _DeveloperConsoleWidth, "Option", "DeveloperConsoleWidth", "640");
      ReadIniFile(IniFilePath, ref _DeveloperConsoleHeight, "Option", "DeveloperConsoleHeight", "480");

      //-----------------------------------
      // Others
      ReadIniFile(IniFilePath, ref _Language, "Option", "Language", "");
      ReadIniFile(IniFilePath, ref _SaveToIniDirPath, "Option", "SaveToIniDirPath", "");

    }

    //======================================================================
    /// <summary>
    /// 指定のINIファイルへ設定を書き込む
    /// Write options to specified INI file
    /// </summary>
    /// <param name="IniFilePath">Specified INI file</param>
    //======================================================================
    public void WriteOptionToIniFile(string IniFilePath)
    {
      //-----------------------------------
      // Open the registry key (AppInfo).
      WriteIniFile(IniFilePath, _ApplicationPath, "AppInfo", "AppPath");
      WriteIniFile(IniFilePath, _AppVersion, "AppInfo", "AppVersion");

      //-----------------------------------
      // Window
      WriteIniFile(IniFilePath, _FormTop, "WindowPos", "WindowTop");
      WriteIniFile(IniFilePath, _FormLeft, "WindowPos", "WindowLeft");
      WriteIniFile(IniFilePath, _FormWidth, "WindowPos", "WindowWidth");
      WriteIniFile(IniFilePath, _FormHeight, "WindowPos", "WindowHeight");
      WriteIniFile(IniFilePath, _FormStyle, "WindowPos", "FormStyle");

      WriteIniFile(IniFilePath, _ActiveTreeNode, "WindowPos", "ActiveTreeNode");
      WriteIniFile(IniFilePath, _InitDirPath, "WindowPos", "InitDirPath");

      //----------------------------------------------------------------------
      // Passwords
      WriteIniFile(IniFilePath, _fMyEncryptPasswordKeep, "MyKey", "fMyEncryptPasswordKeep");
      WriteIniFile(IniFilePath, _fMyDecryptPasswordKeep, "MyKey", "fMyDecryptPasswordKeep");

      var bytes = new byte[32];
      bytes = EncryptMyPassword(MyEncryptPasswordString);
      var p = ByteArrayToHexString(bytes);
      Console.WriteLine(p);
      WriteIniFile(IniFilePath, p, "MyKey", "MyEncryptPasswordString");

      bytes = new byte[32];
      bytes = EncryptMyPassword(MyDecryptPasswordString);
      p = ByteArrayToHexString(bytes);
      Console.WriteLine(p);
      WriteIniFile(IniFilePath, p, "MyKey", "MyDecryptPasswordString");

      WriteIniFile(IniFilePath, _fMemPasswordExe, "MyKey", "fMemPasswordExe");
      WriteIniFile(IniFilePath, _fNotMaskPassword, "MyKey", "fNotMaskPassword");

      WriteIniFile(IniFilePath, _fPasswordStrengthMeter, "MyKey", "fPasswordStrengthMeter");

      //----------------------------------------------------------------------
      // Options

      // General
      WriteIniFile(IniFilePath, _fEndToExit, "Option", "fEndToExit");
      WriteIniFile(IniFilePath, _fOpenFile, "Option", "fOpenFile");
      WriteIniFile(IniFilePath, _fShowDialogWhenExeFile, "Option", "fShowDialogWhenExeFile");
      WriteIniFile(IniFilePath, _ShowDialogWhenMultipleFilesNum, "Option", "ShowDialogWhenMultipleFilesNum");
      WriteIniFile(IniFilePath, _fAskEncDecode, "Option", "fAskEncDecode");
      WriteIniFile(IniFilePath, _fSaveToExeout, "Option", "fSaveToExeout");
      WriteIniFile(IniFilePath, _fShowExeoutChkBox, "Option", "fShowExeoutChkBox");
      WriteIniFile(IniFilePath, _ThemeColorName, "Option", "ThemeColorName");

      //-----------------------------------
      // Window
      WriteIniFile(IniFilePath, _fMainWindowMinimize, "Option", "fMainWindowMinimize");
      WriteIniFile(IniFilePath, _fTaskBarHide, "Option", "fTaskBarHide");
      WriteIniFile(IniFilePath, _fTaskTrayIcon, "Option", "fTaskTrayIcon");
      WriteIniFile(IniFilePath, _fOpenFolder, "Option", "fOpenFolder");
      WriteIniFile(IniFilePath, _fWindowForeground, "Option", "fWindowForeground");
      WriteIniFile(IniFilePath, _fNoMultipleInstance, "Option", "fNoMultipleInstance");
      WriteIniFile(IniFilePath, _fTurnOnIMEsTextBoxForPasswordEntry, "Option", "fTurnOnIMEsTextBoxForPasswordEntry");
      WriteIniFile(IniFilePath, _fNotMaximizedInTabletMode, "Option", "fNotMaximizedInTabletMode");

      //-----------------------------------
      // Save
      WriteIniFile(IniFilePath, _EncryptionFileType, "Option", "EncryptionFileType");
      WriteIniFile(IniFilePath, _fEncryptionSameFileTypeAlways, "Option", "fEncryptionSameFileTypeAlways");
      WriteIniFile(IniFilePath, _EncryptionSameFileTypeAlways, "Option", "EncryptionSameFileTypeAlways");
      WriteIniFile(IniFilePath, _fEncryptionSameFileTypeBefore, "Option", "fEncryptionSameFileTypeBefore");
      WriteIniFile(IniFilePath, _EncryptionSameFileTypeBefore, "Option", "EncryptionSameFileTypeBefore");
      WriteIniFile(IniFilePath, _fOver4GBok, "Option", "fOver4GBok");
      WriteIniFile(IniFilePath, _fAskAboutToExceed4Gib, "Option", "fAskAboutToExceed4Gib");
      WriteIniFile(IniFilePath, _fOutputExeForOlderOS, "Option", "fOutputExeForOlderOS");

      //-----------------------------------
      // Save Encrypt
      WriteIniFile(IniFilePath, _fSaveToSameFldr, "Option", "fSaveToSameFldr");
      WriteIniFile(IniFilePath, _SaveToSameFldrPath, "Option", "SaveToSameFldrPath");
      WriteIniFile(IniFilePath, _fEncryptConfirmOverwrite, "Option", "fEncryptConfirmOverwrite");

      WriteIniFile(IniFilePath, _fAllFilePack, "Option", "fAllFilePack");
      WriteIniFile(IniFilePath, _fFilesOneByOne, "Option", "fFilesOneByOne");
      WriteIniFile(IniFilePath, _fNormal, "Option", "fNormal");
      WriteIniFile(IniFilePath, _fKeepTimeStamp, "Option", "fKeepTimeStamp");
      WriteIniFile(IniFilePath, _fExtInAtcFileName, "Option", "fExtInAtcFileName");

      WriteIniFile(IniFilePath, _fAutoName, "Option", "fAutoName");
      WriteIniFile(IniFilePath, _AutoNameFormatText, "Option", "AutoNameFormatText");
      WriteIniFile(IniFilePath, _fAutoNameAlphabets, "Option", "fAutoNameAlphabets");
      WriteIniFile(IniFilePath, _fAutoNameLowerCase, "Option", "fAutoNameLowerCase");
      WriteIniFile(IniFilePath, _fAutoNameUpperCase, "Option", "fAutoNameUpperCase");
      WriteIniFile(IniFilePath, _fAutoNameNumbers, "Option", "fAutoNameNumbers");
      WriteIniFile(IniFilePath, _fAutoNameSymbols, "Option", "fAutoNameSymbols");

      //-----------------------------------
      // Save Decrypt
      WriteIniFile(IniFilePath, _fDecodeToSameFldr, "Option", "fDecodeToSameFldr");
      WriteIniFile(IniFilePath, _DecodeToSameFldrPath, "Option", "DecodeToSameFldrPath");
      WriteIniFile(IniFilePath, _fDecryptConfirmOverwrite, "Option", "fDecryptConfirmOverwrite");
      WriteIniFile(IniFilePath, _fNoParentFldr, "Option", "fNoParentFldr");
      WriteIniFile(IniFilePath, _fSameTimeStamp, "Option", "fSameTimeStamp");
      WriteIniFile(IniFilePath, _fCompareFile, "Option", "fCompareFile");

      //-----------------------------------
      // Password ZIP
      WriteIniFile(IniFilePath, _fZipToSameFldr, "Option", "fZipToSameFldr");
      WriteIniFile(IniFilePath, _ZipToSameFldrPath, "Option", "ZipToSameFldrPath");
      WriteIniFile(IniFilePath, _fZipConfirmOverwrite, "Option", "fZipConfirmOverwrite");
      WriteIniFile(IniFilePath, _ZipEncryptionAlgorithm, "Option", "ZipEncryptionAlgorithm");

      //-----------------------------------
      // Delete
      WriteIniFile(IniFilePath, _fDelOrgFile, "Option", "fDelOrgFile");
      WriteIniFile(IniFilePath, _fEncryptShowDelChkBox, "Option", "fEncryptShowDelChkBox");
      WriteIniFile(IniFilePath, _fConfirmToDeleteAfterEncryption, "Option", "fConfirmToDeleteAfterEncryption");

      WriteIniFile(IniFilePath, _fDelEncFile, "Option", "fDelEncFile");
      WriteIniFile(IniFilePath, _fDecryptShowDelChkBox, "Option", "fDecryptShowDelChkBox");
      WriteIniFile(IniFilePath, _fConfirmToDeleteAfterDecryption, "Option", "fConfirmToDeleteAfterDecryption");

      WriteIniFile(IniFilePath, _fCompleteDelFile, "Option", "fCompleteDelFile");
      WriteIniFile(IniFilePath, _DelRandNum, "Option", "DelRandNum");
      WriteIniFile(IniFilePath, _DelZeroNum, "Option", "DelZeroNum");

      //-----------------------------------
      //Compression
      WriteIniFile(IniFilePath, _CompressRate, "Option", "CompressRate");
      WriteIniFile(IniFilePath, _CompressionLevel, "Option", "CompressionLevel");

      //-----------------------------------
      // System
      WriteIniFile(IniFilePath, _fAssociationFile, "Option", "fAssociationFile");  // int
      WriteIniFile(IniFilePath, _UserRegIconFilePath, "Option", "UserRegIconFilePath");

      //-----------------------------------
      // Import / Export ( Only this setting is written to the registry )

      //-----------------------------------
      // Advanced
      WriteIniFile(IniFilePath, _isCheckMotwExecutableFiles, "Option", "fMotwExecutableFiles");
      WriteIniFile(IniFilePath, _isCheckMotwOfficeFiles, "Option", "fMotwOfficeFiles");
      WriteIniFile(IniFilePath, _isCheckMotwUserDefinedTypes, "Option", "fMotwUserDefinedTypes");
      WriteIniFile(IniFilePath, _isCheckMotwAllFiles, "Option", "fMotwAllFiles");

      WriteIniFile(IniFilePath, string.Join(",", _MotwUserDefinedTypes), "Option", "MotwUserDefinedTypes");

      //-----------------------------------
      //Password file
      WriteIniFile(IniFilePath, _fAllowPassFile, "Option", "fAllowPassFile");
      WriteIniFile(IniFilePath, _fCheckPassFile, "Option", "fCheckPassFile");
      WriteIniFile(IniFilePath, _PassFilePath, "Option", "PassFilePath");

      WriteIniFile(IniFilePath, _fCheckPassFileDecrypt, "Option", "fCheckPassFileDecrypt");
      WriteIniFile(IniFilePath, _PassFilePathDecrypt, "Option", "PassFilePathDecrypt");
      WriteIniFile(IniFilePath, _fNoErrMsgOnPassFile, "Option", "fNoErrMsgOnPassFile");
      WriteIniFile(IniFilePath, _fPasswordFileExe, "Option", "fPasswordFileExe");

      //-----------------------------------
      //Camouflage Extension
      WriteIniFile(IniFilePath, _fAddCamoExt, "Option", "fAddCamoExt");
      WriteIniFile(IniFilePath, _CamoExt, "Option", "CamoExt");

      //-----------------------------------
      // Input Password limit
      WriteIniFile(IniFilePath, _MissTypeLimitsNum, "Option", "MissTypeLimitsNum");
      WriteIniFile(IniFilePath, _fBroken, "Option", "fBroken");

      //-----------------------------------
      // Salvage
      WriteIniFile(IniFilePath, _fSalvageToCreateParentFolderOneByOne, "Option", "fSalvageToCreateParentFolderOneByOne");
      WriteIniFile(IniFilePath, _fSalvageIntoSameDirectory, "Option", "fSalvageIntoSameDirectory");

      //-----------------------------------
      // Developer mode
      WriteIniFile(IniFilePath, _fDeveloperConsole, "Option", "fDeveloperConsole");
      WriteIniFile(IniFilePath, _DeveloperConsolePosX, "Option", "DeveloperConsolePosX");
      WriteIniFile(IniFilePath, _DeveloperConsolePosY, "Option", "DeveloperConsolePosY");
      WriteIniFile(IniFilePath, _DeveloperConsoleWidth, "Option", "DeveloperConsoleWidth");
      WriteIniFile(IniFilePath, _DeveloperConsoleHeight, "Option", "DeveloperConsoleHeight");

      //-----------------------------------
      // Others
      WriteIniFile(IniFilePath, _Language, "Option", "Language");
      WriteIniFile(IniFilePath, _SaveToIniDirPath, "Option", "SaveToIniDirPath");
    }

    //======================================================================
    /// <summary>
    /// INIファイルからの読み込み（オーバーロード）
    /// Read options from INI file ( Overload )
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="o">Option variable</param>
    /// <param name="section">INI file 'section' item</param>
    /// <param name="key">INI file 'key' item</param>
    /// <param name="defval">INI file default value</param>
    /// <returns></returns>
    //======================================================================
    public void ReadIniFile(string filePath, ref int o, string section, string key, string defval)  // Integer
    {
      var ResultValue = new StringBuilder(255);
      if (UnsafeNativeMethods.GetPrivateProfileString(section, key, defval, ResultValue, 255, filePath) > 0)
      {
        o = int.Parse(ResultValue.ToString());
      }
    }

    public void ReadIniFile(string filePath, ref string o, string section, string key, string defval)  // string
    {
      var ResultValue = new StringBuilder(255);
      if (UnsafeNativeMethods.GetPrivateProfileString(section, key, defval, ResultValue, 255, filePath) > 0)
      {
        o = ResultValue.ToString();
      }
    }

    public void ReadIniFile(string filePath, ref bool o, string section, string key, string defval)  // bool
    {
      var ResultValue = new StringBuilder(255);
      if (UnsafeNativeMethods.GetPrivateProfileString(section, key, defval, ResultValue, 255, filePath) > 0)
      {
        o = (ResultValue.ToString() == "1" ? true : false);
      }
    }

    //======================================================================
    /// <summary>
    /// INIファイルへの書き込み
    /// Write options to INI file
    /// </summary>
    /// <param name="filePath">INI file path</param>
    /// <param name="section">INI file 'section' item</param>
    /// <param name="key">INI file 'key' item</param>
    /// <param name="o">Object(int, string, bool)</param>
    //======================================================================
    public void WriteIniFile(string filePath, object o, string section, string key)
    {
      var value = "";
      switch (o)
      {
        case null:
          value = "";
          break;
        case bool b:
          value = b == true ? "1" : "0";
          break;
        case string or int:
          value = o.ToString();
          break;
      }

      UnsafeNativeMethods.WritePrivateProfileString(section, key, value, filePath);

    }

    //======================================================================
    /// <summary>
    /// 設定を指定のXMLファイルパスから読み込み、インスタンスへ反映する
    /// Read the configuration from the specified XML file path, reflect to an instance.
    /// </summary>
    /// <param name="FilePath"></param>
    //======================================================================
    public void ReadOptionsFromXML(string FilePath)
    {
      var szr = new XmlSerializer(typeof(AppSettings));
      using var sr = new StreamReader(FilePath, new System.Text.UTF8Encoding(false));
      //XMLファイルから読み込み、逆シリアル化する
      Instance = (AppSettings)szr.Deserialize(sr);
    }

    //======================================================================
    /// <summary>
    /// 設定を指定のファイルパスにXMLファイルとして出力する
    /// Save the configuration as an XML file to the specified file path.
    /// </summary>
    /// <param name="FilePath"></param>
    //======================================================================
    public void SaveOptionsToXML(string FilePath)
    {
      var szr = new XmlSerializer(typeof(AppSettings));
      using var sw = new StreamWriter(FilePath, false, new System.Text.UTF8Encoding(false));
      //シリアル化し、XMLファイルに保存する
      szr.Serialize(sw, AppSettings.Instance);
    }

    //======================================================================
    /// <summary>
    /// 起動時のコマンドライン引数から設定を読み込む
    /// Read the configuration from the startup command line arguments
    /// </summary>
    /// <param name="cmds"></param>
    //======================================================================
    public int ParserArguments()
    {
      var i = -1;
      var cmds = Environment.GetCommandLineArgs();
      foreach (var cmd in cmds)
      {
        if (i == -1)
        {
          i++;
          continue;
        }

        char[] charsToTrim = ['\t', ',', ' '];
        var cmdOpt = cmd.Trim(charsToTrim);

        // File list processed
        if (cmdOpt.IndexOf("/", StringComparison.Ordinal) == -1)
        {
          if (File.Exists(cmdOpt) == true || Directory.Exists(cmdOpt))
          {

            if (Path.IsPathRooted(cmdOpt) == false)
            {
              cmdOpt = Path.GetFullPath(cmdOpt);
            }

            //何の種類のファイルか
            FileType[CheckFileType(cmdOpt)]++;
            _FileList.Add(cmdOpt);
          }
          continue;
        }

        // 正規表現を使って一番最初に出てくる「=」で分割する
        // Use regular expressions to split at the first “=”
        char[] splitters = ['='];
        var values = cmdOpt.Split(splitters, 2, StringSplitOptions.None);

        if (values.Length == 2)
        {
          //string key = values[0].ToLower();
          var key = values[0].ToLower();
          var value = values[1];
          int ResultNum;
          switch (key)
          {
            //-----------------------------------
            // General 一般設定
            //-----------------------------------    
            #region
            // Password
            case "/p": // パスワード
                       // 暗号化、復号の両方にパスワードを入れる
              EncryptPasswordStringFromCommandLine = value;
              _DecryptPasswordStringFromCommandLine = value;
              break;

            // Exit AttacheCase after process.
            case "/exit": // 処理後に終了するか
              if (value == "1")
              {
                _fEndToExit = true;
              }
              else if (value == "0")
              {
                _fEndToExit = false;
              }
              break;

            // Open decrypted files by associated application
            case "/opf": // 復号したファイルを関連付けされたソフトで開く
              if (value == "1")
              {
                _fOpenFile = true;
              }
              else if (value == "0")
              {
                _fOpenFile = false;
              }
              break;

            // Show dialog when containing the executable file.
            case "/exe": // 復号したファイルに実行ファイルが含まれるとき警告ダイアログを出す
              if (value == "1")
              {
                _fShowDialogWhenExeFile = true;
              }
              else if (value == "0")
              {
                _fShowDialogWhenExeFile = false;
              }
              break;

            // Show dialog when more than multiple files.
            case "/decnum":  // 復号したファイルが複数個あるとき警告ダイアログを出す
              if (int.TryParse(value, out ResultNum) == true)
              {
                _ShowDialogWhenMultipleFilesNum = ResultNum;
              }
              break;

            // Ask to encrypt or decrypt regardless of file content
            case "/askende": // 暗号/復号処理かを問い合わせる
              if (value == "1")
              {
                _fAskEncDecode = true;
              }
              else if (value == "0")
              {
                _fAskEncDecode = false;
              }
              break;

            // Confirm inputting password without masking
            case "/nohide": //「*」で隠さずパスワードを確認しながら入力する
              if (value == "1")
              {
                _fNotMaskPassword = true;
              }
              else if (value == "0")
              {
                _fNotMaskPassword = false;
              }
              break;

            // Always output to Executable file
            case "/exeout": // 常に自己実行形式で出力する
              if (value == "1")
              {
                _fSaveToExeout = true;
              }
              else if (value == "0")
              {
                _fSaveToExeout = false;
              }
              break;

            // Always display checkbox of this option
            case "/chkexeout": // メインフォームにチェックボックスを表示する
              if (value == "1")
              {
                _fShowExeoutChkBox = true;
              }
              else if (value == "0")
              {
                _fShowExeoutChkBox = false;
              }
              break;

            // Theme color option
            case "/theme": // テーマカラーの指定
              switch (value)
              {
                case "light":
                  _ThemeColorName = "light";
                  break;
                case "dark":
                  _ThemeColorName = "dark";
                  break;
                default:
                  _ThemeColorName = "auto";
                  break;
              }
              break;
            #endregion

            //-----------------------------------
            // Password パスワード
            //-----------------------------------          
            #region
            //Encrypt/Decrypt by &memorized password without confirming
            case "/mempexe": // 記憶パスワードで確認なく実行する
              if (value == "1")
              {
                _fMemPasswordExe = true;
              }
              else if (value == "0")
              {
                _fMemPasswordExe = false;
              }
              break;

            // Enable password strength meter
            case "/psm":  // パスワード強度メーターを表示するか
              if (value == "1")
              {
                _fPasswordStrengthMeter = true;
              }
              else if (value == "0")
              {
                _fPasswordStrengthMeter = false;
              }
              break;
            #endregion

            //-----------------------------------
            // Window ウィンドウ
            //-----------------------------------          
            #region
            // Always execute by minimize the window
            case "/wmin": // 常にウィンドウを最小化して処理する
              if (value == "1")
              {
                _fMainWindowMinimize = true;
                _fWindowForeground = false;  // not coexist
              }
              else if (value == "0")
              {
                _fMainWindowMinimize = false;
              }
              break;

            // Minimizing a window without appearing in the taskbar
            case "/tskb": // タスクバーに表示しない
              if (value == "1")
              {
                _fTaskBarHide = true;
              }
              else if (value == "0")
              {
                _fTaskBarHide = false;
              }
              break;

            // Display in the task tray
            case "/tsktr": // タスクトレイにアイコンを表示する
              if (value == "1")
              {
                _fTaskTrayIcon = true;
              }
              else if (value == "0")
              {
                _fTaskTrayIcon = false;
              }
              break;

            // Bring AttacheCase window in front of Desktop
            case "/front": // デスクトップで最前面にウィンドウを表示する
              if (value == "1")
              {
                _fWindowForeground = true;
                _fMainWindowMinimize = false; // not coexist
              }
              else if (value == "0")
              {
                _fWindowForeground = false;
              }
              break;

            // Not Allow multiple in&stance of AttacheCase
            case "/nomulti": // 複数起動しない 
              if (value == "1")
              {
                _fNoMultipleInstance = true;
              }
              else if (value == "0")
              {
                _fNoMultipleInstance = false;
              }
              break;

            // Turn on IMEs in all text box for password entry
            case "/imeon": // パスワード入力用のすべてのテキストボックスでIMEをオンにする
              if (value == "1")
              {
                _fTurnOnIMEsTextBoxForPasswordEntry = true;
              }
              else if (value == "0")
              {
                _fTurnOnIMEsTextBoxForPasswordEntry = false;
              }
              break;

            // Doesn't start in maximized mode when PC is operating in tablet mode
            case "/notmaximized":  // PCがタブレットモードで動作しているとき、最大化モードで起動しない
              if (value == "1")
              {
                _fNotMaximizedInTabletMode = true;
              }
              else if (value == "0")
              {
                _fNotMaximizedInTabletMode = false;
              }
              break;


            #endregion

            //-----------------------------------
            // Save 保存設定
            //-----------------------------------          
            #region
            // Encryption type ( Integer = 1: ATC, 2: EXE(ATC), 3: ZIP, 0: false )
            case "/entype": // 暗号化ファイルの種類
              if (value == "1")
              {
                _EncryptionFileType = 1;
              }
              else if (value == "2")
              {
                _EncryptionFileType = 2;
              }
              else if (value == "3")
              {
                _EncryptionFileType = 3;
              }
              else
              {
                _EncryptionFileType = 0;
              }
              break;

            // Save same encryption type always. ( Integer = 1: ATC, 2: EXE(ATC), 3: ZIP, 0: false )
            case "/sametype":  // 常に同じ暗号化ファイルの種類にする
              if (value == "1")
              {
                _EncryptionSameFileTypeAlways = 1;
                _fEncryptionSameFileTypeAlways = true;
              }
              else if (value == "2")
              {
                _EncryptionSameFileTypeAlways = 2;
                _fEncryptionSameFileTypeAlways = true;
              }
              else if (value == "3")
              {
                _EncryptionSameFileTypeAlways = 3;
                _fEncryptionSameFileTypeAlways = true;
              }
              else
              {
                _EncryptionSameFileTypeAlways = 0;
                _fEncryptionSameFileTypeAlways = false;
              }
              break;

            // Save same encryption type that was used to before. ( Integer = 1: ATC, 2: EXE(ATC), 3: ZIP, 0: false )
            case "/beforetype":  // 前に使った暗号化ファイルの種類にする
              if (value == "1")
              {
                _EncryptionSameFileTypeBefore = 1;
                _fEncryptionSameFileTypeBefore = true;
              }
              else if (value == "2")
              {
                _EncryptionSameFileTypeBefore = 2;
                _fEncryptionSameFileTypeBefore = true;
              }
              else if (value == "3")
              {
                _EncryptionSameFileTypeBefore = 3;
                _fEncryptionSameFileTypeBefore = true;
              }
              else
              {
                _EncryptionSameFileTypeBefore = 0;
                _fEncryptionSameFileTypeBefore = false;
              }
              break;

            case "/exeolder":  // 古いOSでも復号化できるように自己実行ファイルを出力する
              if (value == "1")
              {
                _fOutputExeForOlderOS = true;
              }
              else if (value == "0")
              {
                _fOutputExeForOlderOS = false;
              }
              break;

            #endregion

            //-----------------------------------
            // Save Encrypt 保存設定（暗号化）
            //-----------------------------------          
            #region
            // Save to same folder in &encryption
            case "/saveto": //常に同じ場所へ暗号化ファイルを保存する
              if (Directory.Exists(value) == true)
              {
                if (Path.IsPathRooted(value) == false)
                {
                  value = Path.GetFullPath(value);
                }
                _SaveToSameFldrPath = value;
                _fSaveToSameFldr = true;
              }
              break;

            // Confirm overwriting when same filename exists 
            case "/ow": // 同名ファイルの上書きを確認するか（確認無で上書きするか？）
              if (value == "1")
              {
                _fEncryptConfirmOverwrite = true;
                _fDecryptConfirmOverwrite = true;
              }
              else if (value == "0")
              {
                _fEncryptConfirmOverwrite = false;
                _fDecryptConfirmOverwrite = false;
              }
              break;

            // Create one encrypted file from files
            case "/allpack": // 複数のファイルを暗号化する際は一つにまとめる
              if (value == "1")
              {
                _fNormal = false;
                _fAllFilePack = true;
                _fFilesOneByOne = false;
              }
              else if (value == "0")
              {
                _fNormal = true;
                _fAllFilePack = false;
                _fFilesOneByOne = false;
              }
              break;

            // Encrypt or decrypt files in directory one by one
            case "/oneby": // フォルダ内のファイルは個別に暗号化する
              if (value == "1")
              {
                _fFilesOneByOne = true;
                _fAllFilePack = false;
                _fNormal = false;
              }
              else if (value == "0")
              {
                _fFilesOneByOne = false;
                _fAllFilePack = false;
                _fNormal = true;
              }
              break;

            // Set the timestamp of encryption file to original files or directories
            case "/orgdt": // 暗号化ファイルのタイムスタンプを元ファイルに合わせる
              if (value == "1")
              {
                _fKeepTimeStamp = true;
              }
              else if (value == "0")
              {
                _fKeepTimeStamp = false;
              }
              break;

            // Create encrypted file &including extension
            case "/withext": // 暗号化ファイル名に拡張子を含める
              if (value == "1")
              {
                _fExtInAtcFileName = true;
              }
              else if (value == "0")
              {
                _fExtInAtcFileName = false;
              }
              break;

            // Specify the format of the encryption file name
            case "/autoname": // 自動で暗号化ファイル名を付加する
              if (value == "")
              {
                _fAutoName = false;
                _AutoNameFormatText = "";
              }
              else
              {
                _fAutoName = true;
                _AutoNameFormatText = value;
              }
              break;

            // Encrypted files camouflage with extension
            case "/camoext": // 暗号化ファイルの拡張子を偽装する

              if (IsValidFileName(value) == false)
              {
                // 注意
                // 
                // Windowsのファイル名には以下の文字が使えません！
                // 
                // \\ / : , * ? \" < > |
                //
                // Alert
                // The following characters cannot be used for the file name of Windows.
                // 
                // \\ / : , * ? \" < > |
                MessageBox.Show(Resources.DialogMessageNotUseWindowsFileName,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return (-1);
              }
              else
              {
                _fAddCamoExt = true;
                _CamoExt = value;
              }

              break;

            #endregion

            //-----------------------------------
            // Save Decrypt 保存設定（復号）
            //-----------------------------------    
            #region
            // Save to the same folder in decryption
            case "/dsaveto": // 常に同じ場所へファイルを復号化する
              if (Directory.Exists(value) == true)
              {
                if (Path.IsPathRooted(value) == false)
                {
                  value = Path.GetFullPath(value);
                }
                _DecodeToSameFldrPath = value;
                _fDecodeToSameFldr = true;
              }
              break;

            // Confirm overwriting when same filename exists
            //case "ow": // 同名ファイルの上書きを確認するか（確認無で上書きするか？）＝暗号化時と共通オプション
            //  break;

            // Create no parent folder in decryption
            case "/nopfldr": // 復号するときに親フォルダを生成しない
              if (value == "1")
              {
                _fNoParentFldr = true;
              }
              else if (value == "0")
              {
                _fNoParentFldr = false;
              }
              break;

            // Set the timestamp to decrypted files or directories
            case "/now": // 復号したファイルのタイムスタンプを生成日時にする
              if (value == "1")
              {
                _fSameTimeStamp = true;
              }
              else if (value == "0")
              {
                _fSameTimeStamp = false;
              }
              break;

            #endregion

            //-----------------------------------
            // Save ZIP 保存設定（ZIP）
            //-----------------------------------    
            #region
            // Save to the same folder in ZIP
            case "/zipsaveto": // 常に同じ場所へファイルを暗号化する
              if (Directory.Exists(value) == true)
              {
                if (Path.IsPathRooted(value) == false)
                {
                  value = Path.GetFullPath(value);
                }
                _ZipToSameFldrPath = value;
                _fZipToSameFldr = true;
              }
              break;

            // Confirm overwriting when same filename exists
            //case "ow": // 同名ファイルの上書きを確認するか（確認無で上書きするか？）＝暗号化時と共通オプション
            //  break;

            // EncryptionAlgorithm ( 0: PkzipWeak, 1: WinZipAes128, 2: WinZipAes256 ) 
            case "/zipalgo":
              if (value == "1")
              {
                _ZipEncryptionAlgorithm = 1;
              }
              else if (value == "2")
              {
                _ZipEncryptionAlgorithm = 2;
              }
              else
              {
                _ZipEncryptionAlgorithm = 0;
              }
              break;

            #endregion

            //-----------------------------------
            // Delete 削除設定
            //-----------------------------------          
            #region
            // Delete original files or directories after encryption
            case "/del": // 元ファイルの完全削除を行うか
              if (int.TryParse(value, out ResultNum))
              {
                if (ResultNum > 0 && ResultNum < 4)
                {
                  _fDelOrgFile = true;
                  _fCompleteDelFile = ResultNum;  // 0: 削除しない, 1: 通常削除, 2: ごみ箱, 3: 完全削除
                }
                else
                {
                  _fDelOrgFile = false;
                }
              }
              break;

            // Delete encrypted file after decryption
            case "/delenc": // 暗号化ファイルの完全削除を行うか
              if (int.TryParse(value, out ResultNum))
              {
                if (ResultNum > 0 && ResultNum < 4)
                {
                  _fDelEncFile = true;
                  _fCompleteDelFile = ResultNum;  // 0: Not de;ete 1: Normal delete, 2: Go to trash, 3: Completely delete
                }
                else
                {
                  _fDelEncFile = false;
                }
              }
              break;

            // Show the check box in main form window
            case "/chkdel": // メインフォームにチェックボックスを表示する
              if (value == "1")
              {
                _fEncryptShowDelChkBox = true;
                _fDecryptShowDelChkBox = true;
              }
              else if (value == "0")
              {
                _fEncryptShowDelChkBox = false;
                _fDecryptShowDelChkBox = false;
              }
              break;

            // Show confirmation dialog to delete file or directories
            case "/comfdel":  //削除確認メッセージを表示するか
              if (value == "1")
              {
                _fConfirmToDeleteAfterEncryption = true;
                _fConfirmToDeleteAfterDecryption = true;
              }
              else if (value == "0")
              {
                _fConfirmToDeleteAfterEncryption = false;
                _fConfirmToDeleteAfterDecryption = false;
              }
              break;

            //Advanced Delete Option [0: Normal Delete, 1: Complete erase, 2: Send to Trash ]
            case "/delrand": // 乱数を何回書き込み消去するか
              if (int.TryParse(value, out ResultNum) == true)
              {
                if (0 < ResultNum && ResultNum < 100)
                {
                  _DelRandNum = ResultNum;
                }
              }
              break;

            case "/delnull": // NULLを何回書き込み消去するか
              if (int.TryParse(value, out ResultNum) == true)
              {
                if (0 < ResultNum && ResultNum < 100)
                {
                  _DelZeroNum = ResultNum;
                }
              }
              break;

            #endregion

            //-----------------------------------
            // Compression 圧縮
            //-----------------------------------          
            #region
            // Enable compression
            case "/comprate": // 圧縮率
              if (int.TryParse(value, out ResultNum) == true)
              {
                if (-1 < ResultNum || ResultNum < 10)
                {
                  switch (ResultNum)
                  {
                    case 0:
                      _CompressionLevel = 0;
                      break;
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                      _CompressionLevel = 1;
                      break;
                    default:
                      _CompressionLevel = 2;
                      break;
                  }
                  _CompressRate = ResultNum;
                }

              }
              break;

            case "/complv": // 圧縮サイズ
              if (int.TryParse(value, out ResultNum) == true)
              {
                if (ResultNum is > -1 and < 3)
                {
                  _CompressionLevel = ResultNum;
                }

              }
              break;

            #endregion

            //-----------------------------------
            // Password file パスワードファイル
            //-----------------------------------          
            #region
            // Allow a password file to drag and drop
            case "/pf": // パスワードにファイルの指定を許可する
              switch (value)
              {
                case "1":
                  _fAllowPassFile = true;
                  break;
                case "0":
                  _fAllowPassFile = false;
                  break;
              }
              break;

            // Password file path for encryption
            case "/pfile": // 暗号化時のパスワードファイルパス
              if (Directory.Exists(value) == true)
              {
                if (Path.IsPathRooted(value) == false)
                {
                  value = Path.GetFullPath(value);
                }
                _PassFilePath = value;
                _fAllowPassFile = true;
              }
              break;

            // Password file path for decryption
            case "/dpfile": // 復号時のパスワードファイルパス
              if (Directory.Exists(value) == true)
              {
                if (Path.IsPathRooted(value) == false)
                {
                  value = Path.GetFullPath(value);
                }
                _PassFilePathDecrypt = value;
                _fAllowPassFile = true;
              }
              break;

            // It's not issued an error message when password file doesn't exist
            case "/nomsgp": // パスワードファイルがない場合エラーを出さない
              switch (value)
              {
                case "1":
                  _fNoErrMsgOnPassFile = true;
                  break;
                case "0":
                  _fNoErrMsgOnPassFile = false;
                  break;
              }
              break;

            #endregion

            //-----------------------------------
            // Advanced 高度
            //-----------------------------------
            #region 
            case "/motwexe":
              switch (value)
              {
                case "1":
                  _isCheckMotwExecutableFiles = true;
                  break;
                case "0":
                  _isCheckMotwExecutableFiles = false;
                  break;
              }
              break;

            case "/motwoffice":
              switch (value)
              {
                case "1":
                  _isCheckMotwOfficeFiles = true;
                  break;
                case "0":
                  _isCheckMotwOfficeFiles = false;
                  break;
              }
              break;

            case "/motwuser":
              switch (value)
              {
                case "1":
                  _isCheckMotwUserDefinedTypes = true;
                  break;
                case "0":
                  _isCheckMotwUserDefinedTypes = false;
                  break;
              }
              break;

            case "/motwall":
              switch (value)
              {
                case "1":
                  _isCheckMotwAllFiles = true;
                  break;
                case "0":
                  _isCheckMotwAllFiles = false;
                  break;
              }
              break;

            case "/motwtypes":
              if (IsValidExtensionFormat(value))
              {
                _MotwUserDefinedTypes = value.Split(',');
              }
              else
              {
                _isCheckMotwUserDefinedTypes = false;
                _MotwUserDefinedTypes = null;
              }
              break;

            #endregion

            //-----------------------------------
            // Input Password limit パスワード入力制限
            //-----------------------------------          
            #region
            // Set number of times to input password in encrypt files
            case "/typelimit": // パスワードのタイプミス制限回数
              if (int.TryParse(value, out ResultNum) == true)
              {
                if (ResultNum is >= 0 and <= 10)
                {
                  _MissTypeLimitsNum = ResultNum;
                }
              }
              break;

            // If input wrong password to the number of times, destroy it
            case "/breakfile":  // // タイプミス回数を超えたときにファイルを破壊するか否か
              if (value == "1")
              {
                _fBroken = true;
              }
              else if (value == "0")
              {
                _fBroken = false;
              }
              break;

            #endregion

            //-----------------------------------
            // Salvage サルベージ
            //-----------------------------------
            #region

            // Decrypt one by one while creating the parent folder.
            case "/slvgfolder": // 一つずつ親フォルダーを確認、生成しながら復号する
              if (value == "1")
              {
                _fSalvageToCreateParentFolderOneByOne = true;
              }
              else if (value == "0")
              {
                _fSalvageToCreateParentFolderOneByOne = false;
              }
              break;

            // Decrypt all files into the directory of the same hierarchy.
            case "/slvgsame": // すべてのファイルを同じ階層のディレクトリーに復号する
              if (value == "")
              {
                _fSalvageIntoSameDirectory = true;
              }
              else if (value == "0")
              {
                _fSalvageIntoSameDirectory = false;
              }
              break;

            #endregion

            //-----------------------------------
            // Developer mode 開発者モード
            //-----------------------------------
            #region

            // Open developer console window 
            case "/devcl": // 開発用のデベロッパーコンソールウィンドウの表示
              if (value == "")
              {
                _fDeveloperConsole = true;
              }
              else if (value == "0")
              {
                _fDeveloperConsole = false;
              }
              break;

            #endregion

            //-----------------------------------
            //その他（コマンドラインからのみ）
            //-----------------------------------
            #region
            // Explicit encrypt
            case "/en": // 明示的な暗号処理
              if (value == "1")
              {
                _ProcTypeWithoutAsk = 1;
              }
              break;

            // Explicit decrypt
            case "/de": // 明示的な復号処理
              if (value == "1")
              {
                _ProcTypeWithoutAsk = 2;
              }
              break;

            // Allow files over 4GB
            case "/4gbok": // 4GB超えを容認
              if (value == "1")
              {
                _fOver4GBok = true;
              }
              else if (value == "0")
              {
                _fOver4GBok = false;
              }
              break;

            // Reading paths list from a text file
            case "/list": // テキストファイルからのパスリストの読み込み
              if (File.Exists(value) == true)
              {
                var dir = Path.GetDirectoryName(value);
                // Detect text encoding
                var bs = File.ReadAllBytes(value);
                //文字コードを取得する
                var enc = DetectEncoding(bs);

                if (enc == null)
                {
                  // 注意
                  // 
                  // 以下の指定されたファイルの文字エンコードが不明で読み取れません！
                  // [FileListPath]
                  //
                  // Alert
                  // The character encoding of the following specified file is unknown that can not be read!
                  // [FileListPath]
                  MessageBox.Show(Resources.DialogMessageFileListEncodingUnknown + @"\n" + value,
                  Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                  return (-1);
                }

                // Read the List file and Add to FileList.
                using (var sr = new StreamReader(value, enc))
                {
                  while (sr.ReadLine() is { } line)
                  {
                    if (line == "") continue;
                    var filename = Path.Combine(dir, line);
                    var fullpath = Path.GetFullPath(filename);
                    if (File.Exists(fullpath) == true)
                    {
                      _FileList.Add(fullpath);
                    }
                    else
                    {
                      // 注意
                      // 
                      // 以下の指定されたファイルリストの中のファイルが見つかりません！
                      // [FileListPath]
                      // [FilePath]
                      //
                      // Alert
                      // The file in the following specified file's list can not be found!
                      // [FileListPath]
                      // [FilePath]
                      MessageBox.Show(Resources.DialogMessageFileInFileListNotFound + @"\n/list=" + value + @"\n" + fullpath,
                      Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                      return (-1);
                    }
                  }
                }
              }
              else
              {
                // 注意
                // 
                // 以下の指定されたリストファイルが見つかりません！
                // [FilePath]
                //
                // Alert
                // The following specified list file can not be found!
                // [FilePath]
                MessageBox.Show(Resources.DialogMessageFileListNotFound + @"\n" + value,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return (-1);
              }
              break;

            // Loading Commercial Use License File
            case "/lc": // 商用利用ライセンスファイルの読み込み
              if (value == "")
              {
                // 引数が空文字の場合は商用利用ライセンスを削除する
                // If the argument is an empty string, the commercial use license is removed
                var lcr = new LicenseRegister("");
                if (lcr.DeleteLicense() == false)
                {
                  // 注意
                  // 
                  // レジストレーションコードを削除できませんでした。
                  //
                  // Alert
                  // Could not delete registration code.
                  // 
                  MessageBox.Show(Resources.DialogMessageRegistrationCodeNotDeleted,
                  Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                  return (-1);
                }
              }
              else if (File.Exists(value) == true)
              {
                // Detect text encoding
                var bs = File.ReadAllBytes(value);
                //文字コードを取得する
                var enc = DetectEncoding(bs);

                if (enc == null)
                {
                  // 注意
                  // 
                  // 以下の指定されたファイルの文字エンコードが不明で読み取れません！
                  // [FileListPath]
                  //
                  // Alert
                  // The character encoding of the following specified file is unknown that can not be read!
                  // [FileListPath]
                  MessageBox.Show(Resources.DialogMessageFileListEncodingUnknown + @"\n" + value,
                  Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                  return (-1);
                }

                // Read the List file and Add to FileList.
                using (StreamReader sr = new StreamReader(value, enc))
                {
                  LicenseRegister lcr;
                  const string HexString = "0123456789abcdef";
                  var RegistrationCodeString = "";
                  while (sr.ReadLine() is { } line)
                  {
                    if (line == "") continue;
                    var si = new System.Globalization.StringInfo(line);
                    var len = si.LengthInTextElements;
                    for (var c = 0; c < len; c++)
                    {
                      var str = si.SubstringByTextElements(c, 1);
                      // Hex string?
                      if (HexString.IndexOf(str, StringComparison.OrdinalIgnoreCase) >= 0)
                      {
                        RegistrationCodeString += str;
                        if (RegistrationCodeString.Length >= 512)
                        {
                          lcr = new LicenseRegister(RegistrationCodeString);
                          if (lcr.Decypt(true) == false)
                          {
                            // 注意
                            // 
                            // 以下の指定されたファイルの文字エンコードが不明で読み取れません！
                            // [FilePath]
                            //
                            // Alert
                            // The character encoding of the following specified file is unknown that can not be read!
                            // [FilePath]
                            MessageBox.Show(Resources.DialogMessageRegistrationCodeNotRegistered + @"\n" + value,
                            Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return (-1);
                          }
                        }
                      }
                    }

                  }

                  lcr = new LicenseRegister(RegistrationCodeString);
                  if (lcr.Decypt(true) == false)
                  {
                    // 注意
                    // 
                    // 正常にレジストレーションコードの登録が行われませんでした。
                    // [FilePath]
                    //
                    // Alert
                    // The registration code was not successfully registered.
                    // [FilePath]
                    MessageBox.Show(Resources.DialogMessageRegistrationCodeNotRegistered + "\n" + value,
                    Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return (-1);
                  }

                }

              }
              else
              {
                // 注意
                // 
                // 正常にレジストレーションコードの登録が行われませんでした。
                // [FilePath]
                // 
                // Alert
                // The registration code was not successfully registered.
                // [FilePath]
                MessageBox.Show(Resources.DialogMessageRegistrationCodeNotRegistered + @"\n" + value,
                Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return (-1);
              }

              break;

            #endregion

            default:
              break;

          }// end switch;
        }

        i++;

      }// end foreach (string cmd in cmds);

      return (i);

    }

    //======================================================================
    /// <summary>
    /// Add more files or directories.
    /// パラメータ以外（ドラッグ＆ドロップなど）からファイルを追加する
    /// </summary>
    /// <param name="FilePath"></param>
    //======================================================================
    public void AddArgsFile(string FilePath)
    {
      if (File.Exists(FilePath) == true)
      {
        //何の種類のファイルか
        FileType[CheckFileType(FilePath)]++;
        _FileList.Add(FilePath);
      }

    }

    //======================================================================
    /// <summary>
    /// Encrypt stored passwords
    /// 記憶パスワードを暗号化する
    /// </summary>
    /// <param name="MyPasswordString"></param>
    /// <returns></returns>
    //======================================================================
    private static byte[] EncryptMyPassword(string MyPasswordString)
    {
      MyPasswordString ??= "";
      // Get the drive name where the application is installed
      //アプリケーションがインストールされているドライブ名を取得
      var RootDriveName = Path.GetPathRoot(Application.ExecutablePath);
      // Get the drive serial number.
      var volNumString = GetDriveSerialNumber();

      // "The serial number of the drive volume + MachineName" is set by encryption for stored passwords
      // ex).  818980454_HIBARA
      var Password = volNumString + "_" + Environment.MachineName;

      var salt = new byte[8];
      var rng = new RNGCryptoServiceProvider();
      rng.GetBytes(salt);

      var deriveBytes = new Rfc2898DeriveBytes(Password, salt, 1000);

      var key = deriveBytes.GetBytes(256 / 8);
      var iv = deriveBytes.GetBytes(128 / 8);

      var aes = new AesManaged
      {
        BlockSize = 128,              // BlockSize = 16bytes
        KeySize = 256,                // KeySize = 32byte
        Mode = CipherMode.CBC,
        Padding = PaddingMode.Zeros
      };

      aes.Key = key;
      aes.IV = iv;

      var ms = new MemoryStream();
      ms.Write(salt, 0, 8);

      var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
      using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
      {
        using (var sw = new StreamWriter(cs))
        {
          //Write all data to the stream.
          sw.Write(MyPasswordString);
        }
      }

      return ms.ToArray();

    }

    //======================================================================
    /// <summary>
    ///  Decrypt stored passwords
    ///  記憶パスワードを復号して元に戻す
    /// </summary>
    /// <param name="bytesPassword"></param>
    /// <returns></returns>
    //======================================================================
    private string DecryptMyPassword(byte[] MyPasswordBinary)
    {
      // Get the drive name where the application is installed
      //アプリケーションがインストールされているドライブ名を取得
      var RootDriveName = Path.GetPathRoot(Application.ExecutablePath);
      // Get the drive serial number.
      var volNumString = GetDriveSerialNumber();
      // "The serial number of the drive volume + MachineName" is set by encryption for stored passwords
      // ex).  818980454_HIBARA
      var Password = volNumString + "_" + Environment.MachineName;

      var aes = new AesManaged
      {
        BlockSize = 128,
        KeySize = 256,
        Mode = CipherMode.CBC,
        Padding = PaddingMode.Zeros
      };

      using (var ms = new MemoryStream(MyPasswordBinary))
      {
        var salt = new byte[8];
        ms.Read(salt, 0, 8);
        var deriveBytes = new Rfc2898DeriveBytes(Password, salt, 1000);

        aes.Key = deriveBytes.GetBytes(256 / 8);
        aes.IV = deriveBytes.GetBytes(128 / 8);

        try
        {
          var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
          using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
          {
            using (var sr = new StreamReader(cs))
            {
              // Read the decrypted bytes from the decrypting stream
              // and place them in a string.
              return (sr.ReadToEnd());
            }
          }
        }
        catch
        {
          return ("");
        }

      }

    }

    //======================================================================
    /// <summary>
    /// Specify the format of the encryption file name
    /// 指定した書式でファイル名を生成する
    /// </summary>
    /// <param name="FormatString"></param  >
    /// <param name="FilePath"></param>
    /// <returns></returns>
    //======================================================================
    public string getSpecifyFileNameFormat(string FormatString, string FilePath)
    {
      // ファイル名            : <filename> 
      // 拡張子                : <ext> 
      // 日付                  : <date:[指定書式]> 
      // 連番                  : <num:[桁数]> 
      // ランダムな文字列      : <random:[文字数]> 
      #region

      var fSerialNum = false;
      var SerialNum = 1;
      string ReturnString;
      while (true)
      {
        ReturnString = FormatString;

        //-----------------------------------
        // File Name
        ReturnString = Regex.Replace(ReturnString, @"<filename>", Path.GetFileNameWithoutExtension(FilePath));

        //-----------------------------------
        // Extension
        ReturnString = Regex.Replace(ReturnString, @"<ext>", Path.GetExtension(FilePath));

        //-----------------------------------
        // Date time
        var r = new Regex(@"<date:(.*?)>", RegexOptions.IgnoreCase);
        var m = r.Match(ReturnString);
        while (m.Success)
        {
          var dt = DateTime.Now;
          var DateTimeString = dt.ToString(m.Groups[1].Value);
          ReturnString = Regex.Replace(ReturnString, m.Value, DateTimeString);
          m = m.NextMatch();
        }

        //-----------------------------------
        // Serial number
        r = new Regex(@"<num:([0-9]*?)>", RegexOptions.IgnoreCase);
        m = r.Match(ReturnString);
        int FigNum;
        while (m.Success)
        {
          fSerialNum = true;
          if (int.TryParse(m.Groups[1].Value, out FigNum) == true)
          {
            ReturnString = Regex.Replace(ReturnString, m.Value, SerialNum.ToString(new string('0', FigNum)));
          }
          m = m.NextMatch();
        }

        //-----------------------------------
        // Random string 
        r = new Regex(@"<random:([0-9]*?)>", RegexOptions.IgnoreCase);
        m = r.Match(ReturnString);
        while (m.Success)
        {
          if (int.TryParse(m.Groups[1].Value, out FigNum) == false)
          {
            FigNum = 8;
          }

          const string CharAlphabetUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
          const string CharAlphabetLower = "abcdefghijklmnopqrstuvwxyz";
          const string CharNumbers = "0123456789";
          const string CharSymbols = "=-+!_#$%&()[]{}~^`'@";
          var Chars = "";

          if (_fAutoNameAlphabets == true)
          {
            if (_fAutoNameUpperCase == true)
            {
              Chars += CharAlphabetUpper;
            }
            if (_fAutoNameLowerCase == true)
            {
              Chars += CharAlphabetLower;
            }
          }

          if (_fAutoNameNumbers == true)
          {
            Chars += CharNumbers;
          }

          if (_fAutoNameSymbols == true)
          {
            Chars += CharSymbols;
          }

          if (Chars == "")
          {
            Chars = CharAlphabetUpper;
          }

          var stringChars = new char[FigNum];
          var random = new Random();

          for (var i = 0; i < stringChars.Length; i++)
          {
            stringChars[i] = Chars[random.Next(Chars.Length)];
          }

          ReturnString = Regex.Replace(ReturnString, m.Value, new string(stringChars));

          m = m.NextMatch();

        }

        //-----------------------------------
        // Windowsでの禁止文字列を使っていないか
        //-----------------------------------
        if (IsValidFileName(ReturnString) == false)
        {
          // 注意
          // 
          // Windowsのファイル名には以下の文字が使えません！
          // 
          // \\ / : , * ? \" < > |
          //
          // Alert
          // The following characters cannot be used for the file name of Windows.
          // 
          // \\ / : , * ? \" < > |
          MessageBox.Show(Resources.DialogMessageNotUseWindowsFileName,
          Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        if (fSerialNum == false || File.Exists(Path.Combine(Path.GetDirectoryName(FilePath), ReturnString)) == false)
        {
          break;
        }
        else
        {
          SerialNum++;
        }

      }

      return (ReturnString);

      #endregion
    }

    //======================================================================
    /// <summary>
    /// ドライブボリュームのシリアルナンバーを取得する
    /// Get serial number of the drive volume 
    /// </summary>
    /// <returns>serial number string</returns>
    //======================================================================
    private static string GetDriveSerialNumber()
    {
      //アプリケーションがインストールされているドライブ名を取得
      var RootDriveName = Path.GetPathRoot(Application.ExecutablePath);

      uint serial_number = 0;
      uint max_component_length = 0;
      var sb_volume_name = new System.Text.StringBuilder(256);
      var file_system_flags = 0U;
      var sb_file_system_name = new System.Text.StringBuilder(256);

      if (UnsafeNativeMethods.GetVolumeInformation(RootDriveName, sb_volume_name,
          (uint)sb_volume_name.Capacity, ref serial_number, ref max_component_length,
          ref file_system_flags, sb_file_system_name, (uint)sb_file_system_name.Capacity) == 0)
      {
        return ("0");
      }
      else
      {
        return (serial_number.ToString());
      }

    }

    //======================================================================
    /// <summary>
    /// Check for the presence of prohibited strings in the Windows file system
    /// Windowsファイルシステムで禁止文字列があるかチェックする
    /// </summary>
    /// <param name="FileName"></param>
    /// <returns>boolean</returns>
    //======================================================================
    private static bool IsValidFileName(string FileName)
    {
      var containsABadCharacter = new Regex("[" + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]");
      return !containsABadCharacter.IsMatch(FileName);
    }



    /// <summary>
    /// Validates whether the provided extension string is in a correct format.
    /// 指定された拡張子文字列が正しい形式であるかを検証します。
    /// </summary>
    /// <param name="ExtensionString">
    /// A comma-separated string of file extensions. Each extension must start with a period (.) 
    /// followed by one or more alphanumeric characters.
    /// カンマ区切りの拡張子文字列。各拡張子はピリオド（.）で始まり、その後に1文字以上の英数字が続く必要があります。
    /// </param>
    /// <returns>
    /// <c>true</c> if all extensions in the string are valid; otherwise, <c>false</c>.
    /// 文字列内のすべての拡張子が有効であれば<c>true</c>を返し、それ以外の場合は<c>false</c>を返します。
    /// </returns>
    /// <remarks>
    /// If the validation fails, an alert dialog is displayed listing the invalid extensions.
    /// 検証に失敗した場合、無効な拡張子をリストアップした警告ダイアログが表示されます。
    /// </remarks>
    public static bool IsValidExtensionFormat(string ExtensionString)
    {
      if (string.IsNullOrEmpty(ExtensionString)) { return false; }

      // 空白を正規化して分割（連続する空白を1つの空白に置換）
      var extensions = ExtensionString.Trim().Split([','], StringSplitOptions.RemoveEmptyEntries);

      // 正規表現: ピリオドで始まり、アルファベットと数字が1文字以上続く
      var regex = new Regex(@"^\.[a-zA-Z0-9]+$");

      // 拡張子がすべて正しいかどうかをチェック
      var allValid = extensions.All(ext => regex.IsMatch(ext));

      if (allValid)
      {
        // 検証成功
        return (true);
      }

      var extList = new List<string>();
      foreach (var ext in extensions.Where(ext => !regex.IsMatch(ext)))
      {
        extList.Add(ext);
      }

      // The extension was not entered correctly or is an invalid value.
      // Extensions must be entered as comma-separated values containing a period.
      // 拡張子が正しく入力されていないか、不正な値です。
      // 拡張子はピリオドを含むカンマ区切りで入力してください。
      MessageBox.Show(Resources.DialogMessageExtensionInvalid + Environment.NewLine + string.Join(", ", extList),
        Resources.DialogTitleAlert, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
      return (false);

    }

    //======================================================================
    /// <summary>
    /// Convert from hexadecimal (Hex) string to binary data
    /// 16進数(Hex)文字列からバイナリデータに変換する
    /// ref. http://stackoverflow.com/questions/623104/byte-to-hex-string/5919521#5919521
    /// </summary>
    /// <param name="Hex">hexadecimal string</param>
    /// <returns></returns>
    //======================================================================
    public static byte[] HexStringToByteArray(string Hex)
    {
      var Bytes = new byte[Hex.Length / 2];
      var HexValue = new int[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05,
       0x06, 0x07, 0x08, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
       0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };

      for (int x = 0, i = 0; i < Hex.Length; i += 2, x += 1)
      {
        Bytes[x] = (byte)(HexValue[char.ToUpper(Hex[i + 0]) - '0'] << 4 |
                          HexValue[char.ToUpper(Hex[i + 1]) - '0']);
      }

      return Bytes;
    }

    //======================================================================
    /// <summary>
    /// Convert binary data to hexadecimal (Hex) string
    /// バイナリーデータから16進(Hex)文字列に変換する
    /// </summary>
    /// <param name="bytes">binary data ( bytes[] ) </param>
    /// <returns></returns>
    //======================================================================
    public static string BytesToHexString(byte[] bytes)
    {
      return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }

    public static string ByteArrayToHexString(byte[] Bytes)
    {
      var Result = new StringBuilder(Bytes.Length * 2);
      const string HexAlphabet = "0123456789ABCDEF";

      foreach (var B in Bytes)
      {
        Result.Append(HexAlphabet[(int)(B >> 4)]);
        Result.Append(HexAlphabet[(int)(B & 0xF)]);
      }

      return Result.ToString();
    }

    //======================================================================
    /// <summary>
    /// Detect file type to drag and drop ( Directory, ATC, EXE[by ATC], ZIP ).
    /// 投げ込まれたファイルの種類を特定する（ディレクトリ, ATC, EXE[by ATC], ZIP）
    /// </summary>
    /// <remarks>https://stackoverflow.com/a/929418</remarks>
    /// <returns>
    ///   1: ATC, 
    ///   2: EXE(ATC), 
    ///   3: ZIP, 
    ///   4: RSA Encryption data, 
    ///   5: RSA key data ( XML file ), 
    ///   0: Others(Encrypt file?)</returns>
    //======================================================================
    public int CheckFileType(string FilePath)
    {
      //const string SignatureZip = "50-4B-03-04";  // ZIPファイル
      const string SignatureAtc = "_AttacheCaseData";
      const string SignatureAtcBroken = "_Atc_Broken_Data";
      const string SignatureRsaData = "_AttacheCase_Rsa";
      const string SignatureBomXmlData = "EF-BB-BF-3C-3F-78-6D-6C";   // BOM + "<?xml "
      const string SignatureXmlData = "3C-3F-78-6D-6C-20-76-65";   // "<?xml ve"

      //-----------------------------------
      // ディレクトリー
      // Directory
      //-----------------------------------
      if (Directory.Exists(FilePath) == true)
      {
        return (0);
      }
      // ファイルが存在しない
      // File does not exist.
      else if (File.Exists(FilePath) == false)
      {
        return (0);
      }

      using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        if (fs.Length < 4)
        {
          return (0);
        }
        //-----------------------------------
        // ATCファイルの判別
        // Detect atc file
        //-----------------------------------
        fs.Seek(4, SeekOrigin.Begin);
        var bufferSignature = new byte[16];
        fs.Read(bufferSignature, 0, 16);
        var SignatureText = Encoding.ASCII.GetString(bufferSignature);
        if (SignatureText is SignatureAtc or SignatureAtcBroken)
        {
          return (1);
        }
        //-----------------------------------
        // RSA 公開鍵暗号データ
        // Detect atc file
        //-----------------------------------
        fs.Seek(4, SeekOrigin.Begin);
        bufferSignature = new byte[16];
        fs.Read(bufferSignature, 0, 16);
        SignatureText = Encoding.ASCII.GetString(bufferSignature);
        if (SignatureText == SignatureRsaData)
        {
          return (4);
        }
        //-----------------------------------
        // XMLファイル（公開鍵、暗号鍵データ）
        // Detect XML file ( public key or private key data)
        //-----------------------------------
        // <?xml version="1.0" encoding="utf-8"?>
        fs.Seek(0, SeekOrigin.Begin);
        bufferSignature = new byte[8];
        fs.Read(bufferSignature, 0, 8);
        SignatureText = BitConverter.ToString(bufferSignature);
        if (SignatureText is SignatureBomXmlData or SignatureXmlData)
        {
          var xmlElement = XElement.Load(FilePath);
          // Nullが入ってきた場合は許容しない
          if (xmlElement.Element("token")?.Value == "AttacheCase")
          {
            if (xmlElement.Element("type")?.Value == "public")
            {
              return (5); // RSA Encryption process
            }
            else if (xmlElement.Element("type")?.Value == "private")
            {
              return (6); // RSA Decryption process
            }
          }
        }
        //-----------------------------------
        // EXE(ATC)ファイルの判別
        // Detect Exe(atc) file
        // https://stackoverflow.com/questions/2863683/how-to-find-if-a-file-is-an-exe
        //-----------------------------------
        var twoBytes = new byte[2];
        fs.Seek(0, SeekOrigin.Begin);
        fs.Read(twoBytes, 0, 2);
        if (Encoding.UTF8.GetString(twoBytes) == "MZ")
        {
          // _AttacheCaseData
          int[] AtcTokenByte = { 95, 65, 116, 116, 97, 99, 104, 101, 67, 97, 115, 101, 68, 97, 116, 97 };
          // _Atc_Broken_Data
          int[] AtcBrokenTokenByte = { 95, 65, 116, 99, 95, 66, 114, 111, 104, 101, 110, 95, 68, 97, 116, 97 };

          var fToken = false;
          int b, pos = 0;
          while ((b = fs.ReadByte()) > -1 || pos < 50000)
          {
            //-----------------------------------
            // Check the token "_AttacheCaseData"
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
                pos++;
              }
              if (fToken == true)
              {
                if (pos > 20)
                { // Self executable file
                  return (2);
                }
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
                pos++;
              }

              if (fToken == true)
              {
                _fBroken = true;
              }
            }

            pos++;

            if (fToken == true)
            {
              break;
            }
            //-----------------------------------

          }// end while();

        }
        else
        {
          return (0);
        }

      }// end using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

      //-----------------------------------
      // いずれのファイルでもない
      // Not correspond to any file
      //-----------------------------------
      return (0);

    }

    /// <summary>
    /// Determine to what process based on the dragged and dropped file type
    /// 投げ込まれたファイルタイプから処理内容を決定する
    /// </summary>
    public int DetectFileType()
    {
      // Process Type
      // private const int PROCESS_TYPE_ERROR           = -1;
      // private const int PROCESS_TYPE_NONE            =  0;
      // private const int PROCESS_TYPE_ATC             =  1;
      // private const int PROCESS_TYPE_ATC_EXE         =  2;
      // private const int PROCESS_TYPE_PASSWORD_ZIP    =  3;
      // private const int PROCESS_TYPE_DECRYPTION      =  4;
      // private const int PROCESS_TYPE_RSA_ENCRYPTION  =  5;
      // private const int PROCESS_TYPE_RSA_DECRYPTION  =  6;

      FileType = [0, 0, 0, 0, 0, 0, 0];

      foreach (var f in _FileList)
      {
        // 1: ATC,
        // 2: EXE(ATC),
        // 3: ZIP,
        // 4: RSA Encryption data,
        // 5: RSA public key data ( XML file ),
        // 6: RSA private key data ( XML file ),
        // 0: Others(Encrypt file?)
        FileType[CheckFileType(f)]++;
      }

      // Process Type
      // private const int PROCESS_TYPE_ERROR        = -1;
      // private const int PROCESS_TYPE_NONE         = 0;
      // private const int PROCESS_TYPE_ATC          = 1;
      // private const int PROCESS_TYPE_ATC_EXE      = 2;
      // private const int PROCESS_TYPE_PASSWORD_ZIP = 3;
      // private const int PROCESS_TYPE_DECRYPTION   = 4;
      if ((FileType[1] > 0 || FileType[2] > 0) && FileType[0] == 0)
      {
        return PROCESS_TYPE_DECRYPTION;
      }
      else if (FileType[1] == 0 && FileType[2] > 0 && FileType[0] == 0)
      {
        return PROCESS_TYPE_DECRYPTION;
      }
      else if (FileType[1] == 0 && FileType[2] == 0 && FileType[4] == 0 && FileType[5] > 0)
      {
        return PROCESS_TYPE_RSA_ENCRYPTION;
      }
      else if (FileType[1] == 0 && FileType[2] == 0 && (FileType[4] > 0 || FileType[6] > 0))
      {
        return PROCESS_TYPE_RSA_DECRYPTION;
      }
      else if (FileType[1] == 0 && FileType[2] == 0 && FileType[0] > 0)
      {
        return PROCESS_TYPE_ATC;
      }
      else
      {
        return PROCESS_TYPE_ERROR;
      }
    }

    //======================================================================
    /// <summary>
    /// 文字コードを判別する(Detect Encoding)
    /// </summary>
    /// <remarks>
    /// 参考：http://dobon.net/vb/dotnet/string/detectcode.html
    /// Jcode.pmのgetcodeメソッドを移植したものです。
    /// Jcode.pm(http://openlab.ring.gr.jp/Jcode/index-j.html)
    /// Jcode.pmのCopyright: Copyright 1999-2005 Dan Kogai
    /// </remarks>
    /// <param name="bytes">文字コードを調べるデータ</param>
    /// <returns>適当と思われるEncodingオブジェクト。
    /// 判断できなかった時はnull。</returns>
    //======================================================================
    public static Encoding DetectEncoding(byte[] bytes)
    {
      const byte bEscape = 0x1B;
      const byte bAt = 0x40;
      const byte bDollar = 0x24;
      const byte bAnd = 0x26;
      const byte bOpen = 0x28;    //'('
      const byte bB = 0x42;
      const byte bD = 0x44;
      const byte bJ = 0x4A;
      const byte bI = 0x49;

      var len = bytes.Length;
      byte b1, b2, b3;

      //Encode::is_utf8 は無視

      bool isBinary = false;
      for (int i = 0; i < len; i++)
      {
        b1 = bytes[i];
        if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF)
        {
          //'binary'
          isBinary = true;
          if (b1 == 0x00 && i < len - 1 && bytes[i + 1] <= 0x7F)
          {
            //smells like raw unicode
            return System.Text.Encoding.Unicode;
          }
        }
      }
      if (isBinary)
      {
        return null;
      }

      //not Japanese
      bool notJapanese = true;
      for (int i = 0; i < len; i++)
      {
        b1 = bytes[i];
        if (b1 == bEscape || 0x80 <= b1)
        {
          notJapanese = false;
          break;
        }
      }
      if (notJapanese)
      {
        return System.Text.Encoding.ASCII;
      }

      for (var i = 0; i < len - 2; i++)
      {
        b1 = bytes[i];
        b2 = bytes[i + 1];
        b3 = bytes[i + 2];

        if (b1 == bEscape)
        {
          if (b2 == bDollar && b3 == bAt)
          {
            //JIS_0208 1978
            //JIS
            return System.Text.Encoding.GetEncoding(50220);
          }
          else if (b2 == bDollar && b3 == bB)
          {
            //JIS_0208 1983
            //JIS
            return System.Text.Encoding.GetEncoding(50220);
          }
          else if (b2 == bOpen && (b3 == bB || b3 == bJ))
          {
            //JIS_ASC
            //JIS
            return System.Text.Encoding.GetEncoding(50220);
          }
          else if (b2 == bOpen && b3 == bI)
          {
            //JIS_KANA
            //JIS
            return System.Text.Encoding.GetEncoding(50220);
          }
          if (i < len - 3)
          {
            var b4 = bytes[i + 3];
            if (b2 == bDollar && b3 == bOpen && b4 == bD)
            {
              //JIS_0212
              //JIS
              return System.Text.Encoding.GetEncoding(50220);
            }
            if (i < len - 5 &&
                b2 == bAnd && b3 == bAt && b4 == bEscape &&
                bytes[i + 4] == bDollar && bytes[i + 5] == bB)
            {
              //JIS_0208 1990
              //JIS
              return System.Text.Encoding.GetEncoding(50220);
            }
          }
        }
      }

      //should be euc|sjis|utf8
      //use of (?:) by Hiroki Ohzaki <ohzaki@iod.ricoh.co.jp>
      var sjis = 0;
      var euc = 0;
      var utf8 = 0;
      for (var i = 0; i < len - 1; i++)
      {
        b1 = bytes[i];
        b2 = bytes[i + 1];
        if ((b1 is >= 0x81 and <= 0x9F || b1 is >= 0xE0 and <= 0xFC) &&
            (b2 is >= 0x40 and <= 0x7E || b2 is >= 0x80 and <= 0xFC))
        {
          //SJIS_C
          sjis += 2;
          i++;
        }
      }
      for (int i = 0; i < len - 1; i++)
      {
        b1 = bytes[i];
        b2 = bytes[i + 1];
        if ((b1 is >= 0xA1 and <= 0xFE && b2 is >= 0xA1 and <= 0xFE) ||
            (b1 == 0x8E && b2 is >= 0xA1 and <= 0xDF))
        {
          //EUC_C
          //EUC_KANA
          euc += 2;
          i++;
        }
        else if (i < len - 2)
        {
          b3 = bytes[i + 2];
          if (b1 == 0x8F && b2 is >= 0xA1 and <= 0xFE &&
              b3 is >= 0xA1 and <= 0xFE)
          {
            //EUC_0212
            euc += 3;
            i += 2;
          }
        }
      }
      for (var i = 0; i < len - 1; i++)
      {
        b1 = bytes[i];
        b2 = bytes[i + 1];
        if (b1 is >= 0xC0 and <= 0xDF && b2 is >= 0x80 and <= 0xBF)
        {
          //UTF8
          utf8 += 2;
          i++;
        }
        else if (i < len - 2)
        {
          b3 = bytes[i + 2];
          if (b1 is >= 0xE0 and <= 0xEF && b2 is >= 0x80 and <= 0xBF &&
              b3 is >= 0x80 and <= 0xBF)
          {
            //UTF8
            utf8 += 3;
            i += 2;
          }
        }
      }
      //M. Takahashi's suggestion
      //utf8 += utf8 / 2;

      //System.Diagnostics.Debug.WriteLine(string.Format("sjis = {0}, euc = {1}, utf8 = {2}", sjis, euc, utf8));
      if (euc > sjis && euc > utf8)
      {
        //EUC
        return System.Text.Encoding.GetEncoding(51932);
      }
      else if (sjis > euc && sjis > utf8)
      {
        //SJIS
        return System.Text.Encoding.GetEncoding(932);
      }
      else if (utf8 > euc && utf8 > sjis)
      {
        //UTF8
        return System.Text.Encoding.UTF8;
      }
      return null;
    }

    //======================================================================
    /// <summary>
    /// システムのテーマカラーを取得する
    /// Get the system's theme color
    /// </summary>
    /// <remarks>
    /// ref. https://nln.jp/wp/archives/5653
    /// </remarks>
    /// <returns>"light"テーマか、"dark"テーマかいずれかを文字列で返す
    ///           Returns a string indicating whether the theme is a "light" theme or a "dark" theme
    /// </returns>
    //======================================================================
    public static string GetSystemThemeColor()
    {
      var getmode = "None";
      const string rKeyName = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
      const string rGetValueName = "AppsUseLightTheme";
      try
      {
        // レジストリの取得
        using (var rKey = Registry.CurrentUser.OpenSubKey(rKeyName))
        {
          // Windows 10 LTSC版だとテーマカラーがないので値を返さない場合がある（1を返す）
          // The Windows 10 LTSC version does not have a theme color, so it may not return a value (So, returns 1).
          if (rKey != null)
          {
            var theme = (int)rKey.GetValue(rGetValueName, 1);
            // レジストリの値を
            // Windows10 May 2019 はこの値で OK：Theme.Light = 1, Theme.Dark = 0
            getmode = (theme == 1) ? "light" : "dark";
          }
        }
      }
      catch (NullReferenceException)
      {
        return "light";
      }
      return getmode;
    }

    //======================================================================
    /// <summary>
    /// 指定したパスのレジストリーを再帰的に丸ごと別の場所へコピーする
    /// Recursively copy the entire registry of a specified path to another location
    /// ref. https://stackoverflow.com/questions/12262536/copying-subcategories-in-registry
    /// </summary>
    /// <param name="src">source</param>
    /// <param name="dst">destination</param>
    /// <returns></returns>
    //======================================================================
    public static void RegCopyTo(RegistryKey src, RegistryKey dst)
    {
      // copy the values
      foreach (var name in src.GetValueNames())
      {
        dst.SetValue(name, src.GetValue(name), src.GetValueKind(name));
      }

      // copy the sub keys
      foreach (var name in src.GetSubKeyNames())
      {
        using (var srcSubKey = src.OpenSubKey(name, false))
        {
          var dstSubKey = dst.CreateSubKey(name);
          RegCopyTo(srcSubKey, dstSubKey);
        }
      }
    }

  }

}
