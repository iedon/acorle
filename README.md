# Acorle

```Acorle``` is my own tiny microservice solution which integrates a registration center, a configuration center and an API gateway with load balancing support.

It should be used along with a client sdk that is included in iedon-net-api.

## Build

### CLI users

```bash
dotnet build "Acorle.csproj" -c Release -o ./build
dotnet publish "Acorle.csproj" -c Release -o ./publish /p:UseAppHost=false
```

### Visual Studio users

Run ```Acorle.sln``` to build.

### Docker

See Dockerfile.