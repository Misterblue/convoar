#! /bin/bash

OPENSIM=../opensim-ssh/bin
LIBOMV=../libopenmetaverse/bin
RSGPROMISE=../C-Sharp-Promise/bin/Release

# Copy the dll file and the PDB file if it exists
function GetLib() {
    cp "$1/$2" libs
    pdbfile=$1/${2%.dll}.pdb
    if [[ -e "$pdbfile" ]] ; then
        cp "$pdbfile" libs
    fi
}

GetLib "$OPENSIM" "OpenSim.Data.Null.dll"
GetLib "$OPENSIM" "OpenSim.Capabilities.dll"
GetLib "$OPENSIM" "OpenSim.Framework.dll"
GetLib "$OPENSIM" "OpenSim.Framework.Monitoring.dll"
GetLib "$OPENSIM" "OpenSim.Framework.Serialization.dll"
GetLib "$OPENSIM" "OpenSim.Framework.Servers.HttpServer.dll"
GetLib "$OPENSIM" "OpenSim.Services.Interfaces.dll"
GetLib "$OPENSIM" "OpenSim.Services.Connectors.dll"
GetLib "$OPENSIM" "OpenSim.Region.CoreModules.dll"
GetLib "$OPENSIM" "OpenSim.Region.Framework.dll"
GetLib "$OPENSIM" "OpenSim.Region.PhysicsModule.BasicPhysics.dll"
GetLib "$OPENSIM" "OpenSim.Region.PhysicsModules.SharedBase.dll"
GetLib "$OPENSIM" "OpenSim.Tests.Common.dll"

GetLib "$OPENSIM" "log4net.dll"
GetLib "$OPENSIM" "Nini.dll"
GetLib "$OPENSIM" "nunit.framework.dll"
GetLib "$OPENSIM" "Mono.Addins.dll"
GetLib "$OPENSIM" "openjpeg-dotnet.dll"
GetLib "$OPENSIM" "openjpeg-dotnet-x86_64.dll"

GetLib "$LIBOMV" "OpenMetaverse.dll"
GetLib "$LIBOMV" "OpenMetaverse.dll.config"
GetLib "$LIBOMV" "OpenMetaverse.XML"
GetLib "$LIBOMV" "OpenMetaverseTypes.dll"
GetLib "$LIBOMV" "OpenMetaverseTypes.XML"
GetLib "$LIBOMV" "OpenMetaverse.StructuredData.dll"
GetLib "$LIBOMV" "OpenMetaverse.StructuredData.XML"
GetLib "$LIBOMV" "OpenMetaverse.Rendering.Meshmerizer.dll"
GetLib "$LIBOMV" "PrimMesher.dll"

GetLib "$RSGPROMISE" "RSG.Promise.dll"

