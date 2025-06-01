# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

WORKDIR /source

COPY Asahi.WebServices/*.csproj Asahi.WebServices/

ARG TARGETARCH

RUN dotnet restore Asahi.WebServices/ -a $TARGETARCH

COPY . .

RUN set -xe; \
dotnet publish Asahi.WebServices/ -c Release -a $TARGETARCH -o /app; \
chmod +x /app/Asahi.WebServices

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

COPY --from=build-env /app .

ENTRYPOINT [ "dotnet", "Asahi.WebServices.dll" ]
# For configuration, i think you can just set the ASPNETCORE_ENVIRONMENT to "Docker", and add a volume with a file called appsettings.Docker.json :clueless: