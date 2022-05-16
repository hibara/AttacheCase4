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
			if (args.Length < 3)	// 引数の数が足りてない
			{
				MessageBox.Show("Not enough arguments!");
				return (1);
			}
			//-----------------------------------
			// args[0]: Executable file that is written to binary data
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

			//-----------------------------------
			// args[1]: CS file that the binary data is written to
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

			//-----------------------------------
			// args[2]: .NET Framework version of executable file
			int ToolVersionIndex = -1;
			string ToolVersion = args[2];
			if (ToolVersion == "4.0")
			{
				ToolVersionIndex = 0;
			}
			else if (ToolVersion == "4.6.2")
			{
				ToolVersionIndex = 1;
			}
			else
      {
				MessageBox.Show(".NET Framework version is invalid!");
				return (1);
			}

			//----------------------------------------------------------------------
			// Read and make the binary data array of "Exeout.exe"
			// MemoryStreamオブジェクトに「Exeout.exe」を格納する
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
						sw.WriteLine("      new byte[]{");

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
								sw.Write("        " + dataString);
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

						sw.WriteLine("");
						sw.WriteLine("      },");

					}// end using (StreamWriter sw = new StreamWriter(ms, System.Text.Encoding.UTF8));

				}// end using (FileStream fs = new FileStream(ExecutableFilePath, FileMode.Open, FileAccess.Read));
						
				//----------------------------------------------------------------------
				// Output src file text
				//----------------------------------------------------------------------

				string[] lines = System.IO.File.ReadAllLines(CShrpSourceFilePath, Encoding.UTF8);

				List<string> SrcList = new List<string>();

				bool fDelete = false;
				for (int i = 0; i < lines.Count(); i++)
				{
					if (lines[i].IndexOf("public static int[] ExeOutFileSize") > -1)
					{
						SrcList.Add(lines[i]);

						if (ToolVersionIndex == 0)
            {
							SrcList.Add(lines[i+=1]);	                            // {
							SrcList.Add(string.Format("      {0},", ExeOutSize)); // ExeOutSize[0],
							SrcList.Add(lines[i+=2]);                             // ExeOutSize[1]
							SrcList.Add(lines[i+=1]);                             // };
						}
            else
            {
							SrcList.Add(lines[i+=1]);                             // {
							SrcList.Add(lines[i+=1]);                             // ExeOutSize[0]
							SrcList.Add(string.Format("      {0}", ExeOutSize));  // ExeOutSize[1],
							SrcList.Add(lines[i+=2]);                             // };
						}

					}
					else if (lines[i].IndexOf("#region") > -1)
					{
						if ((ToolVersionIndex == 0 && lines[i].IndexOf("4.0") > -1) || 
								(ToolVersionIndex == 1 && lines[i].IndexOf("4.6.2") > -1))
            {
							fDelete = true;
							SrcList.Add(lines[i]);
							SrcList.Add(Encoding.UTF8.GetString(ms.ToArray()));
						}
            else
            {
							SrcList.Add(lines[i]);
						}

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
