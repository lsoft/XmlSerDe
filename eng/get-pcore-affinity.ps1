<#
.SYNOPSIS
    Печатает маску affinity, в которой оставлены только производительные ядра.

.DESCRIPTION
    Нужна для run-benchmarks.bat: на гибридных процессорах (Intel P/E-cores,
    Snapdragon X, Zen 5c) планировщик волен перекинуть бенчмарк с P-ядра на
    E-ядро посреди итерации, и это не шум в пределах StdDev, а разница в разы.
    BenchmarkDotNet такой прогон не забракует - он увидит просто «большой разброс».

    Ядра берутся из GetLogicalProcessorInformationEx(RelationProcessorCore):
    у каждого физического ядра там есть EfficiencyClass. По документации класс
    тем больше, чем производительнее ядро, и он ненулевой ТОЛЬКО на гибридных
    машинах. Отсюда два исхода:

      * все ядра одного класса (обычный Xeon, Ryzen, любой доцикломый Intel) -
        ограничивать нечего, скрипт не печатает ничего;
      * классов несколько - в маску попадают все логические процессоры ядер
        старшего класса, включая SMT-близнецов: бенчмарк однопоточный, и лишний
        свободный гипертред ему не мешает.

    Маска - величина групповая (KAFFINITY), а процесс живёт в одной группе,
    поэтому на машинах с >64 логическими процессорами берётся только группа 0;
    если P-ядер в ней не оказалось, скрипт снова не печатает ничего.

    Диагностика идёт в stderr и намеренно на ASCII: её читает cmd.exe в OEM-кодировке,
    в которой кириллица превратилась бы в мусор. В stdout - ровно одна строка с маской
    или ничего; вызывающая сторона на пустой вывод обязана реагировать как на
    «ограничений нет», а не как на ошибку.

.PARAMETER AsDecimal
    Печатать маску десятичным числом (так её ждёт BenchmarkDotNet в --affinity),
    а не шестнадцатеричным (так её ждёт start /affinity).

.OUTPUTS
    System.String - маска либо пустой вывод.

.EXAMPLE
    PS> .\get-pcore-affinity.ps1
    FFF
#>
[CmdletBinding()]
param(
    [switch] $AsDecimal
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Diagnostic([string] $message)
{
    [Console]::Error.WriteLine($message)
}

# Разбор буфера сделан чтением по смещениям, а не [StructLayout]-классом: запись
# SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX переменной длины (GroupMask - массив
# ANYSIZE_ARRAY), и маршалер такую форму не выражает.
#
#   SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX { DWORD Relationship; DWORD Size; union {...} }
#   PROCESSOR_RELATIONSHIP { BYTE Flags; BYTE EfficiencyClass; BYTE Reserved[20];
#                            WORD GroupCount; GROUP_AFFINITY GroupMask[ANYSIZE_ARRAY]; }
#   GROUP_AFFINITY { KAFFINITY Mask; WORD Group; WORD Reserved[3]; }
$source = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace XmlSerDe.Bench
{
    public struct CoreInfo
    {
        public byte EfficiencyClass;
        public ushort Group;
        public ulong Mask;
    }

    public static class TopologyProbe
    {
        private const int RelationProcessorCore = 0;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(
            int relationshipType, IntPtr buffer, ref int returnedLength);

        public static CoreInfo[] GetCores()
        {
            int length = 0;
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
            if (length <= 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // GROUP_AFFINITY: ULONG_PTR + WORD + WORD[3], то есть 16 байт на x64 и 12 на x86.
                int groupAffinitySize = IntPtr.Size + 8;
                var cores = new List<CoreInfo>();

                int offset = 0;
                while (offset + 8 <= length)
                {
                    int relationship = Marshal.ReadInt32(buffer, offset);
                    int size = Marshal.ReadInt32(buffer, offset + 4);
                    if (size <= 0)
                    {
                        break;
                    }

                    if (relationship == RelationProcessorCore)
                    {
                        int processor = offset + 8;
                        byte efficiencyClass = Marshal.ReadByte(buffer, processor + 1);
                        int groupCount = (ushort)Marshal.ReadInt16(buffer, processor + 22);

                        for (int i = 0; i < groupCount; i++)
                        {
                            int groupAffinity = processor + 24 + (i * groupAffinitySize);
                            ulong mask = IntPtr.Size == 8
                                ? (ulong)Marshal.ReadInt64(buffer, groupAffinity)
                                : (ulong)(uint)Marshal.ReadInt32(buffer, groupAffinity);

                            cores.Add(new CoreInfo
                            {
                                EfficiencyClass = efficiencyClass,
                                Group = (ushort)Marshal.ReadInt16(buffer, groupAffinity + IntPtr.Size),
                                Mask = mask
                            });
                        }
                    }

                    offset += size;
                }

                return cores.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
'@

function Get-BitCount([uint64] $mask)
{
    $count = 0
    while ($mask -ne 0)
    {
        $mask = $mask -band ($mask - 1)
        $count++
    }

    return $count
}

try
{
    if (-not ('XmlSerDe.Bench.TopologyProbe' -as [type]))
    {
        Add-Type -TypeDefinition $source -Language CSharp -ErrorAction Stop
    }

    $cores = @([XmlSerDe.Bench.TopologyProbe]::GetCores())
}
catch
{
    Write-Diagnostic ("P-core detection failed: " + $_.Exception.Message)
    Write-Diagnostic "Falling back to the default affinity (all cores)."
    exit 0
}

if ($cores.Count -eq 0)
{
    Write-Diagnostic "P-core detection: the OS reported no processor cores, using all cores."
    exit 0
}

$classes = @($cores | ForEach-Object { [int] $_.EfficiencyClass } | Sort-Object -Unique)
if ($classes.Count -le 1)
{
    Write-Diagnostic ("P-core detection: uniform CPU, all " + $cores.Count +
        " cores share efficiency class " + $classes[0] + " - no affinity mask needed.")
    exit 0
}

$topClass = $classes[-1]
$fast = @($cores | Where-Object { [int] $_.EfficiencyClass -eq $topClass -and $_.Group -eq 0 })
if ($fast.Count -eq 0)
{
    Write-Diagnostic ("P-core detection: processor group 0 has no cores of the fastest class " +
        $topClass + " - a mask would only narrow the run, using all cores.")
    exit 0
}

$mask = [uint64] 0
foreach ($core in $fast)
{
    $mask = $mask -bor $core.Mask
}

if ($mask -eq 0)
{
    Write-Diagnostic "P-core detection: the fastest cores carry an empty affinity mask, using all cores."
    exit 0
}

$groupZeroTotal = 0
foreach ($core in $cores)
{
    if ($core.Group -eq 0)
    {
        $groupZeroTotal += Get-BitCount $core.Mask
    }
}

Write-Diagnostic ("P-core detection: hybrid CPU, efficiency classes " + ($classes -join ',') +
    "; " + $fast.Count + " core(s) of class " + $topClass + " -> " + (Get-BitCount $mask) +
    " of " + $groupZeroTotal + " logical CPUs in group 0.")

if ($AsDecimal)
{
    Write-Output ([string] $mask)
}
else
{
    Write-Output ('{0:X}' -f $mask)
}
