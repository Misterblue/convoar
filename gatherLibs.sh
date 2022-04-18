#! /bin/bash

OPENSIMBIN=${OPENSIMBIN:-../opensim/bin}
# It is best to get the libomv dll's that OpenSim was built with
LIBOMVBIN=${LIBOMVBIN:-../opensim/bin}
# LIBOMVBIN=${LIBOMVBIN:-../libopenmetaverse/bin}

DSTDIR=./bin

echo "Copying from $OPENSIMBIN to $DSTDIR"

# Copy the dll file and the PDB file if it exists
function GetLib() {
    if [[ -z "$3" ]] ; then
        cp "$1/$2" "$DSTDIR"
    else
        cp "$1/$2" "$3"
    fi
    pdbfile=$1/${2%.dll}.pdb
    if [[ -e "$pdbfile" ]] ; then
        cp "$pdbfile" "$DSTDIR"
    fi
}

GetLib "$OPENSIMBIN" "OpenSim.Data.Null.dll"
GetLib "$OPENSIMBIN" "OpenSim.Capabilities.dll"
GetLib "$OPENSIMBIN" "OpenSim.Framework.dll"
GetLib "$OPENSIMBIN" "OpenSim.Framework.Monitoring.dll"
GetLib "$OPENSIMBIN" "OpenSim.Framework.Serialization.dll"
GetLib "$OPENSIMBIN" "OpenSim.Framework.Servers.HttpServer.dll"
GetLib "$OPENSIMBIN" "OpenSim.Services.Interfaces.dll"
GetLib "$OPENSIMBIN" "OpenSim.Services.Connectors.dll"
GetLib "$OPENSIMBIN" "OpenSim.Region.CoreModules.dll"
GetLib "$OPENSIMBIN" "OpenSim.Region.Framework.dll"
GetLib "$OPENSIMBIN" "OpenSim.Region.PhysicsModule.BasicPhysics.dll"
GetLib "$OPENSIMBIN" "OpenSim.Region.PhysicsModules.SharedBase.dll"
GetLib "$OPENSIMBIN" "OpenSim.Tests.Common.dll"

GetLib "$OPENSIMBIN" "log4net.dll"
GetLib "$OPENSIMBIN" "Nini.dll"
GetLib "$OPENSIMBIN" "nunit.framework.dll"
GetLib "$OPENSIMBIN" "Mono.Addins.dll"
GetLib "$OPENSIMBIN" "SmartThreadPool.dll"
GetLib "$OPENSIMBIN" "zlib.net.dll"
# Following are required for OpenSimulator terrain/baking code.
GetLib "$OPENSIMBIN" "openjpeg-dotnet.dll" "convoar"
GetLib "$OPENSIMBIN" "openjpeg-dotnet-x86_64.dll" "convoar"
GetLib "$OPENSIMBIN" "libopenjpeg-dotnet-2-1.5.0-dotnet-1-x86_64.so" "convoar"

GetLib "$LIBOMVBIN" "OpenMetaverse.dll"
GetLib "$LIBOMVBIN" "OpenMetaverse.dll.config"
# GetLib "$LIBOMVBIN" "OpenMetaverse.XML"
GetLib "$LIBOMVBIN" "OpenMetaverseTypes.dll"
# GetLib "$LIBOMVBIN" "OpenMetaverseTypes.XML"
GetLib "$LIBOMVBIN" "OpenMetaverse.StructuredData.dll"
# GetLib "$LIBOMVBIN" "OpenMetaverse.StructuredData.XML"
GetLib "$LIBOMVBIN" "OpenMetaverse.Rendering.Meshmerizer.dll"
GetLib "$LIBOMVBIN" "PrimMesher.dll"
