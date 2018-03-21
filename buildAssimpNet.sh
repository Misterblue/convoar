#! /bin/bash

TARGET=Release

CMAKE="/cygdrive/c/Program Files (x86)/CMake/bin/cmake.exe"
MSBUILD="/cygdrive/f/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/bin/MSBuild.exe"

cd ..
HERE=$(pwd)

# Assimp has been built as $TARGET
cd "${HERE}"
cd assimp
"${CMAKE}"  -G "Visual Studio 14 2015 Win64" -DASSIMP_BUILD_TESTS=off -DASSIMP_BUILD_ASSIMP_VIEW=off
"${MSBUILD}" /p:Configuration=$TARGET Assimp.sln

cd "${HERE}"
cp assimp/bin/$TARGET/assimp-*.dll assimp-net/libs/Assimp/Assimp64.dll

# Now build assimp-net
cd "${HERE}"
cd "assimp-net"
"${MSBUILD}" /Restore
"${MSBUILD}" /p:Configuration=$TARGET AssimpNet.sln

cd "$HERE"
cp assimp-net/AssimpNet/bin/$TARGET/netstandard2.0/AssimpNet.dll convoar/libs
# cp assimp-net/AssimpNet/bin/$TARGET/netstandard2.0/AssimpNet.dll convoar/bin/$TARGET/
cp assimp-net/libs/Assimp/Assimp64.dll convoar/libs
