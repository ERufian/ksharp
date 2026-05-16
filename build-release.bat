@echo off
REM Cross-platform release build wrapper for K3CSharp
REM Usage: build-release.bat [-SkipTests] [-Zip]

powershell -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" %*
