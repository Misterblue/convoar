# Dockerfile for building a runnable version of convoar

FROM mono:latest as builder

# add the development environment and base tools
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y \
        curl \
        git \
        vim \
        libc6-dev libgdiplus

ARG TARGET=Release

ARG PLACE=/tmp

ARG OPENSIM_BRANCH=master
ARG CONVOAR_BRANCH=master
ARG COMMON_ENTITIES_BRANCH=master
ARG COMMON_UTIL_BRANCH=main

ADD https://api.github.com/repos/opensim/opensim/git/refs/heads/$OPENSIM_BRANCH opensim-git-version.json
RUN cd $PLACE \
    && git clone --depth 1 -b $OPENSIM_BRANCH --single-branch https://github.com/opensim/opensim.git \
    && cd opensim \
    && ./runprebuild48.sh \
    && msbuild /p:Configuration=${TARGET}

# Gather all the sources
# The following ADD's break the caching of the sources changed
ADD https://api.github.com/repos/Misterblue/convoar/git/refs/heads/$CONVOAR_BRANCH convoar-git-version.json
ADD https://api.github.com/repos/Herbal3d/HerbalCommonEntitiesCS/git/refs/heads/$COMMON_ENTITES_BRANCH commonentites-git-version.json
ADD https://api.github.com/repos/Herbal3d/HerbalCommonUtilCS/git/refs/heads/$COMMON_UTIL_BRANCH commonutil-git-version.json
RUN cd $PLACE \
    && git clone --depth 1 -b $CONVOAR_BRANCH --single-branch https://github.com/Misterblue/convoar.git \
    && cd convoar \
    && mkdir -p bin \
    && OPENSIMBIN=$PLACE/opensim/bin LIBOMVBIN=$PLACE/opensim/bin ./gatherLibs.sh \
    && ./updateVersion.sh "mono ./BuildVersion/BuildVersion.exe" \
    && mkdir -p addon-modules \
    && cd addon-modules \
    && git clone --depth 1 -b $COMMON_ENTITIES_BRANCH --single-branch https://github.com/Herbal3d/HerbalCommonEntitiesCS.git \
    && git clone --depth 1 -b $COMMON_UTIL_BRANCH --single-branch https://github.com/Herbal3d/HerbalCommonUtilCS.git

RUN cd $PLACE/convoar \
    && nuget restore convoar.sln \
    && msbuild /p:Configuration=${TARGET}

# Convoar is now built into $PLACE/convoar/dist

# ===================================================================
FROM mono:latest

ARG VERSION

LABEL Version=${VERSION}
LABEL Description="Docker container convoar"

# Optout of the .NET Core Telemetry (https://aka.ms/dotnet-cli-telemetry)
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

# Must be same as in the 'builder' step above
ARG PLACE=/tmp

# An account is created in the docker file for building and running the app.
# Set the real password in the calling environment. This is just a default.
ENV USER=convoar
ENV CONVOAR_PASSWORD=convoarconvoar

# Add and switch to user 'convoar'
RUN adduser --disabled-password --gecos 'Convoar user' ${USER} \
    && echo "${USER}:${CONVOAR_PASSWORD}" | chpasswd
WORKDIR /home/${USER}
USER ${USER}:${USER}

RUN mkdir -p /home/${USER}/convoar/dist
COPY --from=builder --chown=${USER}:${USER} $PLACE/convoar/dist /home/${USER}/convoar/dist/

COPY ./run-convoar.sh /home/convoar

ENTRYPOINT [ "./run-convoar.sh" ]

