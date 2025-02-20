// Copyright © 2025 Always Active Technologies PTY Ltd

using TechAptV1.Client.Models;
using System.Data.SQLite;
using System.Data.Common;
namespace TechAptV1.Client.Services;

/// <summary>
/// Data Access Service for interfacing with the SQLite Database
/// </summary>
public sealed class DataService
{
    private readonly ILogger<DataService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly SQLiteConnection _connection;
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
        using (var connection = new SQLiteConnection(_connectionString))
        {
            await connection.OpenAsync();
            _logger.LogInformation("Database connection initialized.");
        }
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

        // Persistent connection
        await using var connection = new SQLiteConnection(_configuration.GetConnectionString("Default"));
        await connection.OpenAsync();

        int totalRecords = dataList.Count;
        int batchCount = 0;
        int currentBatchSize = 0;
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new SQLiteCommand(insertQuery, connection, (SQLiteTransaction)transaction);

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
                        transaction = await connection.BeginTransactionAsync(); // Create new transaction
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
        const string createTableQuery = @"CREATE TABLE IF NOT EXISTS Number (
                                                Value INTEGER NOT NULL,
                                                IsPrime INTEGER NOT NULL DEFAULT 0
                                                );";

        await using var connection = new SQLiteConnection(_configuration.GetConnectionString("Default"));
        await connection.OpenAsync();

        await using var createCmd = new SQLiteCommand(createTableQuery, connection);
        await createCmd.ExecuteNonQueryAsync();

        this._logger.LogInformation("Table check completed.");
    }
    /// <summary>
    /// Fetch N records from the SQLite Database where N is specified by the count parameter
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public IEnumerable<Number> Get(int count)
    {
        this._logger.LogInformation("Get");
        throw new NotImplementedException();
    }

    /// <summary>
    /// Fetch All the records from the SQLite Database
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Number> GetAll()
    {
        this._logger.LogInformation("GetAll");
        throw new NotImplementedException();
    }
}
