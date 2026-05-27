Set-Location "c:\Users\moote\Documents\Codes\Apps\Laser Cursor"
git add -A
git commit -m "chore: add README, fix tray text Pro->Laser Cursor, update CI workflow"
Write-Host "Commit: $LASTEXITCODE"

dotnet publish "LaserCursorApp\LaserCursorApp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "publish\v1.3.0"
Write-Host "Build: $LASTEXITCODE"

git tag v1.3.0
git push origin HEAD:main
git push origin v1.3.0
Write-Host "Push: $LASTEXITCODE"

gh release create v1.3.0 "publish\v1.3.0\LaserCursorApp.exe" `
    --repo HoodBlah/laser-cursor-app `
    --title "v1.3.0" `
    --notes "## What's new

- Added README
- Tray icon text no longer says 'Pro'
- Updated CI workflow to trigger on version tags and use correct project paths" `
    --latest
Write-Host "Release: $LASTEXITCODE"
