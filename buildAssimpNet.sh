#! /bin/bash

TARGET=Release

cd ..
HERE=$(pwd)

# Assimp has been built as $TARGET
cd "${HERE}"
# cd assimp
# cmake -G "Visual Studio 14 2015 Win64" -DASSIMP_BUILD_TESTS=off
# Assimp must be built with debug info because AssimpNet uses that info
# /cygdrive/c/Windows/Microsoft.NET/Framework64/v4.0.30319/MSBuild.exe /p:Configuration=RelWithDebInfo AssimpNet.sln

cd "${HERE}"
cp assimp/bin/RelWithDebInfo/assimp*.dll assimp-net/libs/Assimp
cd assimp-net/libs/Assimp
mv assimp-vc140-mt.dll Assimp64.dll

# Now build AssimpNet
cd "${HERE}"
# As of 20180211, MSBuild has problems with references to 'netstandard2.0'
#   Ref: https://github.com/dotnet/standard/issues/504 and https://github.com/Microsoft/msbuild/pull/2567
cd "assimp-net"
# '/cygdrive/f/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/MSBuild.exe' /Restore
# '/cygdrive/f/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/MSBuild.exe' /p:Configuration=$TARGET AssimpNet.sln

cd "$HERE"
cp assimp-net/AssimpNet/bin/$TARGET/netstandard2.0/AssimpNet.dll convoar/libs
# cp assimp-net/AssimpNet/bin/$TARGET/netstandard2.0/AssimpNet.dll convoar/bin/$TARGET/
cp assimp-net/libs/Assimp/Assimp64.dll convoar/libs
