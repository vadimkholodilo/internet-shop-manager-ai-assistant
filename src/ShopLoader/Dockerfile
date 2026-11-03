# Build context: the repository root (docker build -f src/ShopLoader/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/ShopLoader/ src/ShopLoader/
RUN dotnet publish src/ShopLoader/ShopLoader.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app
COPY --from=build /out/shop-loader /app/shop-loader
COPY data /data
ENTRYPOINT ["/app/shop-loader"]
