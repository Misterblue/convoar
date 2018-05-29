# Dockerfile for building a runnable version of convoar

FROM mono:latest

ENV TARGET=Release
# ENV TARGET=Debug

# An account is created in the docker file for building and running the app.
# Set the real password in the calling environment. This is just a default.
ENV USER=convoar
ENV CONVOAR_PASSWORD=convoarconvoar

# Optout of the .NET Core Telemetry (https://aka.ms/dotnet-cli-telemetry)
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

LABEL Version="0.9"
LABEL Description="Docker container convoar"

# add the development environment and base tools
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y \
        curl \
        git \
        vim \
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
    && cd ${USER} \
    && msbuild /p:Configuration=${TARGET}

ENTRYPOINT [ "mono", "/home/convoar/convoar/convoar/bin/Release/convoar.exe" ]

