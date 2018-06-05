#!/usr/bin/env bash

docker run \
    -d -it --init --restart always \
    --name benchmarks-server \
    -p 5003:80 \
    benchmarks \
    bash -c \
    "dotnet run -c Release --project src/BenchmarksServer/BenchmarksServer.csproj"
