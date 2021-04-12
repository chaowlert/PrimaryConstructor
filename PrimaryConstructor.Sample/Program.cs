using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PrimaryConstructor.Sample
{
	class Program
	{
		static void Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddSingleton<MyDependency>();
			services.AddSingleton<MyDependencyTwo>();
			services.AddSingleton<MyServiceThree>();
			services.AddLogging(builder => builder.AddConsole());
			var injector = services.BuildServiceProvider();
			var myService = injector.GetService<MyServiceThree>();

			Console.WriteLine(myService.Greeting());
		}
	}

    [PrimaryConstructor]
	public partial class MyServiceThree : MyServiceBaseTwo
    {
        private readonly MyDependencyTwo _myDependencyTwo;

		//initialized field will not be injected
        private readonly string _template = "{0} {1}!";

		public string Greeting()
		{
			return string.Format(_template,
				MyDependency.GetName(),
				_myDependencyTwo.GetName());
		}
	}

	[PrimaryConstructor]
	public partial class MyServiceBaseTwo : MyServiceBase
	{
		public MyDependency MyDependency { get; }
	}

	[PrimaryConstructor]
	public partial class MyServiceBase
	{
		private readonly ILogger<MyServiceBase> _logger;

		/*public MyServiceBase(ILogger<MyServiceBase> logger)
		{
			_logger = logger;
		}*/
	}

    [PrimaryConstructor]
    public partial class MyDependency
	{
		public string GetName()
		{
			return "Hello";
		}
	}
    
    public class MyDependencyTwo
    {
	    public string GetName()
	    {
		    return "World";
	    }
    }
}
