@echo off
rem DO NOT RUN COMPARISONS FOR TESTING PURPOSES. IT IS VERY INEFFICIENT
rem USE THE RUN TEST SKILL FOR RUNNING TESTS
echo Running K3Sharp vs k.exe Comparison...
cd /d "t:\_src\github.com\ERufian\ksharp\K3CSharp.Comparison"
dotnet run
