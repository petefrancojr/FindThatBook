FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FindThatBook.Api/FindThatBook.Api.csproj FindThatBook.Api/
RUN dotnet restore FindThatBook.Api/FindThatBook.Api.csproj
COPY . .
RUN dotnet publish FindThatBook.Api/FindThatBook.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FindThatBook.Api.dll"]
