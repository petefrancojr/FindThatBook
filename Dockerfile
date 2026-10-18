# One image with both the site and the API, for hosting as a single service (see render.yaml).
# docker compose doesn't use this; it builds client/ and server/ separately.

FROM node:24-alpine AS client
WORKDIR /src
COPY client/package.json client/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY client/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY server/FindThatBook.Api/FindThatBook.Api.csproj FindThatBook.Api/
RUN dotnet restore FindThatBook.Api/FindThatBook.Api.csproj
COPY server/ .
RUN dotnet publish FindThatBook.Api/FindThatBook.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /app .
# The API serves the site from wwwroot, so the site's calls to /api stay on the same origin.
COPY --from=client /src/dist/client/browser ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FindThatBook.Api.dll"]
