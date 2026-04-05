# Com.H.EF.Relational
Adds `ExecuteQuery`, `ExecuteQueryAsync`, `ExecuteCommand`, and `ExecuteCommandAsync` extension methods to EF Core's `DbContext` that return `DbQueryResult<dynamic>` (implementing `IEnumerable<dynamic>`) or `DbAsyncQueryResult<dynamic>` (implementing `IAsyncEnumerable<dynamic>`).

This is a thin wrapper around [Com.H.Data.Common](https://www.nuget.org/packages/Com.H.Data.Common) that brings all of its features to Entity Framework Core users by operating on `DbContext` instead of `DbConnection`.

For source code and documentation, kindly visit the project's github page [https://github.com/H7O/Com.H.EF.Relational](https://github.com/H7O/Com.H.EF.Relational)

## Why this package?
Entity Framework Core requires a registered `DbSet<T>` entity to use `FromSqlRaw`/`FromSqlInterpolated`, and its newer `SqlQueryRaw<T>` (EF Core 8+) still requires a concrete type — there's no built-in way to run arbitrary SQL and get back `IEnumerable<dynamic>` results.

This means if you just want to run a quick ad-hoc query without defining a model class or registering a `DbSet`, EF Core doesn't offer a straightforward path.

This package fills that gap. It lets you execute any SQL directly on your `DbContext` and get back dynamic (or strongly-typed) results — no `DbSet`, no pre-defined model required. Since the library executes arbitrary SQL, you can run anything your database supports: stored procedures, CTEs, window functions, user-defined functions, temp tables, dynamic SQL, and more. It also brings flexible parameterization (including JSON/`JsonElement` parameters), automatic nested JSON/XML parsing, and proper resource management via disposable result types.

## Sample 1
This sample demonstrates how to execute a simple query without parameters on a SQL Server Database.

To run this sample, you need to:
1) Create a new console application
2) Add NuGet package [Com.H.EF.Relational](https://www.nuget.org/packages/Com.H.EF.Relational)  
3) Add NuGet package [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer)
4) Copy and paste the following code into your Program.cs file:

```csharp
using Com.H.EF.Relational;
using Microsoft.EntityFrameworkCore;

// Assuming you have your DbContext set up via DI or otherwise:
// DbContext dc = ...;

using var result = dc.ExecuteQuery("select 'John' as name, '123' as phone");
// ^ returns DbQueryResult<dynamic> which implements IEnumerable<dynamic> and IDisposable
// You can also return DbQueryResult<T> where T is your data model class
// by using the ExecuteQuery<T> method.
// Example: using var result = dc.ExecuteQuery<YourDataModelClass>("select 'John' as name, '123' as phone");
// Also, returns DbAsyncQueryResult<dynamic> when called asynchronously via dc.ExecuteQueryAsync()
// or dc.ExecuteQueryAsync<T>()
// And for executing a command that does not return any data, you can use the ExecuteCommand()
// or ExecuteCommandAsync() methods

foreach (var item in result)
{
    System.Console.WriteLine($"name = {item.name}, phone = {item.phone}");
}
```
> **Note**: The returned `DbQueryResult` (whether `DbQueryResult<dynamic>` or `DbQueryResult<T>`) implements the `IEnumerable<T>` interface and `IDisposable` interface, which means you can use it anywhere `IEnumerable<T>` is expected and should be disposed properly using the `using` keyword.<br/>
The asynchronous version returns `DbAsyncQueryResult<T>` which implements `IAsyncEnumerable<T>` and `IAsyncDisposable`.


## Sample 2
This sample demonstrates how to pass parameters to your SQL query

```csharp
using Com.H.EF.Relational;
using Microsoft.EntityFrameworkCore;

// DbContext dc = ...;
var queryParams = new { name = "Jane" };
// ^ queryParams could be an anonymous object (similar to the example above)
// or the following types:
// 1) IDictionary<string, object>
// 2) Normal object with properties that match the parameter names in the query
// 3) JSON string
// 4) System.Text.Json.JsonElement (useful when building Web APIs, allows passing
//    JsonElement input directly from a web client)

using var result = dc.ExecuteQuery(@"
    select * from (values 
        ('John', '55555'), 
        ('Jane', '44444')) as t (name, phone)
    where name = {{name}}", queryParams
);
// ^ note the use of curly braces around the parameter name in the query.
// This is a special syntax that allows you to pass parameters to your query.
// The parameter name must match the property name in the queryParams object.
// It also protects you from SQL injection attacks and is configurable to use other
// delimiters by passing a regular expression

foreach (var item in result)
{
    System.Console.WriteLine($"name = {item.name}, phone = {item.phone}");
}
```

## Sample 3
This sample demonstrates how to return nested hierarchical data from a query (SQL Server).

```csharp
using Com.H.EF.Relational;
using Microsoft.EntityFrameworkCore;

// DbContext dc = ...;

using var result = dc.ExecuteQuery(@"
SELECT 
    'John' as [name],
    (select * from (values 
        ('55555', 'Mobile'), 
        ('44444', 'Work')) 
        as t (number, [type]) for json path) AS {type{json{phones}}}");

foreach (var person in result)
{
    Console.WriteLine($"name = {person.name}");
    Console.WriteLine("--- phones ---");
    foreach (var phone in person.phones)
    {
        System.Console.WriteLine($"{phone.type} = {phone.number}");
    }
}
```
To tell the library to parse nested JSON data, enclose the property name in `{type{json{your_property_name}}}` syntax.
For XML, use `{type{xml{your_property_name}}}`.

## What databases does this library support?
Any database supported by Entity Framework Core. Since this library works through EF Core's underlying `DbConnection`, it inherits the same broad database support as [Com.H.Data.Common](https://www.nuget.org/packages/Com.H.Data.Common), including SQL Server, PostgreSQL, MySQL, SQLite, Oracle, and more.

## Open source
This package is written in C# and is fully open source. 
Kindly visit the project's github page [https://github.com/H7O/Com.H.EF.Relational](https://github.com/H7O/Com.H.EF.Relational)
