@echo off
cd /d C:\developer\app02
dotnet run --project src\NorthwindApi.Api --no-build --urls http://localhost:5297 > run.log 2> run.err.log
