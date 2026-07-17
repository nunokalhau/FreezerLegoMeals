using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FreezerLegoMeals.Domain.DotNet;

namespace FreezerLegoMeals.Repository.DotNet;

/// <summary>
/// Base repository class for implementing common repository functionality.
/// </summary>
public abstract class BaseRepository
{
    protected readonly string _connectionString;

    protected BaseRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Validates that a connection string is provided and not empty.
    /// </summary>
    protected void ValidateConnectionString()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(_connectionString));
    }
}

/// <summary>
/// Repository interface for general data operations in the .NET service layer.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> GetByIdAsync(int id);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
}