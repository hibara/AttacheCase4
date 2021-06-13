using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace ExeToHex
{
  class Program
	{
		private const int BUFFER_SIZE = 16;
    private static int ExeOutSize = 0;

		static int Main(string[] args)
		{
			if (args.Length < 1)
			{
				return (1);
			}

			// Executable file that is written to binary data
			string ExecutableFilePath = args[0];
			if (File.Exists(ExecutableFilePath) == false)
			{
				MessageBox.Show("ExecutableFilePath is not found!");
				return (1);
			}
			else if (Path.GetExtension(ExecutableFilePath).ToLower() != (".exe").ToLower())
			{
				MessageBox.Show("Executable file extension is invalid!");
				return (1);
			}

			// CS file that the binary data is written to
			string CShrpSourceFilePath = args[1];
			if (File.Exists(CShrpSourceFilePath) == false)
			{
				MessageBox.Show("CSharp source is not found!");
				return (1);
			}
			else if (Path.GetExtension(CShrpSourceFilePath).ToLower() != (".cs").ToLower())
			{
				MessageBox.Show("CSharp source file is invalid!");
				return (1);
			}
			
			//----------------------------------------------------------------------
			// Read the binary data of "Exeout.exe"
			//----------------------------------------------------------------------

			using (MemoryStream ms = new MemoryStream())
			{
				using (FileStream fs = new FileStream(ExecutableFilePath, FileMode.Open, FileAccess.Read))
				{
          ExeOutSize = (int)fs.Length;

					//using (StreamWriter sw = new StreamWriter(ms, Encoding.UTF8))	// BOM付きで挿入されソースにゴミデータが混入する
					using (StreamWriter sw = new StreamWriter(ms))
					{
						ms.Position = 0;
						sw.WriteLine("    public static byte[] rawData = {");

						BinaryReader br = new BinaryReader(fs);
						byte[] data = new byte[ExeOutSize];

						int count = 0;
						string dataString = "";
						for (int i = 0; i < ExeOutSize; i++)
						{
							data[i] = br.ReadByte();
							if (i == ExeOutSize - 1)
              {
								dataString = string.Format("0x{0:X2}", data[i]);
							}
							else
              {
								dataString = string.Format("0x{0:X2}, ", data[i]);
							}

							if (count == 0)
              {
								sw.Write("      " + dataString);
              }
							else if (count > 15)
              {
								sw.WriteLine(dataString);	// 改行
								count = -1;
							}
							else
              {
								sw.Write(dataString);
							}
							count++;
						}


						/*
            while ((len = fs.Read(byteArray, 0, BUFFER_SIZE)) > 0)
            {
              List<String> StringList = new List<string>();
              for (int i = 0; i < len; i++)
              {
                StringList.Add(string.Format("0x{0:X2}", byteArray[i]));
                TotalSize++;
              }

              string[] OneLineArray = StringList.ToArray();

              string OneLine = "      " + string.Join(", ", OneLineArray);

              if (fs.Position == fs.Length)
              {
                sw.WriteLine(OneLine);  // Last line of array.
								//Console.WriteLine(OneLine);
              }
              else
              {
                sw.WriteLine(OneLine + ",");
								//Console.WriteLine(OneLine + ",");
							}

						}//end while();
						*/

						sw.WriteLine("");
						sw.WriteLine("    };");

					}// end using (StreamWriter sw = new StreamWriter(ms, System.Text.Encoding.UTF8));

					//----------------------------------------------------------------------
					//
					//System.Windows.Forms.MessageBox.Show("TotalSize: " + TotalSize.ToString());
					//----------------------------------------------------------------------

				}// end using (FileStream fs = new FileStream(ExecutableFilePath, FileMode.Open, FileAccess.Read));
						
				//----------------------------------------------------------------------
				// Output src file text
				//----------------------------------------------------------------------

				string[] lines = System.IO.File.ReadAllLines(CShrpSourceFilePath, Encoding.UTF8);

				List<string> SrcList = new List<string>();

				bool fDelete = false;
				for (int i = 0; i < lines.Count(); i++)
				{
					if (lines[i].IndexOf("public int ExeOutFileSize") > -1)
					{
						SrcList.Add(string.Format("    public int ExeOutFileSize = {0};", ExeOutSize));
					}
					else if (lines[i].IndexOf("#region") > -1)
					{
						fDelete = true;
						SrcList.Add(lines[i]);
            SrcList.Add(Encoding.UTF8.GetString(ms.ToArray()));
          }
					else if (lines[i].IndexOf("#endregion") > -1)
					{
						fDelete = false;
						SrcList.Add(lines[i]);
					}
					else
					{
						if (fDelete == true)
            {
							// fDeleteフラグ中は書き込まない（削除される）
            }
            else  
						{
							SrcList.Add(lines[i]);
						}
					}

				}

				string src = string.Join(Environment.NewLine, SrcList.ToArray());

				/*
				// データ量が多いと、Regexでtime out エラーが発生するようだ。 
				Regex r = new Regex("\t{1,}#region ATC executable file bytes data(\r|\n|\r\n|.)*#endregion", RegexOptions.Multiline);
				MatchCollection matches = r.Matches(src);
				if (matches.Count != 1)
				{
					MessageBox.Show("matches.Count: " + matches.Count.ToString());
				}
				// Replace
				src = r.Replace(src, "\t\t#region ATC executable file bytes data\r\n" + ExeHexString + "\r\n\t\t#endregion");
				*/

				File.WriteAllText(CShrpSourceFilePath, src, Encoding.UTF8);

        //----------------------------------------------------------------------
        string ExeOutSizeString = String.Format("{0:#,0} Bytes", ExeOutSize);
        //MessageBox.Show("以下のファイルに、" + ExeOutSizeString + " を書き込みました。\n" + CShrpSourceFilePath);
				Console.WriteLine("以下のファイルに、" + ExeOutSizeString + " を書き込みました。\n" + CShrpSourceFilePath);
        //----------------------------------------------------------------------
        
      }// end using (MemoryStream ms = new MemoryStream());

      return (0);

		}// end static int Main(string[] args);

	}

}
