# Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

FROM mcr.microsoft.com/dotnet/runtime:8.0-noble
RUN apt-get update && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY bin/Docker/net8.0/sipcraft .
RUN chmod +x sipcraft
EXPOSE 5060
ENTRYPOINT ["/app/sipcraft"]

