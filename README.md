# convoar

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
The current version reads an OAR file and outputs an unoptimized GLTF
scene file.
The output GLTF is not packed or binary
so the output GLTF is a JSON `.gltf` file, one or more `.buf` files
(containing the vertex information), and an `images` directory with
the texture files for the mesh materials. By default, the output textures
are either JPG or PNG format depending if there is any transparency
in the texture.

The unoptimized GLTF conversion is a simple conversion of the OAR primitives
which creates many, many meshes and is very inefficient for rendering but
is good for editing (importing into [Blender], for instance).

Future versions of convoar with either contain or have tools to
create a material optimized form which can be displayed more efficiently
in WebGL.

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

A short list of the available parameters:

Parameter  | Meaning
---------- | ----------
 `--help` | list all available parameters with descriptions and default values
 `--outputdir` | directory for generated GLTF and image files
 `-d`      | equivilent to `--outputdir`

An invocation of `convoar ../REGION.oar` will create, in the current directory,
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
git clone https://github.com/Misterblue/libopenmetaverse.git
git clone https://github.com/Real-Serious-Games/C-Sharp-Promise.git
cd convoar
./gatherLibs.sh
```

Then the source can be built using either Visual Studio or `msbuild`.

# Releases and Roadmap

- [ ] Release 1.0
    * basic OAR to GLTF conversion
- [ ] Release 1.1
    * material-centric optimization
    * option to include all prim information in `extras` (scripts, notes, etc.)
    * pipeline tools in Docker image for binary/DRACO packing of GLTF file
    * invocation options to select sub-regions of OAR region
- [ ] Release 1.2
    * pipeline tools for scene optimizations (small mesh elimination, mesh decimation/simplification, etc.)

[OpenSimulator]: http://opensimulator.org
[Mono]: http://www.mono-project.com/
[parameter wiki page]:
[Blender]: https://www.blender.org/

