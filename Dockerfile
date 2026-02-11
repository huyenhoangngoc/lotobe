FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LoTo.slnx .
COPY src/LoTo.Domain/LoTo.Domain.csproj src/LoTo.Domain/
COPY src/LoTo.Application/LoTo.Application.csproj src/LoTo.Application/
COPY src/LoTo.Infrastructure/LoTo.Infrastructure.csproj src/LoTo.Infrastructure/
COPY src/LoTo.WebApi/LoTo.WebApi.csproj src/LoTo.WebApi/
RUN dotnet restore

COPY . .
RUN dotnet publish src/LoTo.WebApi/LoTo.WebApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

CMD ["sh", "-c", "dotnet LoTo.WebApi.dll --urls http://0.0.0.0:${PORT:-8080}"]
