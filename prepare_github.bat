@echo off
setlocal enabledelayedexpansion
echo ========================================================
echo   Tally WhatsApp Sender - Repository Cleanup ^& Git Init 
echo ========================================================
echo.
echo WARNING! Ensure your "go run main.go" server is absolutely 
echo STOPPED (Press Ctrl+C in that terminal) before proceeding!
echo.
pause

echo.
echo [1/4] Erasing old sub-repositories to prep for Monorepo...
rmdir /s /q TallyWhatsappSender\.git 2>nul
rmdir /s /q whatsapp-mcp\.git 2>nul
rmdir /s /q .git 2>nul

echo.
echo [2/4] Moving the specific WORKING files into clean directories...
mkdir Tally-COM-Interface 2>nul
mkdir Backend-Bridge 2>nul
mkdir Tally-TDL 2>nul
mkdir Build-Scripts 2>nul
mkdir Archive 2>nul

:: Move TDL
copy /y TallyWhatsappSender\TDL\MinimalTest.tdl Tally-TDL\MinimalTest.tdl
:: Move C# Source Code 
xcopy /e /i /y TallyWhatsappSender\TallyWhatsappsender\* Tally-COM-Interface\TallyWhatsappsender\
copy /y TallyWhatsappSender\CompileAndRegister.bat Build-Scripts\
:: Move Go server
xcopy /e /i /y whatsapp-mcp\whatsapp-bridge\* Backend-Bridge\

echo.
echo [3/4] Archiving the old noise and experimental files...
move config.json Archive\ 2>nul
move register-dll.ps1 Archive\ 2>nul
move start-bridge.bat Archive\ 2>nul
move test-integration.bat Archive\ 2>nul
move CRITICAL_FIX.md Archive\ 2>nul
move DO_THIS_NOW.txt Archive\ 2>nul
move DEPLOYMENT_GUIDE.md Archive\ 2>nul
move QUICK_START.md Archive\ 2>nul
move START_TALLY.bat Archive\ 2>nul
move TDL_CLASS_FIX.md Archive\ 2>nul
move TDL_FINAL_FIX.md Archive\ 2>nul
move 0472dfa7-2ceb-4ebd-85dc-b3f13f8ce9a4 Archive\ 2>nul
move Samples Archive\ 2>nul

:: Hide the Archive from Git (we added Archive/ to .gitignore)

echo.
echo [4/4] Initializing Git and committing the clean files!
git init
git add README.md
git add .gitignore
git add Tally-COM-Interface\
git add Backend-Bridge\
git add Tally-TDL\
git add Build-Scripts\

git commit -m "Initial Monorepo Commit: Completed asynchronous Tally-WhatsApp integration with intelligent Receipt parsing"


echo.
echo ========================================================
echo ALL DONE! Your repo is scrubbed and committed locally. 
echo. 
echo To push this to GitHub, type these two commands next:
echo   git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO_NAME.git
echo   git push -u origin master
echo ========================================================
pause
