FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore XerifeTv.CMS.sln

RUN dotnet publish XerifeTv.CMS/XerifeTv.CMS.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update && apt-get install -y ca-certificates

COPY --from=build /app/publish .

EXPOSE 88

ENTRYPOINT ["dotnet","XerifeTv.CMS.dll"]