using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualBasic.FileIO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using AttacheCase.Properties;

namespace AttacheCase
{
  public class Wipe
  {
    // Status code
    private const int EncryptSucceeded = 1; // Encrypt is succeeded.
    private const int DecryptSucceeded = 2; // Decrypt is succeeded.
    private const int DeleteSucceeded  = 3; // Delete is succeeded.
    private const int ReadyForEncrypt = 4; // Getting ready for encryption or decryption.
    private const int ReadyForDecrypt = 5; // Getting ready for encryption or decryption.
    private const int Encrypting        = 6; // Encrypting.
    private const int Decrypting        = 7; // Decrypting.
    private const int Deleting          = 8; // Deleting.

    // Error code
    private const int UserCanceled            = -1;   // User cancel.
    private const int ErrorUnexpected         = -100;
    private const int NotAtcData             = -101;
    private const int AtcBrokenData          = -102;
    private const int NoDiskSpace            = -103;
    private const int FileIndexNotFound     = -104;
    private const int PasswordTokenNotFound = -105;

    private const int BufferSize = 4096;

    /// <summary>
    /// Deletes a file in a secure way by overwriting it with
    /// random garbage data n times.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <param name="filePaths">Array of file paths</param>
    /// <param name="delRandNum">Number of Random data</param>
    /// <param name="delZeroNum">Number of Zeros </param>
    public static int WipeFile(
      object sender, DoWorkEventArgs e,
      List<string> filePaths, int delRandNum, int delZeroNum)
    {
      var swProgress = new Stopwatch();
      swProgress.Start();

      try
      {
        List<string> fileList = new List<string>();

        foreach (string filePath in filePaths)
        {
          if (File.Exists(filePath))
          {
            fileList.Add(filePath);
          }
          else if (Directory.Exists(filePath))
          {
            fileList.AddRange(GetFileList("*", filePath));
          }
        }

        filePaths = fileList;

        // var options = new ParallelOptions
        // {
        //   MaxDegreeOfParallelism = Environment.ProcessorCount
        // };
        // Parallel.ForEach(filePaths, options, filePath =>
        // {
        //   if (!File.Exists(filePath)) return;
        //   // ファイルリストのファイルサイズ合計を計算
        //   lock (filePaths) totalFileListSize += new FileInfo(filePath).Length;
        // });
       
        // ファイルリストのファイルサイズ合計を計算
        var totalFileListSize = filePaths.AsParallel()
          .Where(File.Exists)
          .Select(filePath => new FileInfo(filePath).Length)
          .Sum();

        totalFileListSize *= (delRandNum + delZeroNum);
        
        var worker = sender as BackgroundWorker;
        worker.WorkerSupportsCancellation = true;
        e.Result = Deleting;

        Int64 countSize = 0;
        
        foreach (var filePath in filePaths)
        {
          if (File.Exists(filePath) == false)
          {
            continue;
          }

          var totalTimes = delRandNum + delZeroNum;
          var randNum = delRandNum;
          var zeroNum = delZeroNum;

          // Set the files attributes to normal in case it's read-only.
          File.SetAttributes(filePath, FileAttributes.Normal);

          // Calculate the total number of sectors in the file.
          var totalFileSize = new FileInfo(filePath).Length;

          // Create a dummy-buffer the size of a sector.
          var dummyBuffer = new byte[BufferSize];

          // Open a FileStream to the file.
          using (var fs = new FileStream(filePath, FileMode.Open))
          {
            while (randNum > 0 || zeroNum > 0)
            {
              // 削除経過回数
              var numOfTimes = totalTimes - (randNum + zeroNum) + 1;

              // Go to the beginning of the stream
              fs.Position = 0;

              Int64 totalSize = 0;

              // Loop all sectors
              while( totalSize < totalFileSize)
              {
                // プログレスバーの更新間隔を100msに調整
                if (swProgress.ElapsedMilliseconds > 100)
                {
                  var deleteType = "";
                  if (randNum > 0)
                  {
                    deleteType = Resources.labelDeleteTypeRandom;  // "Random"
                  }
                  else if (zeroNum > 0)
                  {
                    deleteType = Resources.labelDeleteTypeZeros;    // "Zeros"
                  }

                  var messageText = String.Format("{0} ({1}: {2}/{3}) - {4}/{5}",
                    Path.GetFileName(filePath), // {0}
                    deleteType, // {1}
                    numOfTimes, // {2}
                    totalTimes, // {3}
                    FormatFileSizeString(countSize, false), // {4}
                    FormatFileSizeString(totalFileListSize, true) // {5}
                  );

                  var percent = ((float)countSize / totalFileListSize);
                  var messageList = new ArrayList
                  {
                    Deleting,
                    messageText
                  };

                  worker.ReportProgress((int)(percent * 10000), messageList);
                  swProgress.Restart();

                }

                //-----------------------------------
                // User cancel
                if (worker.CancellationPending)
                {
                  fs.Close();
                  e.Cancel = true;
                  return (UserCanceled);
                }

                //-----------------------------------
                // Random number fills
                if (randNum > 0)
                {
                  // Create a cryptographic Random Number Generator.
                  // This is what I use to create the garbage data.
                  // Fill the dummy-buffer with random data
                  var rng = new RNGCryptoServiceProvider();
                  rng.GetBytes(dummyBuffer);
                }
                //-----------------------------------
                // Zeros fills
                else
                {
                  // Zeros fills
                  Array.Clear(dummyBuffer, 0, BufferSize);
                }

                //-----------------------------------
                // Write it to the stream
                fs.Write(dummyBuffer, 0, BufferSize);

                //-----------------------------------
                totalSize += BufferSize;
                countSize += BufferSize;


              } // end while(totalSize < fileSize);

              if (randNum > 0)
              {
                randNum--;
              }
              else if (zeroNum > 0)
              {
                zeroNum--;
              }

              // Truncate the file to 0 bytes.
              // This will hide the original file-length if you try to recover the file.
              if (randNum == 0 && zeroNum == 0)
              {
                fs.SetLength(0);
                fs.Close();
                break;
              }

            } // end while (RandNum > 0 || ZeroNum > 0);

          } // end using (FileStream inputStream = new FileStream(FilePath, FileMode.Open))

          //WipeDone();

          // As an extra precaution I change the dates of the file so the
          // original dates are hidden if you try to recover the file.
          var dt = new DateTime(2037, 1, 1, 0, 0, 0);
          File.SetCreationTime(filePath, dt);
          File.SetLastAccessTime(filePath, dt);
          File.SetLastWriteTime(filePath, dt);

          File.SetCreationTimeUtc(filePath, dt);
          File.SetLastAccessTimeUtc(filePath, dt);
          File.SetLastWriteTimeUtc(filePath, dt);

          // Finally, delete the file
          File.Delete(filePath);

        } // end foreach (string FilePath in FilePaths);


      }
      catch (Exception ex)
      {
        System.Windows.Forms.MessageBox.Show(ex.Message);
        e.Result = ErrorUnexpected;
        return (ErrorUnexpected);
      }
      finally
      {
        swProgress.Stop();
      }

      // Delete root directory
      if (Directory.Exists(filePaths[0]))
      {
        FileSystem.DeleteDirectory(
          filePaths[0],
          UIOption.OnlyErrorDialogs,
          RecycleOption.DeletePermanently,
          UICancelOption.ThrowException
        );
      }

      e.Result = DeleteSucceeded;
      return (DeleteSucceeded);

    }

    /// <summary>
    /// Retrieves a list of files matching the specified search pattern in the given root folder path.
    /// </summary>
    /// <param name="fileSearchPattern">The search pattern to match against the file names.</param>
    /// <param name="rootFolderPath">The root folder path to start the search.</param>
    /// <returns>An enumerable collection of file paths matching the search pattern.</returns>
    private static IEnumerable<string> GetFileList(string fileSearchPattern, string rootFolderPath)
    {
      var pending = new Queue<string>();
      pending.Enqueue(rootFolderPath);
      while (pending.Count > 0)
      {
        rootFolderPath = pending.Dequeue();
        yield return rootFolderPath;
        var tmp = Directory.GetFiles(rootFolderPath, fileSearchPattern);
        foreach (var t in tmp)
        {
          yield return t;
        }
        tmp = Directory.GetDirectories(rootFolderPath);
        foreach (var t in tmp)
        {
          pending.Enqueue(t);
        }
      }
    }
    
    /// <summary>
    /// Formats the given file size in bytes as a human-readable string.
    /// </summary>
    /// <param name="size">The file size in bytes.</param>
    /// <param name="fStringByte">Whether "KiB" is added at the end or not</param>
    /// <returns>The formatted file size string.</returns>
    private static string FormatFileSizeString(Int64 size, bool fStringByte)
    {
      double sizeInKiB = size / 1024.0;

      string sizeFormatted;
      if (sizeInKiB >= 10)
      {
        // sizeInKiB が10以上のときは小数点以下を表示しない
        sizeFormatted = sizeInKiB.ToString("N0") + (fStringByte ? @"KiB" : "");
      }
      else
      {
        // sizeInKiB が10未満のときは小数点以下2桁まで表示する
        sizeFormatted = sizeInKiB.ToString("N2") + (fStringByte ? @"KiB" : "");
      }
      return sizeFormatted;
    }

  }

}
 