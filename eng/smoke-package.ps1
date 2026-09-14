<#
.SYNOPSIS
    Ставит собранные пакеты в чистый проект и проверяет, что они работают.

.DESCRIPTION
    Единственная проверка во всём репозитории, которая идёт через ПАКЕТ, а не
    через ProjectReference. Разница не косметическая: путь analyzers/dotnet/cs,
    выбор ассетов по TFM, netstandard2.0-ветки и транзитивные зависимости живут
    только в пакете, и сломать их можно, не сломав ни одного теста.

    Проверяется четыре утверждения:
      1) пакет XmlSerDe даёт работающий генератор - код появляется, объект
         уезжает в XML и читается обратно;
      2) сборка потребителя чистая при TreatWarningsAsErrors и
         GenerateDocumentationFile, то есть шапка сгенерированных файлов на месте;
      3) потребителю хватает одного using XmlSerDe;
      4) потребитель, подключивший ТОЛЬКО XmlSerDe.Compat, получает ускорение -
         значит анализатор доехал транзитивно, а не только сборки.

    Исходники проектов-образцов лежат рядом, в eng/smoke: собирать их строками
    внутри скрипта нельзя - PowerShell 5.1 разбирает такой файл как ANSI, и
    кириллица в комментариях превращается в мусор ещё до компилятора.

.PARAMETER PackageDirectory
    Папка с .nupkg. По умолчанию artifacts/packages в корне репозитория.

.PARAMETER PackageVersion
    Версия пакетов. По умолчанию берётся из имени найденного XmlSerDe.*.nupkg.
#>
[CmdletBinding()]
param(
    [string] $PackageDirectory,
    [string] $PackageVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$templates = Join-Path $PSScriptRoot 'smoke'

if (-not $PackageDirectory) {
    $PackageDirectory = Join-Path $repositoryRoot 'artifacts/packages'
}

if (-not (Test-Path $PackageDirectory)) {
    throw "Папки с пакетами нет: $PackageDirectory"
}

$PackageDirectory = (Resolve-Path $PackageDirectory).Path

if (-not $PackageVersion) {
    #-Filter умеет только * и ?, поэтому отбор по «версия начинается с цифры»
    #делается регулярным выражением уже после него: XmlSerDe.Compat.*.nupkg
    #иначе попал бы в тот же список и дал бы версию "Compat.0.1.0-alpha.1"
    $main = Get-ChildItem -Path $PackageDirectory -Filter 'XmlSerDe.*.nupkg' |
        Where-Object { $_.Name -match '^XmlSerDe\.\d' } |
        Sort-Object Name |
        Select-Object -First 1

    if (-not $main) {
        throw "В $PackageDirectory нет ни одного XmlSerDe.<версия>.nupkg"
    }

    if ($main.Name -notmatch '^XmlSerDe\.(?<v>.+)\.nupkg$') {
        throw "Не удалось вынуть версию из имени $($main.Name)"
    }

    $PackageVersion = $Matches['v']
}

Write-Host "Пакеты:  $PackageDirectory"
Write-Host "Версия:  $PackageVersion"

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("xmlserde-smoke-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

function New-SmokeProject {
    param(
        [string] $Name,
        [string] $PackageId,
        [string] $ProgramFile
    )

    $dir = Join-Path $work $Name
    New-Item -ItemType Directory -Path $dir | Out-Null

    $project = Get-Content -Path (Join-Path $templates 'Smoke.csproj.template') -Raw
    $project = $project.Replace('__PACKAGE_ID__', $PackageId).Replace('__PACKAGE_VERSION__', $PackageVersion)
    Set-Content -Path (Join-Path $dir ($Name + '.csproj')) -Value $project -Encoding utf8

    Copy-Item -Path (Join-Path $templates $ProgramFile) -Destination (Join-Path $dir 'Program.cs')

    return $dir
}

function Invoke-SmokeProject {
    param(
        [string] $Directory,
        [string] $Title
    )

    Write-Host ""
    Write-Host "=== $Title"

    $output = & dotnet run --project $Directory -c Release --nologo
    $text = $output -join [System.Environment]::NewLine

    Write-Host $text

    if ($LASTEXITCODE -ne 0) {
        throw "$Title : сборка или запуск не удались (код $LASTEXITCODE)"
    }

    return $text
}

try {
    $config = Get-Content -Path (Join-Path $templates 'NuGet.config') -Raw
    $config = $config.Replace('__PACKAGE_DIRECTORY__', $PackageDirectory)
    Set-Content -Path (Join-Path $work 'NuGet.config') -Value $config -Encoding utf8

    $nativeDir = New-SmokeProject -Name 'native' -PackageId 'XmlSerDe' -ProgramFile 'native.Program.cs'
    $nativeText = Invoke-SmokeProject -Directory $nativeDir -Title 'пакет XmlSerDe: генератор, сериализация, чтение'

    $expectedXml = 'XML: <Order><Id>7</Id><Lines><OrderLine><Product>Bolt</Product><Quantity>3</Quantity></OrderLine></Lines></Order>'
    if (-not $nativeText.Contains($expectedXml)) {
        throw 'родной путь: XML не тот, которого ждали'
    }

    if (-not $nativeText.Contains('BACK: Id=7 Lines=1 Product=Bolt')) {
        throw 'родной путь: объект не прочитался обратно'
    }

    $compatDir = New-SmokeProject -Name 'compat' -PackageId 'XmlSerDe.Compat' -ProgramFile 'compat.Program.cs'
    $compatText = Invoke-SmokeProject -Directory $compatDir -Title 'пакет XmlSerDe.Compat: анализатор доехал транзитивно'

    if (-not $compatText.Contains('ACCELERATED: True')) {
        throw 'drop-in: тип не ускорен - значит анализатор не доехал через зависимость пакета'
    }

    if (-not $compatText.Contains('BACK: Id=7')) {
        throw 'drop-in: объект не прочитался обратно'
    }

    Write-Host ""
    Write-Host "Дымовой тест пакетов пройден."
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
