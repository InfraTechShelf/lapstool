# LapsTool — LAPS パスワード参照ツール

> **English summary** — LapsTool is a small Windows GUI for help-desk and infrastructure staff. It looks
> up local administrator passwords managed by **Windows LAPS** (encrypted or clear-text) and
> **legacy Microsoft LAPS** in Active Directory, changes their expiration time, and opens a Remote
> Desktop session as the LAPS-managed local account (user name filled in, password on the clipboard
> ready to paste). The local account name used for the connection is configurable per organization.
> It runs without local admin rights, has no dependencies beyond .NET Framework 4.8 and Windows, and
> leaves all authorization to Active Directory.
> See [SECURITY.md](SECURITY.md) for how passwords are handled. The UI and documentation are in
> Japanese. **Status: 0.2.0-alpha.**

> **非公式ツールです。** Microsoft とは関係がなく、Microsoft による承認・サポートもありません。
> LAPS・Windows・Active Directory は Microsoft グループの商標です。

Active Directory の LAPS で管理されているローカル管理者パスワードを参照し、次の操作を行う Windows 用 GUI ツールです。

- パスワードの参照（表示・コピー）
- パスワード有効期限の書き換え（日時指定／今すぐ期限切れ）
- 参照したパスワードでのリモートデスクトップ接続

対象は **Windows LAPS**（暗号化・平文の両方）と **旧 Microsoft LAPS**（`ms-Mcs-AdmPwd`）です。どちらで管理されているかは、端末ごとに自動で判定します。

## 動作要件

| 項目 | 内容 |
|---|---|
| OS | Windows 11、Windows 10（2023年4月以降の更新プログラム適用済み）、Windows Server 2019 以降 |
| ランタイム | .NET Framework 4.8 |
| 権限 | ローカル管理者は**不要**（UAC昇格なしで起動できます） |
| Windows LAPS | OS 標準の `LAPS` PowerShell モジュールを使います（追加インストールは不要） |

## ダウンロード

[GitHub Releases](https://github.com/InfraTechShelf/lapstool/releases) から `LapsTool-<バージョン>.zip` をダウンロードし、任意のフォルダに展開して `LapsTool.exe` を実行してください。インストールは不要です。

展開する前に、ZIP の SHA-256 が Release ページの記載と一致することを確認してください。

```powershell
Get-FileHash -Algorithm SHA256 .\LapsTool-0.2.0-alpha.zip
```

実行ファイルはコード署名していないため、初回の実行時に Windows SmartScreen の警告が出ることがあります。その場合は「詳細情報」→「実行」で起動できます。

## 必要な AD 側の権限

本ツールは**ログオン中のユーザーの権限**で AD にアクセスします。権限の判定は AD とLAPSモジュールが行い、本ツールがそれを迂回することはありません。

| 操作 | Windows LAPS | 旧 LAPS |
|---|---|---|
| 参照 | `Set-LapsADReadPasswordPermission` で許可されていること。暗号化パスワードの場合は、復号を許可されたグループ（ポリシーの「ADPasswordEncryptionPrincipal」）にも所属していること | `Set-AdmPwdReadPasswordPermission` で許可されていること |
| 有効期限変更 | `Set-LapsADResetPasswordPermission` で許可されていること | `Set-AdmPwdResetPasswordPermission` で許可されていること |

別のアカウントで使う場合は、次のように起動します。

```bat
runas /netonly /user:CORP\admin-user LapsTool.exe
```

## 使い方

1. コンピューター名を入力して［検索］を押します。`*` をワイルドカードとして使えます。`.` を含む入力は FQDN として検索します。
2. 一覧から端末を選び、［取得］を押します。パスワードは伏せ字で表示され、［表示］で平文に切り替わります。
3. 必要に応じて、次のいずれかを行います。
   - **コピー**：クリップボードにコピーします。設定した秒数（初期値30秒）が過ぎると自動で消去されます。
   - **有効期限の変更**：日付と時刻を指定して変更するか、今すぐ期限切れにします。パスワードが実際に変わるのは、対象端末で次にポリシーが処理されたときです。
   - **接続**：リモートデスクトップで接続します（下記）。

## リモートデスクトップ接続の仕組み

［接続］を押すと、次の流れで接続します。

1. パスワードをまだ取得していなければ取得します。
2. mstsc が接続先ごとに覚えているユーザー名（レジストリの `UsernameHint`）を、`<コンピューター名>\<アカウント名>` に書き換えます。
3. `mstsc /v:<接続先>` を起動します。mstsc の資格情報の入力画面には、このユーザー名が最初から入っています。
4. パスワードをクリップボードにコピーします。入力画面のパスワード欄に貼り付けて接続してください。クリップボードは一定時間（初期値30秒）が過ぎると消去されます。

補足：

- パスワードは、mstsc にも資格情報マネージャーにも渡しません。ツールの外に出るのはクリップボードだけです。
- `UsernameHint` は mstsc 自身も接続に成功したときに書き込む値なので、書き換えても副作用はありません。

**パスワードを自動で入力しない理由**：0.1.0 では、資格情報マネージャーに一時登録して自動でログオンする方式でした。しかし、RDP の認証（CredSSP）は mstsc に渡した資格情報で行われます。LAPS のアカウントはローカルアカウントなので、認証は必ず NTLM になり、サーバーの身元を確認できません。そのため Windows の既定のポリシーでは保存資格情報が拒否され、ほとんどの環境で結局パスワードの入力を求められていました。自動ログオンを通すには、「NTLM のみのサーバー認証で保存された資格情報の委任を許可する」ポリシーなど、サーバーのなりすましを防げなくなる設定が必要になります。ツールの前提としてそれを求めないよう、0.2.0 からはユーザー名だけを渡す方式に変えました。

## 設定

［設定…］から変更できます。

| 項目 | 初期値 | 説明 |
|---|---|---|
| ローカルアカウント名 | `Administrator` | 接続に使うアカウント名。企業ごとに設定します |
| Windows LAPS のアカウント名を優先 | オン | Windows LAPS が記録しているアカウント名があれば、それを使います（旧 LAPS は記録しないため、常に設定値を使います） |
| FQDN で接続 | オン | オフにするとコンピューター名で接続します |
| 管理モード（/admin） | オフ | |
| 接続時にパスワードをクリップボードにコピー | オン | mstsc の入力画面に貼り付けるためのものです |
| クリップボード消去まで | 30秒 | 5〜600秒 |
| ドメインコントローラー／検索ベース DN | 空 | 空の場合は自動で決まります |

設定は `%APPDATA%\LapsTool\settings.xml` に保存されます。

### 会社ごとの初期値を配布する

設定画面の［既定値ファイルとして書き出す…］で `LapsTool.defaults.xml` を作成し、`LapsTool.exe` と同じフォルダに置いてください。設定を保存していない利用者には、この値が初期値として使われます。

## 操作ログ

参照・コピー・有効期限変更・RDP 接続の記録を `%APPDATA%\LapsTool\logs\operations_yyyyMM.log` に残します。記録する内容は、日時・実行ユーザー・操作・端末・結果です。**パスワードは記録しません。**

## ビルド

Visual Studio Build Tools（.NET デスクトップビルドツール）の MSBuild を使います。NuGet への依存はありません。

```bat
MSBuild.exe LapsTool.sln /t:Rebuild /p:Configuration=Release
```

ビルド結果は `dist\Release\` に出力されます。配布するのは `LapsTool.exe`、`LapsTool.exe.config`、`LapsTool.Core.dll` の3つです。

配布用の ZIP は次のスクリプトで作ります（`release\LapsTool-<バージョン>.zip`）。`-CertThumbprint <拇印>` を付けると、ZIP にする前にコード署名します。

```powershell
.\pack-release.ps1
```

## 検証状況（0.2.0-alpha）

| 項目 | 状況 |
|---|---|
| ビルド（警告0・エラー0）、GUI の起動・終了 | 確認済み |
| 設定ファイルの書き出し・範囲外の値の補正 | 確認済み |
| **旧 LAPS** 環境での検索・参照・有効期限の変更・RDP 接続 | 確認済み（一通りの動作。0.1.0 で確認） |
| **Windows LAPS** 環境での検索・参照・今すぐ期限切れ | 確認済み（0.1.0 で確認） |
| **Windows LAPS** 環境での RDP 接続（入力画面に LAPS のアカウントが入る・貼り付けて接続できる・クリップボードが消去される） | 確認済み |
| 暗号化パスワードを `runas /netonly` で起動したツールから取得できるか | **未検証** |

検証の手順は [docs/検証手順.md](docs/検証手順.md) にあります。Windows LAPS 環境で試した結果（動いた・動かなかった）を Issue で教えていただけると助かります。

## ライセンス

[Apache License 2.0](LICENSE)。商標と名称の扱いは [NOTICE](NOTICE) を参照してください。
脆弱性の報告は Issue ではなく [SECURITY.md](SECURITY.md) の方法でお願いします。
