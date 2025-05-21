# FormID Database System Documentation

## Overview

The FormID database system is a SQLite-based solution for storing and retrieving information about game FormIDs. It replaces the previous CSV-based implementation to provide better performance and enhanced functionality when dealing with large databases (hundreds of MB in size).

## Key Components

### 1. FormIdDatabaseService

The core service responsible for managing SQLite database connections and performing FormID lookups.

**Key Features:**
- Efficient SQLite database queries with caching
- Thread-safe database operations
- Automatic database initialization and disposal
- Utility methods for database creation and CSV import

### 2. FormIdDatabaseImporter

Service for batch importing FormID data from CSV files into SQLite format.

**Key Features:**
- Import multiple CSV files in a single operation
- Progress reporting during import
- Validation of CSV files

### 3. FormIdDatabaseViewModel

ViewModel to support UI operations for database management.

**Key Features:**
- Import database command
- Display database information
- Control FormID lookup functionality

### 4. FormIdDatabaseView

UI view for managing FormID databases.

**Key Features:**
- CSV import interface
- Database status display
- Enable/disable FormID lookup in crash reports

## Using FormID Database Functionality

### Basic Usage

```csharp
// Get FormID information from the database
public string? GetFormIdInfo(string formId, string plugin)
{
    // Inject FormIdDatabaseService via DI
    var formIdDbService = _serviceProvider.GetRequiredService<FormIdDatabaseService>();
    
    // Look up the FormID
    return formIdDbService.GetEntry(formId, plugin);
}
```

### Enabling FormID Lookups

```csharp
// In your settings code
appSettings.ShowFormIdValues = true;
```

### Creating a New Database

```csharp
// Create a new empty database
string dbPath = @"C:\Path\To\Your\Database.db";
string gameName = "Fallout4"; // Table name in database
bool success = FormIdDatabaseService.CreateNewDatabase(dbPath, gameName);
```

### Importing CSV Data

```csharp
// Import a single CSV file
string csvPath = @"C:\Path\To\Your\FormIDs.csv";
int recordCount = FormIdDatabaseService.ImportCsvToDatabase(
    dbPath, 
    csvPath, 
    gameName, 
    formIdColumn: "FormID",
    pluginColumn: "Plugin",
    hasHeaderRow: true);
```

### Batch Import

```csharp
// Import multiple CSV files from a directory
var importer = new FormIdDatabaseImporter(appSettings, formIdDatabaseService);

// Report progress during import
var progress = new Progress<int>(value => {
    Console.WriteLine($"Import progress: {value}%");
});

var result = await importer.ImportFromDirectory(
    @"C:\Path\To\CSV\Files", 
    @"C:\Output\Database.db",
    progress);

if (result.Success)
{
    Console.WriteLine($"Successfully imported {result.RecordsImported} records");
}
else
{
    Console.WriteLine($"Import failed: {result.ErrorMessage}");
}
```

## Database Schema

The SQLite database uses the following schema:

```sql
CREATE TABLE [GameName] (
    formid TEXT NOT NULL,
    plugin TEXT NOT NULL,
    entry TEXT,
    PRIMARY KEY (formid, plugin)
);

CREATE INDEX idx_[GameName]_formid ON [GameName] (formid);
CREATE INDEX idx_[GameName]_plugin ON [GameName] (plugin);
```

## CSV Format

Expected CSV format for import:

```
FormID,Plugin,EditorID,Name
01234567,plugin1.esp,MyItem,My Item Name
89ABCDEF,plugin2.esp,OtherItem,Another Item
```

## Performance Considerations

- The SQLite implementation includes caching to improve performance for repeated lookups
- Indexes are created on the formid and plugin columns to optimize queries
- For very large databases, consider importing in smaller batches

## Error Handling

The system includes comprehensive error handling for common issues:

- Database file not found
- Invalid CSV format
- Import errors
- Connection issues

## Future Enhancements

- Database compaction and optimization tools
- Support for additional FormID metadata
- Advanced filtering and search capabilities
