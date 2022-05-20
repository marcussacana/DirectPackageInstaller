#!/bin/sh
goto BATCH
clear

echo "DirectPackageInstaller Build Script - Unix";

if !(dotnet --list-sdks | grep -q '6.'); then
  echo ".NET 6 SDK NOT FOUND"
  exit;
fi

echo ".NET 6 SDK FOUND"

dotnet clean
rm -r Release
mkdir Release

Publish () {
   echo "Building for $1"
   dotnet publish -c Release -r $1
   zip -j -r Release/$1.zip DirectPackageInstaller/DirectPackageInstaller.Desktop/bin/Release/net6.0/$1/publish/*
}

Publish win-x64
Publish win-x86
Publish win-arm
Publish win-arm64


Publish linux-x64
Publish linux-arm
Publish linux-arm64


Publish osx-x64
Publish osx-arm64

echo "Build Finished."
exit;

================================    UNIX SCRIPT END   ====================================
================================ WINDOWS SCRIPT BEGIN ====================================

:BATCH
echo off

dotnet --list-sdks | find /i "6."
if errorlevel 1 (
   cls
   echo DirectPackageInstaller Build Script - Windows
   echo .NET 6 SDK NOT FOUND.
   goto :eof
)

cls
echo DirectPackageInstaller Build Script - Windows
echo .NET 6 SDK FOUND

dotnet clean
rmdir /s /q .\Release
mkdir .\Release

call :Build win-x64
call :Build win-x86
call :Build win-arm
call :Build win-arm64

call :Build linux-x64
call :Build linux-arm
call :Build linux-arm64

call :Build osx-x64
call :Build osx-arm64

echo Build Finished.
goto :eof

exit
:Build
echo Building for %1
dotnet publish -c Release -r %1
powershell Compress-Archive .\DirectPackageInstaller\DirectPackageInstaller.Desktop\bin\Release\net6.0\%1\publish\* .\Release\%1.zip
goto :eof
