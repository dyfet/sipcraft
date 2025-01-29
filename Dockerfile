# Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

FROM ubuntu:24.04
RUN apt-get update && apt-get install -y \
    libssl-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY bin/Docker/net8.0/sipcraft .
RUN chmod +x sipcraft
EXPOSE 5060
ENTRYPOINT ["/app/sipcraft"]

