param([string]$OutputFile = '../沧海桑田.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}
Push-Location -LiteralPath $PSScriptRoot
try {
    $motionResources = @(
        '/resource:美术/场景动效/农场动效.png,farm-motion.png'
        '/resource:美术/场景动效/山景农舍动效.png,house-motion.png'
        '/resource:美术/场景动效/避难所动效.png,harbor-motion.png'
        '/resource:美术/场景动效/山谷净景.png,clear-sky.png'
        '/resource:美术/场景动效/窗外净景.png,window-valley.png'
        '/resource:美术/场景动效/飞鸟遮挡.png,flight-window.png'
        '/resource:美术/场景动效/窗台花.png,window-flowers.png'
        '/resource:美术/场景动效/柜上垂藤.png,trailing-vine.png'
        '/resource:美术/场景动效/左窗布.png,curtain-left.png'
        '/resource:美术/场景动效/右窗布.png,curtain-right.png'
        '/resource:美术/场景动效/风车.png,mill-blades.png'
    )
    & $compiler @motionResources /nologo /optimize+ /codepage:65001 /target:winexe /platform:anycpu /win32manifest:game.manifest /win32icon:watch.ico "/out:$OutputFile" '/resource:场景/农场.png,farm.png' '/resource:场景/山景农舍.png,house.png' '/resource:场景/避难所.png,port.png' /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll '/resource:美术/人物动作.png,farmer.png' '/resource:美术/道具.png,items.png' '/resource:美术/耕地.png,soil.png' '/resource:美术/草皮.png,turf.png' '/resource:美术/作物.png,plants.png' Program.cs FarmState.cs Game.cs Breeze.cs SceneryMotion.cs FarmEntrances.cs GameArt.cs HarvestPointer.cs Interface.cs FieldDrawer.cs
    if ($LASTEXITCODE -ne 0) { throw 'Game compilation failed.' }
} finally {
    Pop-Location
}
