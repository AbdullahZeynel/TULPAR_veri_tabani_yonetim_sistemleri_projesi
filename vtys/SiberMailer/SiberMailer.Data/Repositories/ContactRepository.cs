using Dapper;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using SiberMailer.Core.Enums;
using SiberMailer.Core.Models;
using System.Data;

namespace SiberMailer.Data.Repositories;

/// <summary>
/// Repository for Contact entity database operations.
/// </summary>
public class ContactRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ContactRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Bulk imports contacts using the PostgreSQL stored procedure sp_import_contacts_bulk.
    /// Serializes the contact list to JSONB for efficient bulk processing.
    /// </summary>
    /// <param name="listId">Target recipient list ID</param>
    /// <param name="contacts">List of contacts to import</param>
    /// <returns>Import result with counts of inserted, updated, and skipped records</returns>
    public async Task<BulkImportResult> BulkInsertContactsAsync(int listId, IEnumerable<ContactImportDto> contacts)
    {
        // Serialize contacts to JSONB format
        var contactsList = contacts.ToList();
        var jsonData = JsonConvert.SerializeObject(contactsList);

        // Use raw NpgsqlConnection for JSONB parameter handling
        await using var connection = new NpgsqlConnection(_connectionFactory.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand("CALL sp_import_contacts_bulk(@p_list_id, @p_contacts_json, NULL, NULL, NULL)", connection);
        
        cmd.Parameters.AddWithValue("p_list_id", listId);
        cmd.Parameters.Add(new NpgsqlParameter("p_contacts_json", NpgsqlDbType.Jsonb) { Value = jsonData });

        // Execute and read OUT parameters
        await using var reader = await cmd.ExecuteReaderAsync();
        
        var result = new BulkImportResult();
        if (await reader.ReadAsync())
        {
            result.Inserted = reader.GetInt32(0);
            result.Updated = reader.GetInt32(1);
            result.Skipped = reader.GetInt32(2);
        }

        return result;
    }

    /// <summary>
    /// Gets all contacts for a specific list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="activeOnly">If true, only returns active contacts</param>
    /// <returns>Collection of contacts</returns>
    public async Task<IEnumerable<Contact>> GetByListIdAsync(int listId, bool activeOnly = false)
    {
        var sql = @"
            SELECT ContactId, ListId, Email, FullName, Company, 
                   CustomData::text as CustomData,
                   Status, BounceCount, LastBouncedAt, UnsubscribedAt, 
                   CreatedAt, UpdatedAt
            FROM Contacts 
            WHERE ListId = @ListId" + (activeOnly ? " AND Status = 'Active'" : "") + @"
            ORDER BY FullName, Email";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Contact>(sql, new { ListId = listId });
    }

    /// <summary>
    /// Gets all contacts across all lists.
    /// </summary>
    /// <param name="activeOnly">If true, only returns active contacts</param>
    /// <returns>Collection of all contacts</returns>
    public async Task<IEnumerable<Contact>> GetAllAsync(bool activeOnly = false)
    {
        var sql = @"
            SELECT ContactId, ListId, Email, FullName, Company, 
                   CustomData::text as CustomData,
                   Status, BounceCount, LastBouncedAt, UnsubscribedAt, 
                   CreatedAt, UpdatedAt
            FROM Contacts" + (activeOnly ? " WHERE Status = 'Active'" : "") + @"
            ORDER BY CreatedAt DESC
            LIMIT 1000";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Contact>(sql);
    }

    /// <summary>
    /// Gets a contact by ID.
    /// </summary>
    public async Task<Contact?> GetByIdAsync(int contactId)
    {
        const string sql = @"
            SELECT ContactId, ListId, Email, FullName, Company, 
                   CustomData::text as CustomData,
                   Status, BounceCount, LastBouncedAt, UnsubscribedAt, 
                   CreatedAt, UpdatedAt
            FROM Contacts 
            WHERE ContactId = @ContactId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<Contact>(sql, new { ContactId = contactId });
    }

    /// <summary>
    /// Gets a contact by email within a specific list.
    /// </summary>
    public async Task<Contact?> GetByEmailAsync(int listId, string email)
    {
        const string sql = @"
            SELECT ContactId, ListId, Email, FullName, Company, 
                   CustomData::text as CustomData,
                   Status, BounceCount, LastBouncedAt, UnsubscribedAt, 
                   CreatedAt, UpdatedAt
            FROM Contacts 
            WHERE ListId = @ListId AND Email = @Email";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<Contact>(sql, new { ListId = listId, Email = email.ToLower() });
    }

    /// <summary>
    /// Updates a contact's status.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(int contactId, ContactStatus newStatus)
    {
        var sql = @"
            UPDATE Contacts 
            SET Status = @Status::contact_status, UpdatedAt = CURRENT_TIMESTAMP
            WHERE ContactId = @ContactId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new { ContactId = contactId, Status = newStatus.ToString() });
        return affected > 0;
    }

    /// <summary>
    /// Gets the count of contacts in a list by status.
    /// </summary>
    public async Task<Dictionary<ContactStatus, int>> GetCountsByStatusAsync(int listId)
    {
        const string sql = @"
            SELECT Status, COUNT(*) as Count
            FROM Contacts 
            WHERE ListId = @ListId
            GROUP BY Status";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var results = await connection.QueryAsync<(string Status, int Count)>(sql, new { ListId = listId });

        var counts = new Dictionary<ContactStatus, int>();
        foreach (var (status, count) in results)
        {
            if (Enum.TryParse<ContactStatus>(status, true, out var statusEnum))
            {
                counts[statusEnum] = count;
            }
        }
        return counts;
    }

    /// <summary>
    /// Calls the fn_get_list_members PostgreSQL function.
    /// </summary>
    public async Task<IEnumerable<Contact>> GetListMembersAsync(int listId)
    {
        const string sql = "SELECT * FROM fn_get_list_members(@ListId)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync<Contact>(sql, new { ListId = listId });
    }

    /// <summary>
    /// Adds a new contact to the database.
    /// </summary>
    public async Task<int> AddAsync(Contact contact)
    {
        const string sql = @"
            INSERT INTO Contacts (ListId, Email, FullName, Company, CustomData, Status, CreatedAt, UpdatedAt)
            VALUES (@ListId, @Email, @FullName, @Company, @CustomData::jsonb, @Status::contact_status, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING ContactId";

        var customDataJson = contact.CustomData != null 
            ? JsonConvert.SerializeObject(contact.CustomData) 
            : "{}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<int>(sql, new
        {
            contact.ListId,
            Email = contact.Email.ToLowerInvariant(),
            contact.FullName,
            contact.Company,
            CustomData = customDataJson,
            Status = contact.Status.ToString()
        });
    }

    /// <summary>
    /// Updates an existing contact.
    /// </summary>
    public async Task<bool> UpdateAsync(Contact contact)
    {
        const string sql = @"
            UPDATE Contacts 
            SET Email = @Email,
                FullName = @FullName,
                Company = @Company,
                CustomData = @CustomData::jsonb,
                Status = @Status::contact_status,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE ContactId = @ContactId";

        var customDataJson = contact.CustomData != null 
            ? JsonConvert.SerializeObject(contact.CustomData) 
            : "{}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new
        {
            contact.ContactId,
            Email = contact.Email.ToLowerInvariant(),
            contact.FullName,
            contact.Company,
            CustomData = customDataJson,
            Status = contact.Status.ToString()
        });
        return affected > 0;
    }

    /// <summary>
    /// Deletes a contact.
    /// </summary>
    public async Task<bool> DeleteAsync(int contactId)
    {
        const string sql = "DELETE FROM Contacts WHERE ContactId = @ContactId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync();
        var affected = await connection.ExecuteAsync(sql, new { ContactId = contactId });
        return affected > 0;
    }

    /// <summary>
    /// Processes a bounce for a contact using the stored procedure.
    /// </summary>
    public async Task<ContactStatus> ProcessBounceAsync(int contactId, string? reason = null)
    {
        await using var connection = new NpgsqlConnection(_connectionFactory.ConnectionString);
        await connection.OpenAsync();

        // The stored procedure has OUT parameter first, then the default parameter
        await using var cmd = new NpgsqlCommand(
            "CALL sp_process_bounce(@p_contact_id, @p_new_status, @p_bounce_reason)", 
            connection);

        cmd.Parameters.AddWithValue("p_contact_id", contactId);
        cmd.Parameters.Add(new NpgsqlParameter("p_new_status", NpgsqlDbType.Unknown) 
        { 
            Direction = ParameterDirection.Output 
        });
        cmd.Parameters.AddWithValue("p_bounce_reason", reason ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();

        var statusStr = cmd.Parameters["p_new_status"].Value?.ToString() ?? "Bounced";
        return Enum.Parse<ContactStatus>(statusStr, true);
    }
}
