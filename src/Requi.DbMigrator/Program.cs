using DbUp;

// Aplica en orden los scripts de database/scripts que aún no corrieron.
// DbUp registra los aplicados en la tabla schemaversions.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Migrations");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Falta la variable ConnectionStrings__Migrations.");
    return 1;
}

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
if (!result.Successful)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

Console.WriteLine("Base de datos al día.");
return 0;
