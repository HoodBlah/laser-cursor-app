Set-Location "c:\Users\moote\Documents\Codes\Apps\Laser Cursor"
git add -A
git commit -m "fix: change profile export/import from .lasercfg to .json, fix AppData folder name"
git tag v1.3.1
git push origin HEAD:main
git push origin v1.3.1
Write-Host "Git done: $LASTEXITCODE"

dotnet publish "LaserCursorApp\LaserCursorApp.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "publish\v1.3.1"
Write-Host "Build done: $LASTEXITCODE"

gh release create v1.3.1 "publish\v1.3.1\LaserCursorApp.exe" `
    --repo HoodBlah/laser-cursor-app `
    --title "v1.3.1" `
    --notes "## Fix

- Profile export/import now uses `.json` instead of `.lasercfg` so files can be shared in GitHub comments and discussions
- Fixed AppData settings folder name (was `LaserCursorPro`, now `LaserCursorApp`)" `
    --latest

Remove-Item "c:\Users\moote\Documents\Codes\Apps\Laser Cursor\do_v131.ps1" -Force
