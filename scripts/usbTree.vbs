Option Explicit
Dim shell, fso, root, nativePowerShell, powerShell, base, script
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
root = shell.ExpandEnvironmentStrings("%SystemRoot%")
powerShell = root & "\System32\WindowsPowerShell\v1.0\powershell.exe"
nativePowerShell = root & "\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
If fso.FileExists(nativePowerShell) Then powerShell = nativePowerShell
base = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
script = base & "usbTree.ps1"
shell.Run """" & powerShell & """ -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File """ & script & """", 0, False
