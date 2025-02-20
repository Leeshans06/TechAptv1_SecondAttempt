// Copyright © 2025 Always Active Technologies PTY Ltd
using System.Collections.Concurrent;
using TechAptV1.Client.Models;
namespace TechAptV1.Client.Services;

/// <summary>
/// Default constructor providing DI Logger and Data Service
/// </summary>
/// <param name="logger"></param>
/// <param name="dataService"></param>
public sealed class ThreadingService(ILogger<ThreadingService> logger, DataService dataService)
{
    private int _oddNumbers = 0;
    private int _evenNumbers = 0;
    private int _primeNumbers = 0;
    private int _totalNumbers = 0;
    private bool _isRunning = false;
    private readonly int _maxCount = 10000000; // ************************* change back 10 000 000 **************************************************************
    private Task? _oddTask, _primeTask, _evenTask;    
    private readonly ConcurrentDictionary<int, int> _numbers = new(); // Create a Global ConcurrentDictionary - thread-safe dictionary designed for concurrent access in multi-threaded applications
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _limitReachedEvent = new(false); // Event to signal stopping
    private static readonly ConcurrentQueue<int> s_queue = new();
    private static SpinLock s_spinLock = new(false);

    public int GetOddNumbers() => _oddNumbers;
    public int GetEvenNumbers() => _evenNumbers;
    public int GetPrimeNumbers() => _primeNumbers;
    public int GetTotalNumbers() => _totalNumbers;
    public ConcurrentDictionary<int, int> GetGlobalList() => _numbers;

    /// <summary>
    /// Start the random number generation process
    /// </summary>
    public async Task Start()
    {
        logger.LogInformation("Start");
        if (_isRunning) return;
        _isRunning = true;      

        // Run tasks 1 & 2
        _oddTask = Task.Run(() => GenerateOddNumbers(_cts.Token));
        _primeTask = Task.Run(() => GeneratePrimeNegatives(_cts.Token));

        // Monitor count and start Task 3 when _numbers.Count >= 2,500,000
        _ = Task.Run(() =>
        {
            while (_numbers.Count < 2500000) // ***************change back ************************
            {
                Task.Delay(10); // Delay to avoid high CPU usage - test higher ms
            }

            Console.WriteLine("Starting Even Numbers Task...");
            _evenTask = Task.Run(() => GenerateEvenNumbers(_cts.Token));
        });

        // Monitor count and trigger cancellation when _numbers.Count >= 10,000,000
        _ = Task.Run(() =>
        {
            while (_numbers.Count < _maxCount)
            {
                Task.Delay(10);
            }

            Console.WriteLine("Target reached! Cancelling tasks...");
            _cts.Cancel(); // Stop all tasks
            _limitReachedEvent.Set(); // Notify tasks to stop
        });

        // Wait for Task 1 & 2 to finish
        await Task.WhenAll(_oddTask, _primeTask);

        // Wait for Task 3
        if (_evenTask != null)
            await _evenTask;

        _isRunning = false;
        logger.LogInformation($"Generation stopped at count: {_numbers.Count}");
        Console.WriteLine("Processing Complete.");
    }

    /// <summary>
    /// Persist the results to the SQLite database
    /// </summary>
    public async Task Save()
    {
        logger.LogInformation("Save");

        // Convert dictionary to a list of Number objects and sort them
        List<Number> numberList = _numbers
            .Select(kvp => new Number { Value = kvp.Key, IsPrime = kvp.Value })
            .OrderBy(n => n.Value)
            .ToList();

        await dataService.InitializeAsync();
        await dataService.Save(numberList);
        logger.LogInformation("Finished - Save Numbers.");
    }

    /// <summary>
    /// Generate Odd Numbers and adds to gobal Concurrent Dictionary
    /// </summary>
    private void GenerateOddNumbers(CancellationToken token)
    {
        logger.LogInformation("Start GenerateOddNumbers");
        Random rand = new();
        try
        {
            while ((!token.IsCancellationRequested))
            {
                if (_numbers.Count >= _maxCount)  break; // Additional safety check     
                int num = rand.Next(1, int.MaxValue); // Generates a random odd integer between 1 and int.MaxValue
                if (num % 2 == 0)
                {
                    num++;  // Make it odd if it's even
                }

                TryAddNumber(num, IsPrime(num));
                Interlocked.Increment(ref _oddNumbers);                
                //if (_numbers.Count >= _maxCount)  break;  // Check immediately after adding the number
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error occurred in GenerateOddNumbers: {ex.Message}");
        }
        logger.LogInformation("Finished GenerateOddNumbers");
    }
    /// <summary>
    /// Generate Prime Negatives and adds to gobal variable Numbers
    /// </summary>
    private void GeneratePrimeNegatives(CancellationToken token)
    {
        logger.LogInformation("Start GeneratePrimeNegatives");
        Random rand = new();
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_numbers.Count >= _maxCount)  break; // Additional safety check
                int num = rand.Next(2, int.MaxValue); // Random integer greater than or equal to 2 and less than int.MaxValue
                bool isPrime = IsPrime(num);
                if (isPrime) // Ensure its a prime number
                {
                    TryAddNumber(-num, true); // Negate and add number as prime as check is already done
                    Interlocked.Increment(ref _primeNumbers);                    
                }
               // if (_numbers.Count >= _maxCount)  break;   // Check immediately after adding the number
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error occurred in GeneratePrimeNegatives: {ex.Message}");
        }
        logger.LogInformation("Finished GeneratePrimeNegatives");
    }
    /// <summary>
    /// Generate Even Numbers and adds to gobal Concurrent Dictionary
    /// </summary>
    private void GenerateEvenNumbers(CancellationToken token)
    {
        logger.LogInformation("Start GenerateEvenNumbers");
        Random rand = new();
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_numbers.Count >= _maxCount)  break; // Additional safety check
                int num = rand.Next(1, int.MaxValue) * 2;// Ensures even number is generated
                TryAddNumber(num, IsPrime(num));
                Interlocked.Increment(ref _evenNumbers);               
                if (_numbers.Count >= _maxCount)  break;   // Check immediately after adding the number
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error occurred in GenerateEvenNumbers: {ex.Message}");
        }
        logger.LogInformation("Finished GenerateEvenNumbers");
    }

    /// <summary>
    /// Check if the number is prime
    /// </summary>
    /// <param name="num"></param>
    /// <returns>true/false</returns>
    public bool IsPrime(int num)
    {
        if (num < 2) return false;
        if (num == 2 || num == 3) return true; // Check for small primes
        if (num % 2 == 0 || num % 3 == 0) return false; // Exclude even numbers and multiples of 3

        for (int i = 5; i * i <= num; i += 2) // Skip even numbers
        {
            if (num % i == 0) return false;
        }
        return true;
    }
    /// <summary>
    /// Function to handle TryAdd
    /// </summary>
    /// <param name="num"></param>
    /// <param name="isPrime"></param>    
    private void TryAddNumber(int num,bool isPrime)
    {
        bool lockTaken = false;
        try
        {
            s_spinLock.Enter(ref lockTaken); // Acquire the lock - spinlock - to ensure thread safety while modifying _numbers and s_queue.
            if (_numbers.Count < _maxCount)
            {
                // Remove the oldest entry
              //  if (s_queue.TryDequeue(out int oldestKey))
               // {
                   // _numbers.TryRemove(oldestKey, out _); // Remove entries higher than _maxCount
               // }
           

                if (_numbers.TryAdd(num, (isPrime) ? 1 : 0))
                {
                    Interlocked.Increment(ref _totalNumbers); // Increment only on successful addition
                    //s_queue.Enqueue(num); // Tracks the order in which numbers were added, ensuring that the oldest entry is removed when the maximum count is reached.                
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error occurred while adding number {num} to dictionary: {ex.Message}");            
        }
        finally
        {
            if (lockTaken) s_spinLock.Exit(); // Ensure lock is released
        }
    }
}

