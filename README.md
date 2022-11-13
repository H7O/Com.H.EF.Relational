# Com.H.EF.Relational
Dynamically query relational databases without the need for pre-defined data models to retrieve results, and with flexible and safe means for passing parameters to queries.

## Sample 1
This sample demonstrates how to execute a simple query without parameters on a SQL Server Database.

To run this sample, you need to:
1) Create a new console application
2) Add NuGet package [Com.H.EF.Relational](https://www.nuget.org/packages/Com.H.EF.Relational)  
3) Add NuGet package [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer)
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
var queryParams = new { name = "abc" };

var result = dc.ExecuteQuery(@"
	select * from (values ('abc', '123'), ('def', '456')) as t (name, phone)
	where name = {{name}}", queryParams
);

// ^ queryParams could be an anonymous object (similar to the example above)
// a normal object, or IDictionary<string, object>
// Example 1: var queryParams = new Dictionary<string, object> { { "name", "abc" } }
// Example 2: var queryParams = new CustomParamClass { name = "abc" }


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
To override this behavior, i.e. create a DbContext for SqlLite instead of SqlServer (in the event of both packages are present in your project) use `conStr.CreateDbContext("Microsoft.EntityFrameworkCore.SqlLite")` instead of just `conStr.CreateDbContext()`

If you have a different database other than SqlServer or SqlLite, just create a DbContext the normal way you would do usually (i.e. without the use of `CreateDbContext()`) and your DbContext object should have all the extension methods offered by Com.H.EF.Relational package available to it.

> Note
> `CreateDbContext()` is written just to simplify the DbContext creation part, it's not essential by any means.
> Adding support in `CreateDbContext()` for other than SqlServer & SqlLite DbContext creation should be fairly straight forward (takes few minutes per database) and I will hopefully look into adding most common popular databases once I get the time to lookup their corresponding assemblies & extension method calls that generates their DbContext.

## What other features this library has?
This small library has several other options that allow for more advanced features that might not be of much use to most, hence samples for those features have been left out in this quick `how to` documentation.