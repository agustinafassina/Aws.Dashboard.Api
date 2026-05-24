FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

COPY *.sln .
COPY Aws.Models/*.csproj Aws.Models/
COPY Aws.Repository/*.csproj Aws.Repository/
COPY Aws.Services/*.csproj Aws.Services/
COPY Aws.Api/*.csproj Aws.Api/

RUN dotnet restore AwsApi.sln

COPY . .
RUN dotnet publish Aws.Api/Aws.Api.csproj -c Release -o /release

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=80
COPY --from=build /release ./
ENTRYPOINT ["dotnet", "Aws.Api.dll"]