<#
.SYNOPSIS
  Sends one tiny request to OpenRouter and prints which provider served it and what it cost.

.DESCRIPTION
  Uses the model opencode uses in this repo (deepseek/deepseek-v4.1-flash:floor, cheapest provider first).
  The OpenRouter key comes from OPENROUTER_API_KEY, else from opencode's saved login, else it asks.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\check-openrouter-routing.ps1
#>
param([string]$Model = 'deepseek/deepseek-v4.1-flash:floor')

$key = $env:OPENROUTER_API_KEY
if (-not $key) {
    foreach ($f in @("$env:USERPROFILE\.local\share\opencode\auth.json", "$env:APPDATA\opencode\auth.json", "$env:LOCALAPPDATA\opencode\auth.json")) {
        if (Test-Path $f) {
            try { $a = Get-Content $f -Raw | ConvertFrom-Json; if ($a.openrouter.key) { $key = $a.openrouter.key; break } } catch { }
        }
    }
}
if (-not $key) { $key = Read-Host 'OpenRouter API key' }

$body = @{
    model      = $Model
    messages   = @(@{ role = 'user'; content = 'Say hi in one word.' })
    max_tokens = 10
    usage      = @{ include = $true }
} | ConvertTo-Json -Depth 5

$r = Invoke-RestMethod -Uri 'https://openrouter.ai/api/v1/chat/completions' -Method Post -Body $body `
    -ContentType 'application/json' -Headers @{ Authorization = "Bearer $key" }

"model:    $($r.model)"
"provider: $($r.provider)"
"reply:    $($r.choices[0].message.content)"
if ($r.usage) { "tokens:   $($r.usage.prompt_tokens) in, $($r.usage.completion_tokens) out; cost `$$($r.usage.cost)" }
$cheap = @('InferenceNet', 'Wafer', 'Relace', 'Morph', 'Sail Research', 'OpenInference', 'DekaLLM', 'DeepInfra')
if ($cheap -contains $r.provider) { 'OK: a low-cost provider.' } else { 'CHECK: not one of the low-cost providers; tell Claude which provider this was.' }
