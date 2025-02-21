// Copyright © 2025 Always Active Technologies PTY Ltd

using TechAptV1.Client.Models;
using System.Data.SQLite;
namespace TechAptV1.Client.Services;

/// <summary>
/// Data Access Service for interfacing with the SQLite Database
/// </summary>
public sealed class DataService : IDisposable
{
    private readonly ILogger<DataService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private SQLiteConnection? _connection;
    /// <summary>
    /// Default constructor providing DI Logger and Configuration
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configuration"></param>
    public DataService(ILogger<DataService> logger, IConfiguration configuration)
    {
        this._logger = logger;
        this._configuration = configuration;
        _connectionString = configuration.GetConnectionString("Default");       
    }
    /// <summary>
    /// Initialize Sqlite Database Connection
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
            return; // Skip if already connected

        _connection = new SQLiteConnection(_connectionString);
        await _connection.OpenAsync();
        this._logger.LogInformation("Database connection initialized.");
    }
    /// <summary>
    /// Save the list of data to the SQLite Database
    /// </summary>
    /// <param name="dataList"></param>
    public async Task Save(List<Number> dataList,int batchSize = 100000)
    {
        this._logger.LogInformation("Save");

        // Ensure the table exists before inserting data
        await EnsureTableExistsAsync();

        const string insertQuery = "INSERT INTO Number (Value, IsPrime) VALUES (@Value, @IsPrime);";

        // Ensure connection is initialized
        await InitializeAsync();

        // Persistent connection, reuse the already opened connection
        int totalRecords = dataList.Count;
        int batchCount = 0;
        int currentBatchSize = 0;
        var transaction = await _connection.BeginTransactionAsync();
        await using var command = new SQLiteCommand(insertQuery, _connection, (SQLiteTransaction)transaction);

        command.Parameters.Add(new SQLiteParameter("@Value"));
        command.Parameters.Add(new SQLiteParameter("@IsPrime"));

        try
        {
            for (int i = 0; i < totalRecords; i++)
            {
                var num = dataList[i];
                command.Parameters["@Value"].Value = num.Value;
                command.Parameters["@IsPrime"].Value = num.IsPrime;
                await command.ExecuteNonQueryAsync();
                currentBatchSize++;

                // Commit when batch size reached or at the last record
                if ((i + 1) % batchSize == 0 || i == totalRecords - 1)
                {
                    batchCount++;
                    this._logger.LogInformation($"Committing Batch #{batchCount} with {currentBatchSize} records...");
                    await transaction.CommitAsync();
                    currentBatchSize = 0; // Reset current count

                    // Start a new transaction only if there are more records to process
                    if (i < totalRecords - 1)
                    {
                        await transaction.DisposeAsync(); // Dispose the old transaction
                        transaction = await _connection.BeginTransactionAsync(); // Create new transaction
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "An error occurred while saving. Rolling back transaction.");
            await transaction.RollbackAsync(); // Rollback transaction on exception
            throw; // Re-throw the exception to propagate it
        }
        finally
        {
            await transaction.DisposeAsync(); // Ensure transaction is disposed
        }

        this._logger.LogInformation("Save completed");
    }
    /// <summary>
    /// Create the Number table if it is not already created
    /// </summary>
    private async Task EnsureTableExistsAsync()
    {
        try
        {
            const string createTableQuery = @"CREATE TABLE IF NOT EXISTS Number (
                                                    Value INTEGER NOT NULL,
                                                    IsPrime INTEGER NOT NULL DEFAULT 0
                                                    );";

            // Ensure connection is initialized
            await InitializeAsync();

            await using var createCmd = new SQLiteCommand(createTableQuery, _connection);
            await createCmd.ExecuteNonQueryAsync();

            this._logger.LogInformation("Table check completed.");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "An error occurred.");
        }
    }
    /// <summary>
    /// Fetch N records from the SQLite Database where N is specified by the count parameter
    /// </summary>
    /// <param name="count"></param>
    /// <returns>Numbers </returns>
    public async Task<IEnumerable<Number>> Get(int count)
    {
        this._logger.LogInformation("Get");
        try
        {
            var numbers = new List<Number>();
            string query = $"SELECT Value, IsPrime FROM Number ORDER BY Value LIMIT {count};";

            // Ensure connection is initialized
            await InitializeAsync();

            using (var cmd = new SQLiteCommand(query, _connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    numbers.Add(new Number
                    {
                        Value = reader.GetInt32(0),
                        IsPrime = reader.GetInt32(1)
                    });
                }
            }

            return numbers;  // Successfully return the list
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "An error occurred.");
            return Enumerable.Empty<Number>();  // Return an empty collection in case of an error
        }
    }

    /// <summary>
    /// Fetch All the records from the SQLite Database
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<Number>> GetAll()
    {
        this._logger.LogInformation("GetAll");

        var numbers = new List<Number>();
        string query = "SELECT Value, IsPrime FROM Number";

        // Ensure connection is initialized
        await InitializeAsync();

        using (var cmd = new SQLiteCommand(query, _connection))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                this._logger.LogInformation("Adding to Number List : " + reader.GetInt32(0));
                numbers.Add(new Number
                {
                    Value = reader.GetInt32(0),
                    IsPrime = reader.GetInt32(1)
                });
            }
        }

        return numbers;
    }

    /// <summary>
    /// Asynchronously streams all records from the "Number" table in the SQLite database.   
    /// </summary>
    public async IAsyncEnumerable<Number> StreamAllNumbers()
    {
        // Ensure the connection is initialized and open.
        await InitializeAsync();

        using var command = new SQLiteCommand("SELECT Value, IsPrime FROM Number", _connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            yield return new Number
            {
                Value = reader.GetInt32(0),
                IsPrime = reader.GetInt32(1)
            };
        }
    }

    /// <summary>
    /// Disposes of the database connection, ensuring it is properly closed if open.
    /// This method is automatically called at the end of the scope when using dependency injection.
    /// </summary>
    public void Dispose()
    {
        if (_connection != null)
        {
            if (_connection.State == System.Data.ConnectionState.Open)
            {
                this._connection.Close();
                this._logger.LogInformation("Database connection closed.");
            }
            _connection.Dispose();
        }
    }
}
