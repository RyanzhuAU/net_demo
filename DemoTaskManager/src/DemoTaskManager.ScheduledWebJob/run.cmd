@echo off
REM Azure App Service (Windows) triggered WebJob entry point.
REM The published output is framework-dependent, so the shared runtime host is used.
dotnet DemoTaskManager.ScheduledWebJob.dll
exit /b %ERRORLEVEL%
