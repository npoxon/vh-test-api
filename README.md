# VH Test Api

<img width="1553" alt="Test Api Swagger" src="https://user-images.githubusercontent.com/10450246/114572761-122ed600-9c78-11eb-94a4-a1157d2adc51.png">

The Test Api was an api I developed to manage test users and test data, as well as providing a mechanism for the front end tests to retreive data from the backend apis.

## Run Stryker

To run stryker mutation test, go to UnitTest folder under command prompt and run the following command

```bash
dotnet stryker
```

From the results look for line(s) of code highlighted with Survived\No Coverage and fix them.

If in case you have not installed stryker previously, please use one of the following commands

## Global
```bash
dotnet tool install -g dotnet-stryker
```
## Local
```bash
dotnet tool install dotnet-stryker
```

To update latest version of stryker please use the following command

```bash
dotnet tool update --global dotnet-stryker
```

## Run Zap scan locally

To run Zap scan locally update the following settings and run acceptance\integration tests

Update following configuration under appsettings.json under TestApi.IntegrationTests

- "Services:TestApiUrl": "https://TestApi_AC/"
- "ZapConfiguration:ZapScan": true
- "ConnectionStrings:TestApi": "Server=localhost,1433;Database=TestApi;User=sa;Password=VeryStrongPassword!;" (TestApi\appsettings.development.json)

Note: Ensure you have Docker desktop engine installed and setup

