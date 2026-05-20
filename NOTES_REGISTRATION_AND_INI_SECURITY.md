# レジストレーションコード管理と `_AtcCase.ini` セキュリティ強化に関する設計メモ

最終更新: 2026-05-13

本ドキュメントは、ver.4.2.5.5 以降に行った以下のテーマに関する変更点と設計判断の記録です。

- 商用利用ライセンス(レジストレーションコード)のレジストリパス移行
- `_AtcCase.ini` を介したレジストレーションコードの読み書き
- `_AtcCase.ini` 読み込み時の承認マーカー(`_AtcCase.approved`)のセキュリティ強化

---

## 1. レジストレーションコードのレジストリパス変更

### 背景
旧仕様では、AttacheCase4 においてもレジストレーションコードを `HKCU\Software\Hibara\AttacheCase3\Registration\RegistrationCode` に保存していた。Ver.4 系専用の領域へ整理する。

### 仕様
- 読み書きは `HKCU\Software\Hibara\AttacheCase4\Registration\RegistrationCode` に集約。
- 既存ユーザー(AttacheCase3 側にコードがある状態)は、初回 AttacheCase4 起動時に **自動的にコピー移行** され、移行元(`AttacheCase3\Registration` サブキー)は削除される。
- 削除のロジックは「`AttacheCase3\Option` 等の他キーは触らず、`Registration` サブキーだけを除去」する範囲に限定。

### 実装位置
- `AttacheCase/LicenseRegister.cs`
  - クラスフィールドのパス定数を AttacheCase4 系に変更
  - 静的メソッド `MigrateLegacyRegistrationIfNeeded()` を追加し、移行ロジックをコンストラクタから切り出し
- `AttacheCase/AppSettings.cs`
  - `ReadOptionsFromRegistry()` 末尾で `LicenseRegister.MigrateLegacyRegistrationIfNeeded()` を呼ぶ
  - `ReadOptionsFromRegistry` は **通常モード(レジストリ運用時)でのみ呼ばれる** ため、INI 一時設定モードでは移行が走らない=レジストリ汚染が起きない

### 重要なポイント
移行ロジックを `LicenseRegister` のコンストラクタから外したのは、INI 一時設定モード(`_AtcCase.ini` 読み込み)中に `new LicenseRegister("")` が呼ばれてもレジストリに書き込みが発生しないようにするため。Form2 (バージョン情報画面) はライセンス表示の確認用にこのコンストラクタを叩くため、ここで書き込みが起きると「INI モードではレジストリを汚さない」という仕様が破れていた。

---

## 2. `LicenseRegister` コンストラクタの整理(レジストリ汚染防止)

### 背景
旧コンストラクタは、引数空文字呼び出し(=レジストリから現在値を読み取りたい場合)に対して、`AttacheCase4\Registration` サブキーが存在しなければ **無条件で作成** していた。これにより、INI 一時設定モードでも Form2 起動などをトリガーとしてレジストリへサブキーが作られてしまっていた。

### 仕様
- コンストラクタは「読むだけ」に徹し、サブキーが存在しなくても作らない。
- 実際の書き込みは `Decrypt(true)` 成功時にのみ行う(従来通り)。
- `DeleteLicense()` は AttacheCase4 と AttacheCase3 の両方の `Registration` サブキーを掃除(片方だけ残ると次回起動時の移行ロジックでライセンスが復活してしまうため)。

### 実装位置
- `AttacheCase/LicenseRegister.cs`

---

## 3. `_AtcCase.ini` へのレジストレーションコード出力・入力

### 背景
`_AtcCase.ini` には、レジストレーションコードを書き出す/読み込む経路がなかった。ライセンス購入済みユーザーが PC を移行する際、`_AtcCase.ini` を持ち回るだけでライセンスも一緒に移動できるようにする。

### 仕様(出力)
- `WriteOptionToIniFile` の末尾で `[Registration] RegistrationCode=...` を出力する。
- レジストリ上のコードは可読性のため 32 文字ごとに `Environment.NewLine` が挟まっているが、`WritePrivateProfileString` は改行を含む値を正しく扱えず、上書き時に古い「孤児行」が残って同じコードが何重にも記録される問題が起きるため、**改行を除去した 1 行の連結文字列として書き出す**。
- 書き出し前に `WritePrivateProfileString("Registration", null, null, ...)` で `[Registration]` セクション全体を削除して、旧バージョンが残した孤児行(改行付き値の残骸)も合わせて掃除する。

### 仕様(入力)
- `ReadOptionFromIniFile` で `[Registration] RegistrationCode` を読み込み、`AppSettings.RegistrationCodeString` (メモリ上ステージング領域)に格納する。
- **この時点ではレジストリには書き込まない**。他の設定項目と同じ「一時設定」モデルに揃える。
- バッファ長 1024 文字を確保する(コードは RSA 暗号文の 16 進文字列で 512 文字)。`AppSettings.ReadIniFile` に `maxSize` を取るオーバーロードを追加。

### レジストリへの反映タイミング
- 「動作設定」→「設定の保存/復元」の **「一時的な設定を現在の設定に置き換える」ボタン** を押したときに、`SaveOptionsToRegistry` 経由で `LicenseRegister(staged).Decrypt(true)` が走り、RSA 検証が通った場合のみ `AttacheCase4\Registration` に書き込まれる。
- 検証失敗時は何もしない(既存のレジストリ値は破壊しない)。

### 実装位置
- `AttacheCase/AppSettings.cs`
  - 新フィールド `_RegistrationCodeString` とプロパティ `RegistrationCodeString` を追加
  - `ReadIniFile` のオーバーロード `(..., int maxSize)` を追加
  - `ReadOptionFromIniFile` / `WriteOptionToIniFile` / `ReadOptionsFromRegistry` / `SaveOptionsToRegistry` の各末尾に Registration 関連処理を追加
- `AttacheCase/LicenseRegister.cs`
  - `RegistrationCode` プロパティに getter を追加(従来は setter のみ)

---

## 4. Form2 のライセンス表示(INI 一時設定モード対応)

### 背景
INI 一時設定モードではレジストリにコードを書き込まないため、Form2(バージョン情報画面)が `new LicenseRegister("")` でレジストリだけを見に行く既存実装では「未登録」表示になってしまっていた。

### 仕様
- Form2 のライセンス判定を「レジストリ → INI ステージング」の 2 段フォールバックに変更。
  1. `new LicenseRegister("")` → `Decrypt(false)` でレジストリ側を検証
  2. 失敗した場合、`AppSettings.Instance.RegistrationCodeString` で INI ステージング値を検証
- いずれかで成功すれば商用ライセンス表示。`Decrypt(false)` なのでレジストリには一切書き込まない。

### 他経路でフォールバックを入れなかった理由

| 呼び出し箇所 | 役割 | フォールバック |
| --- | --- | --- |
| `AppSettings.SaveOptionsToRegistry` 内の差分比較 | ステージング値 vs レジストリ現在値で変更検知 | 入れない(差分検知が壊れる) |
| `AppSettings.ReadOptionsFromRegistry` 末尾 | レジストリ → ステージング同期 | 不要 |
| `AppSettings.WriteOptionToIniFile`(非INIモード時) | レジストリ → ステージング再同期 | 不要 |
| `/lc ""` コマンドラインによるライセンス削除 | レジストリ操作そのもの | 不要 |
| Form2 のライセンス削除ボタン | レジストリ操作そのもの | 入れていない(INI モードでの削除挙動は将来検討) |

### 実装位置
- `AttacheCase/Form2.cs`

---

## 5. `WriteOptionToIniFile` の null 例外修正

### 背景
`WriteOptionToIniFile` 末尾で `securityManager.UpdateApprovalIfChanged()` を無条件に呼んでいたが、`securityManager` は起動時に `_AtcCase.ini` が検出された場合にのみ初期化されるフィールド。Form3 の「現在の設定をINIファイルへ保存」のように任意パスへ書き出す経路では null のままで、NullReferenceException が発生していた。

### 仕様
- `securityManager?.UpdateApprovalIfChanged()` に変更。

### 実装位置
- `AttacheCase/AppSettings.cs`

### 将来のリファクタ候補
本来このメソッドは「INI パスへ設定を書き出す」だけが責務であり、承認マーカー更新は呼び出し側(`SaveOptions` 経由など)に持っていくほうが綺麗。今回は影響範囲を最小限にとどめるため null ガードで止めている。

---

## 6. `_AtcCase.approved` 承認マーカーのセキュリティ強化

### 背景
`_AtcCase.ini` の改ざん検知に使われている `_AtcCase.approved` マーカーは、旧仕様では **公開情報のみで偽造可能** だった。具体的には:

- マーカーの中身: `{INI の SHA-256}|{ボリュームシリアル番号}`
- SHA-256 は誰でも計算可、ボリュームシリアル番号は WMI で無権限取得可
- → INI を書き換えられる攻撃者は同等の `.approved` を生成して警告を完全に回避できる
- さらに `storedDeviceId == "NO_DEVICE_ID"` または現在の DeviceId が `NO_DEVICE_ID` の場合にデバイスチェックをスキップする抜け道があり、攻撃者は `.approved` 右側に `NO_DEVICE_ID` を書くだけでデバイス検証を素通りできた

### 強化方針
コスパ重視で次の 3 点を導入。

#### (a) HMAC-SHA256 + バイナリ埋め込み鍵
- マーカーのハッシュ値を素の SHA-256 から **HMAC-SHA256(共有秘密鍵)** に差し替え
- 鍵は `IniSecurityManager.HmacKey` として 32 byte の固定バイト列で埋め込み
- AttacheCase は OSS のためソースを読める攻撃者にとっては秘密ではないが、「INI が置かれているディレクトリにだけ書ける(USB スティック、共有フォルダ等)が、アプリ本体は持っていない」攻撃者に対しては偽造ハードルが上がる
- より強固にしたい場合は、ユーザー/マシン固有の値から鍵を導出する仕組みに置き換える(現状は採用見送り。後述「将来の選択肢」参照)

#### (b) `NO_DEVICE_ID` バイパスの撤廃
- `CheckWarning` のデバイスID 検証を厳密一致に変更
- 取得失敗が常態化する環境(一部のネットワークドライブ等)では毎回 `DifferentDevice` 警告が出るが、利便性より検証の厳密性を優先

#### (c) 承認済みデバイスの複数登録サポート(ポータブル USB 運用)
- マーカーのフォーマットを拡張: `{HMAC}|{deviceID1}|{deviceID2}|...`
- `MarkAsApproved` は既存マーカーから承認済みデバイス一覧を読み出し、現在のデバイスを追加してから書き戻す
- これにより、同じ USB を複数 PC に挿し替えても、各 PC での初回承認 1 回だけで以降は無警告になる

### マーカー検証の判定順を変更
- 旧: デバイスIDチェック → ハッシュチェック
- 新: ハッシュチェック(`ModifiedFile`) → デバイスID一覧チェック(`DifferentDevice`)
- 両方とも問題がある場合、より重要な「内容改ざんの可能性」を優先して通知

### 既存マーカーへの影響
- 旧フォーマット(SHA-256 ベース)のマーカーは新フォーマット(HMAC ベース)では値が一致しないため、アップデート直後の初回起動で `ModifiedFile` 警告が一度だけ表示される。ユーザーが「はい」を押せば新フォーマットの値で `.approved` が書き直され、次回以降は通常通り無警告で読み込まれる(=実質ワンタイムマイグレーション)。

### 実装位置
- `AttacheCase/IniSecurityManager.cs`
  - `HmacKey` 定数を追加
  - `CalculateFileHash` を `HMACSHA256` ベースに変更
  - `CheckWarning` でマーカーを複数デバイス対応に分解、判定順を反転、`NO_DEVICE_ID` バイパス撤廃
  - `MarkAsApproved` を既存デバイスリスト保持 + 追記方式に変更
  - ヘルパー `ReadStoredDeviceIds()` を追加

### 期待される挙動(USB ポータブル運用)

| 行為 | 結果 |
| --- | --- |
| PC#A 初回起動 | `NewFile` 警告 → 承認 → `.approved = [h, A]` |
| PC#A で設定変更して終了 | `UpdateApprovalIfChanged` 経由で `[h2, A]` に更新 |
| PC#B に挿して起動 | ハッシュ一致だが B 未登録 → `DifferentDevice` 警告 → 承認 → `[h2, A, B]` |
| PC#A に戻して起動 | ハッシュ一致 + A 登録済み → 無警告で起動 |
| PC#A で設定変更 → PC#B へ | `[h3, A, B]` → ハッシュ一致 + B 登録済み → 無警告 |
| 攻撃者が INI を改ざん(鍵を知らない) | HMAC を再計算できないため、どの PC でも `ModifiedFile` 警告 |
| 知らない PC(PC#C)に挿して起動 | A/B はリストにあるが C は無い → `DifferentDevice` 警告 |

### トレードオフ(留意点)
- 承認済みデバイス間の **相互信頼モデル** を採用しているため、ある PC で INI が書き換えられた場合(かつ攻撃者がそのマシンの権限を持ち、`.approved` も更新できた場合)、他の承認済み PC では `ModifiedFile` 警告が出ない。
- 「ポータブル運用の利便性」と「個別 PC での再承認による厳密性」のトレードオフを前者寄りに振った結果。
- 厳密寄りに振り直したい場合は、`MarkAsApproved` の中で「既存ハッシュと現在ハッシュが異なるときはデバイス一覧をリセットする」分岐を入れるだけで対応可。

### 同時に修正した潜在バグ
- `File.GetAttributes(_approvalMarkerPath)` がマーカー未生成時に `FileNotFoundException` を投げる経路を `File.Exists` チェックでガード(従来は外側 try/catch に拾われて承認情報書き込みが「失敗」扱いとなり、ユーザーへ誤った警告が表示されるおそれがあった)。

---

## 7. その他の関連修正

### Form1 デザイナーで開けない問題
- 原因: `AttacheCase\CustomControl\` 配下にある `CustomDialog.cs` / `DelayTextBox.cs` / `SplitButton.cs` / `TaskbarProgress.cs` のコピーがルート直下にも存在しており、`.csproj` も両方を `<Compile Include>` していた。Form1.cs に `using AttacheCase.CustomControl;` があるため、両者で `CS0104(あいまい参照)` が発生し、デザイナーがロードに失敗していた。
- 対処: ルート直下の重複 4 ファイルを削除、`.csproj` から重複 `<Compile Include>` を削除。

### `DelayTextBox.cs` の文字エンコーディング修正
- 原因: `CustomControl\DelayTextBox.cs` だけが Shift-JIS(BOMなし)で保存されており、他の `.cs` (UTF-8 BOM) と不揃い。Roslyn は BOM なしファイルを UTF-8 として解釈するため、Shift-JIS の日本語コメントが不正バイト列となりコンパイル失敗 → アセンブリが生成されない → デザイナーが「型が見つかりません」と表示していた。
- 対処: ファイルを UTF-8 BOM 付きで保存し直し。

---

## 将来の選択肢(参考)

### `.approved` マーカーをさらに強固にする場合
1. **ユーザー/マシン固有の値から鍵を導出**(例: `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` をソルトとして使う)
   - OSS でもソース閲覧だけでは鍵を再現できなくなる
   - ただし USB ポータブル運用と両立しない(別マシンでは別鍵になるため、毎回 `ModifiedFile` 警告が出る)
2. **INI に RSA 署名を付ける**(ライセンスコードと同じ方式)
   - 最も強い。`_AtcCase.ini` の発行者を厳密に縛れる
   - ただし INI を作成できるのも秘密鍵を持つ開発者だけになるため、ユーザーが自由に INI を書き換えられなくなる(運用が重い)

### `_AtcCase.ini` 由来のライセンス削除挙動
- 現状、Form2 の「ライセンス削除」ボタンはレジストリの `AttacheCase4\Registration` のみを対象としている。
- INI 一時設定モードで動作中に削除操作を行ったとき、`AppSettings.RegistrationCodeString` (ステージング値)もクリアするかどうかは未対応。必要に応じて将来検討。

### `WriteOptionToIniFile` のリファクタ
- 現状は「INI に書き出す」処理と「承認マーカー更新」処理が混ざっている。`SaveOptions` 側に切り分けると責務が綺麗になる。

---

## ⚠️ Git 管理外ファイルの注意

`AttacheCase/LicenseRegister.cs` は **`.gitignore` 対象**(RSA 秘密鍵を含むため)で、本作業で変更を加えてもコミット差分には現れません。バックアップや別 PC への同期時は、Dropbox 経由で物理ファイルが届くことを前提とし、git で追跡されていないことを把握しておく必要があります。

## 関連ファイル(変更箇所一覧)

- `AttacheCase/LicenseRegister.cs` ⚠️ **gitignore 対象** — レジストリパス、コンストラクタ整理、`MigrateLegacyRegistrationIfNeeded` 追加、`RegistrationCode` getter 追加
- `AttacheCase/AppSettings.cs` — `_RegistrationCodeString` フィールド/プロパティ、各読み書き経路への Registration 関連処理、`ReadIniFile` maxSize オーバーロード、null ガード、`[Registration]` セクション再構築
- `AttacheCase/Form2.cs` — ライセンス表示のステージング値フォールバック
- `AttacheCase/IniSecurityManager.cs` — HMAC 化、複数デバイス対応、`NO_DEVICE_ID` バイパス撤廃、判定順反転、`File.GetAttributes` ガード、`ReadStoredDeviceIds` ヘルパー
- `AttacheCase/CustomControl/DelayTextBox.cs` — UTF-8 BOM 付きへ再保存
- `AttacheCase/AttacheCase.csproj` — ルート直下の重複 `<Compile Include>` 4 件削除
- (削除)`AttacheCase/{CustomDialog,DelayTextBox,SplitButton,TaskbarProgress}.cs` — `CustomControl\` 配下の正本と重複していたコピー
