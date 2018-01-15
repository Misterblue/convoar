# Dockerfile for building a runnable version of convoar

# FROM alpine
FROM ubuntu:latest

ENV TARGET=Release
# ENV TARGET=Debug

# An account is created in the docker file for building and running the app.
# Set the real password in the calling environment. This is just a default.
ENV USER=convoar
ENV CONVOAR_PASSWORD=convoarconvoar

# Optout of the .NET Core Telemetry (https://aka.ms/dotnet-cli-telemetry)
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

LABEL Version="0.1"
LABEL Description="Docker container convoar"

# add the development environment and base tools
RUN apt-get update \
    && apt-get upgrade \
    && apt-get install -y \
        apt-transport-https \
        build-essential \
        cmake \
        curl \
        git \
        vim \
    && apt-get clean \
    && rm -rf /tmp/* var/tmp/* \
    && rm -rf /var/lib/apt/lists/*

# Install the latest and greatest mono
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 \
                --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
    && echo "deb http://download.mono-project.com/repo/ubuntu xenial main" \
                | tee /etc/apt/sources.list.d/mono-official.list \
    && apt-get update \
    && apt-get install -y \
        mono-devel \
    && apt-get clean \
    && rm -rf /tmp/* var/tmp/* \
    && rm -rf /var/lib/apt/lists/*
# mono-devel, mono-complete, mono-dbg, referenceassemblies-pcl, ca-certificates-mono

# Install the .NET stuff
RUN curl -s https://packages.microsoft.com/keys/microsoft.asc \
                | gpg --dearmor > /tmp/microsoft.gpg \
    && mv /tmp/microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg \
    && echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-xenial-prod xenial main" \
                | tee /etc/apt/sources.list.d/dotnetdev.list \
    && apt-get update \
    && apt-get install -y \
        dotnet-sdk-2.0.2 \
        nuget \
    && apt-get clean \
    && rm -rf /tmp/* var/tmp/* \
    && rm -rf /var/lib/apt/lists/*

# Add and switch to user 'convoar'
# (From https://stackoverflow.com/questions/27701930/add-user-to-docker-container)
RUN adduser --disabled-password --gecos 'Convoar user' ${USER} \
    && echo "${USER}:${CONVOAR_PASSWORD}" | chpasswd
WORKDIR /home/${USER}
USER ${USER}:${USER}

# Get the 'convoar' sources
RUN cd /home/${USER} \
    && git clone https://github.com/Misterblue/convoar.git \
    && mkdir -p /home/${USER}/bin/$TARGET

# Fetch and build the C++ library for converting scene formats.
# The version of Assimp used is a modified fork (until GLTF2 export is fixed)
RUN cd /home/${USER} \
    && git clone https://github.com/Misterblue/assimp.git \
    && cd /home/${USER}/assimp \
    && git checkout convoar \
    && cmake -G "Unix Makefiles" \
        -DASSIMP_BUILD_TESTS=off \
        -DASSIMP_BUILD_ASSIMP_VIEW=off \
        -DASSIMP_BUILD_ASSIMP_TOOLS=off \
    && make \
    && cp "bin/${TARGET}/assimp*" "bin/${TARGET}/zlib*" /home/${USER}/bin

# Fetch and build the C# interface between managed convoar and unmanaged assimp.
# AssimpNet relies on some packages so use nuget to fetch them.
# Also, copy the executables for 'assimp' here and rename to what AssimpNet
#   is looking for.      
RUN git clone https://bitbucket.org/Starnick/AssimpNet.git \
    && cd AssimpNet \
    && mkdir packages \
    && cd packages \
    && nuget install ../AssimpNet.Interop.Generator/packages.config \
    && cd /home/${USER}/AssimpNet \
    && cp /home/${USER}/bin/assimp* libs/Assimp/ \
    && cd libs/Assimp \
    && mv assimp-vc140-mt.dll Assimp64.dll \
    && mv assimp-vc140-mt.pdb Assimp64.pdb \
    && mv assimp-vc140-mt.ilk Assimp64.ilk \
    && cd /home/AssimpNet \
    && msbuild /p:Configuration=Net45-${TARGET} AssimpNet.sln

# COPY run.opensim.sh /home/opensim

# exit

# The simulator defaults to port 9000
# All ports are usually over-ridden by the 'docker run' command parameters.
# EXPOSE 9000/tcp
# EXPOSE 9000/udp

# The configuration of the region can be supplied via the 'config' subdirectory
# VOLUME /home/opensim/opensim/bin/config
