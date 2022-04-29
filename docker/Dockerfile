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

ARG PLACE=/tmp

ARG OPENSIM_BRANCH=master
ARG CONVOAR_BRANCH=v2
ARG COMMON_ENTITIES_BRANCH=master
ARG COMMON_UTIL_BRANCH=master

# Gather all the sources
RUN cd $PLACE \
    && git clone --depth 1 -b $OPENSIM_BRANCH --single-branch git://opensimulator.org/git/opensim \
    && git clone --depth 1 -b $CONVOAR_BRANCH --single-branch https://github.com/Misterblue/convoar.git \
    && cd convoar \
    && OPENSIMBIN=$PLACE/opensim/bin LIBOMVBIN=$PLACE/opensim/bin ./gatherLibs.sh \
    && mkdir addon-modules \
    && cd addon-modules \
    && git clone --depth 1 -b $COMMON_ENTITIES_BRANCH --single-branch https://github.com/Herbal3d/HerbalCommonEntitiesCS.git \
    && git clone --depth 1 -b $COMMON_UTIL_BRANCH --single-branch https://github.com/Herbal3d/HerbalCommonUtilCS.git

RUN cd $PLACE/convoar \
    && nuget restore convoar.sln \
    && msbuild /p:Configuration=${TARGET}

# Convoar is now built into $PLACE/convoar/dist

# ===================================================================
FROM mono:latest

ARG TARGET=Release
# ENV TARGET=Debug

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
COPY --from=builder --chown=${USER}:${USER} $PLACE/convoar/dist /home/${USER}/convoar

COPY ./run-convoar.sh /home/convoar

ENTRYPOINT [ "./run-convoar.sh" ]

