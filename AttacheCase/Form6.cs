//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttachéCase4 )" -- File encryption software.
// Copyright (C) 2016-2022  Mitsuhiro Hibara
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
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AttacheCase
{
  public partial class Form6 : Form
  {
    public Form6()
    {
      InitializeComponent();
    }

    private void Form6_Load(object sender, EventArgs e)
    {

    }
    private void Form6_FormClosed(object sender, FormClosedEventArgs e)
    {

    }
    private void buttonCancel_Click(object sender, EventArgs e)
    {
      this.Close();
    }

    private void buttonGenerate_Click(object sender, EventArgs e)
    {
      if (File.Exists(AppSettings.Instance.SaveToIniDirPath) == false)
      {
        // Default foloder is Desktop
        saveFileDialog1.InitialDirectory = 
          Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
      }
      else
      {
        saveFileDialog1.InitialDirectory = AppSettings.Instance.SaveToIniDirPath;
      }

      if (saveFileDialog1.ShowDialog() == DialogResult.OK )
      {
        CreateKeyPair(saveFileDialog1.FileName, "");
        DirectoryInfo diParent = Directory.GetParent(saveFileDialog1.FileName);
        AppSettings.Instance.SaveToIniDirPath = diParent.FullName;
      }
    }

    //----------------------------------------------------------------------
    // ペアの公開鍵・暗号鍵を生成する
    //----------------------------------------------------------------------
    private static string CreateKeyPair(string filePath, string guidString)
    {

      if (string.IsNullOrEmpty(guidString))
      {
        // GUIDを生成する
        var guid = Guid.NewGuid();
        guidString = guid.ToString();
      }

      DirectoryInfo diParent = Directory.GetParent(filePath);
      string DirPath = diParent.FullName;
      string FileName = Path.GetFileNameWithoutExtension(filePath);

      // 公開鍵・秘密鍵のファイルパス
      var publicKeyFilePath = Path.Combine(DirPath, FileName + ".atclock");
      var privateFilePath = Path.Combine(DirPath, FileName + ".atckey");

      //-----------------------------------
      //RSACryptoServiceProviderオブジェクトの作成
      var rsa = new RSACryptoServiceProvider(2048);

      //公開鍵をXML形式で取得
      var publicKey = rsa.ToXmlString(false);
      //秘密鍵をXML形式で取得
      var privateKey = rsa.ToXmlString(true);

      //-----------------------------------
      // 公開鍵XMLファイルの編集
      var xml = XElement.Parse(publicKey);

      // アップロード日時（UTC日時）
      //XElement xmlUploadDateTime = new XElement("upload", "");
      //xml.AddFirst(xmlUploadDateTime);
      // 生成日時（UTC日時）
      //XElement xmlDateTime = new XElement("datetime", DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss"));
      //xml.AddFirst(xmlDateTime);
      // From ( メールアドレスなど )
      //XElement xmlFrom = new XElement("from", sendToString);
      //xml.AddFirst(xmlFrom);
      // ラベル（鍵管理用キーワード）
      //XElement xmlTo = new XElement("to", fromString);
      //xml.AddFirst(xmlTo);

      // 種別
      XElement xmlType = new XElement("type", "public");
      xml.AddFirst(xmlType);
      // GUID
      XElement xmlGuid = new XElement("id", guidString);
      xml.AddFirst(xmlGuid);
      // Token
      XElement xmlToken = new XElement("token", "AttacheCase");
      xml.AddFirst(xmlToken);
      // 公開鍵として保存する
      xml.Save(publicKeyFilePath);

      //-----------------------------------
      // 秘密鍵XMLファイルの編集
      xml = XElement.Parse(privateKey);

      //xml.AddFirst(xmlUploadDateTime); // アップロード日時（UTC日時）
      //xml.AddFirst(xmlDateTime);       // 作成日時（UTC日時）
      //xml.AddFirst(xmlFrom);           // 送り主情報（メールアドレスなど格納）
      //xml.AddFirst(xmlTo);             // 鍵管理用キーワード

      // 種別
      xmlType = new XElement("type", "private");
      xml.AddFirst(xmlType);
      // GUID
      xml.AddFirst(xmlGuid);
      // Token
      xml.AddFirst(xmlToken);
      // 秘密鍵として保存する
      xml.Save(privateFilePath);

      rsa.Clear();

      return guidString;

    }

    private void label3_Click(object sender, EventArgs e)
    {

    }
  }
}
