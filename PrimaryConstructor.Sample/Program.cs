using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace PrimaryConstructor.Sample
{
	class Program
	{
		static void Main(string[] args)
		{
			var services = new ServiceCollection();
			services.AddSingleton<MyDependency>();
			services.AddSingleton<MyService>();
			var injector = services.BuildServiceProvider();
			var myService = injector.GetService<MyService>();

			Console.WriteLine(myService.Greeting());
		}
	}

    [PrimaryConstructor]
	public partial class MyService
    {
        private readonly MyDependency _myDependency;

		//initialized field will not be injected
        private readonly string _template = "Hello {0}!";

		public string Greeting()
		{
			return string.Format(_template, _myDependency.GetName());
		}
	}

    [PrimaryConstructor]
    public partial class MyDependency
	{
		public string GetName()
		{
			return "World";
		}
	}


}
