$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$tracked=git -C $projectRoot ls-files
if($LASTEXITCODE -ne 0) {throw 'Cannot enumerate tracked public files'}
$failures=[System.Collections.Generic.List[string]]::new()
foreach($relative in $tracked) {
 if($relative -match '(^|/)(companion-settings\.json[^/]*|\.env[^/]*|\.private-notes|update-history|bin|obj|artifacts|tools)(/|$)' -or $relative -match '\.(pdb|log)$') {$failures.Add($relative);continue}
 if([System.IO.Path]::GetExtension($relative) -notin @('.cs','.csproj','.xaml','.manifest','.md','.yml','.yaml','.ps1','.txt','')) {continue}
 $text=[System.IO.File]::ReadAllText((Join-Path $projectRoot $relative))
 if($text -match '(?i)\b(sk-[a-z0-9_-]{20,}|gh[pousr]_[a-z0-9_]{20,})\b' -or $text -match '(?i)[a-z]:[\\/]Users[\\/][a-z0-9_. -]+[\\/]') {$failures.Add($relative);continue}
 $emails=[regex]::Matches($text,'[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}')
 foreach($email in $emails) {if(-not $email.Value.EndsWith('@users.noreply.github.com') -and -not $email.Value.EndsWith('@noreply.github.com')) {$failures.Add($relative);break}}
}
if($failures.Count -gt 0) {throw ('Public-file privacy check failed for: '+(($failures | Sort-Object -Unique) -join ', '))}
Write-Output "Privacy check passed for $($tracked.Count) tracked files; no matched secret, personal path or private-email patterns."
