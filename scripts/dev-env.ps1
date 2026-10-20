# Usage (from the repo root):  . .\scripts\dev-env.ps1
$vars = @{}
Get-Content .env | Where-Object { $_ -match '^[A-Za-z_]' } | ForEach-Object {
    $key, $value = $_ -split '=', 2
    $vars[$key.Trim()] = $value.Trim()
}

$env:ConnectionStrings__TradeDb =
    "Host=localhost;Port=5432;Database=$($vars['POSTGRES_DB']);Username=$($vars['POSTGRES_USER']);Password=$($vars['POSTGRES_PASSWORD'])"

Write-Host "ConnectionStrings__TradeDb is set for this terminal."