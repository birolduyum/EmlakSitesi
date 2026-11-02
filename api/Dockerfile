FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Emlak.Api/Emlak.Api.csproj Emlak.Api/
COPY nuget.config .
RUN dotnet restore Emlak.Api/Emlak.Api.csproj
COPY Emlak.Api/ Emlak.Api/
RUN dotnet publish Emlak.Api/Emlak.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Emlak.Api.dll"]
