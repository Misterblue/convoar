# convoar

Command line application for converting OpenSimulator OAR files into GLTF scene file.

OAR files are a way to save [OpenSimulator] regions.
An OAR file contains all the information about the region (parcels, terrain, etc.) and
all the objects within the region (prims, meshes, scripts, textures, etc.).
Thus, an OAR file of a region should be convertable into any other scene
representation format.

Convoar reads an OAR file and outputs a GLTF scene and image files
containing most of the region information.
Most specifically, textured mesh representation of all the objects
described in the OAR file.

Convoar is evolving.
See the "Releases and Roadmap" section below.
The current version reads an OAR file and outputs either an unoptimized GLTF
scene file or a material optimized GLTF scene file.
The output GLTF is not packed or binary
so the output GLTF is a JSON `.gltf` file, one or more `.buf` files
(containing the vertex information), and an `images` directory with
the texture files for the mesh materials. By default, the output textures
are either JPG or PNG format depending if there is any transparency
in the texture.

The unoptimized GLTF conversion is a simple conversion of the OAR primitives
which creates many, many meshes and is very inefficient for rendering but
is good for editing (imported into [Blender], for instance).
The material optimized form will have a reduced draw count
and thus might allow the region to be displayed with WebGL.

The name "convoar" is from "convert oar" and is pronounced like "condor".
There is a logo idea in there somewhere.

# Invocation

Convor is a command line application with the form:

    convoar <parameters> inputOARfile

An extensive list of parameters is on the [parameter wiki page]
but a short list is:

Parameter  | Meaning
---------- | ----------
 `--help` | list all available parameters with descriptions and default values
 `--outputdir` | directory for generated GLTF and image files
 `-d`      | equivilent to `--outputdir`
 `--mergeSharedMaterialMeshes` | reduce number of meshes to number of common materials

# Building

Convoar uses [OpenSimulator] sources to do the reading and conversion of the
OAR file. These source files are included in this repository. So there is a *simple*
build where one just builds the sources checked out, and there is the *updating* build
where one fetches new versions of the [OpenSimulator] sources.

## Simple Build

Under windows 10, use Visual Studio 2017 or better. For Linux, `msbuild` from
[Mono] 5 and after will compile Convoar. There are no external dependencies --
everything is included in the Convoar GitHub repository.

## Updating Build

To update the OpenSimulator sources, the script `gatherLibs.sh` fetches the
required DLLs from other repositories. The other repositories must be cloned
into the same directory as the convoar repository so the steps could be:

```
git clone https://github.com/Misterblue/convoar.git
git clone git://opensimulator.org/git/opensim
git clone https://github.com/Misterblue/libopenmetaverse.git
git clone https://github.com/Real-Serious-Games/C-Sharp-Promise.git
cd convoar
./gatherLibs.sh
msbuild
```

# Releases and Roadmap

- [ ] Release 1.0
    * basic OAR to GLTF conversion
- [ ] Release 1.1
    * option to include all prim information in `extras` (scripts, notes, etc.)
    * pipeline tools in Docker image for binary/DRACO packing of GLTF file
    * invocation options to select sub-regions of OAR region
- [ ] Release 1.2
    * pipeline tools for scene optimizations (small mesh elimination, mesh decimation/simplification, etc.)

[OpenSimulator]: http://opensimulator.org
[Mono]: http://www.mono-project.com/
[parameter wiki page]:
[Blender]: https://www.blender.org/

