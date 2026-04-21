Write-Host "=== Запуск автотестов ===" -ForegroundColor Cyan
Write-Host ""

dotnet test

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Все тесты пройдены!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "❌ Некоторые тесты не прошли" -ForegroundColor Red
}