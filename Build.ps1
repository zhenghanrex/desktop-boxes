$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dest = Join-Path $PSScriptRoot 'out'
New-Item $dest -ItemType Directory -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $dest 'DesktopBoxes.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32manifest:DesktopBoxes.manifest "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll Program.cs DesktopBox.cs Native.cs
if ($LASTEXITCODE) { throw 'C# compilation failed' }
$inno = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
& $inno Setup.iss
if ($LASTEXITCODE) { throw 'Installer compilation failed' }
$test = Join-Path $env:TEMP ([guid]::NewGuid().ToString())
New-Item $test -ItemType Directory | Out-Null
function Check-Process($File, $Arguments) {
    $p = Start-Process -FilePath $File -ArgumentList $Arguments -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "$File exited with $($p.ExitCode)" }
}
Check-Process $exe "--self-test `"$test\portable.xml`""
$installer = Join-Path $dest 'DesktopBoxes-Setup.exe'
Check-Process $installer "/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=`"$test\app`""
$installed = Join-Path $test 'app\DesktopBoxes.exe'
if ((Get-FileHash $installed).Hash -ne (Get-FileHash $exe).Hash) { throw 'Installed binary mismatch' }
Check-Process $installed "--self-test `"$test\installed.xml`""
Check-Process (Join-Path $test 'app\unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
if (Test-Path $installed) { throw 'Uninstall failed' }
Copy-Item Measure-DesktopBoxes.ps1, README.md -Destination $dest
$assets = Get-Item $exe, $installer, "$dest\Measure-DesktopBoxes.ps1", "$dest\README.md"
$assets | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_).Hash, $_.Name } | Set-Content "$dest\SHA256SUMS.txt" -Encoding ascii
