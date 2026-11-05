@echo off
REM Azure App Service (Windows) continuous WebJob entry point.
dotnet DemoTaskManager.ServiceBusWebJob.dll
exit /b %ERRORLEVEL%
