#!/bin/bash
goto BATCH
clear

echo "DirectPackageInstaller Build Script - Unix";

if !(dotnet --list-sdks | grep -q '8.'); then
  echo ".NET 8 SDK NOT FOUND"
  exit;
fi

echo ".NET 8 SDK FOUND"

# Desktop-only builds should not evaluate the Android project (and therefore
# must not require Android workloads).
DESKTOP_PROJECT="DirectPackageInstaller/DirectPackageInstaller.Desktop/DirectPackageInstaller.Desktop.csproj"

# The correct SDK directory, contains a build-tools directory
export AndroidSdkDirectory=~/Android/Sdk/

# The correct NDK directory, contains a ndk-build executable
export AndroidNdkDirectory=~/Android/Sdk/ndk/24.0.8215888/

if [ -d "/usr/local/lib/android/sdk" ]; then
   echo "Github Action Android SDK Path Found"
   export AndroidSdkDirectory=/usr/local/lib/android/sdk
   export AndroidNdkDirectory=/usr/local/lib/android/sdk/ndk/24.0.8215888/ 
fi

dotnet clean "$DESKTOP_PROJECT"
rm -r Release
mkdir Release

# BUILD_TARGETS controls which platforms get built. Default = all.
# Accepts comma-separated list: win, linux, osx, android
BUILD_TARGETS="${BUILD_TARGETS:-win,linux,osx,android}"
echo "Build targets: $BUILD_TARGETS"

has_target() {
   case ",$BUILD_TARGETS," in *",$1,"*) return 0;; *) return 1;; esac
}

Publish () {
   echo "Building for $1"
   dotnet restore "$DESKTOP_PROJECT" -r $1
   dotnet publish "$DESKTOP_PROJECT" -c Release -r $1 $2
   zip -j -9 -r Release/$1.zip DirectPackageInstaller/DirectPackageInstaller.Desktop/bin/Release/net8.0/$1/publish/* -x Icon.icns
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

# macOS build:
#  - --self-contained bundles the .NET 8 runtime, so users don't need to install it.
#  - After the .app is assembled, ad-hoc codesign it so arm64 binaries launch
#    on Apple Silicon (unsigned arm64 Mach-O is refused by the loader).
#  - If hdiutil is available, also produce a drag-to-Applications DMG.
OSXPublish (){
   Publish $1 "--self-contained -p:PublishSingleFile=false"

   cd Release

   mkdir -p tmp
   cp -R ../Files/OSXAppBase/DirectPackageInstaller.app tmp/
   unzip $1.zip -d tmp/DirectPackageInstaller.app/Contents/MacOS

   # The template keeps this directory in git with a placeholder file. It is
   # not part of a runnable app and causes strict bundle verification to fail.
   rm -f tmp/DirectPackageInstaller.app/Contents/MacOS/.gitkeep

   if command -v codesign >/dev/null 2>&1; then
      echo "Ad-hoc signing $1 .app bundle"
      # The managed .NET payload is code from macOS's perspective. Sign each
      # file first, then seal the enclosing app bundle.
      find tmp/DirectPackageInstaller.app/Contents/MacOS -type f -exec codesign --force --sign - {} \;
      codesign --force --sign - tmp/DirectPackageInstaller.app
   else
      echo "WARNING: codesign not available, skipping ad-hoc signature"
      echo "         arm64 builds produced on non-macOS hosts will not launch on Apple Silicon"
   fi

   cd tmp
   # Preserve code-signing extended attributes; BSD zip drops them and yields
   # an app that fails strict verification after extraction.
   ditto -c -k --sequesterRsrc --keepParent DirectPackageInstaller.app ../$1-app.zip
   cd ..

   if command -v hdiutil >/dev/null 2>&1; then
      echo "Packaging $1 DMG"
      mkdir -p dmg
      cp -R tmp/DirectPackageInstaller.app dmg/
      ln -s /Applications dmg/Applications
      hdiutil create -volname "DirectPackageInstaller" -srcfolder dmg -ov -format UDZO $1-app.dmg
      rm -rf dmg
   fi

   rm -r tmp

   cd ..
}

AndroidPublish (){
   if ! [ -d "${AndroidSdkDirectory}build-tools" ]; then
   	echo "POSSIBLE INVALID ANDROID SDK PATH";
   fi
   if ! [ -f "${AndroidNdkDirectory}ndk-build" ]; then
   	echo "POSSIBLE INVALID ANDROID NDK PATH";
   fi
   
   dotnet workload restore
   
   Publish $1
   
   rm Release/$1.zip
   rm DirectPackageInstaller/DirectPackageInstaller.Android/bin/Release/net8.0-android/$1/publish/com.marcussacana.DirectPackageInstaller.apk
   zip -j -9 -r Release/$1.zip DirectPackageInstaller/DirectPackageInstaller.Android/bin/Release/net8.0-android/$1/publish/*.apk
}

if has_target win; then
   WINPublish win-x64
   WINPublish win-x86
   WINPublish win-arm
   WINPublish win-arm64
fi

if has_target linux; then
   Publish linux-x64
   Publish linux-arm
   Publish linux-arm64
fi

if has_target osx; then
   OSXPublish osx-x64
   OSXPublish osx-arm64
fi

if has_target android; then
   AndroidPublish android-x64
   AndroidPublish android-x86
   AndroidPublish android-arm
   AndroidPublish android-arm64
fi

cd Release

if has_target win; then
   mv win-x64.zip Windows-X64.zip
   mv win-x86.zip Windows-X86.zip
   mv win-arm.zip Windows-ARM.zip
   mv win-arm64.zip Windows-ARM64.zip
fi

if has_target linux; then
   mv linux-x64.zip Linux-X64.zip
   mv linux-arm.zip Linux-ARM.zip
   mv linux-arm64.zip Linux-ARM64.zip
fi

if has_target osx; then
   mv osx-x64.zip OSX-X64.zip
   mv osx-arm64.zip OSX-ARM64.zip

   mv osx-x64-app.zip OSX-X64-APP.zip
   mv osx-arm64-app.zip OSX-ARM64-APP.zip

   [ -f osx-x64-app.dmg ] && mv osx-x64-app.dmg OSX-X64-APP.dmg
   [ -f osx-arm64-app.dmg ] && mv osx-arm64-app.dmg OSX-ARM64-APP.dmg
fi

if has_target android; then
   mv android-x64.zip Android-X64.zip
   mv android-x86.zip Android-X86.zip
   mv android-arm.zip Android-ARM.zip
   mv android-arm64.zip Android-ARM64.zip
fi

cd ..

echo "Build Finished."
exit;

================================    UNIX SCRIPT END   ====================================
================================ WINDOWS SCRIPT BEGIN ====================================

:BATCH
echo off

dotnet --list-sdks | find /i "8."
if errorlevel 1 (
   cls
   echo DirectPackageInstaller Build Script - Windows
   echo .NET 8 SDK NOT FOUND.
   goto :eof
)


REM The correct SDK directory, contains a build-tools directory
set AndroidSdkDirectory=C:\Program Files (x86)\Android\android-sdk\

REM The correct NDK directory, contains a ndk-build.cmd
set AndroidNdkDirectory=C:\Program Files (x86)\Android\android-sdk\ndk\24.0.8215888\

cls
echo DirectPackageInstaller Build Script - Windows
echo .NET 8 SDK FOUND

dotnet clean
rmdir /s /q .\Release
mkdir .\Release

dotnet workload restore

call :Build win-x64
call :Build win-x86
call :Build win-arm
call :Build win-arm64

call :Build linux-x64
call :Build linux-arm
call :Build linux-arm64

call :OSXBuild osx-x64
call :OSXBuild osx-arm64

call :AndroidBuild android-x64
call :AndroidBuild android-x86
call :AndroidBuild android-arm
call :AndroidBuild android-arm64

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

move android-x64.zip Android-X64.zip
move android-x86.zip Android-X86.zip
move android-arm.zip Android-ARM.zip
move android-arm64.zip Android-ARM64.zip

cd ..

echo Build Finished.
goto :eof

exit
:Build
echo Building for %1
dotnet restore -r %1
dotnet publish -c Release -r %1
powershell Compress-Archive .\DirectPackageInstaller\DirectPackageInstaller.Desktop\bin\Release\net8.0\%1\publish\* .\Release\%1.zip
goto :eof



exit
:OSXBuild
call :Build %1
mkdir .\Release\tmp
xcopy /E /I /Y ".\Files\OSXAppBase\DirectPackageInstaller.app" ".\Release\tmp\DirectPackageInstaller.app"
powershell Expand-Archive -LiteralPath ".\Release\%1.zip" -DestinationPath ".\Release\tmp\DirectPackageInstaller.app\Contents\MacOS" -Force
powershell Compress-Archive .\Release\tmp\* .\Release\%1-app.zip
rmdir /s /q .\Release\tmp
goto :eof
:AndroidBuild
IF NOT EXIST "%AndroidSdkDirectory%build-tools" (
	echo ANDROID SDK NOT FOUND
	goto :eof
)
IF NOT EXIST "%AndroidNdkDirectory%ndk-build.cmd" (
	echo ANDROID NDK NOT FOUND
	goto :eof
)
call :Build %1
del /s /q .\Release\%1.zip
del /s /q .\DirectPackageInstaller\DirectPackageInstaller.Android\bin\Release\net8.0-android\%1\publish\com.marcussacana.DirectPackageInstaller.apk
powershell Compress-Archive .\DirectPackageInstaller\DirectPackageInstaller.Android\bin\Release\net8.0-android\%1\publish\*.apk .\Release\%1.zip
goto :eof
