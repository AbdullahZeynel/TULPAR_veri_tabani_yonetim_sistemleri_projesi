using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using SiberMailer.Core.Models;
using System.Data;
using System.Globalization;
using System.Text;

namespace SiberMailer.Business.Services;

/// <summary>
/// Service for importing contacts from CSV and Excel files.
/// Supports .csv, .xlsx, and .xls formats.
/// </summary>
public class ImportService
{
    static ImportService()
    {
        // Required for ExcelDataReader to work with .NET Core
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    /// <summary>
    /// Result of a CSV parsing operation.
    /// </summary>
    public class ImportParseResult
    {
        public List<ContactImportDto> Contacts { get; set; } = new();
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int SkippedRows { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Parses a file based on its extension (CSV or Excel).
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Parse result with contacts and any errors</returns>
    public ImportParseResult ParseFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".csv" => ParseCsvFile(filePath),
            ".xlsx" or ".xls" => ParseExcelFile(filePath),
            _ => new ImportParseResult 
            { 
                Errors = { $"Unsupported file format: {extension}. Use .csv, .xlsx, or .xls" } 
            }
        };
    }

    /// <summary>
    /// Parses a CSV file and returns a list of ContactImportDto.
    /// </summary>
    /// <param name="filePath">Path to the CSV file</param>
    /// <returns>Parse result with contacts and any errors</returns>
    public ImportParseResult ParseCsvFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ParseCsvStream(stream);
    }

    /// <summary>
    /// Parses an Excel file (.xlsx or .xls) and returns a list of ContactImportDto.
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <returns>Parse result with contacts and any errors</returns>
    public ImportParseResult ParseExcelFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ParseExcelStream(stream);
    }

    /// <summary>
    /// Parses an Excel stream and returns a list of ContactImportDto.
    /// </summary>
    public ImportParseResult ParseExcelStream(Stream stream)
    {
        var result = new ImportParseResult();

        try
        {
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            if (dataSet.Tables.Count == 0)
            {
                result.Errors.Add("Excel file contains no worksheets.");
                return result;
            }

            var table = dataSet.Tables[0]; // Use first worksheet
            
            // Build header map
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 0; col < table.Columns.Count; col++)
            {
                var header = table.Columns[col].ColumnName.Trim().ToLowerInvariant();
                header = NormalizeHeader(header);
                if (!headerMap.ContainsKey(header))
                {
                    headerMap[header] = col;
                }
            }

            if (!headerMap.ContainsKey("email"))
            {
                result.Errors.Add("Excel file must contain an 'Email' column.");
                return result;
            }

            // Process rows
            for (int row = 0; row < table.Rows.Count; row++)
            {
                result.TotalRows++;
                int rowNumber = row + 2; // +2 for header row and 0-index

                try
                {
                    var contact = ParseExcelRow(table.Rows[row], headerMap, rowNumber, result);
                    if (contact != null)
                    {
                        result.Contacts.Add(contact);
                        result.ValidRows++;
                    }
                    else
                    {
                        result.SkippedRows++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                    result.SkippedRows++;
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse Excel file: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Parses a single Excel row into a ContactImportDto.
    /// </summary>
    private ContactImportDto? ParseExcelRow(
        DataRow row,
        Dictionary<string, int> headerMap,
        int rowNumber,
        ImportParseResult result)
    {
        // Get email (required)
        var email = GetExcelFieldValue(row, headerMap, "email")?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
        {
            result.Warnings.Add($"Row {rowNumber}: Empty email, skipped");
            return null;
        }

        if (!IsValidEmail(email))
        {
            result.Warnings.Add($"Row {rowNumber}: Invalid email '{email}', skipped");
            return null;
        }

        // Get full name
        string? fullName = GetExcelFieldValue(row, headerMap, "fullname");
        if (string.IsNullOrWhiteSpace(fullName))
        {
            var firstName = GetExcelFieldValue(row, headerMap, "firstname");
            var lastName = GetExcelFieldValue(row, headerMap, "lastname");
            fullName = $"{firstName} {lastName}".Trim();
        }

        // Get company
        var company = GetExcelFieldValue(row, headerMap, "company");

        // Build custom data from remaining columns
        var customData = new Dictionary<string, object>();
        var standardFields = new HashSet<string> { "email", "fullname", "firstname", "lastname", "company", "name" };

        foreach (var kvp in headerMap)
        {
            if (!standardFields.Contains(kvp.Key))
            {
                var value = row[kvp.Value]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (int.TryParse(value, out int intVal))
                        customData[kvp.Key] = intVal;
                    else if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                        customData[kvp.Key] = dblVal;
                    else
                        customData[kvp.Key] = value;
                }
            }
        }

        return new ContactImportDto
        {
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            Company = string.IsNullOrWhiteSpace(company) ? null : company,
            CustomData = customData.Count > 0 ? customData : null
        };
    }

    /// <summary>
    /// Gets a field value from an Excel DataRow by column name.
    /// </summary>
    private string? GetExcelFieldValue(DataRow row, Dictionary<string, int> headerMap, string fieldName)
    {
        if (headerMap.TryGetValue(fieldName, out int index))
        {
            return row[index]?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Normalizes header names to standard field names.
    /// </summary>
    private string NormalizeHeader(string header)
    {
        return header switch
        {
            "e-mail" or "emailaddress" or "email_address" or "mail" => "email",
            "name" or "fullname" or "full_name" or "full name" => "fullname",
            "firstname" or "first_name" or "first name" => "firstname",
            "lastname" or "last_name" or "last name" => "lastname",
            "company" or "organization" or "org" or "firma" => "company",
            _ => header
        };
    }

    /// <summary>
    /// Parses a CSV stream and returns a list of ContactImportDto.
    /// Supports flexible column mapping (Email, FullName, Name, Company).
    /// </summary>
    /// <param name="stream">CSV data stream</param>
    /// <returns>Parse result with contacts and any errors</returns>
    public ImportParseResult ParseCsvStream(Stream stream)
    {
        var result = new ImportParseResult();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = context =>
            {
                result.Warnings.Add($"Bad data at row {context.Context?.Parser?.Row}: {context.RawRecord}");
            },
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            PrepareHeaderForMatch = args => args.Header?.Trim().ToLowerInvariant() ?? string.Empty,
            Delimiter = ",", // Explicit comma delimiter
            Mode = CsvMode.RFC4180 // Standard CSV mode
        };

        try
        {
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, config);

            // Read header to determine column mapping
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            var headerMap = CreateHeaderMap(headers);

            if (!headerMap.ContainsKey("email"))
            {
                result.Errors.Add("CSV must contain an 'Email' column");
                return result;
            }

            // Process each row
            int rowNumber = 1;
            while (csv.Read())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    var contact = ParseRow(csv, headerMap, rowNumber, result);
                    if (contact != null)
                    {
                        result.Contacts.Add(contact);
                        result.ValidRows++;
                    }
                    else
                    {
                        result.SkippedRows++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                    result.SkippedRows++;
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse CSV: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Creates a case-insensitive header mapping for CSV.
    /// </summary>
    private Dictionary<string, int> CreateHeaderMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        for (int i = 0; i < headers.Length; i++)
        {
            // Skip null or empty headers
            if (string.IsNullOrWhiteSpace(headers[i]))
                continue;

            var header = headers[i].Trim().ToLowerInvariant();
            header = NormalizeHeader(header);

            // Skip empty normalized headers
            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (!map.ContainsKey(header))
            {
                map[header] = i;
            }
        }

        return map;
    }

    /// <summary>
    /// Parses a single CSV row into a ContactImportDto.
    /// </summary>
    private ContactImportDto? ParseRow(
        CsvReader csv, 
        Dictionary<string, int> headerMap, 
        int rowNumber,
        ImportParseResult result)
    {
        // Get email (required)
        var email = GetFieldValue(csv, headerMap, "email")?.Trim().ToLowerInvariant();
        
        if (string.IsNullOrWhiteSpace(email))
        {
            result.Warnings.Add($"Row {rowNumber}: Empty email, skipped");
            return null;
        }

        // Basic email validation
        if (!IsValidEmail(email))
        {
            result.Warnings.Add($"Row {rowNumber}: Invalid email '{email}', skipped");
            return null;
        }

        // Get full name (try fullname, then combine first+last)
        string? fullName = GetFieldValue(csv, headerMap, "fullname");
        if (string.IsNullOrWhiteSpace(fullName))
        {
            var firstName = GetFieldValue(csv, headerMap, "firstname");
            var lastName = GetFieldValue(csv, headerMap, "lastname");
            fullName = $"{firstName} {lastName}".Trim();
        }

        // Get company
        var company = GetFieldValue(csv, headerMap, "company");

        // Build custom data from remaining columns
        var customData = new Dictionary<string, object>();
        var standardFields = new HashSet<string> { "email", "fullname", "firstname", "lastname", "company", "name" };

        foreach (var kvp in headerMap)
        {
            if (!standardFields.Contains(kvp.Key))
            {
                var value = csv.GetField(kvp.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Try to parse as number or keep as string
                    if (int.TryParse(value, out int intVal))
                        customData[kvp.Key] = intVal;
                    else if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                        customData[kvp.Key] = dblVal;
                    else
                        customData[kvp.Key] = value;
                }
            }
        }

        return new ContactImportDto
        {
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            Company = string.IsNullOrWhiteSpace(company) ? null : company,
            CustomData = customData.Count > 0 ? customData : null
        };
    }

    /// <summary>
    /// Gets a field value by mapped column name.
    /// </summary>
    private string? GetFieldValue(CsvReader csv, Dictionary<string, int> headerMap, string fieldName)
    {
        if (headerMap.TryGetValue(fieldName, out int index))
        {
            return csv.GetField(index);
        }
        return null;
    }

    /// <summary>
    /// Basic email validation.
    /// </summary>
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        // Simple validation: contains @ and at least one dot after @
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return false;

        var dotIndex = email.LastIndexOf('.');
        return dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
    }

    /// <summary>
    /// Parses CSV content from a string.
    /// </summary>
    public ImportParseResult ParseCsvString(string csvContent)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
        return ParseCsvStream(stream);
    }

    /// <summary>
    /// Gets a preview of the first N rows from a CSV file.
    /// </summary>
    public (string[] Headers, List<string[]> Rows) PreviewCsv(Stream stream, int maxRows = 10)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        var rows = new List<string[]>();
        int count = 0;
        while (csv.Read() && count < maxRows)
        {
            var row = new string[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                row[i] = csv.GetField(i) ?? "";
            }
            rows.Add(row);
            count++;
        }

        return (headers, rows);
    }
}
