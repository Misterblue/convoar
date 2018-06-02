# convoar

* [Invocation](https://github.com/Misterblue/convoar#invocation)
* [Building](https://github.com/Misterblue/convoar#building)
  * [Simple Build](https://github.com/Misterblue/convoar#simple-build)
  * [Library Updating Build](https://github.com/Misterblue/convoar#update-build)
* [Docker Image](https://github.com/Misterblue/convoar#docker-image)
* [Releases and Roadmap](https://github.com/Misterblue/convoar#releases-and-roadmap)

Command line application for converting OpenSimulator OAR files into GLTF scene file.

OAR files save [OpenSimulator] regions.
All the information about the region (parcels, terrain, etc.)
is saved in an OAR file along with
all the objects and their locations (prims, meshes, scripts, textures, etc.).
Thus, an OAR file of a region is convertable into any other scene
representation format.

Convoar reads an OAR file and outputs a GLTF scene and image files
containing most of the region information.
Most specifically, textured mesh representation of all the objects
described in the OAR file.

Convoar is evolving.
See the "Releases and Roadmap" section below.
The current version reads an OAR file and outputs either an unoptimized GLTF
scene file or a "material reorganized" GLTF scene (see below).
The output GLTF is not packed or binary
so the output GLTF is a JSON `.gltf` file, one or more `.buf` files
(containing the vertex information), and an `images` directory with
the texture files for the mesh materials. By default, the output textures
are either JPG or PNG format depending if there is any transparency
in the texture.

The unoptimized GLTF conversion is a simple conversion of the OAR primitives
which creates many, many meshes and is very inefficient for rendering but
is good for editing (importing into [Blender], for instance).

The "material reorganized" scene has object corresponding to each
unique material (texture/color/features) and the meshes have been
assigned to each of these material objects.
This renders the scene uneditable but this should greatly reduce
the number of draw calls needed to render the scene in OpenGL/WebGL.

Future versions of convoar with either contain or have tools to
perform other optimizations to the scene and the object therein.

The name "convoar" is from "convert oar" and is pronounced like "condor".
There is a logo idea in there somewhere.

# Invocation

Checkout the convoar sources and run the prebuilt binary in the `dist` directory.
The binaries are compiled for .NET Framework 4.6 so you must install that
library version better on Windows10 or, if running on Linux, Mono v4.2.1
or better.

```bash
git clone https://github.com/Misterblue/convoar
if Windows:
convoar/dist/convoar.exe region.oar
if Linux:
mono convoar/dist/convoar.exe region.oar
```

The full invocation form is:

    convoar <parameters> inputOARfile

A short list of the available parameters (a boolean parameter can be followed with
a `true` or `false`):

Parameter  | Meaning
---------- | ----------
`--mergeSharedMaterialMeshes` or `-m` | reorganize all meshes in the scene into groups using the same materials. This makes the scene uneditable but will make for way fewer draw calls when displayed.
 `--outputdir` or `-d` | directory for generated GLTF and image files. Default is `./convoar'.
 `--regionName` | Name to use for generated scene. Default is name of input OAR file.
 `--textureMaxSize` | Maximum size for textures. All images are resized to less than this dimension. Default is 256.
 `--halfRezTerrain` | Whether to reduce terrain resolution by two. Default is true.
 `--createTerrainSplat` | whether to create a terrain texture based on height. Default is true.
 `--verbose` or `-v` | Output information on numbers of meshes, etc.
 `--help` | list all available parameters with descriptions and default values

An invocation of `convoar ../REGION.oar` will create, in the output directory,
the files `REGION.gltf`, one or more `REGION_bufferNNN.buf` files, and an
`images` directory containing .JPG and .PNG files. The GLTF file will reference
the `images  directory and the `.buf` files so the relative directory
position of the `.buf   and `images` files is significant.

The output directory is changed with the `--outputdir` parameter.

# Building

Convoar uses [OpenSimulator] sources to do the reading and conversion of the
OAR file. These source files are included in this repository. So there is a *simple*
build where one just builds the sources checked out, and there is the *updating* build
where one fetches new versions of the [OpenSimulator] sources.

## Simple Build

Under windows 10, use Visual Studio 2017 or better. At the moment, convoar is
built under Windows to create the `dist` directory.
The prebuilt binaries will run on Windows10 and with [Mono] on Linux.
There are no external dependencies --
everything is included in the Convoar GitHub repository.

If compiling on Linux, one needs [Mono] version 5 or better and one
just uses `msbuild`.

## Updating Build

To update the OpenSimulator sources, the script `gatherLibs.sh` fetches the
required DLLs from other repositories. The other repositories must be cloned
into the same directory as the convoar repository so the steps could be:

```
git clone https://github.com/Misterblue/convoar.git
git clone git://opensimulator.org/git/opensim
(build OpenSimulator)
git clone https://github.com/Misterblue/libopenmetaverse.git
(build libOpenMetaverse)
git clone https://github.com/Real-Serious-Games/C-Sharp-Promise.git
(build C-Sharp-Promise)
cd convoar
./gatherLibs.sh
```

Then convoar can be built using either Visual Studio or `msbuild`.

# Docker Image

For those with Docker environments, there is a Docker image of convoar.

Assume the OAR to be converted is at `/tmp/frog/REGION.oar`.
Then, to do the conversion:

```bash
docker pull herbal3d/convoar
docker run -v /tmp/frog:/oar herbal3d/convoar REGION.oar
```

This maps the local directory `/tmp/frog` to the `/oar` directory in
the Docker container, runs the container, and writes the converted file
into the `/tmp/frog` directory.

NOTE: the most common problem is permissions. This docker image attempts
to write the output files as user 1000,1000 which could conflict with
the local systems accounts. Make sure the write permissions in the
destination directory account for this.

# Releases and Roadmap

- [x] Release 1.0
    * basic OAR to GLTF conversion
    * material-centric optimization
- [ ] Release 1.1
    * option to include all prim information in `extras` (scripts, notes, etc.)
    * pipeline tools in Docker image for binary/DRACO packing of GLTF file
    * invocation options to select sub-regions of OAR region
- [ ] Release 1.2
    * pipeline tools for scene optimizations (small mesh elimination, mesh decimation/simplification, etc.)

[OpenSimulator]: http://opensimulator.org
[Mono]: http://www.mono-project.com/
[parameter wiki page]: https://github.com/Misterblue/convoar/wiki/Convoar-Command-Line-Parameters
[Blender]: https://www.blender.org/

