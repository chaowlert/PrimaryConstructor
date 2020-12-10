# PrimaryConstructor
Generate primary constructor from readonly fields

[![NuGet](https://img.shields.io/nuget/v/PrimaryConstructor.svg)](https://www.nuget.org/packages/PrimaryConstructor)

![image](https://user-images.githubusercontent.com/5763993/97197488-4b65ad80-17e0-11eb-9eef-305ce284eb78.png)

### Get it
```
PM> Install-Package PrimaryConstructor
```

### Prerequisites

Visual Studio version 16.8 and above is required as its first version to support source generators.

### Usage

Declare class with `partial`, and annotate with `[PrimaryConstructor]`.  
And then you can declare your dependencies with readonly fields.

```csharp
[PrimaryConstructor]
public partial class MyService
{
    private readonly MyDependency _myDependency;

    ...
}
```

When compile, following source will be injected.

```csharp
partial class MyService
{
    public MyService(MyDependency myDependency)
    {
        this._myDependency = myDependency;
    }
}
```

NOTE: readonly fields with initialized to a value will not be included in constructor.

### Emit generated files

Visual Studio still not fully support source generator, it sometimes shows error marker on symbols referred to the generated code.
Emitting the generated files will allow you to see the code, and also solve Visual Studio error marker problem.

To emit generated files, add following code to your `csproj` file.

```xml
<PropertyGroup>
  <CompilerGeneratedFilesOutputPath>$(MSBuildProjectDirectory)/generated</CompilerGeneratedFilesOutputPath>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>

<Target Name="ExcludeGenerated" BeforeTargets="AssignTargetPaths">
  <ItemGroup>
    <Generated Include="generated/**/*.g.cs" />
    <Compile Remove="@(Generated)" />
  </ItemGroup>
  <Delete Files="@(Generated)" />
</Target>
```

Please check [PrimaryConstructor.Sample.csproj](https://github.com/chaowlert/PrimaryConstructor/blob/main/PrimaryConstructor.Sample/PrimaryConstructor.Sample.csproj) for sample.
