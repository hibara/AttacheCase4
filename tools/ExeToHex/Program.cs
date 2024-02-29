using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace ExeToHex
{
	internal static class Program
	{
    private static int _exeOutSize;
    
    /// <summary>
    /// それぞれのバージョンの自己実行形式ファイル（バイナリファイル）から、
    /// byte[] 文字列へ変換して、アタッシェケースプロジェクト構成ファイル内（ ExeOut4.cs ）に
    /// ソースコードとして書き込む外部ツール
    /// </summary>
    /// <param name="args">
    ///	0: Sourcecode file ( ExeOut4.cs ) path
    ///	1: Exeout binary file ( ExeOut40.exe ) path of older version
    ///	2: Exeout binary file ( ExeOut462.exe ) path of recent version
    /// </param>
    /// <returns></returns>
		static int Main(string[] args)
		{
			if (args.Length < 3)	// 引数の数が足りてない
			{
				MessageBox.Show("３つの引数が必要です！");
				return (1);
			}
			
			//-----------------------------------
			// args[0]: CS file that the binary data is written to
			var cSharpSourceFilePath = args[0];
			if (File.Exists(cSharpSourceFilePath) == false)
			{
				MessageBox.Show("C#ソースファイルの存在が見つかりません！　第一引数が不正です。");
				return (1);
			}
			else if (!string.Equals(Path.GetExtension(cSharpSourceFilePath), (".cs"), StringComparison.CurrentCultureIgnoreCase))
			{
				MessageBox.Show("C#ソースファイルの指定ではありません！　第一引数が不正です。");
				return (1);
			}

			//-----------------------------------
			// args[1]: Executable file (.NET Framework 4.0) that is written to binary data
			if (File.Exists(args[1]) == false)
			{
				MessageBox.Show("実行ファイルの存在が見つかりません！　第二引数が不正です。");
				return (1);
			}
			else if (!string.Equals(Path.GetExtension(args[1]), (".exe"), StringComparison.CurrentCultureIgnoreCase))
			{
				MessageBox.Show("実行ファイルの拡張子ではありません！　第二引数が不正です。");
				return (1);
			}

			//-----------------------------------
			// args[1]: Executable file (.NET Framework 4.6.2) that is written to binary data
			if (File.Exists(args[2]) == false)
			{
				MessageBox.Show("実行ファイルの存在が見つかりません！　第三引数が不正です。");
				return (1);
			}
			else if (!string.Equals(Path.GetExtension(args[2]), (".exe"), StringComparison.CurrentCultureIgnoreCase))
			{
				MessageBox.Show("実行ファイルの拡張子ではありません！　第三引数が不正です。");
				return (1);
			}

			//----------------------------------------------------------------------
			// ソースファイルにある、`#region` ～ `endregion` の間を一旦クリアする
			var srcOutList = new List<string>();
			var srcList = new List<string>(File.ReadAllLines(cSharpSourceFilePath, Encoding.UTF8));

			var fSkip = false;

			for (var i = 0; i < srcList.Count(); i++)
			{
				if (srcList[i].Contains("#region"))
				{
					fSkip = true;
					srcOutList.Add(srcList[i]);
					continue;
				}

				if (srcList[i].Contains("#endregion"))
				{
					fSkip = false;
					srcOutList.Add(srcList[i]);
					continue;
				}
				
				if (fSkip == false)
				{
					srcOutList.Add(srcList[i]);
				}
				
			}

			//----------------------------------------------------------------------
			// ソースファイル（ExeOut4.cs）を読み込みリスト化する
			
			// #region Array of each executable file size
			// #endregion
			
			// #region The bytes data of ATC executable file ( .NET Framework 4.0 )
			// #endregion

			// #region The bytes data of ATC executable file ( .NET Framework 4.6.2 )
			// #endregion

			var sizeList = new List<Int64>();

			for (var i = 0; i < srcOutList.Count(); i++)
			{
				// 各自己実行形式ファイルのサイズを挿入する
				if (srcOutList[i].Contains("#region Array of each executable file size"))
				{
					// 各実行ファイルサイズをリスト化する
					var fileInfo = new FileInfo(args[1]);
					sizeList.Add(fileInfo.Length);
					fileInfo = new FileInfo(args[2]);
					sizeList.Add(fileInfo.Length);
					
					// 各実行ファイルのバージョン番号文字列をリスト化する
					var versionList = new List<string>();
					var fvi = FileVersionInfo.GetVersionInfo(args[1]);
					// or use fvi.FileVersion for file version
					versionList.Add(fvi.ProductVersion); 
					fvi = FileVersionInfo.GetVersionInfo(args[2]);
					versionList.Add(fvi.ProductVersion); 

					// ソースファイルへサイズを表すInt64配列を挿入する
					srcOutList.Insert(i + 1, 
						"      // The size of ATC executable file ( .NET Framework 4.0 )" + Environment.NewLine + 
						     "      " + sizeList[0] + ",  // ver." + versionList[0] + Environment.NewLine +
						     "      // The size of ATC executable file ( .NET Framework 4.6.2 )" + Environment.NewLine + 
								 "      " + sizeList[1] + ",  // ver." + versionList[1] + Environment.NewLine); 
				}
				// 自己実行形式ファイル（.NET Framework 4.0）のバイト配列を挿入
				else if (srcOutList[i].Contains("#region The bytes data of ATC executable file ( .NET Framework 4.0 )"))
				{
					srcOutList.Insert(i + 1, ReadExeBinaryFileToString(args[1]));
					var exeOutSizeString = sizeList[0].ToString("#,0") + " Bytes";
					//MessageBox.Show("以下のファイルに、" + exeOutSizeString + " を書き込みました。\n" + cSharpSourceFilePath);
					Console.WriteLine("以下のファイルに、" + exeOutSizeString + "( .NET Framework 4.0 ) を書き込みました。\n" + cSharpSourceFilePath);

				}
				// 自己実行形式ファイル（.NET Framework 4.6.2）のバイト配列を挿入
				else if (srcOutList[i].Contains("#region The bytes data of ATC executable file ( .NET Framework 4.6.2 )"))
				{
					srcOutList.Insert(i + 1, ReadExeBinaryFileToString(args[2]));
					var exeOutSizeString = sizeList[1].ToString("#,0") + " Bytes";
					//MessageBox.Show("以下のファイルに、" + exeOutSizeString + " を書き込みました。\n" + cSharpSourceFilePath);
					Console.WriteLine("以下のファイルに、" + exeOutSizeString + "( .NET Framework 4.6.2 ) を書き込みました。\n" + cSharpSourceFilePath);
				}
				
			}
			
			// ExeOut4.cs を読み込んで加工したデータをそのまま同じファイルに書き戻す
			File.WriteAllLines(cSharpSourceFilePath, srcOutList);

      return (0);

		}// end static int Main(string[] args);


		/// <summary>
		/// Reads the binary content of an executable file and converts it to a string representation.
		/// </summary>
		/// <param name="exeFilePath">The path to the executable file.</param>
		/// <returns>A string representation of the binary content of the given executable file.</returns>
		private static string ReadExeBinaryFileToString(string exeFilePath)
		{
			using (var ms = new MemoryStream())
			using (var fs = new FileStream(exeFilePath, FileMode.Open, FileAccess.Read))
			{
				_exeOutSize = (int)fs.Length;
				
				string result;
				//using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))	// BOM付きで挿入されソースにゴミデータが混入する
				using (var sw = new StreamWriter(ms))
				{
					ms.Position = 0;
					sw.WriteLine("      new byte[]{");

					var br = new BinaryReader(fs);
					var data = new byte[_exeOutSize];

					var count = 0;

					for (var i = 0; i < _exeOutSize; i++)
					{
						data[i] = br.ReadByte();

						var dataString = i == _exeOutSize - 1 ? $"0x{data[i]:X2}" : $"0x{data[i]:X2}, ";

						if (count == 0)
						{
							sw.Write("        " + dataString);
						}
						else if (count > 15)
						{
							sw.WriteLine(dataString); // 改行
							count = -1;
						}
						else
						{
							sw.Write(dataString);
						}

						count++;

					}

					sw.WriteLine("");
					sw.WriteLine("      },");

					// Be sure to flush the StreamWriter to ensure all data has been written to the MemoryStream
					sw.Flush();
					
					// Set the position back to the start of the MemoryStream
					ms.Position = 0;
		
					// Read the MemoryStream out to a string
					using (var sr = new StreamReader(ms))
					{
						result = sr.ReadToEnd();
					}
				}
				return result;
			}
			
		}


    
    
    
    
	}

}
