#! /bin/bash

BUILDVERSION=${1:-./BuildVersion/BuildVersion.exe}

$BUILDVERSION --namespace org.herbal3d.convoar \
    --verbose \
    --version $(cat VERSION) \
    --versionFile convoar/VersionInfo.cs \
    --assemblyInfoFile convoar/Properties/AssemblyInfo.cs
