# convoar

* [Invocation](https://github.com/Misterblue/convoar#invocation)
* [Building](https://github.com/Misterblue/convoar#building)
  * [Simple Build](https://github.com/Misterblue/convoar#simple-build)
  * [Library Updating Build](https://github.com/Misterblue/convoar#update-build)
* [Docker Image](https://github.com/Misterblue/convoar#docker-image)
* [What Is Converted](https://github.com/Misterblue/convoar#what-is-converted)
* [Releases and Roadmap](https://github.com/Misterblue/convoar#releases-and-roadmap)

Command line application for converting OpenSimulator OAR files into GLTF scene file.

An [OpenSimulator] OAR file saves a region's contents.
All the information about the region (parcels, terrain, etc.)
is saved in an OAR file along with
all the objects and their locations (prims, meshes, scripts, textures, etc.).
Thus, an OAR file of a region is convertable into any other scene
representation format.

Convoar reads an OAR file and outputs a GLTF scene and image files
which is most of the region information.
Most specifically, it outputs the textured mesh representation of
all the objects in the region.

Convoar is evolving.
See the "Releases and Roadmap" section below.
The current version reads an OAR file and outputs either an unoptimized GLTF
scene file or a "material reorganized" GLTF scene (see below).
The output GLTF is not packed or binary
so the output files are a JSON `.gltf` file, one or more `.buf` files
(containing the vertex information), and an `images` directory with
the texture files for the mesh materials. By default, the output textures
are either JPG or PNG format depending if there is any transparency
in the texture.

The unoptimized GLTF conversion is a simple conversion of the OAR primitives
which creates many, many meshes and is very inefficient for rendering but
is good for editing (importing into [Blender], for instance).

The "material reorganized" scene has objects corresponding to each
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
the files `REGION.gltf`, one or more `REGION-bufferNNN.buf` files, and an
`images` directory containing .JPG and .PNG files. The GLTF file will reference
the `images  directory and the `.buf` files so the relative directory
position of the `.buf   and `images` files is significant.

The output directory is changed with the `--outputdir` parameter.

# Building

Convoar uses [OpenSimulator] sources to do the reading and conversion of the
OAR file. These source files are included in this repository. So there is a *simple*
build where one just builds the sources checked out, and there is the *updating* build
where one fetches new versions of the [OpenSimulator] sources.

Some functionality has been moved out into another project that must be
checked out in the same directory as [Convoar]. Checkout [HerbalCommonEntitiesCS]
into the same directory as the [Convoar] project. This will include
```CommonEntities``` and ```CommonEntitiesUtil```.

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
git clone https://github.com/Herbal3d/HerbalCommonEntitiesCS.git
git clone git://opensimulator.org/git/opensim
(build OpenSimulator)
git clone https://github.com/Misterblue/libopenmetaverse.git
(build libOpenMetaverse)
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
docker run --rm --user $(id -u):$(id -g) -v /tmp/frog:/oar herbal3d/convoar REGION.oar
```

This maps the local `/tmp/frog` directory to the `/oar` directory in
the Docker container, runs the container, and writes the output GLTF files
into the `/tmp/frog` directory.

NOTE: the most common problem is permissions. The command above, sets the
user and group IDs to that of the current user (the `--user` parameter)
which is usually is the right thing for write permissions into the mapped directory.

# What Is Converted

Convoar reads in the totality of the OAR file and then maps the various
features of the scene into the GLTF definition. This mapping is incomplete.
What follows is a list of features that are copied:

* Scene
* Primitives: converted to a mesh using PrimMesher using highest LOD setting
* Sculptie: converted into a mesh using PrimMesher using highest LOD setting
* Mesh: repackaged as a mesh. Mesh vertices and indices are deduplicated and repacked for GPU efficiency
* Linksets: All meshes in a linkset are grouped into a GLTF group of meshes
* Images: All images are converted into JPEG or PNG. PNG is used if there is any transparency in the image. The images are all resized to be less than `TextureMaxSize` on a side (default is 256);
* Materials: mesh face info is converted into a material and the following attributes are copied:
  * face image
  * face color (RGBA)
  * transparency
  * bump
  * glow
  * shiny
  * two sided (parameter `DoubleSided`. Default is `false`)
- Terrain: A mesh is created from the region heightmap. The mesh resolution is in meters (so a standard sized region would be 256x256 vertices) but a half size mesh (parameter `HalfRezTerrain` default is `true`) can be generated

# Releases and Roadmap

- [x] Release 1.0
    * basic OAR to GLTF conversion
    * material-centric optimization
- [x] Release 1.1
    * cleaned up and debugged command line and Docker version
- [x] Release 1.2
    * bug release -- trying to figure out why some JPEG2000 don't decomopress.
- [ ] Release 1.3 and after
    * bug releases and optimization experimentation before going to version 2
- [ ] Release 2.0
    * option to include all prim information in `extras` (scripts, notes, etc.)
    * pipeline tools in Docker image for binary/DRACO packing of GLTF file
    * invocation options to select sub-regions of OAR region
- [ ] Release 2.1
    * pipeline tools for scene optimizations (small mesh elimination, mesh decimation/simplification, etc.)

[Convoar]: https://github.com/Misterblue/convoar
[OpenSimulator]: http://opensimulator.org
[Mono]: http://www.mono-project.com/
[Blender]: https://www.blender.org/
[HerbalCommonEntitiesCS]: https://github.com/Herbal3d/HerbalCommonEntitiesCS

