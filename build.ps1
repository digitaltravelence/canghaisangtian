param([string]$OutputFile = '../沧海桑田.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}
Push-Location -LiteralPath $PSScriptRoot
try {
    & $compiler /nologo /optimize+ /codepage:65001 /target:winexe /platform:anycpu /win32manifest:game.manifest /win32icon:watch.ico "/out:$OutputFile" '/resource:场景/农场.png,farm.png' '/resource:场景/避难所.png,port.png' /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll '/resource:美术/人物动作.png,farmer.png' '/resource:美术/道具.png,items.png' '/resource:美术/耕地.png,soil.png' '/resource:美术/作物.png,plants.png' Program.cs FarmState.cs Game.cs GameArt.cs HarvestPointer.cs Interface.cs FieldDrawer.cs
    if ($LASTEXITCODE -ne 0) { throw 'Game compilation failed.' }
} finally {
    Pop-Location
}
