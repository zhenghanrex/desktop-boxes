param([ValidateRange(5,3600)][int]$Seconds=60)
$ErrorActionPreference='Stop'
$p=Get-Process -Name DesktopBoxes | Select-Object -First 1
$os=Get-CimInstance Win32_OperatingSystem
$start=$p.TotalProcessorTime.TotalSeconds
$clock=[Diagnostics.Stopwatch]::StartNew()
$rows=for($i=0;$i -lt $Seconds;$i++) {
    Start-Sleep -Seconds 1
    $p.Refresh()
    if($p.HasExited){throw 'Desktop Boxes exited during measurement.'}
    [pscustomobject]@{WorkingSetMiB=$p.WorkingSet64/1MB;PrivateMiB=$p.PrivateMemorySize64/1MB}
}
$elapsed=$clock.Elapsed.TotalSeconds
[pscustomobject]@{
    Windows=$os.Caption; Build=$os.BuildNumber; ProcessId=$p.Id
    Seconds=[math]::Round($elapsed,2)
    LogicalProcessors=[Environment]::ProcessorCount
    MachineCPUPercent=[math]::Round(100*($p.TotalProcessorTime.TotalSeconds-$start)/($elapsed*[Environment]::ProcessorCount),3)
    MeanWorkingSetMiB=[math]::Round(($rows.WorkingSetMiB | Measure-Object -Average).Average,2)
    PeakWorkingSetSampleMiB=[math]::Round(($rows.WorkingSetMiB | Measure-Object -Maximum).Maximum,2)
    MeanPrivateMiB=[math]::Round(($rows.PrivateMiB | Measure-Object -Average).Average,2)
} | ConvertTo-Json
