# Dockerfile for building a runnable version of convoar

FROM mono:latest
# FROM mono:4.6

ARG VERSION

ENV TARGET=Release
# ENV TARGET=Debug

# An account is created in the docker file for building and running the app.
# Set the real password in the calling environment. This is just a default.
ENV USER=convoar
ENV CONVOAR_PASSWORD=convoarconvoar

# Optout of the .NET Core Telemetry (https://aka.ms/dotnet-cli-telemetry)
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

LABEL Version=${VERSION}
LABEL Description="Docker container convoar"

# add the development environment and base tools
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y \
        curl \
        git \
        vim \
        libc6-dev libgdiplus \
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
    && cd convoar \
    && nuget restore convoar.sln \
    && msbuild /p:Configuration=${TARGET}

# Alternate Linux method that uses the precompiled binaries in  the repository
# RUN mkdir -p /home/${USER}/convoar/dist
# COPY dist /home/${USER}/convoar/dist/

COPY docker/run-convoar.sh /home/convoar

ENTRYPOINT [ "./run-convoar.sh" ]

