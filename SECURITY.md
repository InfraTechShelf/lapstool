# Security policy

LapsTool displays local administrator passwords managed by LAPS, and briefly stores one in the
Windows Credential Manager to start a Remote Desktop session. Security reports are taken seriously
and handled privately.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Use GitHub's private vulnerability reporting: go to the **Security** tab of this repository and choose
**Report a vulnerability**. Only the maintainer can see the report.

If that option is not available to you, contact the maintainer through
[Infra Tech Shelf](https://infratechshelf.com/profile/).

You can write in English or Japanese.

## What to include

- The version of LapsTool, the LAPS type involved (Windows LAPS encrypted / Windows LAPS clear-text /
  legacy Microsoft LAPS), and the Windows version
- Steps to reproduce, or a description of the weakness if you have not reproduced it
- What an attacker could achieve

## What happens next

- You will get an acknowledgement, normally within a few days
- The report stays private while a fix is prepared
- Once a fixed version is released, the issue is described in the release notes. You will be
  credited if you want to be

## Design notes

How LapsTool handles passwords, so that you can tell intended behaviour from a defect:

- **Authorization is left to Active Directory.** LapsTool reads LAPS attributes with the signed-in
  user's own rights. Windows LAPS passwords, including decryption, are obtained through Microsoft's
  `LAPS` PowerShell module; LapsTool has no decryption code of its own
- **Passwords are held as `SecureString`** and turned into plain text only when the user presses
  *Show* or *Copy*
- **Remote Desktop:** the password is written with the `CredWrite` API (never on a command line) as a
  `TERMSRV/<host>` credential with *logon session* persistence, and deleted after a configurable
  delay (default 20 seconds) and when the tool exits
- **Clipboard:** a copied password is cleared after a configurable delay (default 30 seconds), unless
  the user has copied something else in the meantime
- **Operation log** (`%APPDATA%\LapsTool\logs`) records who viewed or changed what and when. It never
  contains passwords

Things that are **by design** and not considered vulnerabilities:

- A user who is allowed to read a LAPS password can see it, copy it and use it. That is the purpose
  of the tool
- During the delay before deletion, the temporary Remote Desktop credential exists in the signed-in
  user's Credential Manager, readable only by that user's logon session
- The released binaries are currently **not code-signed**. Verify the SHA-256 hash published with each
  release, or build from source

Things that **would** be vulnerabilities and should be reported:

- Any way for a password to reach disk, a log file, a command line, or another user
- The temporary Remote Desktop credential surviving past the delay or past tool exit in a normal run
- Any way to read or change a LAPS attribute beyond the rights Active Directory grants the user
