param([string]$OutputFile = '../沧海桑田.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
}

$images = [ordered]@{
    'farm.png' = '场景/农场.png'
    'house.png' = '场景/山景农舍.png'
    'port.png' = '场景/避难所.png'
    'farmer.png' = '美术/人物动作.png'
    'items.png' = '美术/道具.png'
    'spade.png' = '美术/农具/铲子.png'
    'watering-can.png' = '美术/农具/水壶.png'
    'soil.png' = '美术/耕地.png'
    'turf.png' = '美术/草皮.png'
    'plants.png' = '美术/作物.png'
    'farm-motion.png' = '美术/场景动效/农场动效.png'
    'house-motion.png' = '美术/场景动效/山景农舍动效.png'
    'harbor-motion.png' = '美术/场景动效/避难所动效.png'
    'clear-sky.png' = '美术/场景动效/山谷净景.png'
    'window-valley.png' = '美术/场景动效/窗外净景.png'
    'flight-window.png' = '美术/场景动效/飞鸟遮挡.png'
    'window-flowers.png' = '美术/场景动效/窗台花.png'
    'trailing-vine.png' = '美术/场景动效/柜上垂藤.png'
    'curtain-left.png' = '美术/场景动效/左窗布.png'
    'curtain-right.png' = '美术/场景动效/右窗布.png'
    'mill-blades.png' = '美术/场景动效/风车.png'
}

Push-Location -LiteralPath $PSScriptRoot
$scratch = $null
try {
    $resourceArgs = @()
    $embedded = $null
    foreach ($name in $images.Keys) {
        $asset = $images[$name]
        if (!(Test-Path -LiteralPath $asset -PathType Leaf)) {
            # A clone includes the published EXE, which already contains the original artwork.
            if (!$embedded) {
                $release = Join-Path $PSScriptRoot '沧海桑田·农具版.exe'
                $embedded = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($release))
                $scratch = Join-Path ([IO.Path]::GetTempPath()) ('canghai-build-' + [Guid]::NewGuid().ToString('N'))
                [void][IO.Directory]::CreateDirectory($scratch)
            }
            $asset = Join-Path $scratch $name
            $source = $embedded.GetManifestResourceStream($name)
            if (!$source) { throw "发行程序中缺少美术资源 $name" }
            try {
                $file = [IO.File]::Create($asset)
                try { $source.CopyTo($file) } finally { $file.Dispose() }
            } finally { $source.Dispose() }
        }
        $resourceArgs += "/resource:$asset,$name"
    }
    foreach ($name in 'pasture','till','sow','water','pick','reap','fertilize') {
        $resourceArgs += "/resource:音频/$name.wav,audio.$name.wav"
    }
    $sources = @('Program.cs','FarmState.cs','Game.cs','Breeze.cs','SceneryMotion.cs','FarmEntrances.cs',
                 'GameArt.cs','HarvestPointer.cs','Interface.cs','FieldDrawer.cs','FarmAudio.cs',
                 'AudioPreferences.cs','SoundSettings.cs')
    & $compiler /nologo /optimize+ /codepage:65001 /target:winexe /platform:anycpu /win32manifest:game.manifest /win32icon:watch.ico "/out:$OutputFile" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll @resourceArgs @sources
    if ($LASTEXITCODE -ne 0) { throw 'Game compilation failed.' }
} finally {
    if ($scratch) {
        foreach ($asset in [IO.Directory]::GetFiles($scratch)) { [IO.File]::Delete($asset) }
        [IO.Directory]::Delete($scratch)
    }
    Pop-Location
}
