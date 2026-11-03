# Security policy

LapsTool displays local administrator passwords managed by LAPS and helps start a Remote Desktop
session as the managed account. Security reports are taken seriously and handled privately.

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
- **Remote Desktop:** LapsTool passes only the user name to mstsc (through mstsc's per-host
  `UsernameHint` registry value) and starts `mstsc /v:<host>`. The password is never given to mstsc,
  written to Credential Manager, or put on a command line; the user pastes it into mstsc's prompt.
  (Version 0.1.0 stored a temporary `TERMSRV/<host>` credential instead. It was removed in 0.2.0
  because local-account RDP logons use NTLM, which Windows' default policy refuses for saved
  credentials.)
- **Clipboard:** a copied password, including the one copied when connecting, is cleared after a
  configurable delay (default 30 seconds) and when the tool exits, unless the user has copied
  something else in the meantime
- **Operation log** (`%APPDATA%\LapsTool\logs`) records who viewed or changed what and when. It never
  contains passwords

Things that are **by design** and not considered vulnerabilities:

- A user who is allowed to read a LAPS password can see it, copy it and use it. That is the purpose
  of the tool
- Until it is cleared, a copied password is on the clipboard of the signed-in user's session, where
  other programs running in that session can read it
- The released binaries are currently **not code-signed**. Verify the SHA-256 hash published with each
  release, or build from source

Things that **would** be vulnerabilities and should be reported:

- Any way for a password to reach disk, a log file, a command line, or another user
- A copied password staying on the clipboard past the delay or past tool exit in a normal run
  (when the user has not copied anything else)
- Any way to read or change a LAPS attribute beyond the rights Active Directory grants the user
