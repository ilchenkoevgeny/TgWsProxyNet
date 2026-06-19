$secret = -join ((1..16) | ForEach-Object { '{0:x2}' -f (Get-Random -Minimum 0 -Maximum 256) })

Write-Host "Internal secret: $secret"
Write-Host "Telegram secret: dd$secret"
