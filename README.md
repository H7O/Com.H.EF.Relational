# Com.H.EF.Relational
Dynamically query relational databases without the need for pre-defined data models to retrieve results nor pre-defined parameters to pass to queries.

## Sample 1
This sample demonstrates how to execute a simple query without parameters on a SQL Server Database.
To run this sample, you need to:
1) Create a new console application
2) Add NuGet package Com.H.EF.Relational
3) Add NuGet package Microsoft.EntityFrameworkCore.SqlServer
4) copy and paste the following code into your Program.cs file:

```csharp
using Com.H.EF.Relational;
using Microsoft.EntityFrameworkCore;

string conStr = "connection string goes here";
DbContext dc = conStr.CreateDbContext();

var result = dc.ExecuteQuery("select 'abc' as name, '123' as phone");
// ^ returns IEnumerable<dynamic>, you can also return IEnumerable<T> where T is your data model class
// by using the ExecuteQuery<T> method which returns IEnumerable<T>
// example: var result = dc.ExecuteQuery<YourDataModelClass>("select 'abc' as name, '123' as phone");

foreach(var item in result)
{
	System.Console.WriteLine($"name = {item.name}, phone = {item.phone}");
}
```

## Sample 2
This sample demonstrates how to pass parameters to your SQL query

```csharp
using Com.H.EF.Relational;
using Microsoft.EntityFrameworkCore;

string conStr = "connection string goes here";
DbContext dc = conStr.CreateDbContext();

var result = dc.ExecuteQuery(@"
	select * from (values ('abc', '123'), ('def', '456')) as t (name, phone)
	where name = {{name}}", new { name = "abc" }
);

// ^ you can pass normal, anonymous object (similar to the example above), 
// or IDictionary<string, object> as parameters
// Example 1: new Dictionary<string, object> { { "name", "abc" } }
// Example 2: new CustomParamClass { name = "abc" }

foreach(var item in result)
{
	System.Console.WriteLine($"name = {item.name}, phone = {item.phone}");
}
```

## What other databases this library supports?
Any EntityFrameworkCore database, just add your target database NuGet package, create a DbContext and use
the extension methods offered by Com.H.EF.Relational to execute any SQL queries you want.

## How does conStr.CreateDbContext() method in the samples above work?
CreateDbContext() does a lookup for either Microsoft.EntityFrameworkCore.SqlServer 
or Microsoft.EntityFrameworkCore.SqlLite packages in your project and creates a DbContext object
accordingly for whichever package (SqlServer or SqlLite) it finds.

If it detects both packages are added in your project, it'll create a DbContext for SQL Server.
To override this behavior, i.e. create a DbContext for SqlLite instead of SqlServer use `conStr.CreateDbContext("Microsoft.EntityFrameworkCore.SqlServer")` instead of conStr.CreateDbContext

If you have a different database other than SqlServer and SqlLite, use the following `conStr.CreateDbContext('your db package name or path to your db package dll file')`

## What other features this library has?
This small library has several other options that allows for more advanced features that might not be of much use to most, hence it's left out in this quick `how to` documentation (will hopefully be documented in the future).