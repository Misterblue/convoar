# convoar

Command line application for converting OpenSimulator OAR files into various other formats.

OAR files are a way to save [OpenSimulator] regions. I think "OAR" is short for
"OpenSimulator archive". An OAR file includes all the information necessary
to fill a region. This includes all the material images, the meshes, the scripts,
the prims as well as the placement information (location and rotation). Thus,
an OAR file of a region should be able to be converted into any other scene
representation format.

This application is evolving. The current design reads the OAR file and outputs
the converted content into directories that can be copied, uploaded, or used
directly by a web server. This design will change as uses are developed.

The name is "convoar" from "convert oar" and is pronounced like "condor".
It isn't the bird but there is a logo idea in there somewhere.

# Building

This relies on the [OpenSimulator] sources to do the reading and conversion of the
OAR files. Building thus relies on also fetching and building [OpenSimulator].

Since the code is writting in C#, the executable and DLLs will run on Windows
and will run with [Mono] on Linux systems.

[OpenSimulator]: http://opensimulator.org
[Mono]: http://www.mono-project.com/
