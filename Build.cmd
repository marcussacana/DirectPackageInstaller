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
   dotnet restore -r $1
   dotnet publish -c Release -r $1
   zip -j -9 -r Release/$1.zip DirectPackageInstaller/DirectPackageInstaller.Desktop/bin/Release/net6.0/$1/publish/* -x Icon.icns
}


# Fix Console Window on Windows
WINPublish(){
   Publish $1
   
   cd Release
   
   unzip $1.zip -d tmp/
   
   dotnet ../Files/NSubsys.Tasks.dll ./tmp/DirectPackageInstaller.Desktop.exe
   
   rm $1.zip
   zip -j -9 -r $1.zip tmp/*   
   rm -r tmp
   
   cd ..
}

OSXPublish (){
   Publish $1
   
   cd Release
   
   unzip ../Files/OSXAppBase.zip -d ./tmp
   unzip $1.zip -d tmp/DirectPackageInstaller.app/Contents/MacOS
   
   cd tmp
   zip -9 -r ../$1-app.zip ./
   cd ..
   
   rm -r tmp
   
   cd ..
}

WINPublish win-x64
WINPublish win-x86
WINPublish win-arm
WINPublish win-arm64


Publish linux-x64
Publish linux-arm
Publish linux-arm64


OSXPublish osx-x64
OSXPublish osx-arm64

cd Release

mv win-x64.zip Windows-X64.zip
mv win-x86.zip Windows-X86.zip
mv win-arm.zip Windows-ARM.zip
mv win-arm64.zip Windows-ARM64.zip

mv linux-x64.zip Linux-X64.zip
mv linux-arm.zip Linux-ARM.zip
mv linux-arm64.zip Linux-ARM64.zip

mv osx-x64.zip OSX-X64.zip
mv osx-arm64.zip OSX-ARM64.zip

mv osx-x64-app.zip OSX-X64-APP.zip
mv osx-arm64-app.zip OSX-ARM64-APP.zip

cd ..

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

call :OSXBuild osx-x64
call :OSXBuild osx-arm64


cd Release

move win-x64.zip Windows-X64.zip
move win-x86.zip Windows-X86.zip
move win-arm.zip Windows-ARM.zip
move win-arm64.zip Windows-ARM64.zip

move linux-x64.zip Linux-X64.zip
move linux-arm.zip Linux-ARM.zip
move linux-arm64.zip Linux-ARM64.zip

move osx-x64.zip OSX-X64.zip
move osx-arm64.zip OSX-ARM64.zip

move osx-x64-app.zip OSX-X64-app.zip
move osx-arm64-app.zip OSX-ARM64-app.zip

cd ..

echo Build Finished.
goto :eof

exit
:Build
echo Building for %1
dotnet restore -r %1
dotnet publish -c Release -r %1
powershell Compress-Archive .\DirectPackageInstaller\DirectPackageInstaller.Desktop\bin\Release\net6.0\%1\publish\* .\Release\%1.zip
goto :eof



exit
:OSXBuild
call :Build %1
mkdir .\Release\tmp
powershell Expand-Archive -LiteralPath ".\Files\OSXAppBase.zip" -DestinationPath ".\Release\tmp" -Force
powershell Expand-Archive -LiteralPath ".\Release\%1.zip" -DestinationPath ".\Release\tmp\DirectPackageInstaller.app\Contents\MacOS" -Force
powershell Compress-Archive .\Release\tmp\* .\Release\%1-app.zip
rmdir /s /q .\Release\tmp
goto :eof
